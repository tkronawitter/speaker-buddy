using SpeakerTimer.Core;

namespace SpeakerTimer.Core.Tests;

public class ColorSchemeTests
{
    [Theory]
    [InlineData(EvaluationColor.Neutral, "#000000")]
    [InlineData(EvaluationColor.Green, "#00A000")]
    [InlineData(EvaluationColor.Red, "#C00000")]
    [InlineData(EvaluationColor.Purple, "#8000A0")]
    public void Default_colours_are_pinned(EvaluationColor color, string expected)
    {
        Assert.Equal(expected, ColorScheme.Default.For(color));
    }

    [Fact]
    public void Default_colours_serialize_as_valid_state_lines()
    {
        foreach (var color in Enum.GetValues<EvaluationColor>())
        {
            var line = Protocol.Serialize(new StateLine(ColorScheme.Default.For(color), null));
            Assert.True(Protocol.TryParse(line, out _));
        }
    }
}
