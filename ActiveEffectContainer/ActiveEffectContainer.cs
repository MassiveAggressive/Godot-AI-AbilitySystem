using System;
using System.Collections.Generic;
using Godot;
using Tags;
using Timers;

namespace AbilitySystem;

/// <summary>
/// Effects applied to one actor. Applies instant effects to base values, keeps duration and infinite effects
/// (modifiers, granted tags, timers) and undoes them when they expire or are removed. Active effects may follow the
/// actor's tags (inhibited or removed as tags come and go) and may make the actor immune to other effects.
/// Time comes from <see cref="TimerManager"/>, so effects stop while the game is paused. Listeners may apply or
/// remove effects. For attribute level details (old/new value, magnitude, source) listen to
/// <see cref="AttributeContainer.ModifierApplied"/>.
/// </summary>
public sealed class ActiveEffectContainer
{
    // Below this a period tick and the end of the duration count as the same moment (float inputs: 3 × 0.1 ≠ 0.3).
    private const float SameMomentTolerance = 1e-4f;

    // Instant effects with up to this many modifiers calculate their magnitudes on the stack.
    private const int StackModifierLimit = 16;

    // Tag changes made while tag conditions are checked only ask for another pass. Effects still changing after this
    // many passes keep switching each other (or themselves) with their granted tags, and the check stops.
    private const int MaxConditionPasses = 8;

    private readonly AttributeContainer _attributes;
    private readonly TagCountContainer _tags;
    private readonly List<ActiveEffect> _effects = new(8);

    // Bound once, so subscribing again allocates no new delegate.
    private readonly Action<Tag, bool> _onTagPresenceChanged;

    // Effects with ongoing or removal tags, checked whenever a tag comes or goes; null until the first one.
    private List<ActiveEffect> _conditionalEffects;
    private List<ActiveEffect> _conditionScratch;
    private bool _listeningToTags;
    private bool _checkingConditions;
    private bool _conditionsDirty;

    // Applying skips the immunity check while no active effect grants immunity.
    private int _immunityEffectCount;

    // One output per nesting level: a listener reacting to an execution's output may run another execution.
    private List<ExecutionOutput> _executionOutputs;
    private int _executionDepth;

    /// <param name="attributes">The actor's attributes; modifiers change them and target based magnitudes read them.</param>
    /// <param name="tags">The actor's live tags; checked by tag requirements and given granted tags.</param>
    public ActiveEffectContainer(AttributeContainer attributes, TagCountContainer tags)
    {
        _attributes = attributes;
        _tags = tags;
        _onTagPresenceChanged = OnTagPresenceChanged;
    }

    /// <summary>
    /// A duration or infinite effect became active (its modifiers and tags are in place, unless it starts
    /// <see cref="ActiveEffect.IsInhibited"/>).
    /// </summary>
    public event Action<ActiveEffect> EffectAdded;

    /// <summary>An effect expired or was removed (its modifiers and tags are gone).</summary>
    public event Action<ActiveEffect> EffectRemoved;

    /// <summary>An instant effect was applied, or a periodic effect executed one period.</summary>
    public event Action<EffectSpec> EffectExecuted;

    /// <summary>(effect, old count, new count) after the stack grew or shrank and its modifiers were updated.</summary>
    public event Action<ActiveEffect, int, int> StackCountChanged;

    /// <summary>
    /// (effect, inhibited) after an active effect lost or regained its ongoing tag requirements and its modifiers and
    /// tags were taken back or given again. An effect applied while inhibited raises only <see cref="EffectAdded"/>.
    /// </summary>
    public event Action<ActiveEffect, bool> InhibitionChanged;

    /// <summary>(refused spec, active effect granting the immunity) when immunity stopped an application.</summary>
    public event Action<EffectSpec, ActiveEffect> ImmunityBlocked;

    public int Count => _effects.Count;

    public ActiveEffect this[int index] => _effects[index];

