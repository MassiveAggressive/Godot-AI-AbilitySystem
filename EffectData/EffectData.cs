using System;
using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// Definition of an effect, edited in the inspector and applied through <see cref="EffectSpec"/>.
/// Instant effects change base values once; duration and infinite effects add their modifiers to current values
/// and grant tags while active. With a period, the modifiers are instead applied to the base value every period
/// (damage over time, regeneration). Duration and period are <see cref="Magnitude"/>s, so they can be constant,
/// set in code or based on an attribute. Instant and periodic effects may also run code (<see cref="Executions"/>).
/// Empty tag fields may be left unset.
/// Shared by everyone who loads the resource, so it is not changed at run time.
/// </summary>
[Tool]
[GlobalClass]
public partial class EffectData : Resource
{
    private EffectDurationType _durationType;
    private EffectStackingType _stackingType;
    private Magnitude _period;
    private Modifier[] _modifiers = Array.Empty<Modifier>();
    private ExecutionCalculation[] _executions = Array.Empty<ExecutionCalculation>();

    [ExportGroup("Duration")]
    [Export]
    public EffectDurationType DurationType
    {
        get => _durationType;
        set
        {
            _durationType = value;
            NotifyPropertyListChanged();
        }
    }

    /// <summary>Seconds; required when <see cref="DurationType"/> is HasDuration.</summary>
    [Export]
    public Magnitude Duration { get; set; }

    /// <summary>Seconds between executions; leave empty for none. Not used by instant effects.</summary>
    [Export]
    public Magnitude Period
    {
        get => _period;
        set
        {
            _period = value;
            NotifyPropertyListChanged();
        }
    }

    /// <summary>With a period: also execute once right when applied.</summary>
    [Export]
    public bool ExecuteOnApplication { get; set; } = true;

    /// <summary>How repeated applications combine. Not used by instant effects.</summary>
    [ExportGroup("Stacking")]
    [Export]
    public EffectStackingType StackingType
    {
        get => _stackingType;
        set
        {
            _stackingType = value;
            NotifyPropertyListChanged();
        }
    }

    /// <summary>Most stacks at once; 0 for no limit. Applications beyond it only refresh.</summary>
    [Export(PropertyHint.Range, "0,100,1,or_greater")]
    public int StackLimit { get; set; }

    /// <summary>A new application starts the duration again.</summary>
    [Export]
    public bool RefreshDurationOnStack { get; set; } = true;

    /// <summary>A new application starts the period again (Unreal's default). Off, the damage rhythm is left alone.</summary>
    [Export]
    public bool ResetPeriodOnStack { get; set; } = true;

    [Export]
    public StackExpirationPolicy StackExpiration { get; set; }

    [ExportGroup("Modifiers")]
    [Export]
    public Modifier[] Modifiers
    {
        get => _modifiers;
        set => _modifiers = value ?? Array.Empty<Modifier>();
    }

    /// <summary>
    /// Code run after the modifiers each time the effect executes; only instant and periodic effects execute.
    /// Pick [GlobalClass] subclasses of <see cref="ExecutionCalculation"/>.
    /// </summary>
    [Export]
    public ExecutionCalculation[] Executions
    {
        get => _executions;
        set => _executions = value ?? Array.Empty<ExecutionCalculation>();
    }

    /// <summary>What this effect is (e.g. "Effect.Burn"); matched by tag based removal and immunity.</summary>
    [ExportGroup("Tags")]
    [Export]
    public TagContainer AssetTags { get; set; }

    /// <summary>Given to the target while the effect is active. Not used by instant effects.</summary>
    [Export]
    public TagContainer GrantedTags { get; set; }

    /// <summary>The target must have all of these.</summary>
    [Export]
    public TagContainer ApplicationRequiredTags { get; set; }

    /// <summary>The effect is not applied if the target has any of these.</summary>
    [Export]
    public TagContainer ApplicationBlockedTags { get; set; }

    /// <summary>
    /// While active, the target must have all of these; otherwise the effect stays but is inhibited (no modifiers,
    /// granted tags, periods or immunity) until it has them again. Not used by instant effects.
    /// </summary>
    [Export]
    public TagContainer OngoingRequiredTags { get; set; }

    /// <summary>While the target has any of these, the effect is inhibited. Not used by instant effects.</summary>
    [Export]
    public TagContainer OngoingBlockedTags { get; set; }

    /// <summary>The effect is removed when the target gets any of these, and not applied if it has one. Not used by instant effects.</summary>
    [Export]
    public TagContainer RemovalTags { get; set; }

    /// <summary>When applied, active effects whose asset or granted tags match any of these are removed.</summary>
    [Export]
    public TagContainer RemoveEffectsWithTags { get; set; }

    /// <summary>
    /// While active, other effects whose asset tags match any of these are not applied. Active ones are left alone;
    /// add the tags to <see cref="RemoveEffectsWithTags"/> as well to clear them. Not used by instant effects.
    /// </summary>
    [Export]
    public TagContainer ImmunityTags { get; set; }

    public bool IsInstant => _durationType == EffectDurationType.Instant;

    /// <summary>True when applications stack (never for instant effects).</summary>
    public bool Stacks => !IsInstant && _stackingType != EffectStackingType.None;

    /// <summary>True when ongoing or removal tags are set (never for instant effects).</summary>
    internal bool HasTagConditions =>
        !IsInstant && (HasTags(OngoingRequiredTags) || HasTags(OngoingBlockedTags) || HasTags(RemovalTags));

    /// <summary>True when the effect grants immunity while active (never for instant effects).</summary>
    internal bool GrantsImmunity => !IsInstant && HasTags(ImmunityTags);

    private static bool HasTags(TagContainer tags) => tags != null && tags.Count > 0;

    // Godot API: the property info arrives as a Godot Dictionary.
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (!Engine.IsEditorHint())
            return;

        bool hasDuration = _durationType == EffectDurationType.HasDuration;
        bool visible = property["name"].AsString() switch
        {
            nameof(Duration) => hasDuration,
            nameof(Period) or nameof(GrantedTags) or nameof(StackingType) => !IsInstant,
            nameof(OngoingRequiredTags) or nameof(OngoingBlockedTags) or nameof(RemovalTags) or nameof(ImmunityTags) => !IsInstant,
            nameof(ExecuteOnApplication) => !IsInstant && _period != null,
            nameof(Executions) => IsInstant || _period != null,
            nameof(StackLimit) => Stacks,
            nameof(RefreshDurationOnStack) or nameof(StackExpiration) => Stacks && hasDuration,
            nameof(ResetPeriodOnStack) => Stacks && _period != null,
            _ => true,
        };

        // Hidden fields keep the storage flag so their value is still saved.
        if (!visible)
            property["usage"] = (int)PropertyUsageFlags.Storage;
    }
}
