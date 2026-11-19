namespace AbilitySystem;

/// <summary>How one actor stands toward another (<see cref="IAbilitySystem.GetAttitude(IAbilitySystem, IAbilitySystem)"/>).</summary>
public enum TeamAttitude
{
    /// <summary>The same team, or the actor itself.</summary>
    Friendly,

    /// <summary>One of them has no team (animals, props, or no ability system).</summary>
    Neutral,

    /// <summary>Both have a team and the teams differ.</summary>
    Hostile,
}
