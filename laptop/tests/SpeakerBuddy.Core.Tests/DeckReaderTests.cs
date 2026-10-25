using SpeakerBuddy.Core;

namespace SpeakerBuddy.Core.Tests;

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

    // An old .ppt and an encrypted .pptx both are OLE compound files with this signature
    private static readonly byte[] OleSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public static TheoryData<string, byte[]> BrokenDecks => new()
    {
        { "plain text", "not a deck"u8.ToArray() },
        { "empty file", [] },
        { "old .ppt or encrypted .pptx", [.. OleSignature, .. new byte[504]] },
        { "truncated zip", [0x50, 0x4B, 0x03, 0x04, 1, 2, 3] },
        { "zip without a presentation", ZipWithoutPresentation() },
    };

    private static byte[] ZipWithoutPresentation()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("readme.txt");
        }

        return stream.ToArray();
    }

    [Theory]
    [MemberData(nameof(BrokenDecks))]
    public void A_file_that_is_no_deck_fails_with_an_unreadable_exception(string kind, byte[] content)
    {
        var e = Record.Exception(() => DeckReader.Read(new MemoryStream(content)));

        Assert.NotNull(e);
        Assert.True(DeckReader.IsUnreadable(e), $"{kind}: {e.GetType().FullName} is not classed as unreadable");
    }

    [Fact]
    public void A_missing_file_is_unreadable()
    {
        var e = Record.Exception(() => DeckReader.Read(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pptx")));

        Assert.NotNull(e);
        Assert.True(DeckReader.IsUnreadable(e));
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
