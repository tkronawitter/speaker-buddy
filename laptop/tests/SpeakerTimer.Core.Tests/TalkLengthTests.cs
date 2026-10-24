using SpeakerTimer.Core;

namespace SpeakerTimer.Core.Tests;

public class TalkLengthTests
{
    [Theory]
    [InlineData(45 * 60, 60)]
    [InlineData(30 * 60, 40)]
    [InlineData(15 * 60, 30)]
    [InlineData(90 * 60, 120)]
    public void Tolerance_is_a_45th_of_the_length_with_a_30_second_floor(int lengthSeconds, int expected)
    {
        Assert.Equal(expected, TalkLength.Tolerance(lengthSeconds));
    }

    [Fact]
    public void Tolerance_without_a_length_is_the_floor()
    {
        Assert.Equal(30, TalkLength.Tolerance(null));
    }

    [Fact]
    public void Argument_wins_over_the_highest_mark()
    {
        var marks = MarkSet.Build([new SlideNotes(1, false, "## 29:00")]);

        Assert.Equal(2700, TalkLength.Resolve(2700, marks));
    }

    [Fact]
    public void Highest_mark_of_at_least_five_minutes_is_used_without_argument()
    {
        var marks = MarkSet.Build([new SlideNotes(1, false, "## 2:00"), new SlideNotes(5, false, "## 5:00")]);

        Assert.Equal(300, TalkLength.Resolve(null, marks));
    }

    [Fact]
    public void Highest_mark_below_five_minutes_gives_no_length_without_argument()
    {
        var marks = MarkSet.Build([new SlideNotes(1, false, "## 4:00")]);

        Assert.Null(TalkLength.Resolve(null, marks));
    }

    [Fact]
    public void No_marks_and_no_argument_gives_no_length()
    {
        Assert.Null(TalkLength.Resolve(null, MarkSet.Build([])));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_argument_is_rejected(int argument)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TalkLength.Resolve(argument, MarkSet.Build([])));
    }
}
