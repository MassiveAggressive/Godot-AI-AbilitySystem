using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// What a cue scene is told when its cue runs (<see cref="ICueNotify"/>). The nodes may be null or freed; check them
/// with <see cref="GodotObject.IsInstanceValid"/>.
/// </summary>
public readonly struct CueParameters
{
    public CueParameters(Tag tag, Node target, EffectSpec spec, ActiveEffect effect, int stackCount)
    {
        Tag = tag;
        Target = target;
        Spec = spec;
        Effect = effect;
        StackCount = stackCount;
    }

    /// <summary>The cue tag on the effect, e.g. "Cue.Fire.Burn" (also when the scene comes from a parent tag's entry).</summary>
    public Tag Tag { get; }

    /// <summary>The actor showing the cue.</summary>
    public Node Target { get; }

    public EffectSpec Spec { get; }

    /// <summary>The active effect; null for instant effects.</summary>
    public ActiveEffect Effect { get; }

    /// <summary>The effect's stacks when the cue ran; 1 for instant effects.</summary>
    public int StackCount { get; }

    public Node Instigator => Spec.Instigator;

    public Node Causer => Spec.Causer;

    public float Level => Spec.Level;
}
