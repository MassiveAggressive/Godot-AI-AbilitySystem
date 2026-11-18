using System;

namespace AbilitySystem;

/// <summary>Identifies an ability given to an actor. Unique across all containers.</summary>
public readonly struct AbilityHandle : IEquatable<AbilityHandle>
{
    public static readonly AbilityHandle None = default;

    private static int _last;

    public readonly int Value;

    private AbilityHandle(int value) => Value = value;

    public bool IsValid => Value > 0;

    internal static AbilityHandle Next() => new(++_last);

    public bool Equals(AbilityHandle other) => Value == other.Value;

    public override bool Equals(object obj) => obj is AbilityHandle other && Equals(other);

    public override int GetHashCode() => Value;

    public static bool operator ==(AbilityHandle a, AbilityHandle b) => a.Value == b.Value;

    public static bool operator !=(AbilityHandle a, AbilityHandle b) => a.Value != b.Value;

    public override string ToString() => IsValid ? $"Ability {Value}" : "Ability None";
}
