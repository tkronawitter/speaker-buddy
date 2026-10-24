using SpeakerTimer.Core;

namespace SpeakerTimer.Core.Tests;

public class MarkSetTests
{
    [Fact]
    public void Marks_are_collected_in_slide_order()
    {
        var set = MarkSet.Build(
        [
            new SlideNotes(3, false, "## 10:00"),
            new SlideNotes(1, false, "## 2:00"),
            new SlideNotes(2, false, "no mark"),
        ]);

        Assert.Equal(new[] { new Mark(1, 120), new Mark(3, 600) }, set.Marks);
        Assert.Empty(set.Warnings);
    }

    [Fact]
    public void Marks_on_hidden_slides_are_ignored()
    {
        var set = MarkSet.Build(
        [
            new SlideNotes(1, false, "## 2:00"),
            new SlideNotes(2, true, "## 5:00"),
            new SlideNotes(3, false, "## 10:00"),
        ]);

        Assert.Equal(new[] { new Mark(1, 120), new Mark(3, 600) }, set.Marks);
        Assert.Empty(set.Warnings);
    }

    [Fact]
    public void A_decreasing_mark_is_dropped_with_a_warning()
    {
        var set = MarkSet.Build(
        [
            new SlideNotes(1, false, "## 5:00"),
            new SlideNotes(2, false, "## 4:00"),
            new SlideNotes(3, false, "## 6:00"),
        ]);

        Assert.Equal(new[] { new Mark(1, 300), new Mark(3, 360) }, set.Marks);
        Assert.Equal(new[] { new MarkWarning(2, 240, 300) }, set.Warnings);
    }

    [Fact]
    public void A_dropped_mark_does_not_become_the_reference_for_the_next_one()
    {
        var set = MarkSet.Build(
        [
            new SlideNotes(1, false, "## 5:00"),
            new SlideNotes(2, false, "## 1:00"),
            new SlideNotes(3, false, "## 4:00"),
        ]);

        Assert.Equal(new[] { new Mark(1, 300) }, set.Marks);
        Assert.Equal(new[] { new MarkWarning(2, 60, 300), new MarkWarning(3, 240, 300) }, set.Warnings);
    }

    [Fact]
    public void Equal_marks_on_consecutive_slides_are_allowed()
    {
        var set = MarkSet.Build(
        [
            new SlideNotes(1, false, "## 5:00"),
            new SlideNotes(2, false, "## 5:00"),
        ]);

        Assert.Equal(2, set.Marks.Count);
        Assert.Empty(set.Warnings);
    }

    [Fact]
    public void A_deck_without_marks_gives_an_empty_set()
    {
        var set = MarkSet.Build([new SlideNotes(1, false, null), new SlideNotes(2, false, "text")]);

        Assert.Empty(set.Marks);
        Assert.Null(set.HighestSeconds);
    }

    [Fact]
    public void Highest_seconds_is_the_last_mark()
    {
        var set = MarkSet.Build([new SlideNotes(1, false, "## 5:00"), new SlideNotes(9, false, "## 29:30")]);

        Assert.Equal(1770, set.HighestSeconds);
    }
}
