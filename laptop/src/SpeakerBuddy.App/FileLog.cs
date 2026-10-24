using System.Globalization;

namespace SpeakerBuddy.App;

/// <summary>
/// Append-only log under <c>%LOCALAPPDATA%\speaker-buddy\logs</c>, one file per day (A-03). A
/// failing write is ignored: the log must never disturb the talk.
/// </summary>
public sealed class FileLog
{
    private readonly object _lock = new();

    public FileLog()
    {
        Directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "speaker-buddy", "logs");
    }

    public string Directory { get; }

    public string CurrentFile =>
        Path.Combine(Directory, $"speaker-buddy-{DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.log");

    public void Write(string message)
    {
        var line = $"{DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)} {message}{Environment.NewLine}";
        lock (_lock)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.AppendAllText(CurrentFile, line);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Logging is best effort.
            }
        }
    }
}
