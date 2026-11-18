using System;
using System.Collections.Generic;
using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// Something an actor can do (a spell, an attack, a dash). Subclass it with [GlobalClass]: settings are exports,
/// the behaviour is <see cref="Activate"/>. The .tres is the definition, shared by everyone; giving it to an actor
/// (<see cref="AbilityContainer.Give"/>) makes a copy for that actor, so fields may hold state between activations.
/// <code>
/// [GlobalClass]
/// public partial class Fireball : Ability
/// {
///     [Export] public PackedScene Projectile { get; set; }
///     [Export] public EffectData Damage { get; set; }
///
///     // CommitOnActivate is on: cost and cooldown are already applied here.
///     protected override void Activate() => WaitDelay(0.3f, Launch);
///
///     private void Launch()
///     {
///         var projectile = Projectile.Instantiate&lt;FireballProjectile&gt;();
///         projectile.Hit = MakeEffectSpec(Damage, causer: projectile);
///         Actor.GetParent().AddChild(projectile);
///         End();
///     }
/// }
/// </code>
/// An ability is active from activation until <see cref="End"/> (or until it is cancelled), and only once at a time.
/// Exports are not changed at run time: sub-resources are shared with the definition.
/// </summary>
[GlobalClass]
public abstract partial class Ability : Resource
{
    private List<AbilityTask> _tasks;
    private EffectSpec _costSpec;
    private EffectSpec _cooldownSpec;
    private Action<Ability, bool> _ended;

    // How many of the owned tags were given, so ending takes back exactly those.
    private int _ownedTagCount;

    /// <summary>What this ability is (e.g. "Ability.Fire.Ball"); matched by cancel and block tags of other abilities.</summary>
    [ExportGroup("Tags")]
    [Export]
    public TagContainer AssetTags { get; set; }

    /// <summary>Activating cancels the active abilities whose asset tags match any of these.</summary>
    [Export]
    public TagContainer CancelAbilitiesWithTags { get; set; }

    /// <summary>While active, abilities whose asset tags match any of these cannot activate.</summary>
    [Export]
    public TagContainer BlockAbilitiesWithTags { get; set; }

    /// <summary>Given to the actor while the ability is active (e.g. "State.Casting").</summary>
    [Export]
    public TagContainer ActivationOwnedTags { get; set; }

    /// <summary>The actor must have all of these to activate.</summary>
    [Export]
    public TagContainer ActivationRequiredTags { get; set; }

    /// <summary>The ability does not activate while the actor has any of these (e.g. "State.Stunned").</summary>
    [Export]
    public TagContainer ActivationBlockedTags { get; set; }

    /// <summary>Instant effect applied to the actor on commit (e.g. Mana −20). Its Add modifiers must not take an attribute below 0.</summary>
    [ExportGroup("Commit")]
    [Export]
    public EffectData Cost { get; set; }

    /// <summary>Duration effect applied to the actor on commit; its granted tags (e.g. "Cooldown.Fireball") block activation while present.</summary>
    [Export]
    public EffectData Cooldown { get; set; }

    /// <summary>Commit (cost and cooldown) right before <see cref="Activate"/>. Off, call <see cref="Commit"/> yourself (e.g. when a charge is released).</summary>
    [Export]
    public bool CommitOnActivate { get; set; } = true;

    /// <summary>The .tres this copy was made from; null on the definition itself.</summary>
    public Ability Definition { get; private set; }

    public AbilityHandle Handle { get; private set; }

    /// <summary>Set when given (default 1); read it in your own code, e.g. for SetByCaller values.</summary>
    public int Level { get; private set; }

    /// <summary>From activation until the end (including <see cref="OnEnd"/>).</summary>
    public bool IsActive { get; private set; }

    /// <summary>The actor that owns the ability (the parent of its <see cref="AbilitySystemComponent"/>); may be null.</summary>
    public Node Actor { get; private set; }

