using System;
using Timers;

namespace AbilitySystem;

/// <summary>
/// Raises <see cref="Released"/> with the seconds the input was held (game time) when the ability's input action
/// is released, e.g. for a charged attack. If the input is not held when the task starts (the ability was activated
/// by code), it completes right away with 0.
/// </summary>
public sealed class WaitInputReleaseTask : AbilityTask
{
    private readonly bool _alreadyReleased;
    private double _startTime;

    /// <param name="alreadyReleased">The input is not held now; the task completes right away.</param>
    public WaitInputReleaseTask(bool alreadyReleased = false) => _alreadyReleased = alreadyReleased;

    /// <summary>Seconds the input was held.</summary>
    public event Action<float> Released;

    /// <summary>Seconds since the task started; 0 once it is not running.</summary>
    public float HeldTime => IsRunning ? (float)(TimerManager.Time - _startTime) : 0f;

    protected override void OnStart()
    {
        _startTime = TimerManager.Time;
        if (_alreadyReleased)
            Complete();
    }

    protected override void OnInputReleased() => Complete();

    private void Complete()
    {
        float held = HeldTime;
        End();
        Released?.Invoke(held);
    }
}
