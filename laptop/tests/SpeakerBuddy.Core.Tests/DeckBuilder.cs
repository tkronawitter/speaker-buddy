using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

namespace SpeakerBuddy.Core.Tests;

/// <summary>
/// Builds a minimal .pptx in memory with just what <see cref="DeckReader"/> reads: the slide
/// order, the hidden flag and the notes body placeholder. Each notes paragraph is a string; a
/// <c>\v</c> inside it becomes a line break element.
/// </summary>
internal static class DeckBuilder
{
    public static MemoryStream Build(params (bool Hidden, string[]? NotesParagraphs)[] slides)
    {
        var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            var presentationPart = document.AddPresentationPart();
            var slideIdList = new SlideIdList();
            presentationPart.Presentation = new Presentation(slideIdList);

            uint id = 256;
            foreach (var (hidden, paragraphs) in slides)
            {
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.Slide = new Slide(new CommonSlideData(new ShapeTree()));
                if (hidden)
                {
                    slidePart.Slide.Show = false;
                }

                if (paragraphs is not null)
                {
                    var notesPart = slidePart.AddNewPart<NotesSlidePart>();
                    notesPart.NotesSlide = new NotesSlide(new CommonSlideData(new ShapeTree(
                        Placeholder(PlaceholderValues.SlideImage, []),
                        Placeholder(PlaceholderValues.Body, paragraphs))));
                }

                slideIdList.Append(new SlideId { Id = id++, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
            }
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Builds a deck whose presentation part <paramref name="configure"/> fills by hand, for shapes
    /// of a file that <see cref="Build(ValueTuple{bool, string[]}[])"/> never produces.
    /// </summary>
    public static MemoryStream Build(Action<PresentationPart> configure)
    {
        var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            configure(document.AddPresentationPart());
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>A notes placeholder shape; a null type leaves the type out, null paragraphs the text body.</summary>
    public static Shape Placeholder(PlaceholderValues? type, string[]? paragraphs) =>
        PlaceholderOf(type, paragraphs?.Select(Paragraph).ToArray());

    /// <summary>A notes placeholder shape holding the given paragraph elements as they are.</summary>
    public static Shape PlaceholderOf(PlaceholderValues? type, A.Paragraph[]? paragraphs)
    {
        var placeholder = new PlaceholderShape();
        if (type is { } value)
        {
            placeholder.Type = value;
        }

        var shape = new Shape(
            new NonVisualShapeProperties(
                new NonVisualDrawingProperties { Id = 2, Name = type?.ToString() ?? "Placeholder" },
                new NonVisualShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties(placeholder)),
            new ShapeProperties());
        if (paragraphs is not null)
        {
            shape.Append(new TextBody(
                new OpenXmlElement[] { new A.BodyProperties(), new A.ListStyle() }.Concat(paragraphs)));
        }

        return shape;
    }

    private static A.Paragraph Paragraph(string text)
    {
        var paragraph = new A.Paragraph();
        var parts = text.Split('\v');
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                paragraph.Append(new A.Break());
            }

            paragraph.Append(new A.Run(new A.Text(parts[i])));
        }

        return paragraph;
    }
}
