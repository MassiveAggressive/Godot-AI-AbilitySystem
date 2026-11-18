namespace AbilitySystem;

/// <summary>Why an ability could not be activated, e.g. to tell the player "not enough mana".</summary>
public enum ActivationFailure
{
    None = 0,

    /// <summary>The actor was not given the ability.</summary>
    NotGranted,

    AlreadyActive,

    /// <summary>The actor lacks one of <see cref="Ability.ActivationRequiredTags"/> or has one of <see cref="Ability.ActivationBlockedTags"/>.</summary>
    TagRequirements,

    /// <summary>An active ability blocks it (<see cref="Ability.BlockAbilitiesWithTags"/>).</summary>
    BlockedByAbility,

    Cooldown,

    Cost,

    /// <summary>The ability's own check (<c>CanActivate</c> override) refused.</summary>
    Custom,
}
