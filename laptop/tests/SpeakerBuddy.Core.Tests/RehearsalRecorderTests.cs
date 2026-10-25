namespace SpeakerBuddy.Core.Tests;

public class RehearsalRecorderTests
{
    private const string Deck = @"C:\talks\demo.pptx";

    private static readonly SlideNotes[] Slides =
    [
        new(1, false, null),
        new(2, false, "## 1:00"),
        new(3, false, "## 2:00"),
    ];

    private static readonly MarkSet Marks = MarkSet.Build(Slides);
    private static readonly DateTime Clock = new(2026, 10, 25, 14, 30, 5);

    private static ShowState Show(int slide, string deck = Deck) => new(deck, slide, Slides);

    private static TimerStatusLine Timer(int t) => new(t, true);

    private static readonly Evaluation Green = new(EvaluationColor.Green, null, EvaluationRule.OnPlan);

    [Fact]
    public void The_first_slide_with_a_timer_status_opens_the_show()
    {
        var recorder = new RehearsalRecorder();

        var row = recorder.Next(Show(1), Timer(3), Green, Marks, Clock);

        Assert.Equal(new RehearsalRow(Deck, true, Clock, 1, null, 3, null, EvaluationColor.Green), row);
    }

    [Fact]
    public void Each_slide_change_gives_one_row_with_its_mark()
    {
        var recorder = new RehearsalRecorder();
        recorder.Next(Show(1), Timer(3), Green, Marks, Clock);

        Assert.Null(recorder.Next(Show(1), Timer(4), Green, Marks, Clock));
        var purple = new Evaluation(EvaluationColor.Purple, 25, EvaluationRule.Behind);
        var row = recorder.Next(Show(2), Timer(85), purple, Marks, Clock.AddSeconds(82));

        Assert.Equal(new RehearsalRow(Deck, false, Clock.AddSeconds(82), 2, 60, 85, 25, EvaluationColor.Purple), row);
        Assert.Null(recorder.Next(Show(2), Timer(86), purple, Marks, Clock));
    }

    [Fact]
    public void Going_back_a_slide_gives_a_row_too()
    {
        var recorder = new RehearsalRecorder();
        recorder.Next(Show(1), Timer(3), Green, Marks, Clock);
        recorder.Next(Show(2), Timer(60), Green, Marks, Clock);

        var row = recorder.Next(Show(1), Timer(70), Green, Marks, Clock);

        Assert.Equal(1, row!.Slide);
        Assert.False(row.FirstOfShow);
    }

    [Fact]
    public void Without_a_timer_status_or_an_evaluation_nothing_is_written_until_both_arrive()
    {
        var recorder = new RehearsalRecorder();

        Assert.Null(recorder.Next(Show(1), null, null, Marks, Clock));
        Assert.Null(recorder.Next(Show(1), Timer(0), null, Marks, Clock));
        var row = recorder.Next(Show(1), Timer(0), Green, Marks, Clock);

        Assert.True(row!.FirstOfShow);
    }

    [Fact]
    public void A_show_end_and_a_new_show_start_a_new_file()
    {
        var recorder = new RehearsalRecorder();
        recorder.Next(Show(2), Timer(60), Green, Marks, Clock);

        Assert.Null(recorder.Next(null, Timer(61), null, null, Clock));
        var row = recorder.Next(Show(2), Timer(62), Green, Marks, Clock);

        Assert.True(row!.FirstOfShow);
    }

    [Fact]
    public void A_different_presentation_starts_a_new_file()
    {
        var recorder = new RehearsalRecorder();
        recorder.Next(Show(1), Timer(3), Green, Marks, Clock);

        var row = recorder.Next(Show(1, @"C:\talks\other.pptx"), Timer(4), Green, Marks, Clock);

        Assert.True(row!.FirstOfShow);
        Assert.Equal(@"C:\talks\other.pptx", row.Presentation);
        Assert.Null(recorder.Next(Show(1, @"C:\TALKS\OTHER.PPTX"), Timer(5), Green, Marks, Clock));
    }

    [Fact]
    public void Without_marks_the_mark_column_stays_empty()
    {
        var recorder = new RehearsalRecorder();

        var row = recorder.Next(Show(2), Timer(5), Green, null, Clock);

        Assert.Null(row!.MarkSeconds);
    }

    [Fact]
    public void Rows_are_comma_separated_in_invariant_culture()
    {
        var row = new RehearsalRow(Deck, false, new DateTime(2026, 10, 25, 9, 5, 7), 12, 600, 645, -45, EvaluationColor.Red);

        Assert.Equal("time,slide,mark_s,t_s,deviation_s,colour", RehearsalRow.Header);
        Assert.Equal("09:05:07,12,600,645,-45,red", row.ToCsv());
        Assert.Equal("09:05:07,1,,3,,neutral",
            (row with { Slide = 1, MarkSeconds = null, Seconds = 3, Deviation = null, Color = EvaluationColor.Neutral }).ToCsv());
    }

    [Fact]
    public void Rows_ignore_the_current_culture()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            var row = new RehearsalRow(Deck, false, new DateTime(2026, 10, 25, 9, 5, 7), 2, 60, 1234, -5, EvaluationColor.Green);

            Assert.Equal("09:05:07,2,60,1234,-5,green", row.ToCsv());
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void A_deck_name_with_a_comma_never_reaches_a_column()
    {
        var recorder = new RehearsalRecorder();

        var row = recorder.Next(Show(2, @"C:\talks\Vortrag, Teil 1.pptx"), Timer(61), Green, Marks, Clock);

        Assert.Equal(5, row!.ToCsv().Count(c => c == RehearsalRow.Separator));
        Assert.Equal("rehearsal-2026-10-25-143005-Vortrag, Teil 1.csv", RehearsalRecorder.FileName(Clock, row.Presentation));
    }

    [Theory]
    [InlineData(@"C:\talks\demo.pptx", "rehearsal-2026-10-25-143005-demo.csv")]
    [InlineData(@"C:\talks\Mein Vortrag 2026.pptx", "rehearsal-2026-10-25-143005-Mein Vortrag 2026.csv")]
    [InlineData("https://example.com/decks/a|b.pptx", "rehearsal-2026-10-25-143005-a_b.csv")]
    [InlineData(@"C:\talks\v1.2 final.pptx", "rehearsal-2026-10-25-143005-v1.2 final.csv")]
    [InlineData(@"C:\talks\what? <draft>*.pptx", "rehearsal-2026-10-25-143005-what_ _draft__.csv")]
    [InlineData("demo", "rehearsal-2026-10-25-143005-demo.csv")]
    [InlineData("", "rehearsal-2026-10-25-143005.csv")]
    public void The_file_is_named_after_the_start_and_the_deck(string presentation, string expected)
    {
        Assert.Equal(expected, RehearsalRecorder.FileName(Clock, presentation));
    }
}
