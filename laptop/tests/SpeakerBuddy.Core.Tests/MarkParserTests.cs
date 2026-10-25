using SpeakerBuddy.Core;

namespace SpeakerBuddy.Core.Tests;

public class MarkParserTests
{
    [Theory]
    [InlineData("## 10:00", 600)]
    [InlineData("## 0:30", 30)]
    [InlineData("## 99:59", 5999)]
    [InlineData("## 05:00", 300)]
    [InlineData("## 10:00 ", 600)]
    [InlineData("## 10:00   ", 600)]
    [InlineData("## 10:00\t", 600)]
    [InlineData("## 10:00 \t ", 600)]
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
    [InlineData("\t## 10:00")]
    [InlineData("## 10:00 x")]
    [InlineData("## 10:00\u00a0")]
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
        Assert.False(MarkParser.TryParseLine("## \u0661\u0660:\u0660\u0660", out _));
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

    [Theory]
    [InlineData("## 10:75")]
    [InlineData("## 10:00 Demo")]
    [InlineData("## 1:05:00")]
    [InlineData("##10:00")]
    [InlineData(" ## 10:00")]
    [InlineData("# 10:00")]
    [InlineData("### 10:00")]
    [InlineData("## 100:00")]
    [InlineData("## 10 : 00")]
    public void Lines_that_look_like_marks_are_near_misses(string line)
    {
        Assert.True(MarkParser.IsNearMiss(line));
    }

    [Theory]
    [InlineData("## 10:00")]
    [InlineData("## 10:00 ")]
    [InlineData("## Agenda")]
    [InlineData("Start at 10:00")]
    [InlineData("#hashtag")]
    [InlineData("")]
    public void Valid_marks_and_ordinary_text_are_not_near_misses(string line)
    {
        Assert.False(MarkParser.IsNearMiss(line));
    }

    [Fact]
    public void Near_misses_are_collected_from_all_lines_of_the_notes()
    {
        Assert.Equal(new[] { "## 10:75", "##12:00" }, MarkParser.NearMisses("## 10:75\r## 11:00\v##12:00"));
        Assert.Empty(MarkParser.NearMisses(null));
    }

    [Theory]
    [InlineData("## session length 60:00", 3600)]
    [InlineData("## Session Length 60:00", 3600)]
    [InlineData("## SESSION LENGTH 45:30", 2730)]
    [InlineData("## session length 0:45", 45)]
    [InlineData("## session length 120:00", 7200)]
    [InlineData("## session length 60:00 \t", 3600)]
    public void Valid_session_length_lines_are_parsed(string line, int expectedSeconds)
    {
        Assert.True(MarkParser.TryParseSessionLengthLine(line, out var seconds));
        Assert.Equal(expectedSeconds, seconds);
    }

    [Theory]
    [InlineData("## session length 0:00")]
    [InlineData("## session length 60:60")]
    [InlineData("## session length 60")]
    [InlineData("## session length 60:00 min")]
    [InlineData("## session length 1000:00")]
    [InlineData("##session length 60:00")]
    [InlineData("## session  length 60:00")]
    [InlineData("## sessionlength 60:00")]
    [InlineData(" ## session length 60:00")]
    [InlineData("## session lenght 60:00")]
    [InlineData("## length 60:00")]
    public void Lines_that_are_not_exactly_a_session_length_are_rejected_and_are_near_misses(string line)
    {
        Assert.False(MarkParser.TryParseSessionLengthLine(line, out _));
        Assert.True(MarkParser.IsNearMiss(line));
    }

    [Theory]
    [InlineData("## session length 60:00")]
    [InlineData("# Challenges 2")]
    [InlineData("## Lessons learned")]
    [InlineData("Session length is 60 minutes")]
    public void Valid_session_lengths_and_ordinary_text_are_not_near_misses(string line)
    {
        Assert.False(MarkParser.IsNearMiss(line));
    }

    [Fact]
    public void A_session_length_is_no_mark()
    {
        Assert.Null(MarkParser.ParseNotes("## session length 60:00"));
        Assert.False(MarkParser.TryParseLine("## session length 60:00", out _));
    }

    [Fact]
    public void All_session_lengths_of_the_notes_are_returned_in_order()
    {
        Assert.Equal(new[] { 3600, 2700 }, MarkParser.SessionLengths("## 52:00\r## session length 60:00\v## session length 45:00"));
        Assert.Empty(MarkParser.SessionLengths(null));
    }
}
