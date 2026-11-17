using Godot;

namespace AbilitySystem;

/// <summary>
/// Who applied a modifier and with which effect. <see cref="Id"/> is the key used to remove the modifiers again
/// (an effect handle; code that adds modifiers by hand uses negative ids). <see cref="Data"/> is the effect data
/// typed as <see cref="Resource"/> so the attribute layer does not depend on effect classes; cast it where needed.
/// <see cref="InstigatorAbilitySystem"/> is the instigator's ability system component, typed as <see cref="Node"/> for
/// the same reason; cast it to AbilitySystemComponent where needed.
/// Instigator, causer and ability system may be null or freed: check with <see cref="GodotObject.IsInstanceValid"/> before use.
/// </summary>
public readonly struct ModifierSource
{
    public readonly int Id;
    public readonly Resource Data;
    public readonly Node Instigator;
    public readonly Node Causer;
    public readonly Node InstigatorAbilitySystem;

    public ModifierSource(int id, Resource data = null, Node instigator = null, Node causer = null, Node instigatorAbilitySystem = null)
    {
        Id = id;
        Data = data;
        Instigator = instigator;
        Causer = causer;
        InstigatorAbilitySystem = instigatorAbilitySystem;
    }
}
