using Godot;

namespace AbilitySystem;

/// <summary>
/// A magnitude calculated in code (<see cref="MagnitudeType.Custom"/>). Subclass it with [GlobalClass], export the
/// settings a designer tunes, and pick it in a <see cref="Magnitude"/>:
/// <code>
/// [GlobalClass]
/// public partial class MissingHealthHeal : MagnitudeCalculation
/// {
///     private static readonly AttributeId Health = AttributeRegistry.Request("CombatSet.Health");
///     private static readonly AttributeId MaxHealth = AttributeRegistry.Request("CombatSet.MaxHealth");
///
///     [Export] public float Ratio { get; set; } = 0.5f;
///
///     public override float Calculate(in CalculationContext context) =>
///         Ratio * (context.GetTargetValue(MaxHealth) - context.GetTargetValue(Health));
/// }
/// </code>
/// Shared by every effect that loads the resource, so it keeps no run time state.
/// </summary>
[GlobalClass]
public abstract partial class MagnitudeCalculation : Resource
{
    /// <summary>The value for one stack; stacking is applied afterwards, as for any magnitude.</summary>
    public abstract float Calculate(in CalculationContext context);
}
