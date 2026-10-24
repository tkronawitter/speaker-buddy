using SpeakerBuddy.Core;

namespace SpeakerBuddy.Core.Tests;

public class EvaluatorTests
{
    // Worked example from the requirements: L = 45 min, tol = 1:00, slide 15 carries "## 10:00".
    private const int Tolerance = 60;

    private static Evaluator WorkedExample() =>
        new(MarkSet.Build([new SlideNotes(15, false, "## 10:00")]), Tolerance);

    private static Evaluator ThreeMarks() =>
        new(MarkSet.Build(
        [
            new SlideNotes(5, false, "## 5:00"),
            new SlideNotes(10, false, "## 10:00"),
            new SlideNotes(15, false, "## 15:00"),
        ]), Tolerance);

    [Fact]
    public void Worked_example_arrival_at_10_40_is_green()
    {
        var result = WorkedExample().Update(640, 15);

        Assert.Equal(new Evaluation(EvaluationColor.Green, null, EvaluationRule.OnPlan), result);
    }

    [Fact]
    public void Worked_example_arrival_at_12_00_is_purple()
    {
        var result = WorkedExample().Update(720, 15);

        Assert.Equal(new Evaluation(EvaluationColor.Purple, 120, EvaluationRule.Behind), result);
    }

    [Fact]
    public void Worked_example_arrival_at_8_30_is_red_and_turns_green_at_9_00()
    {
        var evaluator = WorkedExample();

        Assert.Equal(new Evaluation(EvaluationColor.Red, -90, EvaluationRule.Ahead), evaluator.Update(510, 15));
        Assert.Equal(new Evaluation(EvaluationColor.Red, -61, EvaluationRule.Ahead), evaluator.Update(539, 15));
        Assert.Equal(new Evaluation(EvaluationColor.Green, null, EvaluationRule.Hold), evaluator.Update(540, 15));
    }

    [Fact]
    public void Worked_example_still_on_slide_13_at_11_01_is_purple_by_b06()
    {
        var evaluator = WorkedExample();

        Assert.Equal(new Evaluation(EvaluationColor.Neutral, null, EvaluationRule.BeforeFirstMark), evaluator.Update(660, 13));
        Assert.Equal(new Evaluation(EvaluationColor.Purple, 61, EvaluationRule.LateForNextMark), evaluator.Update(661, 13));
    }

    [Fact]
    public void B01_before_the_first_mark_is_neutral()
    {
        var result = ThreeMarks().Update(10, 1);

        Assert.Equal(new Evaluation(EvaluationColor.Neutral, null, EvaluationRule.BeforeFirstMark), result);
    }

    [Theory]
    [InlineData(240)]
    [InlineData(300)]
    [InlineData(360)]
    public void B02_arrival_within_tolerance_either_way_is_green(int t)
    {
        var result = ThreeMarks().Update(t, 5);

        Assert.Equal(EvaluationColor.Green, result.Color);
        Assert.Equal(EvaluationRule.OnPlan, result.Rule);
        Assert.Null(result.Deviation);
    }

    [Fact]
    public void B03_arrival_more_than_tolerance_late_is_purple()
    {
        var result = ThreeMarks().Update(361, 5);

        Assert.Equal(new Evaluation(EvaluationColor.Purple, 61, EvaluationRule.Behind), result);
    }

    [Fact]
    public void B04_arrival_more_than_tolerance_early_is_red()
    {
        var result = ThreeMarks().Update(239, 5);

        Assert.Equal(new Evaluation(EvaluationColor.Red, -61, EvaluationRule.Ahead), result);
    }

    [Fact]
    public void B05_colour_of_the_last_evaluation_holds_between_marks()
    {
        var evaluator = ThreeMarks();
        evaluator.Update(380, 5);

        Assert.Equal(new Evaluation(EvaluationColor.Purple, 80, EvaluationRule.Hold), evaluator.Update(400, 6));
        Assert.Equal(new Evaluation(EvaluationColor.Purple, 80, EvaluationRule.Hold), evaluator.Update(500, 7));
    }

    [Fact]
    public void B06_overdue_next_mark_turns_purple_before_the_slide_comes()
    {
        var evaluator = ThreeMarks();
        evaluator.Update(300, 5);

        Assert.Equal(EvaluationRule.Hold, evaluator.Update(660, 8).Rule);
        Assert.Equal(new Evaluation(EvaluationColor.Purple, 61, EvaluationRule.LateForNextMark), evaluator.Update(661, 8));
    }

