using Microsoft.Win32;
using SpeakerBuddy.Core;

namespace SpeakerBuddy.App.Tests;

// The registry walk runs against a scratch key in HKEY_CURRENT_USER laid out like
// HKLM\SYSTEM\CurrentControlSet\Enum\USB; the real enumeration key and the present ports stay untested
public sealed class WindowsPortDiscoveryTests : IDisposable
{
    private readonly string _path = $@"Software\SpeakerBuddy.Tests\{Guid.NewGuid():N}";
    private readonly RegistryKey _usb;

    public WindowsPortDiscoveryTests()
    {
        _usb = Registry.CurrentUser.CreateSubKey(_path);
    }

    public void Dispose()
    {
        _usb.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_path, throwOnMissingSubKey: false);
    }

    private void Instance(string deviceKey, string instance, string? portName)
    {
        using var parameters = _usb.CreateSubKey($@"{deviceKey}\{instance}\Device Parameters");
        if (portName is not null)
        {
            parameters.SetValue("PortName", portName);
        }
    }

    [Fact]
    public void The_device_key_names_the_serial_interface_of_the_esp32_s3()
    {
        Assert.Equal("VID_303A&PID_1001&MI_00", WindowsPortDiscovery.DeviceKey);
    }

    [Fact]
    public void Every_instance_with_a_port_name_is_a_candidate_with_its_instance_id()
    {
        Instance(WindowsPortDiscovery.DeviceKey, "9&35EAB9D0&0&0000", "COM4");
        Instance(WindowsPortDiscovery.DeviceKey, "9&1C2D3E4F&0&0000", "COM9");

        var candidates = WindowsPortDiscovery.Candidates(_usb).OrderBy(c => c.PortName).ToList();

        Assert.Equal(
            [
                new SerialPortCandidate("COM4", @"USB\VID_303A&PID_1001&MI_00\9&35EAB9D0&0&0000"),
                new SerialPortCandidate("COM9", @"USB\VID_303A&PID_1001&MI_00\9&1C2D3E4F&0&0000"),
            ],
            candidates);
        Assert.All(candidates, c => Assert.True(TimerPort.IsTimer(c.DeviceInstanceId)));
    }

    [Fact]
    public void Instances_without_a_port_name_and_other_devices_are_skipped()
    {
        Instance(WindowsPortDiscovery.DeviceKey, "9&35EAB9D0&0&0000", portName: null);
        Instance("VID_303A&PID_1001&MI_02", "9&35EAB9D0&0&0002", "COM5");
        Instance("VID_10C4&PID_EA60", "0001", "COM6");

        Assert.Empty(WindowsPortDiscovery.Candidates(_usb));
    }

    [Fact]
    public void Without_the_enumeration_key_or_the_device_there_are_no_candidates()
    {
        Assert.Empty(WindowsPortDiscovery.Candidates(null));
        Assert.Empty(WindowsPortDiscovery.Candidates(_usb));
    }

    [Fact]
    public void The_lookup_on_this_machine_returns_a_port_name_or_null()
    {
        // Reads the real registry and port list; on a machine without a board it finds nothing
        var port = WindowsPortDiscovery.FindTimerPort();

        Assert.True(port is null || port.StartsWith("COM", StringComparison.OrdinalIgnoreCase), port);
    }
}
