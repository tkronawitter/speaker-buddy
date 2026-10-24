using SpeakerTimer.Core;

namespace SpeakerTimer.Core.Tests;

public class DeckReaderTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Slides_are_numbered_in_deck_order_with_hidden_flags_and_notes()
    {
        using var deck = DeckBuilder.Build(
            (false, ["Intro", "## 0:30"]),
            (true, ["## 7:00"]),
            (false, null));

        var slides = DeckReader.Read(deck);

        Assert.Equal(
            new[]
            {
                new SlideNotes(1, false, "Intro\r## 0:30"),
                new SlideNotes(2, true, "## 7:00"),
                new SlideNotes(3, false, null),
            },
            slides);
    }

    [Fact]
    public void Line_breaks_inside_a_paragraph_become_vertical_tabs()
    {
        using var deck = DeckBuilder.Build((false, ["Demo time\v## 12:00"]));

        Assert.Equal("Demo time\v## 12:00", DeckReader.Read(deck)[0].Notes);
    }

    [Fact]
    public void A_deck_saved_by_powerpoint_is_read_like_the_object_model_reports_it()
    {
        var slides = DeckReader.Read(FixturePath("check-deck.pptx"));

        Assert.Equal(7, slides.Count);
        Assert.Equal(new SlideNotes(1, false, "Intro\r## 0:30"), slides[0]);
        Assert.True(slides[2].Hidden);
        Assert.Equal("Demo time\v## 12:00 ", slides[4].Notes);
        Assert.Equal("Questions\r## 29:30", slides[6].Notes);
    }
}
