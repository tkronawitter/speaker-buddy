using System.Collections.Concurrent;
using System.Text;
using SpeakerBuddy.Core;

namespace SpeakerBuddy.App.Tests;

/// <summary>A board on a serial port: the test feeds what the timer sends and reads what was written.</summary>
internal sealed class FakeSerialDevice : ISerialDevice
{
    private readonly BlockingCollection<byte[]> _incoming = [];
    private readonly StringBuilder _written = new();
    private volatile bool _lost;

    public bool IsOpen { get; private set; } = true;

    public bool Disposed { get; private set; }

    public string Written
    {
        get
        {
            lock (_written)
            {
                return _written.ToString();
            }
        }
    }

    public void Receive(string text) => _incoming.Add(Encoding.UTF8.GetBytes(text));

    /// <summary>The cable is pulled: the next read fails as Windows reports a vanished port.</summary>
    public void Lose() => _lost = true;

    public int Read(byte[] buffer, int offset, int count)
    {
        if (_lost)
        {
            throw new IOException("The device does not recognize the command.");
        }

        if (!_incoming.TryTake(out var chunk, TimeSpan.FromMilliseconds(20)))
        {
            throw new TimeoutException();
        }

        if (chunk.Length > count)
        {
            throw new InvalidOperationException("Test chunks must fit one read.");
        }

        chunk.CopyTo(buffer, offset);
        return chunk.Length;
    }

    public void Write(string text)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("The port is closed.");
        }

        lock (_written)
        {
            _written.Append(text);
        }
    }

    public void Close() => IsOpen = false;

    public void Dispose()
    {
        IsOpen = false;
        Disposed = true;
    }
}

/// <summary>The timer link as TalkRunner sees it, without a thread or a port.</summary>
internal sealed class FakeTimerLink : ITimerLink
{
    private readonly List<ProtocolLine> _sent = [];

    public event Action<string>? Connected;

    public event Action? Disconnected;

    public event Action<TimerStatusLine>? StatusReceived;

    public event Action<FirmwareLine>? FirmwareReceived;

    public bool Started { get; private set; }

    public int DisposeCount { get; private set; }

    public IReadOnlyList<ProtocolLine> Sent
    {
        get
        {
            lock (_sent)
            {
                return [.. _sent];
            }
        }
    }

    public void Start() => Started = true;

    public bool Send(ProtocolLine line)
    {
        lock (_sent)
        {
            _sent.Add(line);
        }

        return true;
    }

    public void Clear()
    {
        lock (_sent)
        {
            _sent.Clear();
        }
    }

    public void RaiseConnected(string port) => Connected?.Invoke(port);

    public void RaiseDisconnected() => Disconnected?.Invoke();

    public void RaiseStatus(TimerStatusLine status) => StatusReceived?.Invoke(status);

    public void RaiseFirmware(FirmwareLine firmware) => FirmwareReceived?.Invoke(firmware);

    public void Dispose() => DisposeCount++;
}

/// <summary>A log that keeps its lines for assertions; written from several threads.</summary>
internal sealed class MemoryLog
{
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lines)
            {
                return [.. _lines];
            }
        }
    }

    public void Write(string line)
    {
        lock (_lines)
        {
            _lines.Add(line);
        }
    }
}

internal static class Wait
{
    /// <summary>Waits for a condition another thread makes true, and fails the test after 5 s.</summary>
    public static void Until(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"timed out waiting for {what}");
            }

            Thread.Sleep(10);
        }
    }
}
