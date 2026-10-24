using System.Globalization;
using System.Text.RegularExpressions;

namespace SpeakerBuddy.Core;

/// <summary>A serial port together with the USB device instance it belongs to.</summary>
/// <param name="PortName">Port name such as <c>COM4</c>.</param>
/// <param name="DeviceInstanceId">Windows device instance ID, for example <c>USB\VID_303A&amp;PID_1001&amp;MI_00\9&amp;35EAB9D0&amp;0&amp;0000</c>.</param>
public sealed record SerialPortCandidate(string PortName, string DeviceInstanceId);

/// <summary>Finds the timer among serial ports by its USB identity (requirement L-06).</summary>
public static partial class TimerPort
{
    /// <summary>Espressif's vendor ID, used by the built-in USB Serial/JTAG of the ESP32-S3.</summary>
    public const int VendorId = 0x303A;

    /// <summary>Product ID of the ESP32-S3 USB Serial/JTAG.</summary>
    public const int ProductId = 0x1001;

    /// <summary>Interface number of the serial (CDC) function; interface 2 is JTAG.</summary>
    public const int SerialInterface = 0;

    [GeneratedRegex(@"^USB\\VID_([0-9A-F]{4})&PID_([0-9A-F]{4})(?:&MI_([0-9A-F]{2}))?(?:\\|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UsbInstance();

    /// <summary>True if the device instance is the serial interface of an ESP32-S3 USB Serial/JTAG.</summary>
    public static bool IsTimer(string deviceInstanceId)
    {
        ArgumentNullException.ThrowIfNull(deviceInstanceId);

        var match = UsbInstance().Match(deviceInstanceId);
        if (!match.Success || !match.Groups[3].Success)
        {
            return false;
        }

        return Hex(match.Groups[1].Value) == VendorId
            && Hex(match.Groups[2].Value) == ProductId
            && Hex(match.Groups[3].Value) == SerialInterface;
    }

    /// <summary>
    /// Picks the timer's port among the candidates that are present right now. Returns null when
    /// none matches. With several boards attached, the lowest port number wins, so the choice is
    /// stable across reconnects.
    /// </summary>
    public static string? Select(IEnumerable<SerialPortCandidate> candidates, IEnumerable<string> presentPorts)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(presentPorts);

        var present = new HashSet<string>(presentPorts, StringComparer.OrdinalIgnoreCase);
        return candidates
            .Where(c => present.Contains(c.PortName) && IsTimer(c.DeviceInstanceId))
            .Select(c => c.PortName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(PortNumber)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static int Hex(string digits) => int.Parse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static int PortNumber(string portName) =>
        portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
        && int.TryParse(portName.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : int.MaxValue;
}
