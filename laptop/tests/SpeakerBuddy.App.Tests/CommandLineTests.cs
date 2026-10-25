namespace SpeakerBuddy.App.Tests;

public class CommandLineTests
{
    private static Command Parse(params string[] args) => CommandLine.Parse(args);

    [Fact]
    public void No_arguments_start_the_tray()
    {
        Assert.Equal(new Command(Mode.Tray, false, null, null, null), Parse());
    }

    [Fact]
    public void The_tray_takes_a_length_and_a_port()
    {
        Assert.Equal(new Command(Mode.Tray, false, 2700, "COM7", null), Parse("--length", "45", "--port", "COM7"));
        Assert.Equal(new Command(Mode.Tray, false, 90, null, null), Parse("--length", "1:30"));
    }

    [Theory]
    [InlineData("--length")]
    [InlineData("--length", "0")]
    [InlineData("--length", "abc")]
    [InlineData("--port")]
    [InlineData("--verbose")]
    [InlineData("--length", "45", "--lenght", "50")]
    public void Wrong_tray_arguments_are_invalid_without_a_console_request(params string[] args)
    {
        Assert.Equal(new Command(Mode.Invalid, false, null, null, null), Parse(args));
    }

    [Fact]
    public void Arguments_that_are_no_options_are_ignored_by_the_tray()
    {
        Assert.Equal(Mode.Tray, Parse("deck.pptx").Mode);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("--check", "deck.pptx", "--help")]
    [InlineData("--demo", "--help", "--length")]
    public void Help_wins_over_everything(params string[] args)
    {
        Assert.Equal(new Command(Mode.Help, true, null, null, null), Parse(args));
    }

    [Fact]
    public void Check_takes_the_deck_and_a_length()
    {
        Assert.Equal(new Command(Mode.Check, true, null, null, "deck.pptx"), Parse("--check", "deck.pptx"));
        Assert.Equal(new Command(Mode.Check, true, 300, null, @"C:\talks\deck.pptx"),
            Parse("--length", "5", "--check", @"C:\talks\deck.pptx"));
    }

    [Fact]
    public void Check_without_a_deck_is_invalid()
    {
        Assert.Equal(new Command(Mode.Invalid, true, null, null, null), Parse("--check"));
    }

    [Fact]
    public void Check_wins_over_demo_and_ignores_unknown_options()
    {
        Assert.Equal(new Command(Mode.Check, true, null, null, "deck.pptx"), Parse("--demo", "--check", "deck.pptx", "--verbose"));
    }

    [Fact]
    public void Demo_takes_a_port()
    {
        Assert.Equal(new Command(Mode.Demo, true, null, null, null), Parse("--demo"));
        Assert.Equal(new Command(Mode.Demo, true, null, "COM4", null), Parse("--demo", "--port", "COM4"));
    }

    [Theory]
    [InlineData("--demo", "--port")]
    [InlineData("--check", "deck.pptx", "--length", "x")]
    public void Wrong_options_in_a_console_mode_are_invalid_with_a_console_request(params string[] args)
    {
        Assert.Equal(new Command(Mode.Invalid, true, null, null, null), Parse(args));
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => CommandLine.Parse(null!));
    }

    [Fact]
    public void The_usage_names_every_mode_and_option()
    {
        foreach (var word in new[] { "--length", "--port", "--check", "--demo", "Out-Host" })
        {
            Assert.Contains(word, CommandLine.Usage);
        }
    }
}
