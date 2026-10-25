using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using SpeakerBuddy.Core;
using A = DocumentFormat.OpenXml.Drawing;

namespace SpeakerBuddy.App.Tests;

public sealed class ConsoleModesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"speaker-buddy-tests-{Guid.NewGuid():N}");
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public ConsoleModesTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>Writes a deck with one notes text per slide (null for a slide without notes).</summary>
    private string Deck(params string?[] notes)
    {
        var path = Path.Combine(_folder, $"{Guid.NewGuid():N}.pptx");
        using var document = PresentationDocument.Create(path, PresentationDocumentType.Presentation);
        var presentationPart = document.AddPresentationPart();
        var slideIds = new SlideIdList();
        presentationPart.Presentation = new Presentation(slideIds);
        uint id = 256;
        foreach (var text in notes)
        {
            var slide = presentationPart.AddNewPart<SlidePart>();
            slide.Slide = new Slide(new CommonSlideData(new ShapeTree()));
            if (text is not null)
            {
                slide.AddNewPart<NotesSlidePart>().NotesSlide = new NotesSlide(new CommonSlideData(new ShapeTree(
                    new Shape(
                        new NonVisualShapeProperties(
                            new NonVisualDrawingProperties { Id = 2, Name = "Notes" },
                            new NonVisualShapeDrawingProperties(),
                            new ApplicationNonVisualDrawingProperties(new PlaceholderShape { Type = PlaceholderValues.Body })),
                        new ShapeProperties(),
                        new TextBody(new OpenXmlElement[] { new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text(text))) })))));
            }

            slideIds.Append(new SlideId { Id = id++, RelationshipId = presentationPart.GetIdOfPart(slide) });
        }

        return path;
    }

    [Fact]
    public void Check_lists_a_clean_deck_and_exits_0()
    {
        var deck = Deck(null, "## 10:00", "## 20:00");

        var code = ConsoleModes.Check(deck, 1800, _output, _error);

        Assert.Equal(0, code);
        Assert.Equal(DeckCheck.Format(DeckCheck.Run(DeckReader.Read(deck), 1800)) + Environment.NewLine, _output.ToString());
        Assert.Empty(_error.ToString());
    }

    [Fact]
    public void Check_exits_1_when_it_reports_problems()
    {
        var deck = Deck("## 10:75", "## 20:00");

        var code = ConsoleModes.Check(deck, null, _output, _error);

        Assert.Equal(1, code);
        Assert.Contains("## 10:75", _output.ToString());
    }

    [Fact]
    public void Check_exits_2_for_a_file_it_cannot_read()
    {
        var path = Path.Combine(_folder, "deck.pptx");
        File.WriteAllText(path, "not a deck");

        var code = ConsoleModes.Check(path, null, _output, _error);

        Assert.Equal(2, code);
        Assert.StartsWith($"Cannot read {path}: ", _error.ToString());
        Assert.Empty(_output.ToString());
    }

    [Fact]
    public void Check_exits_2_for_a_missing_file()
    {
        var path = Path.Combine(_folder, "missing.pptx");

        Assert.Equal(2, ConsoleModes.Check(path, null, _output, _error));
        Assert.StartsWith($"Cannot read {path}: ", _error.ToString());
    }

    [Fact]
    public void Demo_sends_the_pattern_until_stopped_then_the_end_line()
    {
        var link = new FakeTimerLink();
        using var stop = new CancellationTokenSource();
        var now = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
        var steps = 0;

        var code = ConsoleModes.Demo(link, _output, stop.Token, () => now, () =>
        {
            now = now.AddSeconds(1);
            if (++steps == DemoSequence.StepSeconds + 1)
            {
                stop.Cancel();
            }
        });

        Assert.Equal(0, code);
        Assert.True(link.Started);
        var expected = Enumerable.Range(0, DemoSequence.StepSeconds + 1)
            .Select(s => (ProtocolLine)DemoSequence.At(s, ColorScheme.Default))
            .Append(new EndedLine());
        Assert.Equal(expected, link.Sent);

        // Neutral for the first step, then green on plan
        var neutral = DemoSequence.At(0, ColorScheme.Default);
        var green = DemoSequence.At(DemoSequence.StepSeconds, ColorScheme.Default);
        Assert.Equal(
            [
                $"demo running (speaker-buddy {AppVersion.Current}), Ctrl+C to stop",
                $"sent colour {neutral.Color} deviation none",
                $"sent colour {green.Color} deviation {green.Deviation}",
            ],
            _output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Demo_reports_only_changes_of_the_sent_state()
    {
        var link = new FakeTimerLink();
        using var stop = new CancellationTokenSource();
        var now = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
        var steps = 0;

        // The clock stands still, so the same state goes out every step but is reported once
        ConsoleModes.Demo(link, _output, stop.Token, () => now, () =>
        {
            if (++steps == 4)
            {
                stop.Cancel();
            }
        });

        Assert.Equal(5, link.Sent.Count);
        Assert.Single(_output.ToString().Split(Environment.NewLine), l => l.StartsWith("sent colour ", StringComparison.Ordinal));
    }

    [Fact]
    public void Demo_reports_the_timer_and_sends_the_length_on_connect()
    {
        var link = new FakeTimerLink();
        using var stop = new CancellationTokenSource();

        ConsoleModes.Demo(link, _output, stop.Token, () => DateTime.UnixEpoch, () =>
        {
            link.RaiseConnected("COM4");
            link.RaiseFirmware(new FirmwareLine("v0.1.0"));
            link.RaiseFirmware(new FirmwareLine("v0.1.0"));
            link.RaiseStatus(new TimerStatusLine(5, true));
            link.RaiseStatus(new TimerStatusLine(5, true));
            link.RaiseStatus(new TimerStatusLine(6, true));
            link.RaiseDisconnected();
            stop.Cancel();
        });

        var text = _output.ToString();
        Assert.Contains($"connected on COM4{Environment.NewLine}", text);
        Assert.Contains(new LengthLine(DemoSequence.LengthSeconds), link.Sent);
        Assert.Single(text.Split(Environment.NewLine), l => l == "timer firmware v0.1.0");
        Assert.Single(text.Split(Environment.NewLine), l => l == "timer t=5 run=True");
        Assert.Contains($"timer t=6 run=True{Environment.NewLine}", text);
        Assert.Contains($"disconnected, waiting for the timer{Environment.NewLine}", text);
    }

    [Fact]
    public void A_stopped_demo_sends_only_the_end_line()
    {
        var link = new FakeTimerLink();
        using var stop = new CancellationTokenSource();
        stop.Cancel();

        ConsoleModes.Demo(link, _output, stop.Token, () => DateTime.UnixEpoch, () => { });

        Assert.Equal(new ProtocolLine[] { new EndedLine() }, link.Sent);
    }

    private sealed class FailingLink : ITimerLink
    {
        public event Action<string>? Connected { add { } remove { } }

        public event Action? Disconnected { add { } remove { } }

        public event Action<TimerStatusLine>? StatusReceived { add { } remove { } }

        public event Action<FirmwareLine>? FirmwareReceived { add { } remove { } }

        public void Start()
        {
        }

        public bool Send(ProtocolLine line) => false;

        public void Dispose()
        {
        }
    }

    [Fact]
    public void A_state_the_link_could_not_send_is_not_reported()
    {
        using var stop = new CancellationTokenSource();

        ConsoleModes.Demo(new FailingLink(), _output, stop.Token, () => DateTime.UnixEpoch, stop.Cancel);

        Assert.DoesNotContain("sent colour", _output.ToString());
    }
}
