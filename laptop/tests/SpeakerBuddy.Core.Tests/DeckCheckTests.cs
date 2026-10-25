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
            Talk length 29:30 from the highest mark (slide 7), tolerance 0:45
            Near misses (probably typos, not used):
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

    private static DeckCheckResult Check(int? lengthArgument, params SlideNotes[] slides) =>
        DeckCheck.Run(slides, lengthArgument);

    private static string Report(int? lengthArgument, params SlideNotes[] slides) =>
        DeckCheck.Format(Check(lengthArgument, slides)).ReplaceLineEndings("\n");

    [Fact]
    public void The_session_length_counts_and_the_report_says_where_it_comes_from()
    {
        var report = Report(null,
            new SlideNotes(1, false, "## 10:00"),
            new SlideNotes(30, false, "## 52:00\r## session length 60:00"));

        Assert.Contains("Talk length 60:00 from ## session length on slide 30, tolerance 1:20\n", report);
        Assert.EndsWith("0 problems", report);
    }

    [Fact]
    public void The_length_argument_is_named_and_shows_the_session_length_it_overrides()
    {
        var report = Report(2700, new SlideNotes(30, false, "## session length 60:00"));

        Assert.Contains("Talk length 45:00 from --length, tolerance 1:00\n", report);
        Assert.Contains("  (--length overrides ## session length 60:00 on slide 30)\n", report);
        Assert.EndsWith("0 problems", report);
    }

    [Fact]
    public void An_unknown_length_names_all_three_sources()
    {
        Assert.Contains(
            "Talk length unknown (no --length, no ## session length, highest mark below 5:00)",
            Report(null, new SlideNotes(1, false, "## 3:00")));
    }

    [Fact]
    public void Session_length_typos_are_near_misses_and_do_not_count()
    {
        var result = Check(null,
            new SlideNotes(1, false, "## session lenght 60:00"),
            new SlideNotes(2, false, "## length 60:00"),
            new SlideNotes(3, false, "## 20:00"));

        Assert.Equal(new[] { new NearMiss(1, "## session lenght 60:00"), new NearMiss(2, "## length 60:00") }, result.NearMisses);
        Assert.Equal(LengthSource.HighestMark, result.Length.Source);
        Assert.Equal(2, result.ProblemCount);
    }

    [Fact]
    public void A_session_length_on_a_hidden_slide_is_ignored_and_reported()
    {
        var result = Check(null,
            new SlideNotes(1, false, "## 20:00"),
            new SlideNotes(2, true, "## session length 60:00"));

        Assert.Equal(1200, result.LengthSeconds);
        Assert.Equal(new[] { new SessionLength(2, 3600) }, result.HiddenSessionLengths);
        Assert.Equal(1, result.ProblemCount);
        Assert.Contains(
            "Session lengths on hidden slides (ignored):\n  slide   2  ## session length 60:00\n",
            DeckCheck.Format(result).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void A_second_session_length_is_a_problem_and_the_first_counts()
    {
        var result = Check(null,
            new SlideNotes(2, false, "## session length 45:00"),
            new SlideNotes(30, false, "## session length 60:00"));

        Assert.Equal(2700, result.LengthSeconds);
        Assert.Equal(new[] { new SessionLength(30, 3600) }, result.ExtraSessionLengths);
        Assert.Equal(1, result.ProblemCount);
        Assert.Contains(
            "More than one session length (only the first counts, these are ignored):\n  slide  30  ## session length 60:00\n",
            DeckCheck.Format(result).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void One_problem_is_singular()
    {
        using var deck = DeckBuilder.Build((false, ["## 1:75"]));

        Assert.EndsWith("1 problem", DeckCheck.Format(DeckCheck.Run(DeckReader.Read(deck), null)));
    }
}
