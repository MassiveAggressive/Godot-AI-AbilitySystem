using Tags;

namespace AbilitySystem;

/// <summary>
/// What a <see cref="MagnitudeCalculation"/> or <see cref="ExecutionCalculation"/> works with: the spec (SetByCaller
/// values, instigator, causer), the attributes and tags of the source and the target, and the stack count.
/// Passed with <c>in</c>, so nothing is copied. The source fields and <see cref="TargetTags"/> may be null.
/// </summary>
public readonly struct CalculationContext
{
    public CalculationContext(EffectSpec spec, AttributeContainer targetAttributes, TagCountContainer targetTags = null, int stackCount = 1)
    {
        Spec = spec;
        TargetAttributes = targetAttributes;
        TargetTags = targetTags;
        StackCount = stackCount;
    }

    public EffectSpec Spec { get; }

    /// <summary>The applying actor's attributes, from <see cref="EffectSpec.SourceAttributes"/>.</summary>
    public AttributeContainer SourceAttributes => Spec?.SourceAttributes;

    /// <summary>The applying actor's tags, from <see cref="EffectSpec.SourceTags"/>.</summary>
    public TagCountContainer SourceTags => Spec?.SourceTags;

    /// <summary>The receiving actor's attributes.</summary>
    public AttributeContainer TargetAttributes { get; }

    /// <summary>The receiving actor's tags.</summary>
    public TagCountContainer TargetTags { get; }

    /// <summary>Stacks of the effect; 1 for instant and non-stacking effects.</summary>
    public int StackCount { get; }

    /// <summary>Current value of a source attribute; 0 if the source or the attribute is missing.</summary>
    public float GetSourceValue(AttributeId attribute) => GetValue(SourceAttributes, attribute);

    /// <summary>Current value of a target attribute; 0 if the target or the attribute is missing.</summary>
    public float GetTargetValue(AttributeId attribute) => GetValue(TargetAttributes, attribute);

    private static float GetValue(AttributeContainer attributes, AttributeId attribute) =>
        attributes?.GetAttribute(attribute)?.CurrentValue ?? 0f;
}
