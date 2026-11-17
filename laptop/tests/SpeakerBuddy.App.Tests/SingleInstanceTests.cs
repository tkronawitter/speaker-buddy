namespace SpeakerBuddy.App.Tests;

public sealed class SingleInstanceTests
{
    // A fresh name per test, so the tests never meet a running tray or each other.
    private readonly string _name = $@"Local\SpeakerBuddy-tests-{Guid.NewGuid():N}";

    [Fact]
    public void The_tray_guard_is_per_logon_session()
    {
        Assert.Equal(@"Local\SpeakerBuddy", SingleInstance.TrayName);
    }

    [Fact]
    public void The_first_start_owns_the_guard()
    {
        using var first = SingleInstance.Acquire(_name);

        Assert.Equal(SingleInstanceState.First, first.State);
        Assert.Null(first.Error);
    }

    [Fact]
    public void A_second_start_finds_the_first_one_running()
    {
        using var first = SingleInstance.Acquire(_name);
        using var second = SingleInstance.Acquire(_name);

        Assert.Equal(SingleInstanceState.AlreadyRunning, second.State);
    }

    [Fact]
    public void A_start_after_the_first_one_ended_owns_the_guard_again()
    {
        SingleInstance.Acquire(_name).Dispose();

        using var next = SingleInstance.Acquire(_name);

        Assert.Equal(SingleInstanceState.First, next.State);
    }

    [Fact]
    public void A_second_start_that_exits_leaves_the_first_one_guarded()
    {
        using var first = SingleInstance.Acquire(_name);
        SingleInstance.Acquire(_name).Dispose();

        using var third = SingleInstance.Acquire(_name);

        Assert.Equal(SingleInstanceState.AlreadyRunning, third.State);
    }

    [Fact]
    public void Disposing_twice_is_harmless()
    {
        var first = SingleInstance.Acquire(_name);

        first.Dispose();
        first.Dispose();
    }

    [Fact]
    public void A_foreign_object_under_the_name_lets_the_program_run_unguarded()
    {
        using var foreign = new EventWaitHandle(false, EventResetMode.ManualReset, _name);

        using var guard = SingleInstance.Acquire(_name);

        Assert.Equal(SingleInstanceState.GuardFailed, guard.State);
        Assert.Contains(nameof(WaitHandleCannotBeOpenedException), guard.Error, StringComparison.Ordinal);
    }
}
