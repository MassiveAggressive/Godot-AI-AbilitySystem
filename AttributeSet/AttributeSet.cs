namespace AbilitySystem;

/// <summary>
/// Group of attributes, declared as get-only public properties: <c>public AttributeValue Health { get; } = new(100f);</c>.
/// Every change goes through this class so the overridable rules (clamping, reactions) run each time.
/// Order of a change: Pre hooks → values written → <see cref="PostCurrentValueChange"/> → attribute events.
/// </summary>
public abstract class AttributeSet
{
    /// <summary>Writes the base value and recalculates current. Recalculates even if the value is unchanged, so clamps are reapplied.</summary>
    public void SetBaseValue(AttributeValue attribute, float value)
    {
        float oldBase = attribute.BaseValue;
        float oldCurrent = attribute.CurrentValue;

        PreBaseValueChange(attribute, ref value);
        attribute.SetBase(value);
        Recalculate(attribute, oldBase, oldCurrent);
    }

    /// <summary><paramref name="sourceId"/> identifies the source (e.g. an active effect) for <see cref="RemoveModifiers"/>.</summary>
    public void AddModifier(AttributeValue attribute, ModifierOp op, float magnitude, int sourceId, ModifierEvaluationChannel channel = ModifierEvaluationChannel.Channel0)
    {
        float oldCurrent = attribute.CurrentValue;

        attribute.AddModifier(op, magnitude, sourceId, channel);
        Recalculate(attribute, attribute.BaseValue, oldCurrent);
    }

    /// <summary>Removes every modifier that <paramref name="sourceId"/> added to <paramref name="attribute"/>.</summary>
    public void RemoveModifiers(AttributeValue attribute, int sourceId)
    {
        float oldCurrent = attribute.CurrentValue;

        if (attribute.RemoveModifiers(sourceId))
            Recalculate(attribute, attribute.BaseValue, oldCurrent);
    }

    /// <summary>Before the base value is written; clamp <paramref name="newValue"/> here.</summary>
    protected virtual void PreBaseValueChange(AttributeValue attribute, ref float newValue)
    {
    }

    /// <summary>Before the calculated current value is written; clamp <paramref name="newValue"/> here. Modifiers are not changed, the clamp is reapplied on every recalculation.</summary>
    protected virtual void PreCurrentValueChange(AttributeValue attribute, ref float newValue)
    {
    }

    /// <summary>After the current value changed, before the attribute's events. React inside the set here (e.g. MaxHealth → Health).</summary>
    protected virtual void PostCurrentValueChange(AttributeValue attribute, float oldValue, float newValue)
    {
    }

    private void Recalculate(AttributeValue attribute, float oldBase, float oldCurrent)
    {
        float current = attribute.Evaluate();
        PreCurrentValueChange(attribute, ref current);
        attribute.SetCurrent(current);

        if (current != oldCurrent)
            PostCurrentValueChange(attribute, oldCurrent, current);

        attribute.RaiseChanged(oldBase, oldCurrent);
    }
}
