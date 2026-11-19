using System;

namespace AbilitySystem;

/// <summary>Which attitudes a filter lets through (<see cref="TargetQuery2D.Attitudes"/>).</summary>
// No None/All members: Godot would show them as extra flag boxes in the inspector.
[Flags]
public enum TeamAttitudeMask
{
    Hostile = 1,
    Neutral = 2,
    Friendly = 4,
}
