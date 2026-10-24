using SpeakerTimer.Core;

namespace SpeakerTimer.Core.Tests;

public class MarkTests
{
    [Fact]
    public void Marks_with_same_slide_and_time_are_equal()
    {
        var a = new Mark(SlideIndex: 15, Seconds: 600);
        var b = new Mark(SlideIndex: 15, Seconds: 600);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Theory]
    [InlineData(16, 600)]
    [InlineData(15, 601)]
    public void Marks_differing_in_slide_or_time_are_not_equal(int slideIndex, int seconds)
    {
        var reference = new Mark(SlideIndex: 15, Seconds: 600);

        Assert.NotEqual(reference, new Mark(slideIndex, seconds));
    }
}
