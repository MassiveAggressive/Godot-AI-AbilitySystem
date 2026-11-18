namespace AbilitySystem;

/// <summary>
/// Something an active ability waits for (a delay, an event, an input release). Started by the ability
/// (<c>RunTask</c> or helpers such as <c>WaitDelay</c>) and ended with it, so its callbacks never run after the
/// ability ended. Write your own by overriding <see cref="OnStart"/> (subscribe) and <see cref="OnEnd"/>
/// (unsubscribe), and call <see cref="End"/> once the awaited thing happened, before raising your own event.
/// </summary>
public abstract class AbilityTask
{
    public bool IsRunning { get; private set; }

    /// <summary>Stops the task; its callbacks no longer run. Does nothing if it is not running.</summary>
    public void End()
    {
        if (!IsRunning)
            return;

        IsRunning = false;
        OnEnd();
    }

    internal void Start()
    {
        IsRunning = true;
        OnStart();
    }

    protected abstract void OnStart();

    protected virtual void OnEnd()
    {
    }
}
