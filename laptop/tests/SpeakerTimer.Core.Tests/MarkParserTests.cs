using SpeakerTimer.Core;

namespace SpeakerTimer.Core.Tests;

public class MarkParserTests
{
    [Theory]
    [InlineData("## 10:00", 600)]
    [InlineData("## 0:30", 30)]
    [InlineData("## 99:59", 5999)]
    [InlineData("## 05:00", 300)]
    public void Valid_mark_lines_are_parsed(string line, int expectedSeconds)
    {
        Assert.True(MarkParser.TryParseLine(line, out var seconds));
        Assert.Equal(expectedSeconds, seconds);
    }

    [Theory]
    [InlineData("## 10:75")]
    [InlineData("## 10:00 Demo")]
    [InlineData("## 1:05:00")]
    [InlineData("##10:00")]
    [InlineData("## 100:00")]
    [InlineData("## 10:0")]
    [InlineData(" ## 10:00")]
    [InlineData("## 10:00 ")]
    [InlineData("##  10:00")]
    [InlineData("# 10:00")]
    [InlineData("")]
    public void Lines_that_are_not_exactly_a_mark_are_rejected(string line)
    {
        Assert.False(MarkParser.TryParseLine(line, out _));
    }

    [Fact]
    public void Arabic_indic_digits_are_rejected()
    {
        Assert.False(MarkParser.TryParseLine("## ١٠:٠٠", out _));
    }

    [Theory]
    [InlineData("Intro\r## 5:00\rmore text", 300)]
    [InlineData("Intro\n## 5:00", 300)]
    [InlineData("Intro\v## 5:00", 300)]
    [InlineData("Intro\r\n## 5:00\r\n", 300)]
    public void Notes_are_split_on_cr_lf_and_vertical_tab(string notes, int expectedSeconds)
    {
        Assert.Equal(expectedSeconds, MarkParser.ParseNotes(notes));
    }

    [Fact]
    public void First_mark_in_the_notes_wins()
    {
        Assert.Equal(300, MarkParser.ParseNotes("## 5:00\r## 6:00"));
    }

    [Fact]
    public void Near_misses_before_a_valid_mark_are_skipped()
    {
        Assert.Equal(360, MarkParser.ParseNotes("## 5:75\r## 6:00"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("No mark here\r## 10:00 Demo")]
    public void Notes_without_a_mark_return_null(string? notes)
    {
        Assert.Null(MarkParser.ParseNotes(notes));
    }
}
