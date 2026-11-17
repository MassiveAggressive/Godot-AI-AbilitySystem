namespace AbilitySystem;

/// <summary>How a <see cref="Magnitude"/> gets its value. Values are stored in resources; append new ones.</summary>
public enum MagnitudeType
{
    /// <summary>The fixed <see cref="Magnitude.Value"/>.</summary>
    Constant = 0,

    /// <summary>Set in code on the spec before applying, keyed by <see cref="Magnitude.CallerTagName"/> (e.g. ability damage).</summary>
    SetByCaller = 1,

    /// <summary>Coefficient × an attribute of the source or target, read once or on every use (<see cref="Magnitude.Snapshot"/>).</summary>
    AttributeBased = 2,

    /// <summary>Calculated in code by <see cref="Magnitude.Calculation"/>, once or on every use (<see cref="Magnitude.Snapshot"/>).</summary>
    Custom = 3,
}
