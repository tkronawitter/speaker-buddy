using SpeakerBuddy.Core;

namespace SpeakerBuddy.Core.Tests;

public class TalkControllerTests
{
    private static readonly ColorScheme Colors = ColorScheme.Default;

    // Worked example deck: slide 15 carries "## 10:00"; slide 30 "## 45:00" makes L = 45 min, tol 1:00.
    private static ShowState Show(int slideIndex, string name = @"C:\talks\deck.pptx") =>
        new(name, slideIndex,
        [
            new SlideNotes(1, false, null),
            new SlideNotes(15, false, "## 10:00"),
            new SlideNotes(30, false, "## 45:00"),
        ]);

    private static TimerStatusLine Timer(int t) => new(t, true);

    [Fact]
    public void Without_a_show_the_neutral_state_goes_out_once_per_second()
    {
        var controller = new TalkController(null, Colors);

        Assert.Equal(new ProtocolLine[] { new StateLine("#000000", null) }, controller.Tick(null, Timer(5), periodic: true));
        Assert.Empty(controller.Tick(null, Timer(5), periodic: false));
        Assert.Equal(new ProtocolLine[] { new StateLine("#000000", null) }, controller.Tick(null, null, periodic: true));
    }

    [Fact]
    public void Every_periodic_tick_sends_a_line_whatever_the_state()
    {
        var controller = new TalkController(null, Colors);
        var ticks = new (ShowState? Show, TimerStatusLine? Timer)[]
        {
            (null, null), (Show(1), null), (Show(1), Timer(5)), (Show(15), Timer(640)), (Show(15), null), (null, Timer(700)), (null, null),
        };

        foreach (var (show, timer) in ticks)
        {
            Assert.NotEmpty(controller.Tick(show, timer, periodic: true));
        }
    }

    [Fact]
    public void Without_timer_status_the_last_state_keeps_the_link_alive()
    {
        var controller = new TalkController(null, Colors);
        controller.Tick(Show(15), Timer(640), periodic: true);

        Assert.Equal(new ProtocolLine[] { new StateLine("#00A000", null) }, controller.Tick(Show(15), null, periodic: true));
        Assert.Empty(controller.Tick(Show(15), null, periodic: false));
    }

    [Fact]
    public void Show_start_sends_the_length_from_the_highest_mark_then_the_state()
    {
        var controller = new TalkController(null, Colors);

        var lines = controller.Tick(Show(1), Timer(5), periodic: true);

        Assert.Equal(new ProtocolLine[] { new LengthLine(2700), new StateLine("#000000", null) }, lines);
    }

    [Fact]
    public void Without_timer_status_the_length_and_the_neutral_state_go_out()
    {
        var controller = new TalkController(null, Colors);

        Assert.Equal(new ProtocolLine[] { new LengthLine(2700), new StateLine("#000000", null) }, controller.Tick(Show(1), null, periodic: true));
    }

    [Fact]
    public void Length_argument_is_sent_on_connect_and_not_repeated_at_show_start()
    {
        var controller = new TalkController(1800, Colors);

        Assert.Equal(new ProtocolLine[] { new LengthLine(1800) }, controller.OnConnected());
        Assert.DoesNotContain(controller.Tick(Show(1), Timer(5), periodic: true), l => l is LengthLine);
    }

    [Fact]
    public void State_is_sent_on_change_and_periodically_but_not_otherwise()
    {
        var controller = new TalkController(null, Colors);
        controller.Tick(Show(1), Timer(5), periodic: true);

        Assert.Empty(controller.Tick(Show(1), Timer(5), periodic: false));
        Assert.Equal(new ProtocolLine[] { new StateLine("#000000", null) }, controller.Tick(Show(2), Timer(6), periodic: true));
        Assert.Equal(new ProtocolLine[] { new StateLine("#00A000", null) }, controller.Tick(Show(15), Timer(640), periodic: false));
    }

    [Fact]
    public void Worked_example_arrival_late_is_purple_with_the_deviation()
    {
        var controller = new TalkController(null, Colors);
        controller.Tick(Show(13), Timer(600), periodic: true);

        Assert.Equal(new ProtocolLine[] { new StateLine("#8000A0", 120) }, controller.Tick(Show(15), Timer(720), periodic: false));
        Assert.Equal(EvaluationRule.Behind, controller.LastEvaluation!.Rule);
    }

    [Fact]
    public void Show_end_sends_the_end_line_once()
    {
        var controller = new TalkController(null, Colors);
        controller.Tick(Show(1), Timer(5), periodic: true);

        Assert.Equal(new ProtocolLine[] { new EndedLine() }, controller.Tick(null, Timer(6), periodic: true));
        Assert.Equal(new ProtocolLine[] { new StateLine("#000000", null) }, controller.Tick(null, Timer(7), periodic: true));
        Assert.Null(controller.Marks);
    }

