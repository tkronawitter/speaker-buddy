using SpeakerBuddy.Core;

namespace SpeakerBuddy.Core.Tests;

public class LogRepeatFilterTests
{
    private static readonly DateTime Start = new(2026, 11, 12, 9, 0, 0);
    private const string Busy = "PowerPoint call failed, tick skipped: COMException busy";

    [Fact]
    public void The_first_line_is_written()
    {
        Assert.Equal(new[] { Busy }, new LogRepeatFilter().Next(Busy, Start));
    }

    [Fact]
    public void Repeats_are_held_back_and_counted_on_recovery()
    {
        var filter = new LogRepeatFilter();
        filter.Next(Busy, Start);

        for (var i = 1; i <= 7; i++)
        {
            Assert.Empty(filter.Next(Busy, Start.AddMilliseconds(250 * i)));
        }

        Assert.Equal(
            new[] { $"repeated 7 more times: {Busy}", "timer connected on COM4" },
            filter.Next("timer connected on COM4", Start.AddSeconds(2)));
    }

    [Fact]
    public void A_line_that_keeps_repeating_is_reported_once_a_minute()
    {
        var filter = new LogRepeatFilter();
        filter.Next(Busy, Start);

        var written = new List<string>();
        for (var i = 1; i <= 4 * 150; i++)
        {
            written.AddRange(filter.Next(Busy, Start.AddMilliseconds(250 * i)));
        }

        // 150 s of four failures a second: reports at 60 s and 120 s, nothing else
        Assert.Equal(new[] { $"repeated 240 more times: {Busy}", $"repeated 240 more times: {Busy}" }, written);
    }

    [Fact]
    public void Different_lines_are_all_written()
    {
        var filter = new LogRepeatFilter();

        Assert.Equal(new[] { "a" }, filter.Next("a", Start));
        Assert.Equal(new[] { "b" }, filter.Next("b", Start));
        Assert.Equal(new[] { "a" }, filter.Next("a", Start));
    }
}
