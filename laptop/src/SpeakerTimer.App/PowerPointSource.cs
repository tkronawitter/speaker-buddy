using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.CSharp.RuntimeBinder;
using SpeakerTimer.Core;

namespace SpeakerTimer.App;

/// <summary>Result of one poll: whether it worked, and the running show if there is one.</summary>
/// <param name="Ok">False when a COM call failed; the caller skips this tick (A-03).</param>
/// <param name="Show">The show of the locked presentation, or null when none runs.</param>
public readonly record struct PollResult(bool Ok, ShowState? Show);

/// <summary>
/// Reads the running slide show from PowerPoint, read-only and late-bound (plan step 35, A-03).
/// Attaches to an already running PowerPoint through <c>GetActiveObject</c> and never starts it.
/// When a show starts, the source locks onto that presentation by its full name, reads its notes
/// once, and from then on follows only that presentation's show window, so a second deck in
/// slide show never takes over.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PowerPointSource
{
    private const string ProgId = "PowerPoint.Application";
    private const int MkEUnavailable = unchecked((int)0x800401E3);
    private const int RegdbEClassNotRegistered = unchecked((int)0x80040154);
    private const int CoEClassStringInvalid = unchecked((int)0x800401F3);
    private const int PpPlaceholderBody = 2;
    private const int PpSlideShowDone = 5;
    private const int MsoTrue = -1;

    private readonly Action<string> _log;
    private string? _locked;
    private IReadOnlyList<SlideNotes>? _slides;
    private int _lastSlideIndex = 1;

    public PowerPointSource(Action<string> log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public PollResult Poll()
    {
        object? application;
        try
        {
            application = GetActiveObject(ProgId);
        }
        catch (COMException e) when (e.HResult is MkEUnavailable or RegdbEClassNotRegistered or CoEClassStringInvalid)
        {
            // PowerPoint is not running (or not installed): no show.
            Unlock();
            return new PollResult(true, null);
        }
        catch (COMException e)
        {
            _log($"PowerPoint not reachable, tick skipped: {e.Message}");
            return new PollResult(false, null);
        }

        try
        {
            return new PollResult(true, ReadShow(application));
        }
        // A busy PowerPoint (RPC_E_CALL_REJECTED, for example while a show starts or during a
        // transition) makes the late-bound call fail with MissingMemberException, not COMException
        catch (Exception e) when (e is COMException or InvalidCastException or RuntimeBinderException
                                      or InvalidComObjectException or ArgumentException or MissingMemberException)
        {
            _log($"PowerPoint call failed, tick skipped: {e.GetType().Name} {e.Message}");
            return new PollResult(false, null);
        }
        finally
        {
            Marshal.ReleaseComObject(application);
        }
    }

    private ShowState? ReadShow(dynamic application)
    {
        dynamic windows = application.SlideShowWindows;
        int count = windows.Count;

        dynamic? window = null;
        if (_locked is not null)
        {
            for (var i = 1; i <= count && window is null; i++)
            {
                dynamic candidate = windows.Item(i);
                if (string.Equals((string)candidate.Presentation.FullName, _locked, StringComparison.OrdinalIgnoreCase))
                {
                    window = candidate;
                }
            }

            if (window is null)
            {
                _log($"slide show of {_locked} ended");
                Unlock();
                return null;
            }
        }
        else
        {
            if (count == 0)
            {
                return null;
            }

            window = windows.Item(1);
            dynamic presentation = window.Presentation;
            var name = (string)presentation.FullName;
            var slides = ReadSlides(presentation);
            _locked = name;
            _slides = slides;
            _lastSlideIndex = 1;
            _log($"slide show of {name} started, {slides.Count} slides" + (count > 1 ? $", {count} shows running, took the first" : ""));
        }

        dynamic view = window.View;
        // At the black end-of-show screen there is no current slide; keep the last one.
        if ((int)view.State != PpSlideShowDone)
        {
            _lastSlideIndex = (int)view.Slide.SlideIndex;
        }

        return new ShowState(_locked!, _lastSlideIndex, _slides!);
    }

    private static List<SlideNotes> ReadSlides(dynamic presentation)
    {
        var result = new List<SlideNotes>();
        dynamic slides = presentation.Slides;
        int count = slides.Count;
        for (var i = 1; i <= count; i++)
        {
            dynamic slide = slides.Item(i);
            var hidden = (int)slide.SlideShowTransition.Hidden == MsoTrue;
            result.Add(new SlideNotes((int)slide.SlideIndex, hidden, NotesText(slide)));
        }

        return result;
    }

    private static string? NotesText(dynamic slide)
    {
        dynamic placeholders = slide.NotesPage.Shapes.Placeholders;
        int count = placeholders.Count;
        for (var i = 1; i <= count; i++)
        {
            dynamic shape = placeholders.Item(i);
            if ((int)shape.PlaceholderFormat.Type == PpPlaceholderBody && (int)shape.HasTextFrame == MsoTrue)
            {
                return (string)shape.TextFrame.TextRange.Text;
            }
        }

        return null;
    }

    private void Unlock()
    {
        _locked = null;
        _slides = null;
    }

    private static object GetActiveObject(string progId)
    {
        CLSIDFromProgID(progId, out var clsid);
        GetActiveObject(ref clsid, IntPtr.Zero, out var instance);
        return instance;
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void CLSIDFromProgID(string progId, out Guid clsid);

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(ref Guid clsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
}
