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
    [InlineData("45", 2700)]
    [InlineData("30", 1800)]
    [InlineData(" 45 ", 2700)]
    [InlineData("44:30", 2670)]
    [InlineData("0:45", 45)]
    [InlineData("120:00", 7200)]
    public void Length_argument_accepts_minutes_or_m_ss(string text, int expected)
    {
        Assert.True(TalkLength.TryParseArgument(text, out var seconds));
        Assert.Equal(expected, seconds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("0:00")]
    [InlineData("-5")]
    [InlineData("45min")]
    [InlineData("44:5")]
    [InlineData("44:60")]
    [InlineData("1:00:00")]
    [InlineData("4,5")]
    [InlineData("1000")]
    public void Invalid_length_arguments_are_rejected(string? text)
    {
        Assert.False(TalkLength.TryParseArgument(text, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_argument_is_rejected(int argument)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TalkLength.Resolve(argument, MarkSet.Build([])));
    }
}
