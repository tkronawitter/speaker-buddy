using SpeakerBuddy.Core;

namespace SpeakerBuddy.Core.Tests;

public class TimerPortTests
{
    private const string TimerSerial = @"USB\VID_303A&PID_1001&MI_00\9&35EAB9D0&0&0000";

    [Theory]
    [InlineData(TimerSerial)]
    [InlineData(@"usb\vid_303a&pid_1001&mi_00\abc")]
    public void Serial_interface_of_the_esp32_s3_is_the_timer(string id)
    {
        Assert.True(TimerPort.IsTimer(id));
    }

    [Theory]
    [InlineData(@"USB\VID_303A&PID_1001&MI_02\9&35EAB9D0&0&0002")]
    [InlineData(@"USB\VID_303A&PID_1001\90:70:69:FE:86:78")]
    [InlineData(@"USB\VID_10C4&PID_EA60\0001")]
    [InlineData(@"USB\VID_303A&PID_1002&MI_00\x")]
    [InlineData(@"FTDIBUS\VID_0403+PID_6001+A\0000")]
    [InlineData("")]
    public void Other_devices_and_interfaces_are_not_the_timer(string id)
    {
        Assert.False(TimerPort.IsTimer(id));
    }

    [Fact]
    public void Only_present_ports_are_selected()
    {
        var candidates = new[] { new SerialPortCandidate("COM4", TimerSerial) };

        Assert.Equal("COM4", TimerPort.Select(candidates, ["COM1", "COM4"]));
        Assert.Null(TimerPort.Select(candidates, ["COM1"]));
    }

    [Fact]
    public void Other_devices_are_ignored()
    {
        var candidates = new[]
        {
            new SerialPortCandidate("COM3", @"USB\VID_10C4&PID_EA60\0001"),
            new SerialPortCandidate("COM7", TimerSerial),
        };

        Assert.Equal("COM7", TimerPort.Select(candidates, ["COM3", "COM7"]));
    }

    [Fact]
    public void Lowest_port_number_wins_with_several_timers()
    {
        var candidates = new[]
        {
            new SerialPortCandidate("COM12", TimerSerial),
            new SerialPortCandidate("COM9", TimerSerial),
        };

        Assert.Equal("COM9", TimerPort.Select(candidates, ["COM12", "COM9"]));
    }

    [Fact]
    public void No_candidates_gives_null()
    {
        Assert.Null(TimerPort.Select([], ["COM4"]));
    }
}
