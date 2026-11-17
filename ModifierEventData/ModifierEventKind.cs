namespace AbilitySystem;

public enum ModifierEventKind
{
    /// <summary>Applied to the base value once (instant or periodic effect).</summary>
    Executed = 0,

    /// <summary>Added to the current value until removed.</summary>
    Added = 1,

    /// <summary>All modifiers of one source were removed from the attribute.</summary>
    Removed = 2,

    /// <summary>An added modifier got a new magnitude (it follows an attribute).</summary>
    Updated = 3,
}
