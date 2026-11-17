using Godot;

namespace AbilitySystem;

/// <summary>
/// Who applied a modifier and with which effect. <see cref="Id"/> is the key used to remove the modifiers again
/// (an effect handle; code that adds modifiers by hand uses negative ids). <see cref="Data"/> is the effect data
/// typed as <see cref="Resource"/> so the attribute layer does not depend on effect classes; cast it where needed.
/// Instigator and causer may be null or freed: check with <see cref="GodotObject.IsInstanceValid"/> before use.
/// </summary>
public readonly struct ModifierSource
{
    public readonly int Id;
    public readonly Resource Data;
    public readonly Node Instigator;
    public readonly Node Causer;

    public ModifierSource(int id, Resource data = null, Node instigator = null, Node causer = null)
    {
        Id = id;
        Data = data;
        Instigator = instigator;
        Causer = causer;
    }
}
