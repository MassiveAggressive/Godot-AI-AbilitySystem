using System;
using System.Collections.Generic;

namespace AbilitySystem;

/// <summary>
/// Collects the active modifiers of one attribute and calculates the result from a base value.
/// Channels (<see cref="ModifierEvaluationChannel"/>) are evaluated in ascending order; each channel's result is
/// the base of the next one. Per channel: the latest <see cref="ModifierOp.Override"/> if any, otherwise
/// ((Base + ΣAdd) × (1 + Σ(Multiply − 1)) ÷ (1 + Σ(Divide − 1)) × ΠMultiplyCompound) + ΣAddFinal.
/// The result is recalculated from scratch every time, so adding and removing never builds up float error.
/// </summary>
public sealed class Aggregator
{
    // Below this the divisor sum counts as zero and is ignored (treated as 1), as in Unreal.
    private const float MinDivisor = 1e-6f;

    // Sorted by channel, insertion order within a channel: evaluation is one pass and the last override of a
    // channel is the latest one.
    private readonly List<ModifierEntry> _modifiers = new(4);

    /// <summary><paramref name="sourceId"/> identifies the source (e.g. an active effect) for <see cref="RemoveModifiers"/>.</summary>
    public void AddModifier(ModifierOp op, float magnitude, int sourceId, ModifierEvaluationChannel channel = ModifierEvaluationChannel.Channel0)
    {
        int index = _modifiers.Count;
        while (index > 0 && _modifiers[index - 1].Channel > channel)
            index--;

        _modifiers.Insert(index, new ModifierEntry(op, magnitude, sourceId, channel));
    }

    /// <summary>Removes every modifier of <paramref name="sourceId"/> and keeps the order of the rest. False if there was none.</summary>
    public bool RemoveModifiers(int sourceId)
    {
        int kept = 0;
        for (int i = 0; i < _modifiers.Count; i++)
        {
            ModifierEntry entry = _modifiers[i];
            if (entry.SourceId != sourceId)
                _modifiers[kept++] = entry;
        }

        int removed = _modifiers.Count - kept;
        if (removed == 0)
            return false;

        _modifiers.RemoveRange(kept, removed);
        return true;
    }

    /// <summary>Result of a single modifier on <paramref name="baseValue"/>, the same as a channel holding only that modifier.</summary>
    public static float EvaluateSingle(float baseValue, ModifierOp op, float magnitude)
    {
        switch (op)
        {
            case ModifierOp.Add:
            case ModifierOp.AddFinal:
                return baseValue + magnitude;
            case ModifierOp.Multiply:
            case ModifierOp.MultiplyCompound:
                return baseValue * magnitude;
            case ModifierOp.Divide:
                return MathF.Abs(magnitude) < MinDivisor ? baseValue : baseValue / magnitude;
            case ModifierOp.Override:
                return magnitude;
            default:
                return baseValue;
        }
    }

    public float Evaluate(float baseValue)
    {
        float value = baseValue;
        int i = 0;
        while (i < _modifiers.Count)
        {
            ModifierEvaluationChannel channel = _modifiers[i].Channel;
            float add = 0f;
            float multiply = 1f;
            float divide = 1f;
            float compound = 1f;
            float addFinal = 0f;
            bool hasOverride = false;
            float overrideValue = 0f;

            for (; i < _modifiers.Count && _modifiers[i].Channel == channel; i++)
            {
                ModifierEntry entry = _modifiers[i];
                switch (entry.Op)
                {
                    case ModifierOp.Add:
                        add += entry.Magnitude;
                        break;
                    case ModifierOp.Multiply:
                        multiply += entry.Magnitude - 1f;
                        break;
                    case ModifierOp.Divide:
                        divide += entry.Magnitude - 1f;
                        break;
                    case ModifierOp.MultiplyCompound:
                        compound *= entry.Magnitude;
                        break;
                    case ModifierOp.AddFinal:
                        addFinal += entry.Magnitude;
                        break;
                    case ModifierOp.Override:
                        hasOverride = true;
                        overrideValue = entry.Magnitude;
                        break;
                }
            }

            if (hasOverride)
            {
                value = overrideValue;
                continue;
            }

            if (MathF.Abs(divide) < MinDivisor)
                divide = 1f;

            value = (value + add) * multiply / divide * compound + addFinal;
        }

        return value;
    }

    private readonly struct ModifierEntry
    {
        public readonly ModifierOp Op;
        public readonly float Magnitude;
        public readonly int SourceId;
        public readonly ModifierEvaluationChannel Channel;

        public ModifierEntry(ModifierOp op, float magnitude, int sourceId, ModifierEvaluationChannel channel)
        {
            Op = op;
            Magnitude = magnitude;
            SourceId = sourceId;
            Channel = channel;
        }
    }
}
