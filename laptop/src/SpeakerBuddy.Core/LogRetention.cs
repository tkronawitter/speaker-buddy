using System.Globalization;

namespace SpeakerBuddy.Core;

/// <summary>
/// Which daily log files are old enough to delete. The date comes from the file name
/// (<c>speaker-buddy-yyyy-MM-dd.log</c>), so a file touched later is still judged by its day.
/// </summary>
public static class LogRetention
{
    public const int KeepDays = 30;
    public const string Prefix = "speaker-buddy-";
    public const string Extension = ".log";

    /// <summary>The log file name for a day.</summary>
    public static string FileName(DateTime day) =>
        $"{Prefix}{day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}{Extension}";

    /// <summary>
    /// True when <paramref name="fileName"/> is a daily log older than <see cref="KeepDays"/> days
    /// before <paramref name="today"/>. Any other file name is never expired.
    /// </summary>
    public static bool IsExpired(string fileName, DateTime today)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        if (!fileName.StartsWith(Prefix, StringComparison.Ordinal)
            || !fileName.EndsWith(Extension, StringComparison.Ordinal)
            || !DateTime.TryParseExact(
                fileName[Prefix.Length..^Extension.Length], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var day))
        {
            return false;
        }

        return day < today.Date.AddDays(-KeepDays);
    }
}