    /// <summary>The actor's ability system component (<c>as AbilitySystemComponent</c>); may be null.</summary>
    public Node AbilitySystem { get; private set; }

    public AttributeContainer Attributes { get; private set; }

    public TagCountContainer Tags { get; private set; }

    public ActiveEffectContainer Effects { get; private set; }

    /// <summary>True while the actor has one of the <see cref="Cooldown"/> effect's granted tags (from any source).</summary>
    public bool IsOnCooldown
    {
        get
        {
            TagContainer cooldownTags = Cooldown?.GrantedTags;
            return Tags != null && cooldownTags != null && Tags.HasAny(cooldownTags);
        }
    }

    /// <summary>Seconds until the cooldown ends; 0 when not on cooldown.</summary>
    public float CooldownRemaining
    {
        get
        {
            TagContainer cooldownTags = Cooldown?.GrantedTags;
            return Effects == null || cooldownTags == null ? 0f : Effects.GetRemainingTime(cooldownTags);
        }
    }

    /// <summary>
    /// True if no Add modifier of <see cref="Cost"/> would take its attribute below 0 (other operations and
    /// executions are not checked; override <see cref="CanActivate"/> for those). SetByCaller is not supported here.
    /// </summary>
    public bool CanAffordCost
    {
        get
        {
            if (_costSpec == null)
                return true;

            Modifier[] modifiers = Cost.Modifiers;
            for (int i = 0; i < modifiers.Length; i++)
            {
                Modifier modifier = modifiers[i];
                if (modifier == null || modifier.Op != ModifierOp.Add)
                    continue;

                AttributeValue attribute = Attributes.GetAttribute(modifier.Attribute);
                if (attribute == null)
                    continue;

                float magnitude = _costSpec.CalculateMagnitude(modifier.Magnitude, Attributes, Tags);
                if (attribute.CurrentValue + magnitude < 0f)
                    return false;
            }

            return true;
        }
    }

    internal bool IsEnding { get; private set; }

    /// <summary>Increases with every activation in the container; tells abilities activated during a call apart.</summary>
    internal int ActivationId { get; private set; }

    /// <summary>What the ability does. Call <see cref="End"/> when it is done; it may end right here.</summary>
    protected abstract void Activate();

    /// <summary>Clean up here. Tasks are already stopped; owned tags are still present.</summary>
    protected virtual void OnEnd(bool cancelled)
    {
    }

    /// <summary>Extra activation condition, checked after tags, cooldown and cost.</summary>
    protected virtual bool CanActivate() => true;

    /// <summary>
    /// Checks cooldown and cost again and applies them (cooldown first). Only while active; false if they no longer
    /// pass. Needed only when <see cref="CommitOnActivate"/> is off.
    /// </summary>
    protected bool Commit() => IsActive && !IsEnding && TryCommit() == ActivationFailure.None;

    /// <summary>Ends the ability: tasks stop, <see cref="OnEnd"/> runs, owned tags are taken back. Calling it again does nothing.</summary>
    protected void End() => Finish(false);

    /// <summary>Ends the ability as cancelled.</summary>
    protected void Cancel() => Finish(true);

    /// <summary>A spec of <paramref name="data"/> coming from this actor (instigator, its attributes and tags), to apply to anyone.</summary>
    protected EffectSpec MakeEffectSpec(EffectData data, Node causer = null) =>
        new(data, Actor, causer, AbilitySystem) { SourceAttributes = Attributes, SourceTags = Tags };

    protected EffectHandle ApplyEffectToSelf(EffectData data)
    {
        if (Effects != null)
            return Effects.Apply(MakeEffectSpec(data));

        GD.PushError($"Ability '{ResourcePath}': ApplyEffectToSelf on an ability that was not given to an actor.");
        return EffectHandle.None;
    }

    /// <summary>Runs <paramref name="completed"/> after <paramref name="seconds"/>, unless the ability ends first.</summary>
    protected WaitDelayTask WaitDelay(float seconds, Action completed)
    {
        var task = new WaitDelayTask(seconds);
        task.Completed += completed;
        return RunTask(task);
    }

