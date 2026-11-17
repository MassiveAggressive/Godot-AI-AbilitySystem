namespace AbilitySystem;

/// <summary>Whose attribute an attribute based <see cref="Magnitude"/> reads. Values are stored in resources; append new ones.</summary>
public enum AttributeCaptureSource
{
    /// <summary>The actor applying the effect (<see cref="EffectSpec.SourceAttributes"/>).</summary>
    Source = 0,

    /// <summary>The actor receiving the effect.</summary>
    Target = 1,
}
