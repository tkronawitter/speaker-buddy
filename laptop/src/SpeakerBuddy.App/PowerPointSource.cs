using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.CSharp.RuntimeBinder;
using SpeakerBuddy.Core;

namespace SpeakerBuddy.App;

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

        var com = new ComScope();
        try
        {
            return new PollResult(true, ReadShow(com.Track(application), com));
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
            com.Dispose();
        }
    }

    // Every object a late-bound call returns is tracked and released at the end of the poll, so
    // four polls a second leave no proxies for the finalizer and PowerPoint can close cleanly
    private ShowState? ReadShow(dynamic application, ComScope com)
    {
        dynamic windows = com.Track(application.SlideShowWindows);
        int count = windows.Count;

        dynamic? window = null;
        if (_locked is not null)
        {
            for (var i = 1; i <= count && window is null; i++)
            {
                dynamic candidate = com.Track(windows.Item(i));
                dynamic presentation = com.Track(candidate.Presentation);
                if (string.Equals((string)presentation.FullName, _locked, StringComparison.OrdinalIgnoreCase))
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

            window = com.Track(windows.Item(1));
            dynamic presentation = com.Track(window.Presentation);
            var name = (string)presentation.FullName;
            var slides = ReadSlides(presentation, com);
            _locked = name;
            _slides = slides;
            _lastSlideIndex = 1;
            _log($"slide show of {name} started, {slides.Count} slides" + (count > 1 ? $", {count} shows running, took the first" : ""));
        }

        dynamic view = com.Track(window.View);
        // At the black end-of-show screen there is no current slide; keep the last one.
        if ((int)view.State != PpSlideShowDone)
        {
            dynamic slide = com.Track(view.Slide);
            _lastSlideIndex = (int)slide.SlideIndex;
        }

        return new ShowState(_locked!, _lastSlideIndex, _slides!);
    }

    private static List<SlideNotes> ReadSlides(dynamic presentation, ComScope com)
    {
        var result = new List<SlideNotes>();
        dynamic slides = com.Track(presentation.Slides);
        int count = slides.Count;
        for (var i = 1; i <= count; i++)
        {
            dynamic slide = com.Track(slides.Item(i));
            dynamic transition = com.Track(slide.SlideShowTransition);
            var hidden = (int)transition.Hidden == MsoTrue;
            result.Add(new SlideNotes((int)slide.SlideIndex, hidden, NotesText(slide, com)));
        }

        return result;
    }

    private static string? NotesText(dynamic slide, ComScope com)
    {
        dynamic notesPage = com.Track(slide.NotesPage);
        dynamic shapes = com.Track(notesPage.Shapes);
        dynamic placeholders = com.Track(shapes.Placeholders);
        int count = placeholders.Count;
        for (var i = 1; i <= count; i++)
        {
            dynamic shape = com.Track(placeholders.Item(i));
            dynamic format = com.Track(shape.PlaceholderFormat);
            if ((int)format.Type == PpPlaceholderBody && (int)shape.HasTextFrame == MsoTrue)
            {
                dynamic textFrame = com.Track(shape.TextFrame);
                dynamic textRange = com.Track(textFrame.TextRange);
                return (string)textRange.Text;
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

/// <summary>
/// The COM objects of one poll, released newest first when the poll ends. Values that are not COM
/// objects (strings, numbers, null) pass through untracked.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class ComScope : IDisposable
{
    private readonly List<object> _objects = [];

    public int Count => _objects.Count;

    public T Track<T>(T value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            _objects.Add(value);
        }

        return value;
    }

    public void Dispose()
    {
        for (var i = _objects.Count - 1; i >= 0; i--)
        {
            try
            {
                Marshal.ReleaseComObject(_objects[i]);
            }
            catch (Exception e) when (e is InvalidComObjectException or ArgumentException)
            {
                // Already released or no longer attached; nothing left to free.
            }
        }

        _objects.Clear();
    }
}
