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

    private static Shape Placeholder(PlaceholderValues type, string[] paragraphs) =>
        new(
            new NonVisualShapeProperties(
                new NonVisualDrawingProperties { Id = 2, Name = type.ToString() },
                new NonVisualShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties(new PlaceholderShape { Type = type })),
            new ShapeProperties(),
            new TextBody(
                new OpenXmlElement[] { new A.BodyProperties(), new A.ListStyle() }
                    .Concat(paragraphs.Select(Paragraph))));

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
