using Tags;

namespace AbilitySystem;

/// <summary>
/// Raises <see cref="Received"/> when an event matching <see cref="Tag"/> (hierarchically: "Event.Hit" also takes
/// "Event.Hit.Crit") is sent to the actor. Ends after the first one, unless <see cref="OnlyOnce"/> is off.
/// </summary>
public sealed class WaitEventTask : AbilityTask
{
    public WaitEventTask(Tag tag, bool onlyOnce = true)
    {
        Tag = tag;
        OnlyOnce = onlyOnce;
    }

    public event AbilityEventHandler Received;

    public Tag Tag { get; }

    public bool OnlyOnce { get; }

    protected override void OnStart()
    {
    }

    protected override void OnEvent(in AbilityEventData data)
    {
        if (!TagRegistry.Matches(data.Tag, Tag))
            return;

        if (OnlyOnce)
            End();

        Received?.Invoke(data);
    }
}
