using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// A number an effect uses: a modifier's magnitude, an effect's duration or period. Pick the type in the inspector;
/// only the fields of that type are shown. The value is calculated by <see cref="EffectSpec.CalculateMagnitude"/>
/// when the effect is applied. Shared by everyone who loads the resource, so it is not changed at run time.
/// </summary>
[Tool]
[GlobalClass]
public partial class Magnitude : Resource
{
    private MagnitudeType _type;
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
    [Export]
    public MagnitudeCalculation Calculation { get; set; }

    /// <summary>
    /// AttributeBased and Custom: on, the value is calculated once when the effect is applied. Off, it is calculated
    /// every time the modifier is used: right before an instant modifier runs and on every period. A duration or
    /// infinite modifier follows its attribute when AttributeBased; a Custom one is calculated once, since what it
    /// reads is not known. Durations and periods are always calculated once.
    /// </summary>
    [Export]
    public bool Snapshot { get; set; } = true;

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
            _ => true,
        };

        // Hidden fields keep the storage flag so their value is still saved.
        if (!visible)
        {
            property["usage"] = (int)PropertyUsageFlags.Storage;
            return;
        }

        if (name == nameof(CallerTagName))
            SetDropdown(property, TagContainer.BuildTagOptions());
        else if (name == nameof(BackingAttributeName))
            SetDropdown(property, AttributeRegistry.BuildOptions());
    }

    private static void SetDropdown(Godot.Collections.Dictionary property, string options)
    {
        property["hint"] = (int)PropertyHint.Enum;
        property["hint_string"] = options;
    }
}
