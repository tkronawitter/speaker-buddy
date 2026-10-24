using SpeakerBuddy.Core;

namespace SpeakerBuddy.Core.Tests;

public class DeckCheckTests
{
    // The fixture was saved by PowerPoint; its notes are listed in make-fixture order:
    // 1 "## 0:30", 2 "## 5:00", 3 hidden "## 7:00", 4 "## 10:75", 5 "## 12:00 " after a line
    // break, 6 "## 11:00" (decreasing), 7 "## 29:30".
    private static DeckCheckResult CheckFixture(int? lengthArgument = null) =>
        DeckCheck.Run(DeckReader.Read(Path.Combine(AppContext.BaseDirectory, "fixtures", "check-deck.pptx")), lengthArgument);

    [Fact]
    public void Fixture_marks_problems_and_length_are_found()
    {
        var result = CheckFixture();

        Assert.Equal(new[] { new Mark(1, 30), new Mark(2, 300), new Mark(5, 720), new Mark(7, 1770) }, result.Marks);
        Assert.Equal(new[] { new NearMiss(4, "## 10:75") }, result.NearMisses);
        Assert.Equal(new[] { new Mark(3, 420) }, result.HiddenMarks);
        Assert.Equal(new[] { new MarkWarning(6, 660, 720) }, result.Decreasing);
        Assert.Equal(1770, result.LengthSeconds);
        Assert.Equal(45, result.ToleranceSeconds);
        Assert.Equal(3, result.ProblemCount);
    }

    [Fact]
    public void Report_lists_everything_and_counts_problems()
    {
        var report = DeckCheck.Format(CheckFixture()).ReplaceLineEndings("\n");

        Assert.Equal(
            """
            Marks:
              slide   1  ## 0:30
              slide   2  ## 5:00
              slide   5  ## 12:00
              slide   7  ## 29:30
            Talk length 29:30, tolerance 0:45
            Near misses (not used as marks):
              slide   4  "## 10:75"
            Marks on hidden slides (ignored):
              slide   3  ## 7:00
            Decreasing marks (ignored):
              slide   6  ## 11:00 after 12:00
            3 problems
            """,
            report);
    }

    [Fact]
    public void Length_argument_overrides_the_highest_mark()
    {
        var result = CheckFixture(lengthArgument: 2700);

        Assert.Equal(2700, result.LengthSeconds);
        Assert.Equal(60, result.ToleranceSeconds);
    }

    [Fact]
    public void A_clean_deck_reports_zero_problems()
    {
        using var deck = DeckBuilder.Build((false, ["## 1:00"]), (false, ["## 6:00"]));

        var report = DeckCheck.Format(DeckCheck.Run(DeckReader.Read(deck), null));

        Assert.EndsWith("0 problems", report);
    }

    [Fact]
    public void A_deck_without_marks_says_so()
    {
        using var deck = DeckBuilder.Build((false, ["Just text"]));

        var report = DeckCheck.Format(DeckCheck.Run(DeckReader.Read(deck), null)).ReplaceLineEndings("\n");

        Assert.StartsWith("No marks found.\nTalk length unknown", report);
        Assert.EndsWith("0 problems", report);
    }

    [Fact]
    public void One_problem_is_singular()
    {
        using var deck = DeckBuilder.Build((false, ["## 1:75"]));

        Assert.EndsWith("1 problem", DeckCheck.Format(DeckCheck.Run(DeckReader.Read(deck), null)));
    }
}
