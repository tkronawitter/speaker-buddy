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

    /// <summary>Name of the timer's serial interface below the USB enumeration key.</summary>
    internal static readonly string DeviceKey =
        $"VID_{TimerPort.VendorId:X4}&PID_{TimerPort.ProductId:X4}&MI_{TimerPort.SerialInterface:X2}";

    public static string? FindTimerPort()
    {
        using var usb = Registry.LocalMachine.OpenSubKey(UsbEnumKey);
        return TimerPort.Select(Candidates(usb), SerialPort.GetPortNames());
    }

    /// <summary>
    /// The port of every instance of the timer's serial interface below <paramref name="usb"/>, the
    /// USB enumeration key; the tests pass a key of their own.
    /// </summary>
    internal static IEnumerable<SerialPortCandidate> Candidates(RegistryKey? usb)
    {
        if (usb is null)
        {
            yield break;
        }

        var deviceKey = DeviceKey;
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
