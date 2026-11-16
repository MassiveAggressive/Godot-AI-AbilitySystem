using System;

namespace AbilitySystem;

/// <summary>
/// Base and current value of one attribute. Current is the base run through the active modifiers
/// (<see cref="Aggregator"/>). Values change only through the owning <see cref="AttributeSet"/>,
/// so the set's rules run on every change.
/// </summary>
public sealed class AttributeValue
{
    // Created on the first modifier; attributes that are never modified allocate nothing.
    private Aggregator _aggregator;

    public AttributeValue(float baseValue = 0f)
    {
        BaseValue = baseValue;
        CurrentValue = baseValue;
    }

    public float BaseValue { get; private set; }

    public float CurrentValue { get; private set; }

    /// <summary>(attribute, old value, new value). Raised only when the value actually changes.</summary>
    public event Action<AttributeValue, float, float> BaseValueChanged;

    /// <summary>(attribute, old value, new value). Raised only when the value actually changes.</summary>
    public event Action<AttributeValue, float, float> CurrentValueChanged;

    internal void SetBase(float value) => BaseValue = value;

    internal void SetCurrent(float value) => CurrentValue = value;

    internal void AddModifier(ModifierOp op, float magnitude, int sourceId, ModifierEvaluationChannel channel)
    {
        _aggregator ??= new Aggregator();
        _aggregator.AddModifier(op, magnitude, sourceId, channel);
    }

    internal bool RemoveModifiers(int sourceId) => _aggregator != null && _aggregator.RemoveModifiers(sourceId);

    /// <summary>Current value from base and modifiers, before the set's clamp.</summary>
    internal float Evaluate() => _aggregator?.Evaluate(BaseValue) ?? BaseValue;

    /// <summary>
    /// Raises events against the live values, so a nested change made by a set hook never leaves listeners with a
    /// stale "new" value. A listener must not change the attribute it listens to: the delegate passes the same
    /// arguments to every listener, so the later ones would see the old value. Put such rules in a set hook.
    /// </summary>
    internal void RaiseChanged(float oldBase, float oldCurrent)
    {
        if (BaseValue != oldBase)
            BaseValueChanged?.Invoke(this, oldBase, BaseValue);

        if (CurrentValue != oldCurrent)
            CurrentValueChanged?.Invoke(this, oldCurrent, CurrentValue);
    }

    public override string ToString() => $"{CurrentValue} (base {BaseValue})";
}
