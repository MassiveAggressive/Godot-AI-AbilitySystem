using System;
using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// A number an effect uses: a modifier's magnitude, an effect's duration or period. Pick the type in the inspector;
/// only the fields of that type are shown. The value is calculated by <see cref="EffectSpec.CalculateMagnitude"/>
/// when the effect is applied. Constant and AttributeBased numbers may change with the spec's level
/// (<see cref="LevelValues"/>, <see cref="LevelCurve"/>). Shared by everyone who loads the resource, so it is not
/// changed at run time.
/// </summary>
[Tool]
[GlobalClass]
public partial class Magnitude : Resource
{
    private MagnitudeType _type;
    private float[] _levelValues = Array.Empty<float>();
    private string _callerTagName = string.Empty;
    private string _backingAttributeName = string.Empty;
    private Tag _callerTag;
    private bool _callerTagResolved;
    private AttributeId _backingAttribute;
    private bool _backingAttributeResolved;

    [Export]
    public MagnitudeType Type
    {
        get => _type;
        set
        {
            _type = value;
            NotifyPropertyListChanged();
        }
    }

    /// <summary>Constant: the value. Ratios for multiply/divide: +50% = 1.5.</summary>
    [Export]
    public float Value { get; set; }

    /// <summary>SetByCaller: the key code sets on the spec, e.g. "Data.Damage".</summary>
    [Export]
    public string CallerTagName
    {
        get => _callerTagName;
        set
        {
            _callerTagName = value ?? string.Empty;
            _callerTagResolved = false;
        }
    }

    /// <summary>AttributeBased: the attribute read, "SetClass.Property".</summary>
    [Export]
    public string BackingAttributeName
    {
        get => _backingAttributeName;
        set
        {
            _backingAttributeName = value ?? string.Empty;
            _backingAttributeResolved = false;
        }
    }

    /// <summary>AttributeBased: read from the applying actor or the receiving one.</summary>
    [Export]
    public AttributeCaptureSource CaptureFrom { get; set; }

    /// <summary>AttributeBased: the result is Coefficient × the attribute's current value.</summary>
    [Export]
    public float Coefficient { get; set; } = 1f;

    /// <summary>Custom: the calculation, a [GlobalClass] subclass of <see cref="MagnitudeCalculation"/>.</summary>
    // Typed as Resource: in the editor a subclass without [Tool] is only a placeholder Resource, which a
    // MagnitudeCalculation property cannot hold. The hint keeps the inspector menu to MagnitudeCalculation subclasses.
    [Export(PropertyHint.ResourceType, nameof(MagnitudeCalculation))]
    public Resource Calculation { get; set; }

    /// <summary>
    /// AttributeBased and Custom: on, the value is calculated once when the effect is applied. Off, it is calculated
    /// every time the modifier is used: right before an instant modifier runs and on every period. A duration or
    /// infinite modifier follows its attribute when AttributeBased; a Custom one is calculated once, since what it
    /// reads is not known. Durations and periods are always calculated once.
    /// </summary>
    [Export]
    public bool Snapshot { get; set; } = true;

    /// <summary>
    /// Constant and AttributeBased: the value (or coefficient) for level 1, 2, 3…; when filled, it replaces
    /// <see cref="Value"/>/<see cref="Coefficient"/> and <see cref="LevelCurve"/>. Between levels it is read on a
    /// straight line (2.5 → halfway between the 2nd and 3rd); below 1 the first, past the end the last value.
    /// </summary>
    [Export]
    public float[] LevelValues
    {
        get => _levelValues;
        set => _levelValues = value ?? Array.Empty<float>();
    }

    /// <summary>
    /// Constant and AttributeBased, when <see cref="LevelValues"/> is empty: <see cref="Value"/>/<see cref="Coefficient"/>
    /// × the curve at the level. Save it as a .tres to share one curve between abilities. Set the curve's Max Domain
    /// to the highest level and Max Value to the largest multiplier (Godot starts both at 0–1); linear tangents give
    /// straight lines between points. Outside its domain the end value is used.
    /// </summary>
    [Export]
    public Curve LevelCurve { get; set; }

    /// <summary>SetByCaller key; None if the name is not found (reported once).</summary>
    public Tag CallerTag
    {
        get
        {
            if (!_callerTagResolved)
            {
                _callerTag = TagRegistry.Request(_callerTagName);
                _callerTagResolved = true;
            }

            return _callerTag;
        }
    }

    /// <summary>AttributeBased attribute; None if the name is not found (reported once).</summary>
    public AttributeId BackingAttribute
    {
        get
        {
            if (!_backingAttributeResolved)
            {
                _backingAttribute = AttributeRegistry.Request(_backingAttributeName);
                _backingAttributeResolved = true;
            }

            return _backingAttribute;
        }
    }

    /// <summary><paramref name="number"/> (Value or Coefficient) at <paramref name="level"/>: from <see cref="LevelValues"/>, else × <see cref="LevelCurve"/>.</summary>
    internal float ScaleByLevel(float number, float level)
    {
        float[] values = _levelValues;
        if (values.Length == 0)
            return LevelCurve == null ? number : number * LevelCurve.Sample(level);

        // Level 1 is the first value, as in a curve table keyed by level; ends are held.
        float index = level - 1f;
        int last = values.Length - 1;
        if (!(index > 0f))
            return values[0];

        if (index >= last)
            return values[last];

        int lower = (int)index;
        return Mathf.Lerp(values[lower], values[lower + 1], index - lower);
    }

    // Godot API: the property info arrives as a Godot Dictionary.
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (!Engine.IsEditorHint())
            return;

        string name = property["name"].AsString();
        bool attributeBased = _type == MagnitudeType.AttributeBased;
        bool visible = name switch
        {
            nameof(Value) => _type == MagnitudeType.Constant,
            nameof(CallerTagName) => _type == MagnitudeType.SetByCaller,
            nameof(BackingAttributeName) or nameof(CaptureFrom) or nameof(Coefficient) => attributeBased,
            nameof(Calculation) => _type == MagnitudeType.Custom,
            nameof(Snapshot) => attributeBased || _type == MagnitudeType.Custom,
            nameof(LevelValues) or nameof(LevelCurve) => attributeBased || _type == MagnitudeType.Constant,
            _ => true,
        };

        // Hidden fields keep the storage flag so their value is still saved.
        if (!visible)
        {
            property["usage"] = (int)PropertyUsageFlags.Storage;
            return;
        }

        if (name == nameof(CallerTagName))
        {
            // Picked from the Tags plugin's tag tree.
            property["hint"] = (int)PropertyHint.None;
            property["hint_string"] = TagContainer.TagNameHint;
        }
        else if (name == nameof(BackingAttributeName))
            SetDropdown(property, AttributeRegistry.BuildOptions());
    }

    private static void SetDropdown(Godot.Collections.Dictionary property, string options)
    {
        property["hint"] = (int)PropertyHint.Enum;
        property["hint_string"] = options;
    }
}