    [Fact]
    public void Timer_reset_starts_the_evaluation_over()
    {
        var controller = new TalkController(null, Colors);
        controller.Tick(Show(15), Timer(720), periodic: true);

        var lines = controller.Tick(Show(15), new TimerStatusLine(0, false), periodic: false);

        Assert.Equal(new ProtocolLine[] { new StateLine("#C00000", -600) }, lines);
    }

    [Fact]
    public void A_smaller_t_while_running_is_no_reset()
    {
        var controller = new TalkController(null, Colors);
        controller.Tick(Show(13), Timer(600), periodic: true);
        var arrival = controller.Tick(Show(15), Timer(720), periodic: true);

        // A fresh evaluation would read the arrival at 700 as 1:40 late; the talk keeps its 2:00
        Assert.Equal(arrival, controller.Tick(Show(15), Timer(700), periodic: true));
        Assert.Equal(arrival, controller.Tick(Show(15), new TimerStatusLine(0, true), periodic: true));
    }

    [Fact]
    public void A_stopped_clock_above_zero_is_no_reset()
    {
        var controller = new TalkController(null, Colors);
        controller.Tick(Show(13), Timer(600), periodic: true);
        var arrival = controller.Tick(Show(15), Timer(720), periodic: true);

        Assert.Equal(arrival, controller.Tick(Show(15), new TimerStatusLine(700, false), periodic: true));
    }

    [Fact]
    public void A_reset_after_a_reconnect_still_starts_over()
    {
        var controller = new TalkController(null, Colors);
        controller.Tick(Show(15), Timer(720), periodic: true);
        controller.OnDisconnected();

        var lines = controller.Tick(Show(15), new TimerStatusLine(0, false), periodic: false);

        Assert.Equal(new ProtocolLine[] { new StateLine("#C00000", -600) }, lines);
    }

    [Fact]
    public void Reconnect_resends_length_and_the_last_state()
    {
        var controller = new TalkController(null, Colors);
        controller.Tick(Show(15), Timer(640), periodic: true);

        Assert.Equal(new ProtocolLine[] { new LengthLine(2700), new StateLine("#00A000", null) }, controller.OnConnected());
    }

    [Fact]
    public void A_different_presentation_starts_a_new_evaluation()
    {
        var controller = new TalkController(null, Colors);
        controller.Tick(Show(15), Timer(640), periodic: true);

        var lines = controller.Tick(Show(1, @"C:\talks\other.pptx"), Timer(650), periodic: false);

        Assert.Equal(new ProtocolLine[] { new StateLine("#000000", null) }, lines);
    }

    // Highest mark 3:00 is below 5:00, so without an argument L stays unknown
    private static ShowState ShortShow(int slideIndex) =>
        new(@"C:\talks\short.pptx", slideIndex,
        [
            new SlideNotes(1, false, null),
            new SlideNotes(2, false, "## 3:00"),
        ]);

    [Fact]
    public void Connect_without_a_known_length_clears_the_length_on_the_timer()
    {
        var controller = new TalkController(null, Colors);

        Assert.Equal(new ProtocolLine[] { new LengthLine(null) }, controller.OnConnected());
    }

    [Fact]
    public void Every_show_start_with_an_unknown_length_clears_it()
    {
        var controller = new TalkController(null, Colors);
        controller.OnConnected();

        Assert.Contains(new LengthLine(null), controller.Tick(ShortShow(1), Timer(5), periodic: true));
        Assert.DoesNotContain(controller.Tick(ShortShow(1), Timer(6), periodic: true), l => l is LengthLine);

        controller.Tick(null, Timer(7), periodic: true);
        Assert.Contains(new LengthLine(null), controller.Tick(ShortShow(1), Timer(8), periodic: true));
    }

    [Fact]
    public void Show_end_forgets_the_length_so_the_next_unknown_show_clears_it()
    {
        var controller = new TalkController(null, Colors);
        controller.OnConnected();
        Assert.Contains(new LengthLine(2700), controller.Tick(Show(1), Timer(5), periodic: true));
        controller.Tick(null, Timer(6), periodic: true);

        Assert.Contains(new LengthLine(null), controller.Tick(ShortShow(1), Timer(7), periodic: true));
    }

    [Fact]
    public void Show_end_keeps_the_length_argument()
    {
        var controller = new TalkController(1800, Colors);
        controller.OnConnected();
        controller.Tick(ShortShow(1), Timer(5), periodic: true);
        controller.Tick(null, Timer(6), periodic: true);

        Assert.DoesNotContain(controller.Tick(ShortShow(1), Timer(7), periodic: true), l => l is LengthLine);
        Assert.Equal(new LengthLine(1800), controller.OnConnected()[0]);
    }

    [Fact]
    public void Zero_length_argument_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TalkController(0, Colors));
    }
}
