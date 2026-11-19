using System;
using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// Gives its parent (the actor) attributes, tags, effects, abilities and effect cues. Add it as a child of the actor,
/// pick the attribute sets, starting effects (e.g. an instant effect setting initial values) and starting abilities in
/// the inspector. Everything is created when the node first enters the tree, so it is ready in every _Ready; code may
/// add more sets and abilities from there:
/// <code>
/// var asc = AbilitySystemComponent.Find(body);
/// asc?.Effects.Apply(hitSpec);
/// </code>
/// When the node is freed, its abilities are cancelled and its effects removed, so no timer keeps running for it.
/// </summary>
[Tool]
[GlobalClass]
public partial class AbilitySystemComponent : Node, IAbilitySystem
{
    private string[] _attributeSets = Array.Empty<string>();
    private EffectData[] _startingEffects = Array.Empty<EffectData>();
    private Resource[] _startingAbilities = Array.Empty<Resource>();
    private bool _processInput;
    private bool _started;
    private string _teamName = string.Empty;

    /// <summary>Attribute set classes added when the node enters the tree (e.g. "CombatSet").</summary>
    [Export]
    public string[] AttributeSets
    {
        get => _attributeSets;
        set => _attributeSets = value ?? Array.Empty<string>();
    }

    /// <summary>Applied after the sets are added, in order, with the actor as instigator.</summary>
    [Export]
    public EffectData[] StartingEffects
    {
        get => _startingEffects;
        set => _startingEffects = value ?? Array.Empty<EffectData>();
    }

    /// <summary>Given after the starting effects. Pick [GlobalClass] subclasses of <see cref="Ability"/>.</summary>
    // Typed as Resource: in the editor an ability subclass without [Tool] is only a placeholder Resource, which an
    // Ability array cannot hold. _ValidateProperty limits the element menu to abilities.
    [Export]
    public Resource[] StartingAbilities
    {
        get => _startingAbilities;
        set => _startingAbilities = value ?? Array.Empty<Resource>();
    }

    /// <summary>
    /// Passes unhandled input of the abilities' input actions to <see cref="AbilityContainer.PressInput"/> and
    /// <see cref="AbilityContainer.ReleaseInput"/> (and marks it handled). For the player; leave off for AI.
    /// </summary>
    [Export]
    public bool ProcessInput
    {
        get => _processInput;
        set
        {
            _processInput = value;
            if (IsInsideTree())
                SetProcessUnhandledInput(value && !Engine.IsEditorHint());
        }
    }

    /// <summary>
    /// The actor's team tag (e.g. "Team.Player"), picked from the tag menu; empty for none (neutral to everyone).
    /// Read at run time as <see cref="Team"/>.
    /// </summary>
    [Export]
    public string TeamName
    {
        get => _teamName;
        set
        {
            _teamName = value ?? string.Empty;
            if (_started)
                Team = ResolveTeam();
        }
    }

    /// <summary>
    /// With <see cref="ProcessInput"/>: pressing it calls <see cref="AbilityContainer.InputConfirm"/> (e.g. placing an
    /// aimed spell). Taken, the input reaches no ability; otherwise it works as usual (a left click still attacks).
    /// </summary>
    [Export(PropertyHint.InputName)]
    public StringName ConfirmAction { get; set; }

    /// <summary>With <see cref="ProcessInput"/>: pressing it calls <see cref="AbilityContainer.InputCancel"/>; see <see cref="ConfirmAction"/>.</summary>
    [Export(PropertyHint.InputName)]
    public StringName CancelAction { get; set; }

    /// <summary>The parent node; set when the node first enters the tree.</summary>
    public Node Actor { get; private set; }

    // All created when the node first enters the tree: the main scene is instantiated before the autoloads enter
    // the tree, so the tag registry is not loaded yet at construction.
    public AttributeContainer Attributes { get; private set; }

    public TagCountContainer Tags { get; private set; }

    public ActiveEffectContainer Effects { get; private set; }

    public AbilityContainer Abilities { get; private set; }

    /// <summary>Shows the effects' cues on the actor with <see cref="CueLibrary.Default"/>.</summary>
    public CueContainer Cues { get; private set; }

    /// <summary>
    /// The team, from <see cref="TeamName"/> when the node first enters the tree. Set it from code to change sides
    /// (e.g. while charmed); targeting reads it on every query.
    /// </summary>
    public Tag Team { get; set; }

