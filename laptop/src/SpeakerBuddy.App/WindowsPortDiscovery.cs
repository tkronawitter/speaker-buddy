using System.IO.Ports;
using System.Runtime.Versioning;
using Microsoft.Win32;
using SpeakerBuddy.Core;

namespace SpeakerBuddy.App;

/// <summary>
/// Finds the timer's COM port from the Windows device registry (requirement L-06): the
/// "Device Parameters\PortName" value of every USB device instance with the timer's VID and PID.
/// Only ports Windows reports as present count, so a stale registry entry of an unplugged board
/// is ignored.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsPortDiscovery
{
    private const string UsbEnumKey = @"SYSTEM\CurrentControlSet\Enum\USB";

    public static string? FindTimerPort() =>
        TimerPort.Select(Candidates(), SerialPort.GetPortNames());

    private static IEnumerable<SerialPortCandidate> Candidates()
    {
        using var usb = Registry.LocalMachine.OpenSubKey(UsbEnumKey);
        if (usb is null)
        {
            yield break;
        }

        var deviceKey = $"VID_{TimerPort.VendorId:X4}&PID_{TimerPort.ProductId:X4}&MI_{TimerPort.SerialInterface:X2}";
        using var device = usb.OpenSubKey(deviceKey);
        if (device is null)
        {
            yield break;
        }

        foreach (var instance in device.GetSubKeyNames())
        {
            using var parameters = device.OpenSubKey($@"{instance}\Device Parameters");
            if (parameters?.GetValue("PortName") is string portName)
            {
                yield return new SerialPortCandidate(portName, $@"USB\{deviceKey}\{instance}");
            }
        }
    }
}
