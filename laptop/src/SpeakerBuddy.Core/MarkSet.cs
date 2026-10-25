namespace SpeakerBuddy.Core;

/// <summary>Speaker notes of one slide as read from the deck.</summary>
/// <param name="SlideIndex">1-based position of the slide in the deck.</param>
/// <param name="Hidden">True if the slide is hidden in the slide show.</param>
/// <param name="Notes">Notes text, or null if the slide has no notes.</param>
public sealed record SlideNotes(int SlideIndex, bool Hidden, string? Notes);

/// <summary>A mark that was dropped because it is lower than the mark before it.</summary>
/// <param name="SlideIndex">Slide carrying the dropped mark.</param>
/// <param name="Seconds">The dropped mark.</param>
/// <param name="PreviousSeconds">The last accepted mark it was compared with.</param>
public sealed record MarkWarning(int SlideIndex, int Seconds, int PreviousSeconds);

/// <summary>A <c>## session length m:ss</c> line: the planned talk length, not a mark.</summary>
/// <param name="SlideIndex">Slide whose notes carry the line.</param>
/// <param name="Seconds">The planned talk length.</param>
public sealed record SessionLength(int SlideIndex, int Seconds);

/// <summary>
/// The valid marks of a deck in slide order. Marks on hidden slides are ignored; a mark lower than
/// the last accepted one is dropped with a warning (L-01). Also the deck's session length: the first
/// <c>## session length</c> line on a visible slide; further ones are kept as extras for the check.
/// </summary>
public sealed class MarkSet
{
    private MarkSet(
        IReadOnlyList<Mark> marks, IReadOnlyList<MarkWarning> warnings,
        SessionLength? sessionLength, IReadOnlyList<SessionLength> extraSessionLengths)
    {
        Marks = marks;
        Warnings = warnings;
        SessionLength = sessionLength;
        ExtraSessionLengths = extraSessionLengths;
    }

    /// <summary>The first session length on a visible slide, or null if the deck has none.</summary>
    public SessionLength? SessionLength { get; }

    /// <summary>Session lengths after the first one; they do not count.</summary>
    public IReadOnlyList<SessionLength> ExtraSessionLengths { get; }

    /// <summary>Accepted marks, ordered by slide index, never decreasing.</summary>
    public IReadOnlyList<Mark> Marks { get; }

    /// <summary>Marks dropped because they decrease.</summary>
    public IReadOnlyList<MarkWarning> Warnings { get; }

    /// <summary>The highest accepted mark, or null if there is none.</summary>
    public int? HighestSeconds => Marks.Count == 0 ? null : Marks[^1].Seconds;

    public static MarkSet Build(IEnumerable<SlideNotes> slides)
    {
        ArgumentNullException.ThrowIfNull(slides);

        var marks = new List<Mark>();
        var warnings = new List<MarkWarning>();
        var sessionLengths = new List<SessionLength>();

        foreach (var slide in slides.Where(s => !s.Hidden).OrderBy(s => s.SlideIndex))
        {
            sessionLengths.AddRange(MarkParser.SessionLengths(slide.Notes).Select(s => new SessionLength(slide.SlideIndex, s)));

            if (MarkParser.ParseNotes(slide.Notes) is not int seconds)
            {
                continue;
            }

            if (marks.Count > 0 && seconds < marks[^1].Seconds)
            {
                warnings.Add(new MarkWarning(slide.SlideIndex, seconds, marks[^1].Seconds));
                continue;
            }

            marks.Add(new Mark(slide.SlideIndex, seconds));
        }

        return new MarkSet(marks, warnings, sessionLengths.FirstOrDefault(), sessionLengths.Skip(1).ToList());
    }
}