    /// <summary><paramref name="node"/> itself or its first direct child that is an ability system component; null if none.</summary>
    public static AbilitySystemComponent Find(Node node)
    {
        if (!IsInstanceValid(node))
            return null;

        if (node is AbilitySystemComponent self)
            return self;

        int count = node.GetChildCount();
        for (int i = 0; i < count; i++)
        {
            if (node.GetChild(i) is AbilitySystemComponent child)
                return child;
        }

        return null;
    }

    public override void _EnterTree()
    {
        // Leaving and entering again (reparenting) keeps everything as it is.
        if (Engine.IsEditorHint() || _started)
            return;

        _started = true;
        Actor = GetParent();
        Team = ResolveTeam();
        Attributes = new AttributeContainer();
        Tags = new TagCountContainer();
        Effects = new ActiveEffectContainer(Attributes, Tags);
        Abilities = new AbilityContainer(Attributes, Tags, Effects, Actor, this);
        Cues = new CueContainer(Actor, Effects);

        foreach (string setName in _attributeSets)
        {
            if (!string.IsNullOrEmpty(setName))
                Attributes.Add(AttributeRegistry.CreateSet(setName));
        }

        foreach (EffectData effect in _startingEffects)
        {
            if (effect != null)
                Effects.Apply(new EffectSpec(effect, Actor, null, this) { SourceAttributes = Attributes, SourceTags = Tags });
        }

        foreach (Resource resource in _startingAbilities)
        {
            if (resource is Ability ability)
                Abilities.Give(ability);
            else if (resource != null)
                GD.PushError($"{Name}: starting ability '{resource.ResourcePath}' is not an Ability.");
        }
    }

    // Godot turns unhandled input on for every node overriding _UnhandledInput when it becomes ready; only the
    // player's component needs it.
    public override void _Ready() => SetProcessUnhandledInput(_processInput && !Engine.IsEditorHint());

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_processInput || Abilities == null)
            return;

        // Before the abilities' actions: while aiming, the confirm click must not also start an attack bound to it.
        if ((IsPressed(@event, ConfirmAction) && Abilities.InputConfirm()) || (IsPressed(@event, CancelAction) && Abilities.InputCancel()))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        bool handled = false;
        for (int i = 0; i < Abilities.Count; i++)
        {
            StringName action = Abilities[i].InputAction;
            if (action == null || action.IsEmpty || IsBoundEarlier(i, action))
                continue;

            if (@event.IsActionPressed(action))
            {
                Abilities.PressInput(action);
                handled = true;
            }
            else if (@event.IsActionReleased(action))
            {
                Abilities.ReleaseInput(action);
                handled = true;
            }
        }

        if (handled)
            GetViewport().SetInputAsHandled();
    }

    public override void _Notification(int what)
    {
        // Freed, not just removed from the tree: timers would otherwise keep running effects of a dead actor.
        if (what != NotificationPredelete || Effects == null)
            return;

        // First, so no cue code runs on a node being freed; the cue scenes on the actor go with it.
        Cues.Detach();
        Abilities?.CancelAll();
        Effects.RemoveAll();
    }

    private Tag ResolveTeam() => _teamName.Length == 0 ? Tag.None : TagRegistry.Request(_teamName);

    private static bool IsPressed(InputEvent @event, StringName action) => action != null && !action.IsEmpty && @event.IsActionPressed(action);

    /// <summary>Several abilities may share an action; it is passed on once.</summary>
    private bool IsBoundEarlier(int index, StringName action)
    {
        for (int i = 0; i < index; i++)
        {
            if (Abilities[i].InputAction == action)
                return true;
        }

        return false;
    }

    // Godot API: the property info arrives as a Godot Dictionary.
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (!Engine.IsEditorHint())
            return;

        string name = property["name"].AsString();
        if (name == nameof(AttributeSets))
        {
            property["hint"] = (int)PropertyHint.TypeString;
            property["hint_string"] = $"{(int)Variant.Type.String}/{(int)PropertyHint.Enum}:{AttributeRegistry.BuildSetOptions()}";
        }
        else if (name == nameof(TeamName))
        {
            property["hint"] = (int)PropertyHint.Enum;
            property["hint_string"] = TagContainer.BuildTagOptions();
        }
        else if (name == nameof(StartingAbilities))
        {
            property["hint"] = (int)PropertyHint.TypeString;
            property["hint_string"] = $"{(int)Variant.Type.Object}/{(int)PropertyHint.ResourceType}:{nameof(Ability)}";
        }
    }
}
