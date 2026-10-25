using SpeakerBuddy.Core;

namespace SpeakerBuddy.App.Tests;

public sealed class FileLogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"speaker-buddy-tests-{Guid.NewGuid():N}");
    private DateTime _now = new(2026, 10, 25, 9, 30, 15, 250);

    private string Logs => Path.Combine(_root, "logs");

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_root))
        {
            System.IO.Directory.Delete(_root, recursive: true);
        }
    }

    private FileLog Log() => new(Logs, () => _now);

    private string[] Lines(DateTime day) => File.ReadAllLines(Path.Combine(Logs, LogRetention.FileName(day)));

    [Fact]
    public void The_default_log_lives_under_local_app_data()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "speaker-buddy", "logs");

        Assert.Equal(expected, new FileLog().Directory);
    }

    [Fact]
    public void The_current_file_is_named_after_the_day()
    {
        Assert.Equal(Path.Combine(Logs, "speaker-buddy-2026-10-25.log"), Log().CurrentFile);
    }

    [Fact]
    public void Writing_creates_the_folder_and_appends_lines_with_the_time()
    {
        var log = Log();

        log.Write("started");
        _now = _now.AddSeconds(2);
        log.Write("timer connected on COM4");

        Assert.Equal(["09:30:15.250 started", "09:30:17.250 timer connected on COM4"], Lines(_now));
    }

    [Fact]
    public void A_new_day_writes_a_new_file()
    {
        var log = Log();
        var first = _now;

        log.Write("before midnight");
        _now = new DateTime(2026, 10, 26, 0, 0, 1);
        log.Write("after midnight");

        Assert.Equal(["09:30:15.250 before midnight"], Lines(first));
        Assert.Equal(["00:00:01.000 after midnight"], Lines(_now));
    }

    [Fact]
    public void Repeating_lines_are_held_back_and_reported_with_their_count()
    {
        var log = Log();

        log.Write("PowerPoint busy");
        _now = _now.AddSeconds(1);
        log.Write("PowerPoint busy");
        _now = _now.AddSeconds(1);
        log.Write("PowerPoint busy");
        _now = _now.AddSeconds(1);
        log.Write("show ended");

        Assert.Equal(
            [
                "09:30:15.250 PowerPoint busy",
                "09:30:18.250 repeated 2 more times: PowerPoint busy",
                "09:30:18.250 show ended",
            ],
            Lines(_now));
    }

    [Fact]
    public void A_line_that_keeps_repeating_is_reported_once_a_minute()
    {
        var log = Log();

        log.Write("PowerPoint busy");
        _now = _now.AddSeconds(30);
        log.Write("PowerPoint busy");
        _now = _now.AddSeconds(30);
        log.Write("PowerPoint busy");

        Assert.Equal(
            ["09:30:15.250 PowerPoint busy", "09:31:15.250 repeated 2 more times: PowerPoint busy"],
            Lines(_now));
    }

    [Fact]
    public void A_failing_write_is_ignored()
    {
        // A file where the log folder should be makes creating the folder fail
        System.IO.Directory.CreateDirectory(_root);
        File.WriteAllText(Logs, "in the way");

        var log = Log();
        log.Write("lost");

        Assert.Equal("in the way", File.ReadAllText(Logs));
    }

    [Fact]
    public void A_locked_log_file_is_skipped_without_an_error()
    {
        var log = Log();
        log.Write("first");
        using (new FileStream(log.CurrentFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            log.Write("while locked");
        }

        log.Write("after");

        Assert.Equal(["09:30:15.250 first", "09:30:15.250 after"], Lines(_now));
    }

    private string CreateLog(string name)
    {
        System.IO.Directory.CreateDirectory(Logs);
        var path = Path.Combine(Logs, name);
        File.WriteAllText(path, "old");
        return path;
    }

    [Fact]
    public void Old_daily_logs_are_deleted_and_the_deletion_is_logged()
    {
        var expired = CreateLog("speaker-buddy-2026-09-24.log");
        var alsoExpired = CreateLog("speaker-buddy-2025-01-01.log");
        var kept = CreateLog("speaker-buddy-2026-09-25.log");
        var foreign = CreateLog("speaker-buddy-notes.log");

        Log().DeleteOldFiles();

        Assert.False(File.Exists(expired));
        Assert.False(File.Exists(alsoExpired));
        Assert.True(File.Exists(kept));
        Assert.True(File.Exists(foreign));
        Assert.Equal(["09:30:15.250 deleted 2 log files older than 30 days"], Lines(_now));
    }

    [Fact]
    public void Nothing_to_delete_writes_nothing()
    {
        CreateLog("speaker-buddy-2026-10-24.log");

        Log().DeleteOldFiles();

        Assert.False(File.Exists(Path.Combine(Logs, LogRetention.FileName(_now))));
    }

    [Fact]
    public void A_missing_log_folder_is_no_error()
    {
        Log().DeleteOldFiles();

        Assert.False(System.IO.Directory.Exists(Logs));
    }

    [Fact]
    public void A_locked_old_log_stays_for_the_next_start()
    {
        var locked = CreateLog("speaker-buddy-2026-01-01.log");

        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Log().DeleteOldFiles();
        }

        Assert.True(File.Exists(locked));
        Assert.False(File.Exists(Path.Combine(Logs, LogRetention.FileName(_now))));
    }
}
