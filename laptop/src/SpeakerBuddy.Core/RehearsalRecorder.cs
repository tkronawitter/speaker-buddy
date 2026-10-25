using System.Globalization;

namespace SpeakerBuddy.Core;

/// <summary>
/// One row of the rehearsal log (requirement L-08): where the talk stood when a slide came up.
/// </summary>
/// <param name="Presentation">Full name of the presentation, for the file name.</param>
/// <param name="FirstOfShow">True for the first row of a show: the log starts a new file.</param>
/// <param name="Time">Local wall clock time of the slide change.</param>
/// <param name="Slide">Slide index in the deck.</param>
/// <param name="MarkSeconds">The slide's mark, or null if it has none.</param>
/// <param name="Seconds">Elapsed time <c>t</c> on the timer.</param>
/// <param name="Deviation">Deviation from the plan in seconds, or null if there is none.</param>
/// <param name="Color">Colour the timer showed for the slide.</param>
public sealed record RehearsalRow(
    string Presentation, bool FirstOfShow, DateTime Time, int Slide, int? MarkSeconds, int Seconds, int? Deviation,
    EvaluationColor Color)
{
    /// <summary>
    /// Field separator: a comma, standard CSV (RFC 4180). No field needs quoting: each is a time, an
    /// integer or a colour word, and nothing taken from the deck (the presentation name only goes
    /// into the file name) reaches a column.
    /// </summary>
    public const char Separator = ',';

    public static string Header { get; } = string.Join(Separator, "time", "slide", "mark_s", "t_s", "deviation_s", "colour");

    /// <summary>The row as CSV, invariant culture, without a line end.</summary>
    public string ToCsv() => string.Join(
        Separator,
        Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        Slide.ToString(CultureInfo.InvariantCulture),
        MarkSeconds?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        Seconds.ToString(CultureInfo.InvariantCulture),
        Deviation?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        Color.ToString().ToLowerInvariant());
}

/// <summary>
/// Decides when the rehearsal log gets a row: on the first tick with a show, a timer status and an
/// evaluation for a slide other than the last row's, so going back a slide gets a row too. A show
/// start (or a different presentation) begins a new file. Pure logic, called under the runner's lock.
/// </summary>
public sealed class RehearsalRecorder
{
    // Characters Windows forbids in a file name; spelled out because Path.GetInvalidFileNameChars and
    // Path.GetFileNameWithoutExtension follow the OS the code runs on, and Core is tested on Linux too
    private const string InvalidFileNameChars = "<>:\"/\\|?*";

    private string? _presentation;
    private int? _lastSlide;

    /// <summary>File name for a show that started at <paramref name="localStart"/> with the given presentation.</summary>
    public static string FileName(DateTime localStart, string presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        // PowerPoint's FullName is a Windows path or a URL: the deck is the last segment without its extension
        var deck = presentation[(presentation.LastIndexOfAny(['\\', '/']) + 1)..];
        var dot = deck.LastIndexOf('.');
        if (dot >= 0)
        {
            deck = deck[..dot];
        }

        var safe = new string(deck.Select(c => c < ' ' || InvalidFileNameChars.Contains(c) ? '_' : c).ToArray());
        return $"rehearsal-{localStart.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture)}"
               + (safe.Length > 0 ? $"-{safe}" : string.Empty) + ".csv";
    }

    /// <summary>The row to write for this tick, or null.</summary>
    /// <param name="show">The running show, or null when none runs.</param>
    /// <param name="timer">Last status from the timer, or null.</param>
    /// <param name="evaluation">The evaluation of this tick, or null when nothing was evaluated.</param>
    /// <param name="marks">Marks of the current show.</param>
    /// <param name="localNow">Local wall clock time.</param>
    public RehearsalRow? Next(ShowState? show, TimerStatusLine? timer, Evaluation? evaluation, MarkSet? marks, DateTime localNow)
    {
        if (show is null)
        {
            _presentation = null;
            _lastSlide = null;
            return null;
        }

        if (!string.Equals(show.PresentationName, _presentation, StringComparison.OrdinalIgnoreCase))
        {
            _presentation = show.PresentationName;
            _lastSlide = null;
        }

        if (timer is null || evaluation is null || show.SlideIndex == _lastSlide)
        {
            return null;
        }

        var first = _lastSlide is null;
        _lastSlide = show.SlideIndex;
        var mark = marks?.Marks.FirstOrDefault(m => m.SlideIndex == show.SlideIndex);
        return new RehearsalRow(show.PresentationName, first, localNow, show.SlideIndex, mark?.Seconds, timer.Seconds,
            evaluation.Deviation, evaluation.Color);
    }
}
