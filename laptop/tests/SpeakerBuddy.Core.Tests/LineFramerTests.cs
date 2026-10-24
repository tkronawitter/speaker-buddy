using System.Text;
using SpeakerBuddy.Core;

namespace SpeakerBuddy.Core.Tests;

public class LineFramerTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void Complete_lines_are_returned_without_terminator()
    {
        var framer = new LineFramer();

        Assert.Equal(new[] { "a", "bc" }, framer.Feed(Bytes("a\nbc\n")));
    }

    [Fact]
    public void A_line_split_across_reads_is_joined()
    {
        var framer = new LineFramer();

        Assert.Empty(framer.Feed(Bytes("{\"v\":1,")));
        Assert.Equal(new[] { "{\"v\":1,\"t\":5}" }, framer.Feed(Bytes("\"t\":5}\n")));
    }

    [Fact]
    public void A_carriage_return_stays_for_the_parser()
    {
        var framer = new LineFramer();

        Assert.Equal(new[] { "x\r" }, framer.Feed(Bytes("x\r\n")));
    }

    [Fact]
    public void A_line_at_the_limit_passes_and_a_longer_one_is_dropped_up_to_the_next_newline()
    {
        var framer = new LineFramer();
        var atLimit = new string('a', Protocol.MaxLineBytes - 1);
        var tooLong = new string('b', Protocol.MaxLineBytes);

        Assert.Equal(new[] { atLimit, "next" }, framer.Feed(Bytes(atLimit + "\n" + tooLong + "\nnext\n")));
    }

    [Fact]
    public void Reset_discards_a_partial_line()
    {
        var framer = new LineFramer();
        framer.Feed(Bytes("partial"));

        framer.Reset();

        Assert.Equal(new[] { "fresh" }, framer.Feed(Bytes("fresh\n")));
    }

    [Fact]
    public void Utf8_split_inside_a_character_is_decoded_once_the_line_completes()
    {
        var framer = new LineFramer();
        var bytes = Bytes("\u00e4\n");

        Assert.Empty(framer.Feed(bytes.AsSpan(0, 1)));
        Assert.Equal(new[] { "\u00e4" }, framer.Feed(bytes.AsSpan(1)));
    }
}