    /// <summary>Null if the effect is not active here.</summary>
    public ActiveEffect Get(EffectHandle handle)
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            if (_effects[i].Handle == handle)
                return _effects[i];
        }

        return null;
    }

    /// <summary>
    /// Applies the effect. Returns None if it was not applied (application tags, removal tags already present,
    /// immunity, duration not above zero). Instant effects return a valid handle that is never active. Magnitudes are
    /// calculated here, against this actor. A stacking effect that is already active gains a stack and returns the
    /// existing handle; the stack keeps its own magnitudes and duration, the new spec only adds to the count.
    /// </summary>
    public EffectHandle Apply(EffectSpec spec)
    {
        if (spec?.Data == null)
        {
            GD.PushError("ActiveEffectContainer.Apply: spec or its data is null.");
            return EffectHandle.None;
        }

        EffectData data = spec.Data;
        if (!MeetsRequirements(data))
            return EffectHandle.None;

        ActiveEffect immunity = FindImmunity(data);
        if (immunity != null)
        {
            ImmunityBlocked?.Invoke(spec, immunity);
            return EffectHandle.None;
        }

        float duration = data.DurationType switch
        {
            EffectDurationType.HasDuration => spec.CalculateMagnitude(data.Duration, _attributes, _tags, 1),
            EffectDurationType.Infinite => float.PositiveInfinity,
            _ => 0f,
        };

        if (data.DurationType == EffectDurationType.HasDuration && !(duration > 0f))
            return EffectHandle.None;

        // A period that comes out at zero or below means "not periodic" rather than a timer firing every frame.
        float period = data.IsInstant || data.Period == null ? 0f : Math.Max(0f, spec.CalculateMagnitude(data.Period, _attributes, _tags, 1));

        RemoveEffectsWithTags(data.RemoveEffectsWithTags);

        ActiveEffect stack = data.Stacks ? FindStack(spec) : null;
        if (stack != null)
        {
            AddStack(stack);
            return stack.Handle;
        }

        EffectHandle handle = EffectHandle.Next();
        var source = new ModifierSource(handle.Value, data, spec.Instigator, spec.Causer, spec.InstigatorAbilitySystem);

        if (data.IsInstant)
            ExecuteInstant(spec, source);
        else
            AddActive(spec, handle, source, duration, period);

        return handle;
    }

    /// <summary>Removes <paramref name="stacks"/> stacks (all by default); the effect ends with its last stack.</summary>
    public bool RemoveEffect(EffectHandle handle, int stacks = int.MaxValue)
    {
        ActiveEffect effect = Get(handle);
        if (effect == null || stacks <= 0)
            return false;

        RemoveStacks(effect, stacks);
        return true;
    }

    /// <summary>Removes active effects whose asset or granted tags match any of <paramref name="tags"/> (hierarchically). Returns how many.</summary>
    public int RemoveEffectsWithTags(TagContainer tags)
    {
        if (tags == null || tags.Count == 0)
            return 0;

        // Effects applied by listeners during this call are left alone, so a listener re-applying cannot loop forever.
        int lastHandle = EffectHandle.LastValue;
        int removed = 0;
        int i = 0;
        while (i < _effects.Count)
        {
            ActiveEffect effect = _effects[i];
            if (effect.Handle.Value > lastHandle || !HasAnyTag(effect.Data, tags))
            {
                i++;
                continue;
            }

            Remove(effect);
            removed++;
            // Listeners may have changed the list; start over (lists are short).
            i = 0;
        }

        return removed;
    }

    /// <summary>
    /// Removes every active effect, e.g. when the actor is freed, so no timer keeps running for it. Effects applied
    /// by listeners during this call stay. Returns how many were removed.
    /// </summary>
    public int RemoveAll()
    {
        int lastHandle = EffectHandle.LastValue;
        int removed = 0;
        int i = 0;
        while (i < _effects.Count)
        {
            ActiveEffect effect = _effects[i];
            if (effect.Handle.Value > lastHandle)
            {
                i++;
                continue;
            }

            Remove(effect);
            removed++;
            i = 0;
        }

        return removed;
    }

    /// <summary>
    /// Longest remaining time among the active, uninhibited effects that grant any of <paramref name="grantedTags"/>
    /// (hierarchically); 0 if there is none. Used for cooldowns and buff timers.
    /// </summary>
    public float GetRemainingTime(TagContainer grantedTags)
    {
        if (grantedTags == null || grantedTags.Count == 0)
            return 0f;

        float longest = 0f;
        for (int i = 0; i < _effects.Count; i++)
        {
            ActiveEffect effect = _effects[i];
            TagContainer granted = effect.Data.GrantedTags;
            if (effect.IsInhibited || granted == null || !granted.HasAny(grantedTags))
                continue;

            longest = Math.Max(longest, effect.RemainingTime);
        }

        return longest;
    }

    private bool MeetsRequirements(EffectData data)
    {
        if (data.ApplicationRequiredTags != null && !_tags.HasAll(data.ApplicationRequiredTags))
            return false;

        if (data.ApplicationBlockedTags != null && _tags.HasAny(data.ApplicationBlockedTags))
            return false;

        // It would be removed right away.
        return data.IsInstant || data.RemovalTags == null || !_tags.HasAny(data.RemovalTags);
    }

    private bool MeetsOngoingRequirements(EffectData data)
    {
        if (data.OngoingRequiredTags != null && !_tags.HasAll(data.OngoingRequiredTags))
            return false;

        return data.OngoingBlockedTags == null || !_tags.HasAny(data.OngoingBlockedTags);
    }

    /// <summary>The active effect whose immunity refuses <paramref name="data"/>; null if none.</summary>
    private ActiveEffect FindImmunity(EffectData data)
    {
        if (_immunityEffectCount == 0 || data.AssetTags == null || data.AssetTags.Count == 0)
            return null;

        for (int i = 0; i < _effects.Count; i++)
        {
            ActiveEffect effect = _effects[i];

            // An effect does not refuse itself, so its own stacks and refreshes still apply.
            if (effect.IsInhibited || effect.Data == data || !effect.Data.GrantsImmunity)
                continue;

            if (data.AssetTags.HasAny(effect.Data.ImmunityTags))
                return effect;
        }

        return null;
    }

    private static bool HasAnyTag(EffectData data, TagContainer tags)
    {
        return (data.AssetTags != null && data.AssetTags.HasAny(tags))
            || (data.GrantedTags != null && data.GrantedTags.HasAny(tags));
    }

    private void ExecuteInstant(EffectSpec spec, in ModifierSource source)
    {
        Modifier[] modifiers = spec.Data.Modifiers;

        // Snapshot magnitudes are all taken before any modifier runs; the others are read right before their modifier.
        Span<float> magnitudes = modifiers.Length <= StackModifierLimit ? stackalloc float[modifiers.Length] : new float[modifiers.Length];
        for (int i = 0; i < modifiers.Length; i++)
        {
            if (modifiers[i] != null && IsSnapshot(modifiers[i].Magnitude))
                magnitudes[i] = spec.CalculateMagnitude(modifiers[i].Magnitude, _attributes, _tags, 1);
        }

        for (int i = 0; i < modifiers.Length; i++)
        {
            Modifier modifier = modifiers[i];
            if (modifier == null)
                continue;

            float magnitude = IsSnapshot(modifier.Magnitude) ? magnitudes[i] : spec.CalculateMagnitude(modifier.Magnitude, _attributes, _tags, 1);
            _attributes.ExecuteModifier(modifier.Attribute, modifier.Op, magnitude, source);
        }

        RunExecutions(spec, source, 1);
        EffectExecuted?.Invoke(spec);
    }

    private void AddActive(EffectSpec spec, EffectHandle handle, in ModifierSource source, float duration, float period)
    {
        EffectData data = spec.Data;
        Modifier[] modifiers = data.Modifiers;
        var magnitudes = new float[modifiers.Length];
        for (int i = 0; i < modifiers.Length; i++)
        {
            if (modifiers[i] != null)
                magnitudes[i] = spec.CalculateMagnitude(modifiers[i].Magnitude, _attributes, _tags, 1);
        }

        var effect = new ActiveEffect(handle, spec, source, magnitudes, duration, period);
        _effects.Add(effect);
        if (data.GrantsImmunity)
            _immunityEffectCount++;

        // Unmet ongoing requirements do not refuse the effect; it waits inhibited until they are met (as in Unreal).
        bool conditional = data.HasTagConditions;
        effect.IsInhibited = conditional && !MeetsOngoingRequirements(data);

        // A listener reacting to a modifier (e.g. death) may remove this effect; then the remaining steps are skipped.
        if (!effect.IsInhibited)
            AddModifiers(effect);

        if (!effect.IsPeriodic)
            BindLiveModifiers(effect);

        if (!effect.IsInhibited)
            GrantTags(effect);

        if (effect.IsRemoved)
            return;

        // Followed only from here, so a tag change during the setup above cannot inhibit a half applied effect; the
        // check catches up with whatever changed meanwhile, the effect's own granted tags included.
        if (conditional)
        {
            TrackConditions(effect);
            CheckConditions(effect);
            if (effect.IsRemoved)
                return;
        }

        effect.ExpireCallback = () => Expire(effect);
        effect.PeriodCallback = () => ExecutePeriod(effect);

        // The period timer starts first so that a period ending together with the duration still runs.
        if (effect.IsPeriodic)
            RestartPeriod(effect);

        if (data.DurationType == EffectDurationType.HasDuration)
            RestartDuration(effect);

        EffectAdded?.Invoke(effect);

        if (effect.IsPeriodic && data.ExecuteOnApplication)
            ExecutePeriod(effect);
    }

    private void ExecutePeriod(ActiveEffect effect)
    {
        if (effect.IsRemoved || effect.IsInhibited)
            return;

        Modifier[] modifiers = effect.Data.Modifiers;
        for (int i = 0; i < modifiers.Length; i++)
        {
            Modifier modifier = modifiers[i];
            if (modifier == null)
                continue;

            if (!IsSnapshot(modifier.Magnitude))
                effect.Magnitudes[i] = effect.Spec.CalculateMagnitude(modifier.Magnitude, _attributes, _tags, effect.StackCount);

            _attributes.ExecuteModifier(modifier.Attribute, modifier.Op, StackedMagnitude(effect, i), effect.Source);
        }

        RunExecutions(effect.Spec, effect.Source, effect.StackCount);
        EffectExecuted?.Invoke(effect.Spec);
    }

    /// <summary>Runs the executions in order; each one's output is applied to base values before the next runs.</summary>
    private void RunExecutions(EffectSpec spec, in ModifierSource source, int stackCount)
    {
        Resource[] executions = spec.Data.Executions;
        if (executions.Length == 0)
            return;

        _executionOutputs ??= new List<ExecutionOutput>(1);
        if (_executionDepth == _executionOutputs.Count)
            _executionOutputs.Add(new ExecutionOutput());

        ExecutionOutput output = _executionOutputs[_executionDepth++];
        try
        {
            var context = new CalculationContext(spec, _attributes, _tags, stackCount);
            for (int i = 0; i < executions.Length; i++)
            {
                if (executions[i] is not ExecutionCalculation execution)
                    continue;

                output.Clear();
                execution.Execute(context, output);
                for (int j = 0; j < output.Count; j++)
                {
                    ExecutionOutput.Entry entry = output[j];
                    _attributes.ExecuteModifier(entry.Attribute, entry.Op, entry.Magnitude, source);
                }
            }
        }
        finally
        {
            output.Clear();
            _executionDepth--;
        }
    }

    private ActiveEffect FindStack(EffectSpec spec)
    {
        bool bySource = spec.Data.StackingType == EffectStackingType.BySource;
        for (int i = 0; i < _effects.Count; i++)
        {
            ActiveEffect effect = _effects[i];
            if (effect.Data == spec.Data && (!bySource || effect.Spec.Instigator == spec.Instigator))
                return effect;
        }

        return null;
    }

    private void AddStack(ActiveEffect effect)
    {
        EffectData data = effect.Data;
        int oldCount = effect.StackCount;
        bool grows = data.StackLimit <= 0 || oldCount < data.StackLimit;
        if (grows)
        {
            effect.StackCount = oldCount + 1;
            UpdateStackedModifiers(effect);
            if (effect.IsRemoved)
                return;
        }

        // At the limit an application still refreshes, as in Unreal.
        if (data.DurationType == EffectDurationType.HasDuration && data.RefreshDurationOnStack)
            RestartDuration(effect);

        if (effect.IsPeriodic && data.ResetPeriodOnStack)
            RestartPeriod(effect);

        if (grows)
            StackCountChanged?.Invoke(effect, oldCount, effect.StackCount);
    }

    private void RemoveStacks(ActiveEffect effect, int stacks)
    {
        if (stacks >= effect.StackCount)
        {
            Remove(effect);
            return;
        }

        int oldCount = effect.StackCount;
        effect.StackCount = oldCount - stacks;
        UpdateStackedModifiers(effect);
        if (!effect.IsRemoved)
            StackCountChanged?.Invoke(effect, oldCount, effect.StackCount);
    }

    /// <summary>
    /// Periodic effects use the stack count when they execute, inhibited ones when their modifiers are added again;
    /// the others update their added modifiers.
    /// </summary>
    private void UpdateStackedModifiers(ActiveEffect effect)
    {
        if (effect.IsPeriodic || effect.IsInhibited)
            return;

        Modifier[] modifiers = effect.Data.Modifiers;
        for (int i = 0; i < modifiers.Length && !effect.IsRemoved; i++)
        {
            if (modifiers[i] != null)
                _attributes.UpdateModifier(modifiers[i].Attribute, effect.Source, i, StackedMagnitude(effect, i));
        }
    }

    private static float StackedMagnitude(ActiveEffect effect, int index) =>
        Aggregator.StackMagnitude(effect.Data.Modifiers[index].Op, effect.Magnitudes[index], effect.StackCount);

    private static void RestartDuration(ActiveEffect effect)
    {
        TimerManager.Stop(effect.DurationTimer);
        effect.DurationTimer = TimerManager.Start(effect.Duration, effect.ExpireCallback);
    }

    private static void RestartPeriod(ActiveEffect effect)
    {
        TimerManager.Stop(effect.PeriodTimer);
        effect.PeriodTimer = TimerManager.Start(effect.Period, effect.PeriodCallback, effect.Period);
    }

    private static bool IsSnapshot(Magnitude magnitude) =>
        magnitude == null || magnitude.Snapshot || magnitude.Type is MagnitudeType.Constant or MagnitudeType.SetByCaller;

    /// <summary>Adds a non-periodic effect's modifiers to current values with the stacked magnitudes.</summary>
    private void AddModifiers(ActiveEffect effect)
    {
        if (effect.IsPeriodic)
            return;

        Modifier[] modifiers = effect.Data.Modifiers;
        for (int i = 0; i < modifiers.Length && !effect.IsRemoved; i++)
        {
            Modifier modifier = modifiers[i];
            if (modifier != null)
                _attributes.AddModifier(modifier.Attribute, modifier.Op, StackedMagnitude(effect, i), effect.Source, modifier.Channel, key: i);
        }
    }

    private void RemoveModifiers(ActiveEffect effect)
    {
        if (effect.IsPeriodic)
            return;

        Modifier[] modifiers = effect.Data.Modifiers;
        for (int i = 0; i < modifiers.Length; i++)
        {
            if (modifiers[i] != null)
                _attributes.RemoveModifiers(modifiers[i].Attribute, effect.Handle.Value);
        }
    }

    private void GrantTags(ActiveEffect effect)
    {
        TagContainer granted = effect.Data.GrantedTags;
        for (int i = 0; granted != null && i < granted.Count && !effect.IsRemoved; i++)
        {
            // Counted before adding: a listener of this very tag may remove the effect, and must take this tag back too.
            effect.GrantedTagCount = i + 1;
            _tags.Add(granted[i]);
        }
    }

    /// <summary>Takes back exactly the granted tags that were given.</summary>
    private void UngrantTags(ActiveEffect effect)
    {
        int count = effect.GrantedTagCount;
        effect.GrantedTagCount = 0;
        for (int i = 0; i < count; i++)
            _tags.Remove(effect.Data.GrantedTags[i]);
    }

    /// <summary>Non-snapshot modifiers of a duration or infinite effect follow their attribute until the effect ends.</summary>
    private void BindLiveModifiers(ActiveEffect effect)
    {
        Modifier[] modifiers = effect.Data.Modifiers;
        for (int i = 0; i < modifiers.Length && !effect.IsRemoved; i++)
        {
            Modifier modifier = modifiers[i];
            AttributeValue followed = modifier == null ? null : effect.Spec.GetLiveAttribute(modifier.Magnitude, _attributes);
            if (followed == null)
                continue;

            // An attribute raised by its own value would grow on every update; it keeps the applied value instead.
            if (followed == _attributes.GetAttribute(modifier.Attribute))
            {
                GD.PushWarning($"Effect '{effect.Data.ResourcePath}': '{modifier.AttributeName}' is based on itself; it is not followed (snapshot).");
                continue;
            }

            int index = i;
            Action<AttributeValue, float, float> handler = (_, _, _) => RefreshModifier(effect, index);
            followed.CurrentValueChanged += handler;
            effect.LiveBindings ??= new List<ActiveEffect.LiveBinding>(1);
            effect.LiveBindings.Add(new ActiveEffect.LiveBinding(followed, handler));
        }
    }

    private void RefreshModifier(ActiveEffect effect, int index)
    {
        if (effect.IsRemoved || effect.IsRefreshing)
            return;

        Modifier modifier = effect.Data.Modifiers[index];
        float magnitude = effect.Spec.CalculateMagnitude(modifier.Magnitude, _attributes, _tags, effect.StackCount);
        if (magnitude == effect.Magnitudes[index])
            return;

        effect.Magnitudes[index] = magnitude;

        // An inhibited effect keeps the value for when its modifiers are added again.
        if (effect.IsInhibited)
            return;

        effect.IsRefreshing = true;
        try
        {
            _attributes.UpdateModifier(modifier.Attribute, effect.Source, index, StackedMagnitude(effect, index));
        }
        finally
        {
            effect.IsRefreshing = false;
        }
    }

    private void TrackConditions(ActiveEffect effect)
    {
        _conditionalEffects ??= new List<ActiveEffect>(4);
        _conditionalEffects.Add(effect);
        if (_listeningToTags)
            return;

        _tags.PresenceChanged += _onTagPresenceChanged;
        _listeningToTags = true;
    }

    private void UntrackConditions(ActiveEffect effect)
    {
        if (_conditionalEffects == null || !_conditionalEffects.Remove(effect) || _conditionalEffects.Count > 0)
            return;

        // Unsubscribed with the last one, so tag changes cost nothing while no effect depends on them.
        _tags.PresenceChanged -= _onTagPresenceChanged;
        _listeningToTags = false;
    }

    private void OnTagPresenceChanged(Tag tag, bool present) => CheckConditions(null);

    /// <summary>
    /// Brings inhibition and removal up to date with the tags, for <paramref name="first"/> or, when null, every
    /// conditional effect. Changes made meanwhile (inhibiting takes tags back) only ask for another pass, so no effect
    /// is checked while it is half inhibited.
    /// </summary>
    private void CheckConditions(ActiveEffect first)
    {
        if (_checkingConditions)
        {
            _conditionsDirty = true;
            return;
        }

        _checkingConditions = true;
        try
        {
            _conditionsDirty = first == null;
            if (first != null)
                UpdateConditions(first);

            for (int pass = 0; _conditionsDirty; pass++)
            {
                if (pass == MaxConditionPasses)
                {
                    GD.PushWarning("ActiveEffectContainer: ongoing tag requirements keep changing; an effect may inhibit itself with its own granted tags. Checking stopped.");
                    break;
                }

                _conditionsDirty = false;

                // Walked over a copy: removals and listeners change the list meanwhile.
                _conditionScratch ??= new List<ActiveEffect>(_conditionalEffects.Count);
                _conditionScratch.AddRange(_conditionalEffects);
                for (int i = 0; i < _conditionScratch.Count; i++)
                    UpdateConditions(_conditionScratch[i]);

                _conditionScratch.Clear();
            }
        }
        finally
        {
            _checkingConditions = false;
            _conditionScratch?.Clear();
        }
    }

    private void UpdateConditions(ActiveEffect effect)
    {
        if (effect.IsRemoved)
            return;

        EffectData data = effect.Data;
        if (data.RemovalTags != null && _tags.HasAny(data.RemovalTags))
        {
            Remove(effect);
            return;
        }

        bool inhibited = !MeetsOngoingRequirements(data);
        if (inhibited == effect.IsInhibited)
            return;

        if (inhibited)
            Inhibit(effect);
        else
            Uninhibit(effect);
    }

    private void Inhibit(ActiveEffect effect)
    {
        effect.IsInhibited = true;
        RemoveModifiers(effect);
        if (effect.IsRemoved)
            return;

        UngrantTags(effect);
        if (!effect.IsRemoved)
            InhibitionChanged?.Invoke(effect, true);
    }

    /// <summary>Gives the modifiers (with the current stacks and followed values) and tags back; the period keeps its rhythm.</summary>
    private void Uninhibit(ActiveEffect effect)
    {
        effect.IsInhibited = false;
        AddModifiers(effect);
        if (effect.IsRemoved)
            return;

        GrantTags(effect);
        if (!effect.IsRemoved)
            InhibitionChanged?.Invoke(effect, false);
    }

    private void Expire(ActiveEffect effect)
    {
        if (effect.IsRemoved)
            return;

        if (effect.IsPeriodic
            && TimerManager.IsActive(effect.PeriodTimer)
            && TimerManager.GetRemaining(effect.PeriodTimer) <= SameMomentTolerance)
        {
            ExecutePeriod(effect);
        }

        if (effect.IsRemoved)
            return;

        StackExpirationPolicy policy = effect.Data.Stacks ? effect.Data.StackExpiration : StackExpirationPolicy.ClearStack;
        switch (policy)
        {
            case StackExpirationPolicy.RemoveOneAndRefresh:
                RemoveStacks(effect, 1);
                if (!effect.IsRemoved)
                    RestartDuration(effect);
                break;
            case StackExpirationPolicy.RefreshDuration:
                RestartDuration(effect);
                break;
            default:
                Remove(effect);
                break;
        }
    }

    private void Remove(ActiveEffect effect)
    {
        if (effect.IsRemoved)
            return;

        effect.IsRemoved = true;
        TimerManager.Stop(effect.PeriodTimer);
        TimerManager.Stop(effect.DurationTimer);

        EffectData data = effect.Data;
        if (data.HasTagConditions)
            UntrackConditions(effect);

        if (data.GrantsImmunity)
            _immunityEffectCount--;

        if (effect.LiveBindings != null)
        {
            foreach (ActiveEffect.LiveBinding binding in effect.LiveBindings)
                binding.Attribute.CurrentValueChanged -= binding.Handler;
        }

        // Nothing to take back from an inhibited effect; both calls then do nothing.
        RemoveModifiers(effect);
        UngrantTags(effect);

        _effects.Remove(effect);
        EffectRemoved?.Invoke(effect);
    }
}
