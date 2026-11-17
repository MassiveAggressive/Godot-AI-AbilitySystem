using System;
using System.Collections.Generic;
using Timers;

namespace AbilitySystem;

/// <summary>A duration or infinite effect currently applied to a target. Owned by its <see cref="ActiveEffectContainer"/>.</summary>
public sealed class ActiveEffect
{
    internal ActiveEffect(EffectHandle handle, EffectSpec spec, in ModifierSource source, float[] magnitudes, float duration, float period)
    {
        Handle = handle;
        Spec = spec;
        Source = source;
        Magnitudes = magnitudes;
        Duration = duration;
        Period = period;
    }

    public EffectHandle Handle { get; }

    public EffectSpec Spec { get; }

    public EffectData Data => Spec.Data;

    /// <summary>Seconds, calculated when applied; +∞ for infinite effects.</summary>
    public float Duration { get; }

    /// <summary>Seconds between executions, calculated when applied; 0 if not periodic.</summary>
    public float Period { get; }

    public bool IsPeriodic => Period > 0f;

    /// <summary>Seconds left; +∞ for infinite effects, 0 once removed.</summary>
    public float RemainingTime
    {
        get
        {
            if (IsRemoved)
                return 0f;

            return float.IsPositiveInfinity(Duration) ? Duration : TimerManager.GetRemaining(DurationTimer);
        }
    }

    /// <summary>True once the effect expired or was removed; a kept reference then only describes the past.</summary>
    public bool IsRemoved { get; internal set; }

    internal ModifierSource Source { get; }

    /// <summary>Per modifier, calculated once when applied.</summary>
    internal float[] Magnitudes { get; }

    internal TimerHandle DurationTimer { get; set; }

    internal TimerHandle PeriodTimer { get; set; }

    /// <summary>How many of the granted tags were given, so removal takes back exactly those.</summary>
    internal int GrantedTagCount { get; set; }

    /// <summary>Attributes followed by non-snapshot modifiers; null if there are none.</summary>
    internal List<LiveBinding> LiveBindings { get; set; }

    /// <summary>Set while a followed attribute updates a modifier, so a change it causes cannot feed back into itself.</summary>
    internal bool IsRefreshing { get; set; }

    internal readonly struct LiveBinding
    {
        public readonly AttributeValue Attribute;
        public readonly Action<AttributeValue, float, float> Handler;

        public LiveBinding(AttributeValue attribute, Action<AttributeValue, float, float> handler)
        {
            Attribute = attribute;
            Handler = handler;
        }
    }
}
