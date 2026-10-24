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

/// <summary>
/// The valid marks of a deck in slide order. Marks on hidden slides are ignored; a mark lower than
/// the last accepted one is dropped with a warning (L-01).
/// </summary>
public sealed class MarkSet
{
    private MarkSet(IReadOnlyList<Mark> marks, IReadOnlyList<MarkWarning> warnings)
    {
        Marks = marks;
        Warnings = warnings;
    }

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

        foreach (var slide in slides.Where(s => !s.Hidden).OrderBy(s => s.SlideIndex))
        {
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

        return new MarkSet(marks, warnings);
    }
}
