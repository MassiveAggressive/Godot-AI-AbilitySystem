namespace AbilitySystem;

/// <summary>
/// What a modifier did to an attribute. Old/new values are the base value for <see cref="ModifierEventKind.Executed"/>
/// and the current value otherwise. For <see cref="ModifierEventKind.Removed"/> every modifier of the source left at
/// once, so <see cref="Op"/> and <see cref="Magnitude"/> are empty.
/// </summary>
public readonly struct ModifierEventData
{
    public readonly ModifierEventKind Kind;
    public readonly AttributeValue Attribute;
    public readonly ModifierOp Op;
    public readonly float Magnitude;
    public readonly float OldValue;
    public readonly float NewValue;
    public readonly ModifierSource Source;

    public ModifierEventData(ModifierEventKind kind, AttributeValue attribute, ModifierOp op, float magnitude, float oldValue, float newValue, in ModifierSource source)
    {
        Kind = kind;
        Attribute = attribute;
        Op = op;
        Magnitude = magnitude;
        OldValue = oldValue;
        NewValue = newValue;
        Source = source;
    }
}
