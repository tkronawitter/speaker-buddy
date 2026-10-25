using System.Collections.Concurrent;
using SpeakerBuddy.Core;

namespace SpeakerBuddy.App.Tests;

public class SerialLinkTests
{
    private readonly MemoryLog _log = new();
    private readonly ConcurrentQueue<FakeSerialDevice> _opened = new();

    private SerialLink Link(Func<string?>? findPort = null) =>
        new(findPort ?? (() => "COM9"), _ =>
        {
            var device = new FakeSerialDevice();
            _opened.Enqueue(device);
            return device;
        }, _log.Write);

    private FakeSerialDevice Device(int index) => _opened.ElementAt(index);

    [Fact]
    public void Connects_to_the_found_port_and_raises_connected()
    {
        using var link = Link();
        string? connected = null;
        link.Connected += port => connected = port;

        link.Start();

        Wait.Until(() => connected is not null, "connected");
        Assert.Equal("COM9", connected);
    }

    [Fact]
    public void Status_and_firmware_lines_are_raised_and_garbage_is_ignored()
    {
        using var link = Link();
        var statuses = new ConcurrentQueue<TimerStatusLine>();
        var firmware = new ConcurrentQueue<FirmwareLine>();
        link.StatusReceived += statuses.Enqueue;
        link.FirmwareReceived += firmware.Enqueue;
        link.Start();
        Wait.Until(() => !_opened.IsEmpty, "open");

        Device(0).Receive("noise\n{\"v\":2,\"t\":1,\"run\":true}\n{\"v\":1,\"t\":42,");
        Device(0).Receive("\"run\":true}\n{\"v\":1,\"fw\":\"v0.1.0-3-gabc1234\"}\n");

        Wait.Until(() => !firmware.IsEmpty, "firmware line");
        Assert.Equal([new TimerStatusLine(42, true)], statuses);
        Assert.Equal([new FirmwareLine("v0.1.0-3-gabc1234")], firmware);
    }

    [Fact]
    public void Send_writes_one_serialized_line_and_fails_while_disconnected()
    {
        using var link = Link(() => null);

        Assert.False(link.Send(new LengthLine(600)));

        using var connectedLink = Link();
        var connected = false;
        connectedLink.Connected += _ => connected = true;
        connectedLink.Start();
        Wait.Until(() => connected, "connected");

        Assert.True(connectedLink.Send(new LengthLine(600)));
        Assert.True(connectedLink.Send(new EndedLine()));
        Assert.Equal("{\"v\":1,\"L\":600}\n{\"v\":1,\"state\":\"ended\"}\n", Device(0).Written);
    }

    [Fact]
    public void A_lost_port_raises_disconnected_and_the_link_reconnects()
    {
        using var link = Link();
        var connects = 0;
        var disconnects = 0;
        link.Connected += _ => Interlocked.Increment(ref connects);
        link.Disconnected += () => Interlocked.Increment(ref disconnects);
        link.Start();
        Wait.Until(() => connects == 1, "first connect");

        Device(0).Lose();

        Wait.Until(() => disconnects == 1, "disconnect");
        Assert.True(Device(0).Disposed);
        Wait.Until(() => connects == 2, "reconnect");
        Assert.Equal(2, _opened.Count);
    }

    [Fact]
    public void A_failing_open_is_retried()
    {
        var attempts = 0;
        var device = new FakeSerialDevice();
        using var link = new SerialLink(() => "COM9", _ =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                throw new UnauthorizedAccessException("Access to the port 'COM9' is denied.");
            }

            return device;
        }, _log.Write);
        var connected = false;
        link.Connected += _ => connected = true;

        link.Start();

        Wait.Until(() => connected, "connect after the retry");
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void A_throwing_handler_is_logged_and_the_connection_stays()
    {
        using var link = Link();
        var statuses = new ConcurrentQueue<TimerStatusLine>();
        link.Connected += _ => throw new InvalidOperationException("handler bug");
        link.StatusReceived += statuses.Enqueue;
        link.Start();
        Wait.Until(() => !_opened.IsEmpty, "open");

        Device(0).Receive("{\"v\":1,\"t\":3,\"run\":false}\n");

        Wait.Until(() => !statuses.IsEmpty, "status after the failed handler");
        Assert.Contains("Connected handler failed: InvalidOperationException handler bug", _log.Lines);
        Assert.Single(_opened);
    }

    [Fact]
    public void Dispose_closes_the_port_and_can_run_twice()
    {
        var link = Link();
        var connected = false;
        link.Connected += _ => connected = true;
        link.Start();
        Wait.Until(() => connected, "connected");

        link.Dispose();
        link.Dispose();

        Assert.True(Device(0).Disposed);
        Assert.False(link.Send(new EndedLine()));
    }

    [Fact]
    public void Without_a_port_nothing_is_opened()
    {
        var searches = 0;
        using var link = Link(() =>
        {
            Interlocked.Increment(ref searches);
            return null;
        });

        link.Start();

        Wait.Until(() => searches >= 1, "a port search");
        Assert.Empty(_opened);
    }

    [Fact]
    public void Starting_twice_is_refused()
    {
        using var link = Link(() => null);
        link.Start();

        Assert.Throws<InvalidOperationException>(link.Start);
    }
}
