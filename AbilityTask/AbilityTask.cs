namespace AbilitySystem;

/// <summary>
/// Something an active ability waits for (a delay, an event, an input release). Started by the ability
/// (<c>RunTask</c> or helpers such as <c>WaitDelay</c>) and ended with it, so its callbacks never run after the
/// ability ended. Write your own by overriding <see cref="OnStart"/> (subscribe) and <see cref="OnEnd"/>
/// (unsubscribe), and call <see cref="End"/> once the awaited thing happened, before raising your own event.
/// While running, the task also receives the events and the input of its ability.
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

    internal void DeliverEvent(in AbilityEventData data) => OnEvent(data);

    internal void DeliverInput(bool pressed)
    {
        if (pressed)
            OnInputPressed();
        else
            OnInputReleased();
    }

    protected abstract void OnStart();

    protected virtual void OnEnd()
    {
    }

    /// <summary>An event sent to the ability's actor (<see cref="AbilityContainer.SendEvent"/>).</summary>
    protected virtual void OnEvent(in AbilityEventData data)
    {
    }

    /// <summary>The ability's input action was pressed again while it is active.</summary>
    protected virtual void OnInputPressed()
    {
    }

    /// <summary>The ability's input action was released.</summary>
    protected virtual void OnInputReleased()
    {
    }
}
