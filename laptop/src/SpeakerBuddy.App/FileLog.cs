using System.Globalization;
using SpeakerBuddy.Core;

namespace SpeakerBuddy.App;

/// <summary>
/// Append-only log under <c>%LOCALAPPDATA%\speaker-buddy\logs</c>, one file per day (A-03). A
/// failing write is ignored: the log must never disturb the talk. Repeating lines are held back
/// (<see cref="LogRepeatFilter"/>), and files older than 30 days are deleted at start.
/// </summary>
public sealed class FileLog
{
    private readonly object _lock = new();
    private readonly LogRepeatFilter _repeats = new();
    private readonly Func<DateTime> _now;

    public FileLog()
        : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "speaker-buddy", "logs"),
            () => DateTime.Now)
    {
    }

    /// <summary>A log in <paramref name="directory"/> that reads the time from <paramref name="now"/>, for tests.</summary>
    internal FileLog(string directory, Func<DateTime> now)
    {
        Directory = directory;
        _now = now;
    }

    public string Directory { get; }

    public string CurrentFile => Path.Combine(Directory, LogRetention.FileName(_now()));

    public void Write(string message)
    {
        var now = _now();
        var time = now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        lock (_lock)
        {
            var lines = _repeats.Next(message, now);
            if (lines.Count == 0)
            {
                return;
            }

            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.AppendAllLines(CurrentFile, lines.Select(line => $"{time} {line}"));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Logging is best effort.
            }
        }
    }

    /// <summary>Deletes the daily logs older than 30 days; best effort, like writing.</summary>
    public void DeleteOldFiles()
    {
        var deleted = 0;
        try
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(Directory, $"{LogRetention.Prefix}*{LogRetention.Extension}"))
            {
                if (LogRetention.IsExpired(Path.GetFileName(file), _now()))
                {
                    File.Delete(file);
                    deleted++;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A missing folder or a locked file stays for the next start.
        }

        if (deleted > 0)
        {
            Write($"deleted {deleted} log files older than {LogRetention.KeepDays} days");
        }
    }
}
