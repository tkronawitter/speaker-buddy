using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace SpeakerTimer.App;

/// <summary>
/// Brings a serial port's DTR and RTS lines to off without passing through DTR off with RTS on, which
/// resets the ESP32-S3 over its USB serial. A freshly plugged-in board's port starts with both lines
/// on, and <see cref="System.IO.Ports.SerialPort.Open"/> clears DTR before RTS, so opening it directly
/// reset the timer during its boot. The driver keeps the line state between opens, so after this the
/// regular open finds both lines off and changes nothing.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class SafeLineState
{
    private const uint GenericReadWrite = 0x80000000 | 0x40000000;
    private const uint OpenExisting = 3;

    // DCB bit fields: fDtrControl in bits 4-5, fRtsControl in bits 12-13 (0 = disable, 1 = enable)
    private const uint DtrControlMask = 0x3u << 4;
    private const uint DtrControlEnable = 0x1u << 4;
    private const uint RtsControlMask = 0x3u << 12;

    /// <summary>Sets RTS off with DTR on, then DTR off. Throws <see cref="IOException"/> on failure.</summary>
    public static void TurnOff(string portName)
    {
        using var handle = CreateFileW(@"\\.\" + portName, GenericReadWrite, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw Failure("open " + portName);
        }

        var dcb = new Dcb { Length = (uint)Marshal.SizeOf<Dcb>() };
        if (!GetCommState(handle, ref dcb))
        {
            throw Failure("read the line state of " + portName);
        }

        dcb.Flags = (dcb.Flags & ~(DtrControlMask | RtsControlMask)) | DtrControlEnable;
        if (!SetCommState(handle, ref dcb))
        {
            throw Failure("clear RTS on " + portName);
        }

        dcb.Flags &= ~DtrControlMask;
        if (!SetCommState(handle, ref dcb))
        {
            throw Failure("clear DTR on " + portName);
        }
    }

    private static IOException Failure(string action) =>
        new($"Could not {action}.", new Win32Exception(Marshal.GetLastPInvokeError()));

    [StructLayout(LayoutKind.Sequential)]
    private struct Dcb
    {
        public uint Length;
        public uint BaudRate;
        public uint Flags;
        public ushort Reserved;
        public ushort XonLim;
        public ushort XoffLim;
        public byte ByteSize;
        public byte Parity;
        public byte StopBits;
        public byte XonChar;
        public byte XoffChar;
        public byte ErrorChar;
        public byte EofChar;
        public byte EvtChar;
        public ushort Reserved1;
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCommState(SafeFileHandle file, ref Dcb dcb);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCommState(SafeFileHandle file, ref Dcb dcb);
}
