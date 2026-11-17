using System.Collections.Generic;

namespace AbilitySystem;

/// <summary>
/// Changes an <see cref="ExecutionCalculation"/> asks for. Applied to the target's base values in the order added,
/// after the calculation returns. Reused by its <see cref="ActiveEffectContainer"/>, so do not keep a reference.
/// </summary>
public sealed class ExecutionOutput
{
    private readonly List<Entry> _entries = new(4);

    internal ExecutionOutput()
    {
    }

    internal int Count => _entries.Count;

    internal Entry this[int index] => _entries[index];

    /// <summary>Applies <paramref name="op"/> with <paramref name="magnitude"/> to the base value of <paramref name="attribute"/>.</summary>
    public void AddModifier(AttributeId attribute, ModifierOp op, float magnitude) => _entries.Add(new Entry(attribute, op, magnitude));

    internal void Clear() => _entries.Clear();

    internal readonly struct Entry
    {
        public readonly AttributeId Attribute;
        public readonly ModifierOp Op;
        public readonly float Magnitude;

        public Entry(AttributeId attribute, ModifierOp op, float magnitude)
        {
            Attribute = attribute;
            Op = op;
            Magnitude = magnitude;
        }
    }
}
