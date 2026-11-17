using System;
using System.Collections.Generic;
using Godot;

namespace AbilitySystem;

/// <summary>
/// Attribute sets of one actor. Finds attributes by <see cref="AttributeId"/> in O(1) and routes id based changes
/// to the owning set so its rules run. Changes to attributes the actor does not have are ignored, so one effect
/// can target actors with different sets.
/// </summary>
public sealed class AttributeContainer
{
    private readonly AttributeValue[] _attributesById;
    private readonly AttributeSet[] _setsById;
    private readonly List<AttributeSet> _sets = new(2);

    // Bound once so subscribing each set allocates no new delegate.
    private readonly ModifierEventHandler _forwardModifierApplied;

    public AttributeContainer()
    {
        int count = AttributeRegistry.Count;
        _attributesById = new AttributeValue[count];
        _setsById = new AttributeSet[count];
        _forwardModifierApplied = ForwardModifierApplied;
    }

    /// <summary>Raised for every modifier executed, added or removed in any of the sets.</summary>
    public event ModifierEventHandler ModifierApplied;

    /// <summary>Adds a set; one set per type. A set instance belongs to one container only.</summary>
    public bool Add(AttributeSet set)
    {
        if (set == null)
            return false;

        Type setType = set.GetType();
        if (HasSetOfType(setType))
        {
            GD.PushError($"AttributeContainer already has a {setType.Name}.");
            return false;
        }

        if (!AttributeRegistry.TryGetSetInfo(setType, out AttributeRegistry.SetInfo info))
        {
            GD.PushError($"{setType.Name} is not a registered AttributeSet (abstract, generic or duplicate class name).");
            return false;
        }

        // Read everything first so a null property leaves the container unchanged.
        var attributes = new AttributeValue[info.Ids.Length];
        for (int i = 0; i < attributes.Length; i++)
        {
            attributes[i] = info.Properties[i].GetValue(set) as AttributeValue;
            if (attributes[i] == null)
            {
                GD.PushError($"{setType.Name}.{info.Properties[i].Name} is null; initialize it in its declaration.");
                return false;
            }
        }

        for (int i = 0; i < attributes.Length; i++)
        {
            int id = info.Ids[i].Value;
            _attributesById[id] = attributes[i];
            _setsById[id] = set;
        }

        _sets.Add(set);
        set.ModifierApplied += _forwardModifierApplied;
        return true;
    }

    /// <summary>Null if the actor has no set of type <typeparamref name="T"/>. Cache the result in frequently running code.</summary>
    public T Get<T>() where T : AttributeSet
    {
        for (int i = 0; i < _sets.Count; i++)
        {
            if (_sets[i] is T set)
                return set;
        }

        return null;
    }

    public bool Has(AttributeId id) => GetAttribute(id) != null;

    /// <summary>Null if the actor does not have this attribute.</summary>
    public AttributeValue GetAttribute(AttributeId id) => IsInRange(id) ? _attributesById[id.Value] : null;

    public void SetBaseValue(AttributeId id, float value)
    {
        AttributeSet owner = GetOwner(id);
        owner?.SetBaseValue(_attributesById[id.Value], value);
    }

    public void AddModifier(AttributeId id, ModifierOp op, float magnitude, in ModifierSource source, ModifierEvaluationChannel channel = ModifierEvaluationChannel.Channel0, int key = 0)
    {
        AttributeSet owner = GetOwner(id);
        owner?.AddModifier(_attributesById[id.Value], op, magnitude, source, channel, key);
    }

    public void UpdateModifier(AttributeId id, in ModifierSource source, int key, float magnitude)
    {
        AttributeSet owner = GetOwner(id);
        owner?.UpdateModifier(_attributesById[id.Value], source, key, magnitude);
    }

    public void ExecuteModifier(AttributeId id, ModifierOp op, float magnitude, in ModifierSource source)
    {
        AttributeSet owner = GetOwner(id);
        owner?.ExecuteModifier(_attributesById[id.Value], op, magnitude, source);
    }

    public void RemoveModifiers(AttributeId id, int sourceId)
    {
        AttributeSet owner = GetOwner(id);
        owner?.RemoveModifiers(_attributesById[id.Value], sourceId);
    }

    private void ForwardModifierApplied(in ModifierEventData data) => ModifierApplied?.Invoke(data);

    private AttributeSet GetOwner(AttributeId id) => IsInRange(id) ? _setsById[id.Value] : null;

    private bool IsInRange(AttributeId id) => (uint)id.Value < (uint)_setsById.Length;

    private bool HasSetOfType(Type setType)
    {
        for (int i = 0; i < _sets.Count; i++)
        {
            if (_sets[i].GetType() == setType)
                return true;
        }

        return false;
    }
}
