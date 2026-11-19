using System.Collections.Generic;
using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// One application of an <see cref="EffectData"/>: who applies it and the SetByCaller values.
/// Create, fill and pass to <see cref="ActiveEffectContainer.Apply"/>:
/// <c>effects.Apply(new EffectSpec(fireball, caster).SetCallerMagnitude(damageTag, 35f));</c>
/// Do not change a spec after applying it. One spec may be applied to several targets (area damage); each target
/// calculates the magnitudes when the effect is applied to it.
/// </summary>
public sealed class EffectSpec
{
    // Created on the first SetByCaller value; most specs have none.
    private List<CallerMagnitude> _callerMagnitudes;

    /// <param name="instigator">Responsible actor, e.g. the player who cast the spell.</param>
    /// <param name="causer">What directly caused it, e.g. the projectile.</param>
    /// <param name="instigatorAbilitySystem">The instigator's ability system component.</param>
    public EffectSpec(EffectData data, Node instigator = null, Node causer = null, Node instigatorAbilitySystem = null)
    {
        Data = data;
        Instigator = instigator;
        Causer = causer;
        InstigatorAbilitySystem = instigatorAbilitySystem;
    }

    public EffectData Data { get; }

    /// <summary>May be null or freed while the effect is active; check with <see cref="GodotObject.IsInstanceValid"/>.</summary>
    public Node Instigator { get; }

    /// <summary>May be null or freed while the effect is active; check with <see cref="GodotObject.IsInstanceValid"/>.</summary>
    public Node Causer { get; }

    /// <summary>May be null or freed while the effect is active; check with <see cref="GodotObject.IsInstanceValid"/>.</summary>
    public Node InstigatorAbilitySystem { get; }

    /// <summary>The applying actor's attributes, read by attribute based magnitudes with <see cref="AttributeCaptureSource.Source"/>.</summary>
    public AttributeContainer SourceAttributes { get; set; }

    /// <summary>The applying actor's tags, read by custom calculations (<see cref="CalculationContext.SourceTags"/>).</summary>
    public TagCountContainer SourceTags { get; set; }

    /// <summary>
    /// Scales Constant and AttributeBased magnitudes that have level values or a level curve; custom calculations
    /// read it from <see cref="CalculationContext.Level"/>. 1 by default; abilities pass their own level.
    /// </summary>
    public float Level { get; set; } = 1f;

    /// <summary>Where it hit (global), set with <see cref="SetHit(Vector2, Vector2)"/>; the effect's executed cues open there.</summary>
    public Vector3 HitPoint { get; private set; }

    /// <summary>The surface direction at <see cref="HitPoint"/>; zero if not known.</summary>
    public Vector3 HitNormal { get; private set; }

    public bool HasHit { get; private set; }

    public Vector2 HitPoint2D => new(HitPoint.X, HitPoint.Y);

    public Vector2 HitNormal2D => new(HitNormal.X, HitNormal.Y);

    /// <summary>
    /// Records where it hit (e.g. a projectile's contact), before <see cref="ActiveEffectContainer.Apply"/>. A spec
    /// applied to several targets has one hit; <c>Ability.ApplyEffectToTargets</c> gives each target its own.
    /// Returns this spec for chaining.
    /// </summary>
    public EffectSpec SetHit(Vector2 point, Vector2 normal = default) =>
        SetHit(new Vector3(point.X, point.Y, 0f), new Vector3(normal.X, normal.Y, 0f));

    public EffectSpec SetHit(Vector3 point, Vector3 normal = default)
    {
        HitPoint = point;
        HitNormal = normal;
        HasHit = true;
        return this;
    }

    /// <summary>A copy with another hit (and the same SetByCaller values), for one of several targets.</summary>
    internal EffectSpec CopyWithHit(Vector3 point, Vector3 normal)
    {
        var copy = new EffectSpec(Data, Instigator, Causer, InstigatorAbilitySystem)
        {
            SourceAttributes = SourceAttributes,
            SourceTags = SourceTags,
            Level = Level,
        };

        if (_callerMagnitudes != null)
            copy._callerMagnitudes = new List<CallerMagnitude>(_callerMagnitudes);

        return copy.SetHit(point, normal);
    }

