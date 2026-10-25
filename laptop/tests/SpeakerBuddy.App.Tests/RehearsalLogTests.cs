using SpeakerBuddy.Core;

namespace SpeakerBuddy.App.Tests;

public sealed class RehearsalLogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"speaker-buddy-tests-{Guid.NewGuid():N}");
    private readonly MemoryLog _log = new();

    private string Logs => Path.Combine(_root, "logs");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static RehearsalRow Row(bool first, int slide, int t, DateTime? time = null, string deck = @"C:\talks\demo.pptx") =>
        new(deck, first, time ?? new DateTime(2026, 10, 25, 14, 30, 5), slide, slide == 2 ? 60 : null, t,
            slide == 2 ? 25 : null, slide == 2 ? EvaluationColor.Purple : EvaluationColor.Neutral);

    [Fact]
    public void The_first_row_of_a_show_creates_the_file_with_a_header()
    {
        var log = new RehearsalLog(Logs, _log.Write);

        log.Write(Row(true, 1, 3));
        log.Write(Row(false, 2, 85));

        var file = Path.Combine(Logs, "rehearsal-2026-10-25-143005-demo.csv");
        Assert.Equal(file, log.CurrentFile);
        Assert.Equal(
            ["time,slide,mark_s,t_s,deviation_s,colour", "14:30:05,1,,3,,neutral", "14:30:05,2,60,85,25,purple"],
            File.ReadAllLines(file));
        Assert.Equal([$"rehearsal log: {file}"], _log.Lines);
    }

    [Fact]
    public void The_file_is_utf8_without_a_byte_order_mark()
    {
        var log = new RehearsalLog(Logs, _log.Write);

        log.Write(Row(true, 1, 3, deck: @"C:\talks\Übung.pptx"));

        var bytes = File.ReadAllBytes(log.CurrentFile!);
        Assert.Equal((byte)'t', bytes[0]);
        Assert.EndsWith("Übung.csv", log.CurrentFile);
    }

    [Fact]
    public void Each_show_gets_a_file_of_its_own()
    {
        var log = new RehearsalLog(Logs, _log.Write);
        log.Write(Row(true, 1, 3));

        log.Write(Row(true, 1, 4, new DateTime(2026, 10, 25, 15, 0, 0)));

        Assert.Equal(2, Directory.GetFiles(Logs, "rehearsal-*.csv").Length);
        Assert.Equal(["time,slide,mark_s,t_s,deviation_s,colour", "15:00:00,1,,4,,neutral"], File.ReadAllLines(log.CurrentFile!));
    }

    [Fact]
    public void A_failing_write_is_logged_once_and_the_log_recovers()
    {
        // A file where the log folder should be makes creating the folder fail
        Directory.CreateDirectory(_root);
        File.WriteAllText(Logs, "in the way");
        var log = new RehearsalLog(Logs, _log.Write);

        log.Write(Row(true, 1, 3));
        log.Write(Row(false, 2, 85));

        Assert.Null(log.CurrentFile);
        Assert.Single(_log.Lines, line => line.StartsWith("rehearsal log failed: ", StringComparison.Ordinal));

        // Once the folder can be made, the next row opens the file, even mid-show
        File.Delete(Logs);
        log.Write(Row(false, 3, 120));

        Assert.NotNull(log.CurrentFile);
        Assert.Equal(2, File.ReadAllLines(log.CurrentFile!).Length);
    }

    [Fact]
    public void A_locked_file_skips_the_row_without_an_error()
    {
        var log = new RehearsalLog(Logs, _log.Write);
        log.Write(Row(true, 1, 3));

        using (new FileStream(log.CurrentFile!, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            log.Write(Row(false, 2, 85));
        }

        log.Write(Row(false, 3, 120));

        Assert.Equal(3, File.ReadAllLines(log.CurrentFile!).Length);
        Assert.Single(_log.Lines, line => line.StartsWith("rehearsal log failed: ", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_arguments_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new RehearsalLog(null!, _log.Write));
        Assert.Throws<ArgumentNullException>(() => new RehearsalLog(Logs, null!));
        Assert.Throws<ArgumentNullException>(() => new RehearsalLog(Logs, _log.Write).Write(null!));
    }
}
