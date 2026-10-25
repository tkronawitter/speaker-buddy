namespace SpeakerBuddy.App.Tests;

// Plain objects with the members PowerPointSource reads through dynamic calls. They are no COM
// objects, so ComScope leaves them untracked. Public, because the runtime binder only sees members
// that are accessible from SpeakerBuddy.App.

public sealed class FakeApplication
{
    public FakeShowWindows Windows { get; } = new();

    /// <summary>Thrown by the first call, as PowerPoint does while it is busy.</summary>
    public Exception? Busy { get; set; }

    public FakeShowWindows SlideShowWindows => Busy is null ? Windows : throw Busy;

    public FakeShowWindow Run(FakePresentation presentation, int slideIndex = 1)
    {
        var window = new FakeShowWindow(presentation) { View = { Slide = presentation.Slides.Item(slideIndex) } };
        Windows.All.Add(window);
        return window;
    }
}

public sealed class FakeShowWindows
{
    public List<FakeShowWindow> All { get; } = [];

    public int Count => All.Count;

    public FakeShowWindow Item(int index) => All[index - 1];
}

public sealed class FakeShowWindow(FakePresentation presentation)
{
    public FakePresentation Presentation { get; } = presentation;

    public FakeView View { get; } = new();
}

public sealed class FakeView
{
    public const int Running = 1;
    public const int Done = 5;

    private FakeSlide? _slide;

    public int State { get; set; } = Running;

    // PowerPoint has no current slide at the black end-of-show screen
    public FakeSlide Slide
    {
        get => State == Done ? throw new InvalidOperationException("no current slide at the end of the show") : _slide!;
        set => _slide = value;
    }
}

public sealed class FakePresentation(string fullName, params FakeSlide[] slides)
{
    public string FullName { get; set; } = fullName;

    public FakeSlides Slides { get; } = new(slides);
}

public sealed class FakeSlides(FakeSlide[] slides)
{
    public int Count => slides.Length;

    public FakeSlide Item(int index) => slides[index - 1];
}

public sealed class FakeSlide(int index, bool hidden = false, params FakeShape[] notesPlaceholders)
{
    public int SlideIndex { get; } = index;

    public FakeTransition SlideShowTransition { get; } = new(hidden ? -1 : 0);

    public FakeNotesPage NotesPage { get; } = new(notesPlaceholders);

    public static FakeSlide WithNotes(int index, string text, bool hidden = false) =>
        new(index, hidden, FakeShape.Title(), FakeShape.Body(text));
}

public sealed class FakeTransition(int hidden)
{
    public int Hidden { get; } = hidden;
}

public sealed class FakeNotesPage(FakeShape[] placeholders)
{
    public FakeShapes Shapes { get; } = new(placeholders);
}

public sealed class FakeShapes(FakeShape[] placeholders)
{
    public FakePlaceholders Placeholders { get; } = new(placeholders);
}

public sealed class FakePlaceholders(FakeShape[] placeholders)
{
    public int Count => placeholders.Length;

    public FakeShape Item(int index) => placeholders[index - 1];
}

public sealed class FakeShape(int type, bool hasTextFrame, string text)
{
    private const int PpPlaceholderTitle = 1;
    private const int PpPlaceholderBody = 2;

    public FakePlaceholderFormat PlaceholderFormat { get; } = new(type);

    public int HasTextFrame { get; } = hasTextFrame ? -1 : 0;

    public FakeTextFrame TextFrame { get; } = new(text);

    public static FakeShape Title() => new(PpPlaceholderTitle, true, "slide image");

    public static FakeShape Body(string text) => new(PpPlaceholderBody, true, text);

    public static FakeShape BodyWithoutTextFrame() => new(PpPlaceholderBody, false, "not read");
}

public sealed class FakePlaceholderFormat(int type)
{
    public int Type { get; } = type;
}

public sealed class FakeTextFrame(string text)
{
    public FakeTextRange TextRange { get; } = new(text);
}

public sealed class FakeTextRange(string text)
{
    public string Text { get; } = text;
}
