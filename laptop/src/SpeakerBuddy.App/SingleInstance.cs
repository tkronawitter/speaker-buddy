namespace SpeakerBuddy.App;

/// <summary>
/// Single-instance guard of the tray mode (#66): a named mutex per logon session. The first tray
/// instance creates and owns it; a second start finds it and exits. The handle lives as long as this
/// object, so it is kept in a local over the message loop and disposed on the thread that created it.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    /// <summary><c>Local\</c>: one tray per logon session, so a second user or RDP session gets its own.</summary>
    public const string TrayName = @"Local\SpeakerBuddy";

    private Mutex? _mutex;

    private SingleInstance(Mutex? mutex, SingleInstanceState state, string? error)
    {
        _mutex = mutex;
        State = state;
        Error = error;
    }

    public SingleInstanceState State { get; }

    /// <summary>Why the guard could not be taken, when <see cref="State"/> is <see cref="SingleInstanceState.GuardFailed"/>.</summary>
    public string? Error { get; }

    /// <summary>
    /// Tries to become the one instance under <paramref name="name"/>. Never throws: a mutex that
    /// belongs to another user or elevation level counts as a running instance, any other failure
    /// (a foreign object under the name) lets the program run unguarded, because a guard fault must
    /// not stop the talk.
    /// </summary>
    public static SingleInstance Acquire(string name)
    {
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
            if (createdNew)
            {
                return new SingleInstance(mutex, SingleInstanceState.First, null);
            }

            mutex.Dispose();
            return new SingleInstance(null, SingleInstanceState.AlreadyRunning, null);
        }
        catch (UnauthorizedAccessException)
        {
            mutex?.Dispose();
            return new SingleInstance(null, SingleInstanceState.AlreadyRunning, null);
        }
        catch (Exception e) when (e is IOException or WaitHandleCannotBeOpenedException)
        {
            mutex?.Dispose();
            return new SingleInstance(null, SingleInstanceState.GuardFailed, $"{e.GetType().Name} {e.Message}");
        }
    }

    public void Dispose()
    {
        if (_mutex is null)
        {
            return;
        }

        // Owned since creation; releasing before closing keeps it from being left abandoned.
        var mutex = _mutex;
        _mutex = null;
        try
        {
            mutex.ReleaseMutex();
        }
        finally
        {
            mutex.Dispose();
        }
    }
}

internal enum SingleInstanceState
{
    /// <summary>This process owns the guard and runs.</summary>
    First,

    /// <summary>Another instance owns the guard: log it and exit.</summary>
    AlreadyRunning,

    /// <summary>The guard could not be taken; the program runs without it.</summary>
    GuardFailed,
}
