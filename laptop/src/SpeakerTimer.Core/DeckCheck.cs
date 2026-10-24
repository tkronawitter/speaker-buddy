using System.Globalization;
using System.Text;

namespace SpeakerTimer.Core;

/// <summary>A line that looks like a mark but is not one.</summary>
public sealed record NearMiss(int SlideIndex, string Line);

/// <summary>Result of checking a deck's marks without PowerPoint (requirement L-09).</summary>
/// <param name="Marks">Marks the timer will use.</param>
/// <param name="NearMisses">Lines that look like marks but are not, probably typos.</param>
/// <param name="HiddenMarks">Marks on hidden slides; ignored during the talk.</param>
/// <param name="Decreasing">Marks lower than the one before; ignored during the talk.</param>
/// <param name="LengthSeconds">Talk length L as the program would use it, or null if unknown.</param>
/// <param name="ToleranceSeconds">Tolerance derived from L.</param>
public sealed record DeckCheckResult(
    IReadOnlyList<Mark> Marks,
    IReadOnlyList<NearMiss> NearMisses,
    IReadOnlyList<Mark> HiddenMarks,
    IReadOnlyList<MarkWarning> Decreasing,
    int? LengthSeconds,
    int ToleranceSeconds)
{
    public int ProblemCount => NearMisses.Count + HiddenMarks.Count + Decreasing.Count;
}

/// <summary>Checks the marks of a deck and formats the report for <c>--check</c>.</summary>
public static class DeckCheck
{
    public static DeckCheckResult Run(IReadOnlyList<SlideNotes> slides, int? lengthArgument)
    {
        ArgumentNullException.ThrowIfNull(slides);

        var marks = MarkSet.Build(slides);
        var nearMisses = slides
            .OrderBy(s => s.SlideIndex)
            .SelectMany(s => MarkParser.NearMisses(s.Notes).Select(line => new NearMiss(s.SlideIndex, line)))
            .ToList();
        var hidden = slides
            .Where(s => s.Hidden)
            .OrderBy(s => s.SlideIndex)
            .Select(s => (s.SlideIndex, Seconds: MarkParser.ParseNotes(s.Notes)))
            .Where(s => s.Seconds is not null)
            .Select(s => new Mark(s.SlideIndex, s.Seconds!.Value))
            .ToList();
        var length = TalkLength.Resolve(lengthArgument, marks);

        return new DeckCheckResult(marks.Marks, nearMisses, hidden, marks.Warnings, length, TalkLength.Tolerance(length));
    }

    public static string Format(DeckCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var text = new StringBuilder();
        text.AppendLine(result.Marks.Count == 0 ? "No marks found." : "Marks:");
        foreach (var mark in result.Marks)
        {
            text.AppendLine(Invariant($"  slide {mark.SlideIndex,3}  ## {Time(mark.Seconds)}"));
        }

        text.AppendLine(result.LengthSeconds is int length
            ? Invariant($"Talk length {Time(length)}, tolerance {Time(result.ToleranceSeconds)}")
            : Invariant($"Talk length unknown (no argument, highest mark below 5:00), tolerance {Time(result.ToleranceSeconds)}"));

        if (result.NearMisses.Count > 0)
        {
            text.AppendLine("Near misses (not used as marks):");
            foreach (var miss in result.NearMisses)
            {
                text.AppendLine(Invariant($"  slide {miss.SlideIndex,3}  \"{miss.Line}\""));
            }
        }

        if (result.HiddenMarks.Count > 0)
        {
            text.AppendLine("Marks on hidden slides (ignored):");
            foreach (var mark in result.HiddenMarks)
            {
                text.AppendLine(Invariant($"  slide {mark.SlideIndex,3}  ## {Time(mark.Seconds)}"));
            }
        }

        if (result.Decreasing.Count > 0)
        {
            text.AppendLine("Decreasing marks (ignored):");
            foreach (var warning in result.Decreasing)
            {
                text.AppendLine(Invariant(
                    $"  slide {warning.SlideIndex,3}  ## {Time(warning.Seconds)} after {Time(warning.PreviousSeconds)}"));
            }
        }

        text.Append(result.ProblemCount == 1 ? "1 problem" : Invariant($"{result.ProblemCount} problems"));
        return text.ToString();
    }

    private static string Time(int seconds) => Invariant($"{seconds / 60}:{seconds % 60:00}");

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
