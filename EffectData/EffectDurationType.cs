namespace AbilitySystem;

/// <summary>Values are stored in resources; append new ones.</summary>
public enum EffectDurationType
{
    /// <summary>Changes base values once and leaves nothing behind.</summary>
    Instant = 0,

    /// <summary>Active for <see cref="EffectData.Duration"/> seconds.</summary>
    HasDuration = 1,

    /// <summary>Active until removed.</summary>
    Infinite = 2,
}