    /// <summary>Sets the value of SetByCaller magnitudes keyed by <paramref name="tag"/>. Returns this spec for chaining.</summary>
    public EffectSpec SetCallerMagnitude(Tag tag, float value)
    {
        _callerMagnitudes ??= new List<CallerMagnitude>(2);
        for (int i = 0; i < _callerMagnitudes.Count; i++)
        {
            if (_callerMagnitudes[i].Tag == tag)
            {
                _callerMagnitudes[i] = new CallerMagnitude(tag, value);
                return this;
            }
        }

        _callerMagnitudes.Add(new CallerMagnitude(tag, value));
        return this;
    }

    public bool TryGetCallerMagnitude(Tag tag, out float value)
    {
        if (_callerMagnitudes != null)
        {
            for (int i = 0; i < _callerMagnitudes.Count; i++)
            {
                if (_callerMagnitudes[i].Tag == tag)
                {
                    value = _callerMagnitudes[i].Value;
                    return true;
                }
            }
        }

        value = 0f;
        return false;
    }

    /// <summary>
    /// The value of <paramref name="magnitude"/> with this spec against <paramref name="target"/>, e.g. to check a
    /// cost before activating. A missing attribute counts as 0. <paramref name="targetTags"/> is passed on to custom
    /// calculations.
    /// </summary>
    public float CalculateMagnitude(Magnitude magnitude, AttributeContainer target, TagCountContainer targetTags = null) =>
        CalculateMagnitude(magnitude, target, targetTags, 1);

    internal float CalculateMagnitude(Magnitude magnitude, AttributeContainer target, TagCountContainer targetTags, int stackCount)
    {
        if (magnitude == null)
        {
            GD.PushError($"EffectSpec: a magnitude is not set{Where()}; 0 is used.");
            return 0f;
        }

        switch (magnitude.Type)
        {
            case MagnitudeType.SetByCaller:
                if (TryGetCallerMagnitude(magnitude.CallerTag, out float value))
                    return value;

                GD.PushError($"EffectSpec: no SetByCaller value for '{magnitude.CallerTagName}'{Where()}; 0 is used.");
                return 0f;
            case MagnitudeType.AttributeBased:
                return CalculateAttributeBased(magnitude, target);
            case MagnitudeType.Custom:
                if (magnitude.Calculation is MagnitudeCalculation calculation)
                    return calculation.Calculate(new CalculationContext(this, target, targetTags, stackCount));

                GD.PushError($"EffectSpec: a custom magnitude has no MagnitudeCalculation{Where()}; 0 is used.");
                return 0f;
            default:
                return magnitude.ScaleByLevel(magnitude.Value, Level);
        }
    }

    private float CalculateAttributeBased(Magnitude magnitude, AttributeContainer target)
    {
        AttributeContainer container = magnitude.CaptureFrom == AttributeCaptureSource.Source ? SourceAttributes : target;
        if (container == null)
        {
            GD.PushError($"EffectSpec: '{magnitude.BackingAttributeName}' is read from the {magnitude.CaptureFrom}, but its attributes are not set{Where()}; 0 is used.");
            return 0f;
        }

        AttributeValue attribute = container.GetAttribute(magnitude.BackingAttribute);
        return magnitude.ScaleByLevel(magnitude.Coefficient, Level) * (attribute?.CurrentValue ?? 0f);
    }

    /// <summary>The attribute an attribute based, non-snapshot magnitude follows; null if there is none to follow.</summary>
    internal AttributeValue GetLiveAttribute(Magnitude magnitude, AttributeContainer target)
    {
        if (magnitude == null || magnitude.Type != MagnitudeType.AttributeBased || magnitude.Snapshot)
            return null;

        AttributeContainer container = magnitude.CaptureFrom == AttributeCaptureSource.Source ? SourceAttributes : target;
        return container?.GetAttribute(magnitude.BackingAttribute);
    }

    private string Where() => string.IsNullOrEmpty(Data?.ResourcePath) ? string.Empty : $" in '{Data.ResourcePath}'";

    private readonly struct CallerMagnitude
    {
        public readonly Tag Tag;
        public readonly float Value;

        public CallerMagnitude(Tag tag, float value)
        {
            Tag = tag;
            Value = value;
        }
    }
}
