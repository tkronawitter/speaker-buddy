using SpeakerBuddy.Core;

namespace SpeakerBuddy.Core.Tests;

public class LogRetentionTests
{
    private static readonly DateTime Today = new(2026, 11, 12, 9, 30, 0);

    [Theory]
    [InlineData("speaker-buddy-2026-10-13.log", false)]
    [InlineData("speaker-buddy-2026-10-12.log", true)]
    [InlineData("speaker-buddy-2025-01-01.log", true)]
    [InlineData("speaker-buddy-2026-11-12.log", false)]
    [InlineData("speaker-buddy-2026-13-01.log", false)]
    [InlineData("speaker-buddy-old.log", false)]
    [InlineData("speaker-buddy-2025-01-01.txt", false)]
    [InlineData("notes-2025-01-01.log", false)]
    public void Only_daily_logs_older_than_30_days_expire(string fileName, bool expired)
    {
        Assert.Equal(expired, LogRetention.IsExpired(fileName, Today));
    }

    [Fact]
    public void The_file_name_of_a_day_is_recognised_as_a_daily_log()
    {
        var name = LogRetention.FileName(Today.AddDays(-31));

        Assert.Equal("speaker-buddy-2026-10-12.log", name);
        Assert.True(LogRetention.IsExpired(name, Today));
    }
}
