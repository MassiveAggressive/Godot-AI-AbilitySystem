namespace AbilitySystem;

/// <summary>When <see cref="WaitTargetDataTask"/> takes the aim.</summary>
public enum TargetConfirmation
{
    /// <summary>Right away at the mouse position, without a reticle (quick cast).</summary>
    Instant,

    /// <summary>On the generic confirm (<see cref="AbilityContainer.InputConfirm"/>) or pressing the ability's input again.</summary>
    Confirm,

    /// <summary>Aim while the ability's input is held, take it on release (the generic confirm works too).</summary>
    InputRelease,
}
