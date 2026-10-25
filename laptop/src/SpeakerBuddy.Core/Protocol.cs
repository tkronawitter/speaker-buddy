using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SpeakerBuddy.Core;

/// <summary>A line of the serial protocol, version 1 (requirement S-02, docs/protocol.md).</summary>
public abstract record ProtocolLine;

/// <summary>Timer to laptop: elapsed time and whether the clock runs.</summary>
public sealed record TimerStatusLine(int Seconds, bool Run) : ProtocolLine;

/// <summary>Laptop to timer: background colour and deviation (null hides the deviation line).</summary>
public sealed record StateLine(string Color, int? Deviation) : ProtocolLine;

/// <summary>Laptop to timer: planned talk length in seconds, or null for "unknown, clear it".</summary>
public sealed record LengthLine(int? LengthSeconds) : ProtocolLine;

/// <summary>Laptop to timer: the slide show has ended.</summary>
public sealed record EndedLine : ProtocolLine;

/// <summary>Timer to laptop: the firmware version (git describe of the build), sent now and then.</summary>
public sealed record FirmwareLine(string Version) : ProtocolLine;

/// <summary>Serializes and parses protocol lines. Lines carry no terminator here.</summary>
public static partial class Protocol
{
    public const int Version = 1;

    /// <summary>Maximum line length in bytes, including the <c>\n</c> terminator.</summary>
    public const int MaxLineBytes = 128;

    private const string EndedState = "ended";

    // Exactly one of these fields identifies the line type.
    private static readonly string[] Discriminators = ["t", "c", "L", "state", "fw"];

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColor();

    // Characters the timer leaves in a version; it replaces anything else with '_' (line.c)
    [GeneratedRegex("^[0-9A-Za-z._+-]{1,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex FirmwareVersion();

    /// <summary>Serializes a line to compact JSON without terminator.</summary>
    /// <exception cref="ArgumentException">The line holds a value the protocol does not allow.</exception>
    public static string Serialize(ProtocolLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var buffer = new ArrayBufferWriter<byte>(MaxLineBytes);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", Version);
            switch (line)
            {
                case TimerStatusLine status:
                    ArgumentOutOfRangeException.ThrowIfNegative(status.Seconds, nameof(line));
                    writer.WriteNumber("t", status.Seconds);
                    writer.WriteBoolean("run", status.Run);
                    break;
                case StateLine state:
                    if (state.Color is null || !HexColor().IsMatch(state.Color))
                    {
                        throw new ArgumentException($"Colour must be #RRGGBB, got '{state.Color}'.", nameof(line));
                    }

                    writer.WriteString("c", state.Color);
                    if (state.Deviation is int deviation)
                    {
                        writer.WriteNumber("d", deviation);
                    }
                    else
                    {
                        writer.WriteNull("d");
                    }

                    break;
                case LengthLine length:
                    if (length.LengthSeconds is int seconds)
                    {
                        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seconds, nameof(line));
                        writer.WriteNumber("L", seconds);
                    }
                    else
                    {
                        writer.WriteNull("L");
                    }

                    break;
                case EndedLine:
                    writer.WriteString("state", EndedState);
                    break;
                case FirmwareLine firmware:
                    if (firmware.Version is null || !FirmwareVersion().IsMatch(firmware.Version))
                    {
                        throw new ArgumentException($"Firmware version must be 1 to 40 of [0-9A-Za-z._+-], got '{firmware.Version}'.", nameof(line));
                    }

                    writer.WriteString("fw", firmware.Version);
                    break;
                default:
                    throw new ArgumentException($"Unknown line type {line.GetType().Name}.", nameof(line));
            }

            writer.WriteEndObject();
        }

        if (buffer.WrittenCount + 1 > MaxLineBytes)
        {
            throw new InvalidOperationException($"Serialized line exceeds {MaxLineBytes} bytes.");
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Parses one received line (without <c>\n</c>; a trailing <c>\r</c> is accepted). Returns
    /// false for lines that are too long, broken, of another version or of no known type.
    /// </summary>
    public static bool TryParse(string line, [NotNullWhen(true)] out ProtocolLine? result)
    {
        result = null;
        if (line is null || Encoding.UTF8.GetByteCount(line) + 1 > MaxLineBytes)
        {
            return false;
        }

        if (line.EndsWith('\r'))
        {
            line = line[..^1];
        }

        // Lines are flat and carry no escapes (docs/protocol.md); outside strings a backslash is
        // invalid JSON anyway, so any backslash drops the line, as on the timer.
        if (line.Contains('\\'))
        {
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !IsFlatWithUniqueNames(root)
                || !TryGetInt(root, "v", out var version)
                || version != Version)
            {
                return false;
            }

            var present = Discriminators.Where(name => root.TryGetProperty(name, out _)).ToList();
            if (present.Count != 1)
            {
                return false;
            }

            result = present[0] switch
            {
                "t" => ParseStatus(root),
                "c" => ParseState(root),
                "L" => ParseLength(root),
                "fw" => ParseFirmware(root),
                _ => ParseEnded(root),
            };
            return result is not null;
        }
    }

    private static TimerStatusLine? ParseStatus(JsonElement root)
    {
        if (!TryGetInt(root, "t", out var seconds) || seconds < 0
            || !root.TryGetProperty("run", out var run)
            || run.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        return new TimerStatusLine(seconds, run.GetBoolean());
    }

    private static StateLine? ParseState(JsonElement root)
    {
        var color = root.GetProperty("c");
        if (color.ValueKind != JsonValueKind.String
            || !HexColor().IsMatch(color.GetString()!)
            || !root.TryGetProperty("d", out var d))
        {
            return null;
        }

        if (d.ValueKind == JsonValueKind.Null)
        {
            return new StateLine(color.GetString()!, null);
        }

        return d.ValueKind == JsonValueKind.Number && d.TryGetInt32(out var deviation)
            ? new StateLine(color.GetString()!, deviation)
            : null;
    }

    private static LengthLine? ParseLength(JsonElement root)
    {
        if (root.GetProperty("L").ValueKind == JsonValueKind.Null)
        {
            return new LengthLine(null);
        }

        return TryGetInt(root, "L", out var length) && length > 0 ? new LengthLine(length) : null;
    }

    private static EndedLine? ParseEnded(JsonElement root)
    {
        var state = root.GetProperty("state");
        return state.ValueKind == JsonValueKind.String && state.GetString() == EndedState ? new EndedLine() : null;
    }

    private static FirmwareLine? ParseFirmware(JsonElement root)
    {
        var version = root.GetProperty("fw");
        return version.ValueKind == JsonValueKind.String && FirmwareVersion().IsMatch(version.GetString()!)
            ? new FirmwareLine(version.GetString()!)
            : null;
    }

    private static bool IsFlatWithUniqueNames(JsonElement root)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!names.Add(property.Name)
                || property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                return false;
            }
        }

        return true;
    }

    // TryGetInt32 rejects fractions and exponents such as 1.0 or 1e0, so only plain integers pass.
    private static bool TryGetInt(JsonElement root, string name, out int value)
    {
        value = 0;
        return root.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value);
    }
}
