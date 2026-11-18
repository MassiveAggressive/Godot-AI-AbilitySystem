namespace AbilitySystem;

/// <summary>Order of the targets a <see cref="TargetQuery2D"/> finds, by distance from the origin; applied before the target limit.</summary>
public enum TargetSort
{
    /// <summary>The order the physics server returned.</summary>
    None = 0,

    Nearest = 1,

    Farthest = 2,
}
