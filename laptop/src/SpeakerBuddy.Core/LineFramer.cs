using System.Text;

namespace SpeakerBuddy.Core;

/// <summary>
/// Splits received bytes into protocol lines (docs/protocol.md). A line longer than
/// <see cref="Protocol.MaxLineBytes"/> including its terminator is dropped up to the next
/// <c>\n</c>, so one broken line never corrupts the next.
/// </summary>
public sealed class LineFramer
{
    private readonly byte[] _buffer = new byte[Protocol.MaxLineBytes];
    private int _length;
    private bool _tooLong;

    /// <summary>Feeds received bytes and returns the complete lines they finish, without terminator.</summary>
    public IReadOnlyList<string> Feed(ReadOnlySpan<byte> data)
    {
        var lines = new List<string>();
        foreach (var b in data)
        {
            if (b == (byte)'\n')
            {
                if (!_tooLong)
                {
                    lines.Add(Encoding.UTF8.GetString(_buffer, 0, _length));
                }

                _length = 0;
                _tooLong = false;
            }
            else if (_length < _buffer.Length - 1)
            {
                _buffer[_length++] = b;
            }
            else
            {
                _tooLong = true;
            }
        }

        return lines;
    }

    /// <summary>Discards a partial line, for example after the port was reopened.</summary>
    public void Reset()
    {
        _length = 0;
        _tooLong = false;
    }
}
