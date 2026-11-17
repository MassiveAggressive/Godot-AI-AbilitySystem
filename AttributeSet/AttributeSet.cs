using System.Collections.Generic;

namespace AbilitySystem;

/// <summary>
/// Group of attributes, declared as get-only public properties: <c>public AttributeValue Health { get; } = new(100f);</c>.
/// Every change goes through this class so the overridable rules (clamping, reactions) run each time.
/// Order of a change: Pre hooks → values written → <see cref="PostCurrentValueChange"/> → attribute events →
/// (for modifiers) <see cref="PostEffectExecute"/> → <see cref="ModifierApplied"/>.
/// </summary>
public abstract class AttributeSet
{
    // Created on the first modifier; sets that are never modified allocate nothing.
    private Dictionary<AttributeValue, Aggregator> _aggregators;

    /// <summary>Raised after a modifier was executed, added or removed, once all values are written.</summary>
    public event ModifierEventHandler ModifierApplied;

    /// <summary>Writes the base value and recalculates current. Recalculates even if the value is unchanged, so clamps are reapplied.</summary>
    public void SetBaseValue(AttributeValue attribute, float value)
    {
        float oldBase = attribute.BaseValue;
        float oldCurrent = attribute.CurrentValue;

        PreBaseValueChange(attribute, ref value);
        attribute.SetBase(value);
        Recalculate(attribute, oldBase, oldCurrent);
    }

    /// <summary>
    /// Adds a modifier to the current value until <see cref="RemoveModifiers"/> is called with <paramref name="source"/>'s id.
    /// <paramref name="key"/> tells this modifier from the source's others for <see cref="UpdateModifier"/>.
    /// </summary>
    public void AddModifier(AttributeValue attribute, ModifierOp op, float magnitude, in ModifierSource source, ModifierEvaluationChannel channel = ModifierEvaluationChannel.Channel0, int key = 0)
    {
        float oldCurrent = attribute.CurrentValue;

        _aggregators ??= new Dictionary<AttributeValue, Aggregator>();
        if (!_aggregators.TryGetValue(attribute, out Aggregator aggregator))
        {
            aggregator = new Aggregator();
            _aggregators.Add(attribute, aggregator);
        }

        aggregator.AddModifier(op, magnitude, source.Id, channel, key);
        Recalculate(attribute, attribute.BaseValue, oldCurrent);
        RaiseModifierApplied(new ModifierEventData(ModifierEventKind.Added, attribute, op, magnitude, oldCurrent, attribute.CurrentValue, source));
    }

    /// <summary>Gives an added modifier a new magnitude, keeping its order among the attribute's modifiers.</summary>
    public void UpdateModifier(AttributeValue attribute, in ModifierSource source, int key, float magnitude)
    {
        if (_aggregators == null || !_aggregators.TryGetValue(attribute, out Aggregator aggregator))
            return;

        float oldCurrent = attribute.CurrentValue;
        if (!aggregator.SetMagnitude(source.Id, key, magnitude, out ModifierOp op))
            return;

        Recalculate(attribute, attribute.BaseValue, oldCurrent);
        RaiseModifierApplied(new ModifierEventData(ModifierEventKind.Updated, attribute, op, magnitude, oldCurrent, attribute.CurrentValue, source));
    }

    /// <summary>Removes every modifier that <paramref name="sourceId"/> added to <paramref name="attribute"/>.</summary>
    public void RemoveModifiers(AttributeValue attribute, int sourceId)
    {
        if (_aggregators == null || !_aggregators.TryGetValue(attribute, out Aggregator aggregator))
            return;

        float oldCurrent = attribute.CurrentValue;
        if (!aggregator.RemoveModifiers(sourceId))
            return;

        Recalculate(attribute, attribute.BaseValue, oldCurrent);
        RaiseModifierApplied(new ModifierEventData(ModifierEventKind.Removed, attribute, default, 0f, oldCurrent, attribute.CurrentValue, new ModifierSource(sourceId)));
    }

    /// <summary>Applies a modifier to the base value once (instant or periodic effect), then runs <see cref="PostEffectExecute"/>.</summary>
    public void ExecuteModifier(AttributeValue attribute, ModifierOp op, float magnitude, in ModifierSource source)
    {
        float oldBase = attribute.BaseValue;
        SetBaseValue(attribute, Aggregator.EvaluateSingle(oldBase, op, magnitude));

        var data = new ModifierEventData(ModifierEventKind.Executed, attribute, op, magnitude, oldBase, attribute.BaseValue, source);
        PostEffectExecute(data);
        RaiseModifierApplied(data);
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

    /// <summary>After an executed modifier changed a base value, before <see cref="ModifierApplied"/>. Meta attributes (Damage → Health) go here.</summary>
    protected virtual void PostEffectExecute(in ModifierEventData data)
    {
    }

    private void Recalculate(AttributeValue attribute, float oldBase, float oldCurrent)
    {
        float current = attribute.BaseValue;
        if (_aggregators != null && _aggregators.TryGetValue(attribute, out Aggregator aggregator))
            current = aggregator.Evaluate(current);

        PreCurrentValueChange(attribute, ref current);
        attribute.SetCurrent(current);

        if (current != oldCurrent)
            PostCurrentValueChange(attribute, oldCurrent, current);

        attribute.RaiseChanged(oldBase, oldCurrent);
    }

    private void RaiseModifierApplied(in ModifierEventData data) => ModifierApplied?.Invoke(data);
}
