using SpeakerBuddy.Core;

namespace SpeakerBuddy.App;

/// <summary>
/// Writes the rehearsal rows (requirement L-08, option <c>--rehearsal</c>) to a CSV file per show,
/// next to the daily log. Best effort like <see cref="FileLog"/>: a failing write is logged once and
/// never disturbs the talk.
/// </summary>
internal sealed class RehearsalLog
{
    private readonly string _directory;
    private readonly Action<string> _log;
    private string? _file;
    private bool _failing;

    public RehearsalLog(string directory, Action<string> log)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>The file of the current show, or null before its first row was written.</summary>
    public string? CurrentFile => _file;

    public void Write(RehearsalRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        try
        {
            if (row.FirstOfShow || _file is null)
            {
                _file = null;
                Directory.CreateDirectory(_directory);
                var file = Path.Combine(_directory, RehearsalRecorder.FileName(row.Time, row.Presentation));
                File.WriteAllText(file, RehearsalRow.Header + Environment.NewLine);
                _file = file;
                _log($"rehearsal log: {file}");
            }

            File.AppendAllText(_file, row.ToCsv() + Environment.NewLine);
            _failing = false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (!_failing)
            {
                _failing = true;
                _log($"rehearsal log failed: {e.Message}");
            }
        }
    }
}
