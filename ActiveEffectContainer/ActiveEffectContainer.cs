using System;
using System.Collections.Generic;
using Godot;
using Tags;
using Timers;

namespace AbilitySystem;

/// <summary>
/// Effects applied to one actor. Applies instant effects to base values, keeps duration and infinite effects
/// (modifiers, granted tags, timers) and undoes them when they expire or are removed. Time comes from
/// <see cref="TimerManager"/>, so effects stop while the game is paused. Listeners may apply or remove effects.
/// For attribute level details (old/new value, magnitude, source) listen to <see cref="AttributeContainer.ModifierApplied"/>.
/// </summary>
public sealed class ActiveEffectContainer
{
    // Below this a period tick and the end of the duration count as the same moment (float inputs: 3 × 0.1 ≠ 0.3).
    private const float SameMomentTolerance = 1e-4f;

    // Instant effects with up to this many modifiers calculate their magnitudes on the stack.
    private const int StackModifierLimit = 16;

    private readonly AttributeContainer _attributes;
    private readonly TagCountContainer _tags;
    private readonly List<ActiveEffect> _effects = new(8);

    /// <param name="attributes">The actor's attributes; modifiers change them and target based magnitudes read them.</param>
    /// <param name="tags">The actor's live tags; checked by tag requirements and given granted tags.</param>
    public ActiveEffectContainer(AttributeContainer attributes, TagCountContainer tags)
    {
        _attributes = attributes;
        _tags = tags;
    }

    /// <summary>A duration or infinite effect became active (its modifiers and tags are in place).</summary>
    public event Action<ActiveEffect> EffectAdded;

    /// <summary>An effect expired or was removed (its modifiers and tags are gone).</summary>
    public event Action<ActiveEffect> EffectRemoved;

    /// <summary>An instant effect was applied, or a periodic effect executed one period.</summary>
    public event Action<EffectSpec> EffectExecuted;

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
    /// Applies the effect. Returns None if it was not applied (tag requirements, duration not above zero).
    /// Instant effects return a valid handle that is never active. Magnitudes are calculated here, against this actor.
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

        float duration = data.DurationType switch
        {
            EffectDurationType.HasDuration => spec.CalculateMagnitude(data.Duration, _attributes),
            EffectDurationType.Infinite => float.PositiveInfinity,
            _ => 0f,
        };

        if (data.DurationType == EffectDurationType.HasDuration && !(duration > 0f))
            return EffectHandle.None;

        // A period that comes out at zero or below means "not periodic" rather than a timer firing every frame.
        float period = data.IsInstant || data.Period == null ? 0f : Math.Max(0f, spec.CalculateMagnitude(data.Period, _attributes));

        RemoveEffectsWithTags(data.RemoveEffectsWithTags);

        EffectHandle handle = EffectHandle.Next();
        var source = new ModifierSource(handle.Value, data, spec.Instigator, spec.Causer, spec.InstigatorAbilitySystem);

        if (data.IsInstant)
            ExecuteInstant(spec, source);
        else
            AddActive(spec, handle, source, duration, period);

