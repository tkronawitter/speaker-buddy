using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

namespace SpeakerBuddy.Core;

/// <summary>
/// Reads slides, hidden flags and speaker notes from a .pptx file without PowerPoint (check mode,
/// requirement L-09). Notes text is joined the way PowerPoint's object model returns it:
/// paragraphs separated by <c>\r</c>, line breaks within a paragraph as <c>\v</c>.
/// </summary>
public static class DeckReader
{
    public static IReadOnlyList<SlideNotes> Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        // Opened here, not by PresentationDocument.Open(path): that leaves the file open when it throws
        // on a file that is no zip. FileShare.Read is what the SDK uses for a read-only open.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Read(stream);
    }

    public static IReadOnlyList<SlideNotes> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var document = PresentationDocument.Open(stream, isEditable: false);
        return Read(document);
    }

    /// <summary>
    /// True for the exceptions <see cref="Read(string)"/> throws on a file it cannot read: missing or
    /// locked (IOException, UnauthorizedAccessException), not a zip at all, such as an old .ppt, an
    /// encrypted .pptx or any other file (FileFormatException, a FormatException, not an
    /// IOException), or a zip without a presentation.
    /// </summary>
    public static bool IsUnreadable(Exception e) =>
        e is IOException or UnauthorizedAccessException or FileFormatException or InvalidDataException
            or OpenXmlPackageException;

    private static List<SlideNotes> Read(PresentationDocument document)
    {
        var presentationPart = document.PresentationPart
            ?? throw new InvalidDataException("The file has no presentation part.");
        var slideIds = presentationPart.Presentation?.SlideIdList?.Elements<SlideId>() ?? [];

        var slides = new List<SlideNotes>();
        var index = 0;
        foreach (var slideId in slideIds)
        {
            index++;
            if (slideId.RelationshipId?.Value is not string relationshipId
                || presentationPart.GetPartById(relationshipId) is not SlidePart slidePart)
            {
                continue;
            }

            var hidden = slidePart.Slide?.Show?.Value == false;
            slides.Add(new SlideNotes(index, hidden, NotesText(slidePart)));
        }

        return slides;
    }

    private static string? NotesText(SlidePart slidePart)
    {
        var shapes = slidePart.NotesSlidePart?.NotesSlide?.CommonSlideData?.ShapeTree?.Elements<Shape>();
        if (shapes is null)
        {
            return null;
        }

        // The notes text sits in the body placeholder; the slide image placeholder has no text.
        var body = shapes.FirstOrDefault(s =>
            s.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape?.Type?.Value
                == PlaceholderValues.Body);
        if (body?.TextBody is null)
        {
            return null;
        }

        var paragraphs = body.TextBody.Elements<A.Paragraph>().Select(ParagraphText);
        return string.Join('\r', paragraphs);
    }

    private static string ParagraphText(A.Paragraph paragraph)
    {
        var text = new StringBuilder();
        foreach (var element in paragraph.ChildElements)
        {
            switch (element)
            {
                case A.Run run:
                    text.Append(run.Text?.Text);
                    break;
                case A.Break:
                    text.Append('\v');
                    break;
                case A.Field field:
                    text.Append(field.Text?.Text);
                    break;
            }
        }

        return text.ToString();
    }
}
