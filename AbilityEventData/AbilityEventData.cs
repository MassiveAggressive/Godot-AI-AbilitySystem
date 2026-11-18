using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// Something that happened to an actor (a hit landed, an animation reached its impact frame), sent with
/// <see cref="AbilityContainer.SendEvent"/>. Activates abilities triggered by <see cref="Tag"/> and reaches the
/// event tasks of active abilities.
/// </summary>
public readonly struct AbilityEventData
{
    public readonly Tag Tag;

    /// <summary>Who caused it; may be null or freed.</summary>
    public readonly Node Instigator;

    /// <summary>Who it happened to or is aimed at; may be null or freed.</summary>
    public readonly Node Target;

    /// <summary>Any number that goes with the event (damage dealt, charge level).</summary>
    public readonly float Magnitude;

    /// <summary>Anything else; cast to what the sender put in.</summary>
    public readonly object Payload;

    /// <summary>Several targets or points (e.g. chosen by AI); may be null. Owned by the sender: copy it to keep it.</summary>
    public readonly TargetData Targets;

    public AbilityEventData(Tag tag, Node instigator = null, Node target = null, float magnitude = 0f, object payload = null, TargetData targets = null)
    {
        Tag = tag;
        Instigator = instigator;
        Target = target;
        Magnitude = magnitude;
        Payload = payload;
        Targets = targets;
    }
}
