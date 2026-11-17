using Godot;

namespace AbilitySystem;

/// <summary>
/// Code that decides what an instant or periodic effect does, from both actors' attributes and tags (e.g. damage
/// reduced by armor). Runs after the effect's modifiers, every time the effect executes. Subclass it with
/// [GlobalClass] and add it to <see cref="EffectData.Executions"/>:
/// <code>
/// [GlobalClass]
/// public partial class ArmorDamage : ExecutionCalculation
/// {
///     private static readonly AttributeId AttackPower = AttributeRegistry.Request("CombatSet.AttackPower");
///     private static readonly AttributeId Armor = AttributeRegistry.Request("CombatSet.Armor");
///     private static readonly AttributeId Damage = AttributeRegistry.Request("CombatSet.Damage");
///
///     [Export] public float ArmorConstant { get; set; } = 100f;
///
///     public override void Execute(in CalculationContext context, ExecutionOutput output)
///     {
///         float armor = context.GetTargetValue(Armor);
///         output.AddModifier(Damage, ModifierOp.Add, context.GetSourceValue(AttackPower) * ArmorConstant / (ArmorConstant + armor));
///     }
/// }
/// </code>
/// Shared by every effect that loads the resource, so it keeps no run time state.
/// </summary>
[GlobalClass]
public abstract partial class ExecutionCalculation : Resource
{
    /// <summary>
    /// Adds the changes to <paramref name="output"/>; they are applied to base values once this returns, with the
    /// effect as their source. The output is not scaled by stacks: use <see cref="CalculationContext.StackCount"/>.
    /// </summary>
    public abstract void Execute(in CalculationContext context, ExecutionOutput output);
}
