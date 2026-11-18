using System;
using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// Gives its parent (the actor) attributes, tags, effects and abilities. Add it as a child of the actor, pick the
/// attribute sets, starting effects (e.g. an instant effect setting initial values) and starting abilities in the
/// inspector. Everything is ready once the node enters the tree, before any _Ready; the containers exist from
/// construction, so code may also add sets and abilities itself:
/// <code>
/// var asc = AbilitySystemComponent.Find(body);
/// asc?.Effects.Apply(hitSpec);
/// </code>
/// When the node is freed, its abilities are cancelled and its effects removed, so no timer keeps running for it.
/// </summary>
[Tool]
[GlobalClass]
public partial class AbilitySystemComponent : Node
{
    private string[] _attributeSets = Array.Empty<string>();
    private EffectData[] _startingEffects = Array.Empty<EffectData>();
    private Resource[] _startingAbilities = Array.Empty<Resource>();
    private bool _started;

    public AbilitySystemComponent()
    {
        // In the editor the tag registry is not loaded and nothing runs.
        if (Engine.IsEditorHint())
            return;

        Attributes = new AttributeContainer();
        Tags = new TagCountContainer();
        Effects = new ActiveEffectContainer(Attributes, Tags);
    }

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

    /// <summary>The parent node; set when the node first enters the tree.</summary>
    public Node Actor { get; private set; }

    public AttributeContainer Attributes { get; }

    public TagCountContainer Tags { get; }

    public ActiveEffectContainer Effects { get; }

    /// <summary>Created when the node first enters the tree (abilities need the actor).</summary>
    public AbilityContainer Abilities { get; private set; }

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
        Abilities = new AbilityContainer(Attributes, Tags, Effects, Actor, this);

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

    public override void _Notification(int what)
    {
        // Freed, not just removed from the tree: timers would otherwise keep running effects of a dead actor.
        if (what != NotificationPredelete || Effects == null)
            return;

        Abilities?.CancelAll();
        Effects.RemoveAll();
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
        else if (name == nameof(StartingAbilities))
        {
            property["hint"] = (int)PropertyHint.TypeString;
            property["hint_string"] = $"{(int)Variant.Type.Object}/{(int)PropertyHint.ResourceType}:{nameof(Ability)}";
        }
    }
}
