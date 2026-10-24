using SpeakerTimer.Core;

namespace SpeakerTimer.Core.Tests;

public class ProtocolTests
{
    public static TheoryData<ProtocolLine, string> Lines => new()
    {
        { new TimerStatusLine(612, true), """{"v":1,"t":612,"run":true}""" },
        { new TimerStatusLine(0, false), """{"v":1,"t":0,"run":false}""" },
        { new StateLine("#00A000", 90), """{"v":1,"c":"#00A000","d":90}""" },
        { new StateLine("#00A000", -45), """{"v":1,"c":"#00A000","d":-45}""" },
        { new StateLine("#000000", null), """{"v":1,"c":"#000000","d":null}""" },
        { new LengthLine(2700), """{"v":1,"L":2700}""" },
        { new EndedLine(), """{"v":1,"state":"ended"}""" },
    };

    [Theory]
    [MemberData(nameof(Lines))]
    public void Lines_serialize_to_the_documented_form(ProtocolLine line, string expected)
    {
        Assert.Equal(expected, Protocol.Serialize(line));
    }

    [Theory]
    [MemberData(nameof(Lines))]
    public void Lines_round_trip(ProtocolLine line, string text)
    {
        Assert.True(Protocol.TryParse(text, out var parsed));
        Assert.Equal(line, parsed);
    }

    [Fact]
    public void A_trailing_carriage_return_is_accepted()
    {
        Assert.True(Protocol.TryParse("{\"v\":1,\"L\":2700}\r", out var parsed));
        Assert.Equal(new LengthLine(2700), parsed);
    }

    [Fact]
    public void Unknown_extra_fields_are_ignored()
    {
        Assert.True(Protocol.TryParse("""{"v":1,"t":5,"run":true,"x":"y"}""", out var parsed));
        Assert.Equal(new TimerStatusLine(5, true), parsed);
    }

    [Fact]
    public void A_line_of_exactly_127_bytes_plus_terminator_is_accepted()
    {
        var line = PaddedStatusLine(Protocol.MaxLineBytes - 1);

        Assert.True(Protocol.TryParse(line, out _));
    }

    [Fact]
    public void A_line_over_the_limit_is_dropped()
    {
        var line = PaddedStatusLine(Protocol.MaxLineBytes);

        Assert.False(Protocol.TryParse(line, out var parsed));
        Assert.Null(parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"v\":1,\"t\":5,\"run\":true")]
    [InlineData("[1,2]")]
    [InlineData("""{"t":5,"run":true}""")]
    [InlineData("""{"v":2,"t":5,"run":true}""")]
    [InlineData("""{"v":"1","t":5,"run":true}""")]
    [InlineData("""{"v":1.0,"t":5,"run":true}""")]
    [InlineData("""{"v":1}""")]
    [InlineData("""{"v":1,"x":1}""")]
    [InlineData("""{"v":1,"t":5,"run":true,"L":2700}""")]
    [InlineData("""{"v":1,"t":-1,"run":true}""")]
    [InlineData("""{"v":1,"t":1.5,"run":true}""")]
    [InlineData("""{"v":1,"t":5}""")]
    [InlineData("""{"v":1,"t":5,"run":"yes"}""")]
    [InlineData("""{"v":1,"c":"green","d":0}""")]
    [InlineData("""{"v":1,"c":"#00A00","d":0}""")]
    [InlineData("""{"v":1,"c":"#00A000"}""")]
    [InlineData("""{"v":1,"c":"#00A000","d":"90"}""")]
    [InlineData("""{"v":1,"L":0}""")]
    [InlineData("""{"v":1,"L":"2700"}""")]
    [InlineData("""{"v":1,"state":"paused"}""")]
    [InlineData("""{"v":1,"L":2700,"x":[1]}""")]
    [InlineData("""{"v":1,"L":2700,"x":{"y":1}}""")]
    [InlineData("""{"v":1,"L":2700,"L":60}""")]
    [InlineData("{\"v\":1,\"state\":\"end\\u0065d\"}")]
    [InlineData("""{"v":1,"L":2700,"x":"a\"b"}""")]
    public void Broken_unknown_or_invalid_lines_are_dropped(string line)
    {
        Assert.False(Protocol.TryParse(line, out var parsed));
        Assert.Null(parsed);
    }

    [Theory]
    [InlineData("green")]
    [InlineData("#00A00")]
    [InlineData("#00A000FF")]
    public void Serializing_an_invalid_colour_throws(string color)
    {
        Assert.Throws<ArgumentException>(() => Protocol.Serialize(new StateLine(color, 0)));
    }

    [Fact]
    public void Serializing_a_non_positive_length_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Protocol.Serialize(new LengthLine(0)));
    }

    [Fact]
    public void Serializing_a_negative_time_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Protocol.Serialize(new TimerStatusLine(-1, true)));
    }

    // A valid status line padded with an ignored field to the given UTF-8 length (terminator not included).
    private static string PaddedStatusLine(int length)
    {
        const string head = "{\"v\":1,\"t\":5,\"run\":true,\"p\":\"";
        const string tail = "\"}";
        return head + new string('x', length - head.Length - tail.Length) + tail;
    }
}