    /// <summary>Starts <paramref name="task"/>; it ends when the ability ends. Only while active.</summary>
    protected T RunTask<T>(T task) where T : AbilityTask
    {
        if (task == null || task.IsRunning)
            return task;

        if (!IsActive || IsEnding)
        {
            GD.PushError($"Ability '{(Definition ?? this).ResourcePath}': a task can only start while the ability is active.");
            return task;
        }

        _tasks ??= new List<AbilityTask>(2);

        // Finished tasks are dropped here, so a long activation starting many tasks does not keep them all.
        _tasks.RemoveAll(static t => !t.IsRunning);
        _tasks.Add(task);
        task.Start();
        return task;
    }

    internal void Grant(Ability definition, AbilityHandle handle, int level, AttributeContainer attributes, TagCountContainer tags,
        ActiveEffectContainer effects, Node actor, Node abilitySystem, Action<Ability, bool> ended)
    {
        Definition = definition;
        Handle = handle;
        Level = level;
        Attributes = attributes;
        Tags = tags;
        Effects = effects;
        Actor = actor;
        AbilitySystem = abilitySystem;
        _ended = ended;

        // Built once, so cost and cooldown checks (polled by UI every frame) allocate nothing.
        _costSpec = Cost == null ? null : MakeEffectSpec(Cost);
        _cooldownSpec = Cooldown == null ? null : MakeEffectSpec(Cooldown);

        if (Cost != null && !Cost.IsInstant)
            GD.PushWarning($"Ability '{definition.ResourcePath}': the cost effect should be instant.");

        if (Cooldown != null && (Cooldown.IsInstant || Cooldown.GrantedTags == null || Cooldown.GrantedTags.Count == 0))
            GD.PushWarning($"Ability '{definition.ResourcePath}': the cooldown effect needs a duration and granted tags; otherwise it never blocks activation.");
    }

    internal bool MeetsTagRequirements()
    {
        if (ActivationRequiredTags != null && !Tags.HasAll(ActivationRequiredTags))
            return false;

        return ActivationBlockedTags == null || !Tags.HasAny(ActivationBlockedTags);
    }

    internal bool PassesCustomCheck() => CanActivate();

    internal ActivationFailure TryCommit()
    {
        if (IsOnCooldown)
            return ActivationFailure.Cooldown;

        if (!CanAffordCost)
            return ActivationFailure.Cost;

        if (_cooldownSpec != null)
            Effects.Apply(_cooldownSpec);

        if (_costSpec != null)
            Effects.Apply(_costSpec);

        return ActivationFailure.None;
    }

    internal void BeginActivation(int activationId)
    {
        IsActive = true;
        ActivationId = activationId;

        TagContainer owned = ActivationOwnedTags;
        for (int i = 0; owned != null && i < owned.Count && IsActive && !IsEnding; i++)
        {
            // Counted before adding: a listener of this very tag may end the ability, which must take this tag back too.
            _ownedTagCount = i + 1;
            Tags.Add(owned[i]);
        }
    }

    internal void RunActivate()
    {
        try
        {
            Activate();
        }
        catch
        {
            // A failed activation must not leave the ability active (and its owned tags given) forever.
            Finish(true);
            throw;
        }
    }

    internal void Finish(bool cancelled)
    {
        if (!IsActive || IsEnding)
            return;

        IsEnding = true;
        try
        {
            if (_tasks != null)
            {
                for (int i = 0; i < _tasks.Count; i++)
                    _tasks[i].End();

                _tasks.Clear();
            }

            OnEnd(cancelled);
        }
        finally
        {
            int count = _ownedTagCount;
            _ownedTagCount = 0;
            for (int i = 0; i < count; i++)
                Tags.Remove(ActivationOwnedTags[i]);

            IsActive = false;
            IsEnding = false;
        }

        _ended?.Invoke(this, cancelled);
    }
}
