using Godot;

namespace AbilitySystem;

/// <summary>
/// One change an effect makes to an attribute: which attribute, which operation, which channel and how much
/// (<see cref="Magnitude"/>). The attribute is picked from a dropdown and resolved to an id once, on first use.
/// Shared by everyone who loads the effect resource, so it is not changed at run time.
/// </summary>
[Tool]
[GlobalClass]
public partial class Modifier : Resource
{
    private string _attributeName = string.Empty;
    private AttributeId _attribute;
    private bool _attributeResolved;

    /// <summary>"SetClass.Property", e.g. "CombatSet.Health".</summary>
    [Export]
    public string AttributeName
    {
        get => _attributeName;
        set
        {
            _attributeName = value ?? string.Empty;
            _attributeResolved = false;
        }
    }

    [Export]
    public ModifierOp Op { get; set; } = ModifierOp.Add;

    [Export]
    public Magnitude Magnitude { get; set; }

    /// <summary>Ignored when the effect is instant or periodic (the modifier then goes to the base value).</summary>
    [Export]
    public ModifierEvaluationChannel Channel { get; set; } = ModifierEvaluationChannel.Channel0;

    /// <summary>The attribute this modifier changes; None if the name is not found (reported once).</summary>
    public AttributeId Attribute
    {
        get
        {
            if (!_attributeResolved)
            {
                _attribute = AttributeRegistry.Request(_attributeName);
                _attributeResolved = true;
            }

            return _attribute;
        }
    }

    // Godot API: the property info arrives as a Godot Dictionary.
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (!Engine.IsEditorHint() || property["name"].AsString() != nameof(AttributeName))
            return;

        property["hint"] = (int)PropertyHint.Enum;
        property["hint_string"] = AttributeRegistry.BuildOptions();
    }
}
