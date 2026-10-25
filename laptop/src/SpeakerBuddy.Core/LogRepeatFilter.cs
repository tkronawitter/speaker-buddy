namespace SpeakerBuddy.Core;

/// <summary>
/// Keeps a repeating log line from flooding the log (a busy PowerPoint fails four polls a second):
/// the first occurrence is written, repeats are counted, and the count goes out once a minute while
/// they last and again with the first different line (the recovery). Not thread-safe; the log
/// calls it under its lock.
/// </summary>
public sealed class LogRepeatFilter
{
    /// <summary>How often a line that keeps repeating is reported with its count.</summary>
    public static readonly TimeSpan ReportInterval = TimeSpan.FromMinutes(1);

    private string? _last;
    private int _repeats;
    private DateTime _lastWritten;

    /// <summary>Returns the lines to write for <paramref name="message"/>, possibly none.</summary>
    public IReadOnlyList<string> Next(string message, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message == _last)
        {
            _repeats++;
            if (now - _lastWritten < ReportInterval)
            {
                return [];
            }

            var report = Repeated();
            _repeats = 0;
            _lastWritten = now;
            return [report];
        }

        var lines = new List<string>();
        if (_repeats > 0)
        {
            lines.Add(Repeated());
        }

        lines.Add(message);
        _last = message;
        _repeats = 0;
        _lastWritten = now;
        return lines;
    }

    private string Repeated() => $"repeated {_repeats} more times: {_last}";
}
