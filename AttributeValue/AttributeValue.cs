using System;

namespace AbilitySystem;

/// <summary>
/// Base and current value of one attribute. Current is the base run through the active modifiers, which the owning
/// <see cref="AttributeSet"/> keeps. Values change only through that set, so the set's rules run on every change.
/// </summary>
public sealed class AttributeValue
{
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
