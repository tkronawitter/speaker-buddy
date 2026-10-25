using System.Globalization;
using System.Text;

namespace SpeakerBuddy.Core;

/// <summary>A line that looks like a mark or a session length but is not one.</summary>
public sealed record NearMiss(int SlideIndex, string Line);

/// <summary>Result of checking a deck's marks without PowerPoint (requirement L-09).</summary>
/// <param name="Marks">Marks the timer will use.</param>
/// <param name="NearMisses">Lines that look like marks or session lengths but are not, probably typos.</param>
/// <param name="HiddenMarks">Marks on hidden slides; ignored during the talk.</param>
/// <param name="Decreasing">Marks lower than the one before; ignored during the talk.</param>
/// <param name="Length">Talk length L as the program would use it, and where it comes from.</param>
/// <param name="ToleranceSeconds">Tolerance derived from L.</param>
/// <param name="SessionLength">The deck's session length, also when <c>--length</c> overrides it.</param>
/// <param name="HiddenSessionLengths">Session lengths on hidden slides; ignored.</param>
/// <param name="ExtraSessionLengths">Session lengths after the first one; ignored.</param>
public sealed record DeckCheckResult(
    IReadOnlyList<Mark> Marks,
    IReadOnlyList<NearMiss> NearMisses,
    IReadOnlyList<Mark> HiddenMarks,
    IReadOnlyList<MarkWarning> Decreasing,
    ResolvedLength Length,
    int ToleranceSeconds,
    SessionLength? SessionLength,
    IReadOnlyList<SessionLength> HiddenSessionLengths,
    IReadOnlyList<SessionLength> ExtraSessionLengths)
{
    /// <summary>Talk length L in seconds, or null if unknown.</summary>
    public int? LengthSeconds => Length.Seconds;

    public int ProblemCount =>
        NearMisses.Count + HiddenMarks.Count + Decreasing.Count + HiddenSessionLengths.Count + ExtraSessionLengths.Count;
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
        var hiddenSlides = slides.Where(s => s.Hidden).OrderBy(s => s.SlideIndex).ToList();
        var hidden = hiddenSlides
            .Select(s => (s.SlideIndex, Seconds: MarkParser.ParseNotes(s.Notes)))
            .Where(s => s.Seconds is not null)
            .Select(s => new Mark(s.SlideIndex, s.Seconds!.Value))
            .ToList();
        var hiddenSessionLengths = hiddenSlides
            .SelectMany(s => MarkParser.SessionLengths(s.Notes).Select(seconds => new SessionLength(s.SlideIndex, seconds)))
            .ToList();
        var length = TalkLength.ResolveWithSource(lengthArgument, marks);

        return new DeckCheckResult(
            marks.Marks, nearMisses, hidden, marks.Warnings, length, TalkLength.Tolerance(length.Seconds),
            marks.SessionLength, hiddenSessionLengths, marks.ExtraSessionLengths);
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

        text.AppendLine(LengthLine(result));
        if (result.Length.Source == LengthSource.Argument && result.SessionLength is { } overridden)
        {
            text.AppendLine(Invariant(
                $"  (--length overrides ## session length {Time(overridden.Seconds)} on slide {overridden.SlideIndex})"));
        }

        if (result.NearMisses.Count > 0)
        {
            text.AppendLine("Near misses (probably typos, not used):");
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

        AppendSessionLengths(text, "Session lengths on hidden slides (ignored):", result.HiddenSessionLengths);
        AppendSessionLengths(text, "More than one session length (only the first counts, these are ignored):", result.ExtraSessionLengths);

        text.Append(result.ProblemCount == 1 ? "1 problem" : Invariant($"{result.ProblemCount} problems"));
        return text.ToString();
    }

    private static string LengthLine(DeckCheckResult result)
    {
        var tolerance = Time(result.ToleranceSeconds);
        var length = result.Length;
        return length.Seconds is not int seconds
            ? Invariant($"Talk length unknown (no --length, no ## session length, highest mark below 5:00), tolerance {tolerance}")
            : length.Source switch
            {
                LengthSource.Argument => Invariant($"Talk length {Time(seconds)} from --length, tolerance {tolerance}"),
                LengthSource.SessionLength => Invariant(
                    $"Talk length {Time(seconds)} from ## session length on slide {length.SlideIndex}, tolerance {tolerance}"),
                _ => Invariant($"Talk length {Time(seconds)} from the highest mark (slide {length.SlideIndex}), tolerance {tolerance}"),
            };
    }

    private static void AppendSessionLengths(StringBuilder text, string heading, IReadOnlyList<SessionLength> lengths)
    {
        if (lengths.Count == 0)
        {
            return;
        }

        text.AppendLine(heading);
        foreach (var length in lengths)
        {
            text.AppendLine(Invariant($"  slide {length.SlideIndex,3}  ## session length {Time(length.Seconds)}"));
        }
    }

    private static string Time(int seconds) => Invariant($"{seconds / 60}:{seconds % 60:00}");

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
