using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// One row of the <see cref="CueLibrary"/>: the scene opened for a cue tag. Also used for the tag's children that
/// have no entry of their own ("Cue.Fire" covers "Cue.Fire.Burn").
/// </summary>
[Tool]
[GlobalClass]
public partial class CueEntry : Resource
{
    private string _tagName = string.Empty;
    private Tag _tag;
    private bool _tagResolved;

    /// <summary>The cue tag, e.g. "Cue.Fire.Burn".</summary>
    [Export]
    public string TagName
    {
        get => _tagName;
        set
        {
            _tagName = value ?? string.Empty;
            _tagResolved = false;
        }
    }

    /// <summary>
    /// Any scene (particles, sound, a Node2D with several children). If the root's script implements
    /// <see cref="ICueNotify"/>, its hooks are called.
    /// </summary>
    [Export]
    public PackedScene Scene { get; set; }

    /// <summary>Seconds a scene opened by an execution stays before it is freed; 0 leaves freeing to the scene itself.</summary>
    [Export(PropertyHint.Range, "0,10,0.05,or_greater,suffix:s")]
    public float ExecutedLifetime { get; set; } = 2f;

    /// <summary>
    /// A scene opened by an execution becomes the actor's child and moves with it. Off, it is placed under the
    /// actor's parent, so it stays where the hit happened. Either way it opens at the cue's location (the hit point)
    /// when it has one, otherwise at the actor.
    /// </summary>
    [Export]
    public bool AttachExecuted { get; set; }

    /// <summary>
    /// When the cue has a hit normal: a 2D scene turns to face it (its +X along the normal), a 3D scene's up (+Y) points
    /// along it. Off, the scene keeps its own rotation; its hook can still read the normal.
    /// </summary>
    [Export]
    public bool RotateToNormal { get; set; }

    /// <summary>None if the name is empty or not found (reported once).</summary>
    public Tag Tag
    {
        get
        {
            if (!_tagResolved)
            {
                _tag = _tagName.Length == 0 ? Tag.None : TagRegistry.Request(_tagName);
                _tagResolved = true;
            }

            return _tag;
        }
    }

    // Godot API: the property info arrives as a Godot Dictionary.
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (!Engine.IsEditorHint() || property["name"].AsString() != nameof(TagName))
            return;

        property["hint"] = (int)PropertyHint.Enum;
        property["hint_string"] = TagContainer.BuildTagOptions();
    }
}