        return handle;
    }

    public bool RemoveEffect(EffectHandle handle)
    {
        ActiveEffect effect = Get(handle);
        if (effect == null)
            return false;

        Remove(effect);
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

    private bool MeetsRequirements(EffectData data)
    {
        if (data.ApplicationRequiredTags != null && !_tags.HasAll(data.ApplicationRequiredTags))
            return false;

        return data.ApplicationBlockedTags == null || !_tags.HasAny(data.ApplicationBlockedTags);
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
                magnitudes[i] = spec.CalculateMagnitude(modifiers[i].Magnitude, _attributes);
        }

        for (int i = 0; i < modifiers.Length; i++)
        {
            Modifier modifier = modifiers[i];
            if (modifier == null)
                continue;

            float magnitude = IsSnapshot(modifier.Magnitude) ? magnitudes[i] : spec.CalculateMagnitude(modifier.Magnitude, _attributes);
            _attributes.ExecuteModifier(modifier.Attribute, modifier.Op, magnitude, source);
        }

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
                magnitudes[i] = spec.CalculateMagnitude(modifiers[i].Magnitude, _attributes);
        }

        var effect = new ActiveEffect(handle, spec, source, magnitudes, duration, period);
        _effects.Add(effect);

        // A listener reacting to a modifier (e.g. death) may remove this effect; then the remaining steps are skipped.
        if (!effect.IsPeriodic)
        {
            for (int i = 0; i < modifiers.Length && !effect.IsRemoved; i++)
            {
                Modifier modifier = modifiers[i];
                if (modifier != null)
                    _attributes.AddModifier(modifier.Attribute, modifier.Op, magnitudes[i], source, modifier.Channel, key: i);
            }

            BindLiveModifiers(effect);
        }

        if (effect.IsRemoved)
            return;

        TagContainer granted = data.GrantedTags;
        for (int i = 0; granted != null && i < granted.Count; i++)
        {
            // Counted before adding: a listener of this very tag may remove the effect, and must take this tag back too.
            effect.GrantedTagCount = i + 1;
            _tags.Add(granted[i]);
            if (effect.IsRemoved)
                return;
        }

        // The period timer starts first so that a period ending together with the duration still runs.
        if (effect.IsPeriodic)
            effect.PeriodTimer = TimerManager.Start(period, () => ExecutePeriod(effect), period);

        if (data.DurationType == EffectDurationType.HasDuration)
            effect.DurationTimer = TimerManager.Start(duration, () => Expire(effect));

        EffectAdded?.Invoke(effect);

        if (effect.IsPeriodic && data.ExecuteOnApplication)
            ExecutePeriod(effect);
    }

    private void ExecutePeriod(ActiveEffect effect)
    {
        if (effect.IsRemoved)
            return;

        Modifier[] modifiers = effect.Data.Modifiers;
        for (int i = 0; i < modifiers.Length; i++)
        {
            Modifier modifier = modifiers[i];
            if (modifier == null)
                continue;

            if (!IsSnapshot(modifier.Magnitude))
                effect.Magnitudes[i] = effect.Spec.CalculateMagnitude(modifier.Magnitude, _attributes);

            _attributes.ExecuteModifier(modifier.Attribute, modifier.Op, effect.Magnitudes[i], effect.Source);
        }

        EffectExecuted?.Invoke(effect.Spec);
    }

    private static bool IsSnapshot(Magnitude magnitude) =>
        magnitude == null || magnitude.Type != MagnitudeType.AttributeBased || magnitude.Snapshot;

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
        float magnitude = effect.Spec.CalculateMagnitude(modifier.Magnitude, _attributes);
        if (magnitude == effect.Magnitudes[index])
            return;

        effect.IsRefreshing = true;
        try
        {
            effect.Magnitudes[index] = magnitude;
            _attributes.UpdateModifier(modifier.Attribute, effect.Source, index, magnitude);
        }
        finally
        {
            effect.IsRefreshing = false;
        }
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

        Remove(effect);
    }

    private void Remove(ActiveEffect effect)
    {
        if (effect.IsRemoved)
            return;

        effect.IsRemoved = true;
        TimerManager.Stop(effect.PeriodTimer);
        TimerManager.Stop(effect.DurationTimer);

        if (effect.LiveBindings != null)
        {
            foreach (ActiveEffect.LiveBinding binding in effect.LiveBindings)
                binding.Attribute.CurrentValueChanged -= binding.Handler;
        }

        if (!effect.IsPeriodic)
        {
            Modifier[] modifiers = effect.Data.Modifiers;
            for (int i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i] != null)
                    _attributes.RemoveModifiers(modifiers[i].Attribute, effect.Handle.Value);
            }
        }

        for (int i = 0; i < effect.GrantedTagCount; i++)
            _tags.Remove(effect.Data.GrantedTags[i]);

        _effects.Remove(effect);
        EffectRemoved?.Invoke(effect);
    }
}
