using System;

namespace AbilitySystem;

/// <summary>
/// Identifies one application of an effect. Unique across all containers, so a handle never removes another
/// actor's effect by mistake. Also the source id of the effect's modifiers; code that adds modifiers by hand uses
/// negative ids so it never collides with effects.
/// </summary>
public readonly struct EffectHandle : IEquatable<EffectHandle>
{
    public static readonly EffectHandle None = default;

    private static int _last;

    public readonly int Value;

    private EffectHandle(int value) => Value = value;

    public bool IsValid => Value > 0;

    /// <summary>Most recently issued value; later handles are greater.</summary>
    internal static int LastValue => _last;

    internal static EffectHandle Next() => new(++_last);

    public bool Equals(EffectHandle other) => Value == other.Value;

    public override bool Equals(object obj) => obj is EffectHandle other && Equals(other);

    public override int GetHashCode() => Value;

    public static bool operator ==(EffectHandle a, EffectHandle b) => a.Value == b.Value;

    public static bool operator !=(EffectHandle a, EffectHandle b) => a.Value != b.Value;

    public override string ToString() => IsValid ? $"Effect {Value}" : "Effect None";
}
