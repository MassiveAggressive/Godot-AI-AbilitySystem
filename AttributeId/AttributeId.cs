using System;

namespace AbilitySystem;

/// <summary>
/// Runtime id of an attribute such as "CombatSet.Health". Create through <see cref="AttributeRegistry.Request"/>.
/// Ids can differ between builds, so saved data stores the name, not the id.
/// </summary>
public readonly struct AttributeId : IEquatable<AttributeId>
{
    public static readonly AttributeId None = default;

    public int Value { get; }

    public bool IsValid => Value != 0;

    internal AttributeId(int value)
    {
        Value = value;
    }

    public bool Equals(AttributeId other) => Value == other.Value;

    public override bool Equals(object obj) => obj is AttributeId other && Value == other.Value;

    public override int GetHashCode() => Value;

    public override string ToString() => AttributeRegistry.GetName(this);

    public static bool operator ==(AttributeId left, AttributeId right) => left.Value == right.Value;

    public static bool operator !=(AttributeId left, AttributeId right) => left.Value != right.Value;
}
