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

    public FileLog()
    {
        Directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "speaker-buddy", "logs");
    }

    public string Directory { get; }

    public string CurrentFile => Path.Combine(Directory, LogRetention.FileName(DateTime.Now));

    public void Write(string message)
    {
        var now = DateTime.Now;
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
                if (LogRetention.IsExpired(Path.GetFileName(file), DateTime.Now))
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
