namespace AbilitySystem;

/// <summary>How repeated applications of the same effect combine. Values are stored in resources; append new ones.</summary>
public enum EffectStackingType
{
    /// <summary>Every application is a separate active effect.</summary>
    None = 0,

    /// <summary>One stack per target, whoever applies it.</summary>
    ByTarget = 1,

    /// <summary>One stack per instigator on each target.</summary>
    BySource = 2,
}
