using System.Runtime.InteropServices;
using SpeakerBuddy.Core;

namespace SpeakerBuddy.App.Tests;

public class PowerPointSourceTests
{
    private const string Talk = @"C:\talks\talk.pptx";
    private const string Other = @"C:\talks\other.pptx";
    private const int MkEUnavailable = unchecked((int)0x800401E3);
    private const int RpcSServerUnavailable = unchecked((int)0x800706BA);

    private readonly MemoryLog _log = new();
    private readonly FakeApplication _application = new();
    private Func<object> _connect;
    private readonly PowerPointSource _source;

    public PowerPointSourceTests()
    {
        _connect = () => _application;
        _source = new PowerPointSource(_log.Write, () => _connect());
    }

    private static FakePresentation Deck(string name = Talk) =>
        new(name, FakeSlide.WithNotes(1, "[0:30]"), new FakeSlide(2, hidden: true), FakeSlide.WithNotes(3, "[1:00]"));

    private void NotRunning() => _connect = () => throw new COMException("not running", MkEUnavailable);

    [Fact]
    public void Without_a_running_powerpoint_there_is_no_show()
    {
        NotRunning();

        Assert.Equal(new PollResult(true, null), _source.Poll());
        Assert.Empty(_log.Lines);
    }

    [Fact]
    public void An_unreachable_powerpoint_skips_the_tick()
    {
        _connect = () => throw new COMException("The RPC server is unavailable.", RpcSServerUnavailable);

        Assert.Equal(new PollResult(false, null), _source.Poll());
        Assert.Equal(["PowerPoint not reachable, tick skipped: The RPC server is unavailable."], _log.Lines);
    }

    [Fact]
    public void A_powerpoint_without_a_show_has_no_show()
    {
        Assert.Equal(new PollResult(true, null), _source.Poll());
        Assert.Empty(_log.Lines);
    }

    [Fact]
    public void A_starting_show_reads_the_slides_once_and_follows_the_current_slide()
    {
        var window = _application.Run(Deck(), slideIndex: 1);

        var first = _source.Poll();
        window.View.Slide = Deck().Slides.Item(3);
        var second = _source.Poll();

        Assert.True(first.Ok);
        Assert.Equal(Talk, first.Show!.PresentationName);
        Assert.Equal(1, first.Show.SlideIndex);
        Assert.Equal(
            [new SlideNotes(1, false, "[0:30]"), new SlideNotes(2, true, null), new SlideNotes(3, false, "[1:00]")],
            first.Show.Slides);
        Assert.Equal(3, second.Show!.SlideIndex);
        Assert.Same(first.Show.Slides, second.Show.Slides);
        Assert.Equal([$"slide show of {Talk} started, 3 slides"], _log.Lines);
    }

    [Fact]
    public void Of_two_running_shows_the_first_is_taken()
    {
        _application.Run(Deck(Talk));
        _application.Run(Deck(Other));

        var poll = _source.Poll();

        Assert.Equal(Talk, poll.Show!.PresentationName);
        Assert.Equal([$"slide show of {Talk} started, 3 slides, 2 shows running, took the first"], _log.Lines);
    }

    [Fact]
    public void The_locked_show_keeps_its_place_when_another_show_starts_before_it()
    {
        var talk = _application.Run(Deck(Talk), slideIndex: 3);
        _source.Poll();

        _application.Windows.All.Insert(0, new FakeShowWindow(Deck(Other)) { View = { Slide = Deck(Other).Slides.Item(1) } });
        var poll = _source.Poll();

        Assert.Equal(Talk, poll.Show!.PresentationName);
        Assert.Equal(3, poll.Show.SlideIndex);
        Assert.Single(_log.Lines);
        Assert.Same(talk, _application.Windows.Item(2));
    }

    [Fact]
    public void The_lock_matches_the_presentation_name_without_case()
    {
        var presentation = Deck(Talk);
        _application.Run(presentation);
        _source.Poll();

        presentation.FullName = Talk.ToUpperInvariant();

        Assert.Equal(Talk, _source.Poll().Show!.PresentationName);
        Assert.Single(_log.Lines);
    }

    [Fact]
    public void When_the_locked_show_ends_the_next_poll_takes_the_other_show()
    {
        _application.Run(Deck(Talk));
        _application.Run(Deck(Other));
        _source.Poll();

        _application.Windows.All.RemoveAt(0);
        var ended = _source.Poll();
        var next = _source.Poll();

        Assert.Equal(new PollResult(true, null), ended);
        Assert.Equal(Other, next.Show!.PresentationName);
        Assert.Equal(
            [
                $"slide show of {Talk} started, 3 slides, 2 shows running, took the first",
                $"slide show of {Talk} ended",
                $"slide show of {Other} started, 3 slides",
            ],
            _log.Lines);
    }

    [Fact]
    public void The_end_of_show_screen_keeps_the_last_slide()
    {
        var window = _application.Run(Deck(), slideIndex: 3);
        _source.Poll();

        window.View.State = FakeView.Done;
        var poll = _source.Poll();

        Assert.True(poll.Ok);
        Assert.Equal(3, poll.Show!.SlideIndex);
    }

    [Fact]
    public void Notes_come_only_from_a_body_placeholder_with_a_text_frame()
    {
        _application.Run(new FakePresentation(Talk,
            new FakeSlide(1),
            new FakeSlide(2, false, FakeShape.Title()),
            new FakeSlide(3, false, FakeShape.Title(), FakeShape.BodyWithoutTextFrame()),
            new FakeSlide(4, false, FakeShape.Body("first body"), FakeShape.Body("second body"))));

        var slides = _source.Poll().Show!.Slides;

        Assert.Equal([null, null, null, "first body"], slides.Select(slide => slide.Notes));
    }

    public static TheoryData<Exception> BusyExceptions => new()
    {
        new COMException("busy", unchecked((int)0x8001010A)),
        new MissingMemberException("Could not get dispatch ID for SlideShowWindows"),
        new InvalidComObjectException("detached"),
        new InvalidCastException("cast"),
        new ArgumentException("argument"),
    };

    [Theory]
    [MemberData(nameof(BusyExceptions))]
    public void A_busy_powerpoint_skips_the_tick_and_keeps_the_lock(Exception busy)
    {
        _application.Run(Deck());
        _source.Poll();

        _application.Busy = busy;
        var skipped = _source.Poll();
        _application.Busy = null;
        var after = _source.Poll();

        Assert.Equal(new PollResult(false, null), skipped);
        Assert.Equal($"PowerPoint call failed, tick skipped: {busy.GetType().Name} {busy.Message}", _log.Lines[1]);
        Assert.Equal(Talk, after.Show!.PresentationName);
        Assert.Equal(2, _log.Lines.Count);
    }

    [Fact]
    public void An_object_without_the_expected_members_skips_the_tick()
    {
        _connect = () => new object();

        var poll = _source.Poll();

        Assert.Equal(new PollResult(false, null), poll);
        Assert.StartsWith("PowerPoint call failed, tick skipped: RuntimeBinderException ", _log.Lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void A_closed_powerpoint_releases_the_lock()
    {
        _application.Run(Deck());
        _source.Poll();

        NotRunning();
        _source.Poll();
        _connect = () => _application;
        _source.Poll();

        Assert.Equal([$"slide show of {Talk} started, 3 slides", $"slide show of {Talk} started, 3 slides"], _log.Lines);
    }
}
