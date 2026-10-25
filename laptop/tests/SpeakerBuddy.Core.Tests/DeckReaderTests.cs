using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using SpeakerBuddy.Core;
using A = DocumentFormat.OpenXml.Drawing;

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
    public void Missing_arguments_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => DeckReader.Read((string)null!));
        Assert.Throws<ArgumentException>(() => DeckReader.Read(string.Empty));
        Assert.Throws<ArgumentNullException>(() => DeckReader.Read((Stream)null!));
    }

    public static TheoryData<Exception, bool> Exceptions => new()
    {
        { new IOException(), true },
        { new FileNotFoundException(), true },
        { new UnauthorizedAccessException(), true },
        { new FileFormatException(), true },
        { new InvalidDataException(), true },
        { new OpenXmlPackageException(), true },
        { new ArgumentException(), false },
        { new InvalidOperationException(), false },
        { new NullReferenceException(), false },
    };

    [Theory]
    [MemberData(nameof(Exceptions))]
    public void Only_file_problems_count_as_unreadable(Exception e, bool unreadable)
    {
        Assert.Equal(unreadable, DeckReader.IsUnreadable(e));
    }

    [Fact]
    public void A_presentation_without_a_slide_list_has_no_slides()
    {
        using var deck = DeckBuilder.Build(presentationPart => presentationPart.Presentation = new Presentation());

        Assert.Empty(DeckReader.Read(deck));
    }

    [Fact]
    public void A_presentation_part_without_content_has_no_slides()
    {
        using var deck = DeckBuilder.Build(_ => { });

        Assert.Empty(DeckReader.Read(deck));
    }

    [Fact]
    public void Slide_ids_that_lead_to_no_slide_are_skipped_but_keep_their_number()
    {
        using var deck = DeckBuilder.Build(presentationPart =>
        {
            var slide = presentationPart.AddNewPart<SlidePart>();
            slide.Slide = new Slide(new CommonSlideData(new ShapeTree()));
            var theme = presentationPart.AddNewPart<ThemePart>();
            presentationPart.Presentation = new Presentation(new SlideIdList(
                new SlideId { Id = 256 },
                new SlideId { Id = 257, RelationshipId = presentationPart.GetIdOfPart(theme) },
                new SlideId { Id = 258, RelationshipId = presentationPart.GetIdOfPart(slide) }));
        });

        Assert.Equal(new[] { new SlideNotes(3, false, null) }, DeckReader.Read(deck));
    }

    [Fact]
    public void A_slide_shown_explicitly_or_without_content_is_not_hidden()
    {
        using var deck = DeckBuilder.Build(presentationPart =>
        {
            var shown = presentationPart.AddNewPart<SlidePart>();
            shown.Slide = new Slide(new CommonSlideData(new ShapeTree())) { Show = true };
            var empty = presentationPart.AddNewPart<SlidePart>();
            presentationPart.Presentation = new Presentation(new SlideIdList(
                new SlideId { Id = 256, RelationshipId = presentationPart.GetIdOfPart(shown) },
                new SlideId { Id = 257, RelationshipId = presentationPart.GetIdOfPart(empty) }));
        });

        Assert.Equal(new[] { new SlideNotes(1, false, null), new SlideNotes(2, false, null) }, DeckReader.Read(deck));
    }

    public static TheoryData<string, NotesSlide?> NotesWithoutText => new()
    {
        { "notes part without content", null },
        { "notes without slide data", new NotesSlide() },
        { "notes without a shape tree", new NotesSlide(new CommonSlideData()) },
        { "a shape that is no placeholder", new NotesSlide(new CommonSlideData(new ShapeTree(new Shape(
            new NonVisualShapeProperties(
                new NonVisualDrawingProperties { Id = 2, Name = "Text box" },
                new NonVisualShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties()),
            new TextBody(new A.Paragraph(new A.Run(new A.Text("## 1:00")))))))) },
        { "a shape without application properties", new NotesSlide(new CommonSlideData(new ShapeTree(new Shape(
            new NonVisualShapeProperties(new NonVisualDrawingProperties { Id = 2, Name = "Bare" }),
            new TextBody(new A.Paragraph(new A.Run(new A.Text("## 1:00")))))))) },
        { "only the slide image placeholder", new NotesSlide(new CommonSlideData(new ShapeTree(DeckBuilder.Placeholder(PlaceholderValues.SlideImage, ["## 1:00"])))) },
        { "a shape without placeholder data", new NotesSlide(new CommonSlideData(new ShapeTree(new Shape(new TextBody(new A.Paragraph(new A.Run(new A.Text("## 1:00")))))))) },
        { "a placeholder without a type", new NotesSlide(new CommonSlideData(new ShapeTree(DeckBuilder.Placeholder(null, ["## 1:00"])))) },
        { "a body placeholder without text", new NotesSlide(new CommonSlideData(new ShapeTree(DeckBuilder.Placeholder(PlaceholderValues.Body, null)))) },
    };

    [Theory]
    [MemberData(nameof(NotesWithoutText))]
    public void Notes_without_a_body_text_read_as_no_notes(string kind, NotesSlide? notes)
    {
        using var deck = DeckBuilder.Build(presentationPart =>
        {
            var slide = presentationPart.AddNewPart<SlidePart>();
            slide.Slide = new Slide(new CommonSlideData(new ShapeTree()));
            var notesPart = slide.AddNewPart<NotesSlidePart>();
            if (notes is not null)
            {
                notesPart.NotesSlide = notes;
            }

            presentationPart.Presentation = new Presentation(new SlideIdList(
                new SlideId { Id = 256, RelationshipId = presentationPart.GetIdOfPart(slide) }));
        });

        Assert.True(DeckReader.Read(deck)[0].Notes is null, kind);
    }

    [Fact]
    public void Fields_are_read_as_their_text_and_other_paragraph_content_is_ignored()
    {
        var paragraph = new A.Paragraph(
            new A.ParagraphProperties(),
            new A.Run(new A.Text("## ")),
            new A.Field(new A.Text("12")) { Id = "{75C6587B-3F66-46D8-BE9B-1595E4A6F5D8}", Type = "slidenum" },
            new A.Run(new A.Text(":00")),
            new A.Run(),
            new A.Field { Id = "{11111111-1111-1111-1111-111111111111}" },
            new A.EndParagraphRunProperties());
        using var deck = DeckBuilder.Build(presentationPart =>
        {
            var slide = presentationPart.AddNewPart<SlidePart>();
            slide.Slide = new Slide(new CommonSlideData(new ShapeTree()));
            slide.AddNewPart<NotesSlidePart>().NotesSlide = new NotesSlide(new CommonSlideData(new ShapeTree(
                DeckBuilder.PlaceholderOf(PlaceholderValues.Body, [paragraph]))));
            presentationPart.Presentation = new Presentation(new SlideIdList(
                new SlideId { Id = 256, RelationshipId = presentationPart.GetIdOfPart(slide) }));
        });

        Assert.Equal("## 12:00", DeckReader.Read(deck)[0].Notes);
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
