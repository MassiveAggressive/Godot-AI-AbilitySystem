using System;
using Timers;

namespace AbilitySystem;

/// <summary>Raises <see cref="Completed"/> after <see cref="Seconds"/> of game time (stops while the game is paused).</summary>
public sealed class WaitDelayTask : AbilityTask
{
    // Bound once, so starting the timer allocates no delegate.
    private readonly Action _onTimer;
    private TimerHandle _timer;

    public WaitDelayTask(float seconds)
    {
        Seconds = seconds;
        _onTimer = OnTimer;
    }

    public event Action Completed;

    public float Seconds { get; }

    /// <summary>Seconds left, e.g. for a cast bar; 0 once the task is not running.</summary>
    public float Remaining => IsRunning ? TimerManager.GetRemaining(_timer) : 0f;

    protected override void OnStart() => _timer = TimerManager.Start(Seconds, _onTimer);

    protected override void OnEnd()
    {
        TimerManager.Stop(_timer);
        _timer = TimerHandle.None;
    }

    private void OnTimer()
    {
        // Ended first, so the callback may end the ability or start new tasks.
        End();
        Completed?.Invoke();
    }
}