    [Fact]
    public void B06_takes_precedence_over_a_green_hold()
    {
        var evaluator = new Evaluator(MarkSet.Build(
        [
            new SlideNotes(1, false, "## 5:00"),
            new SlideNotes(2, false, "## 5:30"),
        ]), Tolerance);
        evaluator.Update(300, 1);

        Assert.Equal(new Evaluation(EvaluationColor.Green, null, EvaluationRule.Hold), evaluator.Update(390, 1));
        Assert.Equal(new Evaluation(EvaluationColor.Purple, 61, EvaluationRule.LateForNextMark), evaluator.Update(391, 1));
    }

    [Fact]
    public void B06_takes_precedence_over_an_arrival()
    {
        var evaluator = ThreeMarks();

        var result = evaluator.Update(661, 5);

        Assert.Equal(new Evaluation(EvaluationColor.Purple, 61, EvaluationRule.LateForNextMark), result);
    }

    [Fact]
    public void Each_mark_is_evaluated_once()
    {
        var evaluator = ThreeMarks();
        evaluator.Update(300, 5);

        Assert.Equal(new Evaluation(EvaluationColor.Green, null, EvaluationRule.Hold), evaluator.Update(380, 5));
    }

    [Fact]
    public void Going_back_re_evaluates_nothing()
    {
        var evaluator = ThreeMarks();
        evaluator.Update(600, 10);

        Assert.Equal(new Evaluation(EvaluationColor.Green, null, EvaluationRule.Hold), evaluator.Update(610, 4));
        Assert.Equal(new Evaluation(EvaluationColor.Green, null, EvaluationRule.Hold), evaluator.Update(620, 5));
        Assert.Equal(new Evaluation(EvaluationColor.Green, null, EvaluationRule.Hold), evaluator.Update(630, 10));
    }

    [Fact]
    public void Jumping_past_marks_reaches_them_and_the_last_one_is_shown()
    {
        var evaluator = ThreeMarks();

        var result = evaluator.Update(640, 12);

        Assert.Equal(new Evaluation(EvaluationColor.Green, null, EvaluationRule.OnPlan), result);
        Assert.Equal(new Evaluation(EvaluationColor.Purple, 61, EvaluationRule.LateForNextMark), evaluator.Update(961, 12));
    }

    [Fact]
    public void After_the_last_mark_the_colour_holds()
    {
        var evaluator = ThreeMarks();
        evaluator.Update(900, 15);

        Assert.Equal(new Evaluation(EvaluationColor.Green, null, EvaluationRule.Hold), evaluator.Update(5000, 20));
    }

    [Fact]
    public void Deviation_never_contradicts_the_colour()
    {
        // Desk test 2026-10-24: red on arrival at -38, then green, then purple for the overdue next
        // mark showed the frozen -38 ("too fast") next to purple ("talk faster")
        var evaluator = new Evaluator(MarkSet.Build(
        [
            new SlideNotes(3, false, "## 2:00"),
            new SlideNotes(4, false, "## 3:00"),
        ]), 30);

        Assert.Equal(new Evaluation(EvaluationColor.Red, -38, EvaluationRule.Ahead), evaluator.Update(82, 3));
        Assert.Equal(new Evaluation(EvaluationColor.Green, null, EvaluationRule.Hold), evaluator.Update(90, 3));
        Assert.Equal(new Evaluation(EvaluationColor.Purple, 31, EvaluationRule.LateForNextMark), evaluator.Update(211, 3));
        Assert.Equal(new Evaluation(EvaluationColor.Purple, 50, EvaluationRule.LateForNextMark), evaluator.Update(230, 3));
    }

    [Fact]
    public void Purple_after_a_late_arrival_keeps_the_lateness()
    {
        var evaluator = ThreeMarks();
        evaluator.Update(380, 5);

        Assert.Equal(new Evaluation(EvaluationColor.Purple, 80, EvaluationRule.Hold), evaluator.Update(600, 6));
    }

    [Fact]
    public void A_deck_without_marks_is_not_evaluated()
    {
        var evaluator = new Evaluator(MarkSet.Build([new SlideNotes(1, false, "text")]), Tolerance);

        Assert.Equal(new Evaluation(EvaluationColor.Neutral, null, EvaluationRule.NoMarks), evaluator.Update(9999, 1));
    }

    [Fact]
    public void Negative_tolerance_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Evaluator(MarkSet.Build([]), -1));
    }
}
