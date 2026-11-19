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

    // Lasting cues this activation added (ActivationCues and AddCue), taken back when it ends.
    private List<Tag> _addedCues;

    // While events or input reach the tasks, finished tasks stay in the list so the walk over it is not disturbed.
    private int _deliveryDepth;

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

    /// <summary>
    /// Activates when an event matching any of these (hierarchically) is sent to the actor; override
    /// <see cref="ActivateFromEvent"/> to read the event.
    /// </summary>
    [ExportGroup("Triggers")]
    [Export]
    public TagContainer TriggerEventTags { get; set; }

    /// <summary>Activates when the actor gets any of these.</summary>
    [Export]
    public TagContainer TriggerOnTagAdded { get; set; }

    /// <summary>Activates when the actor gets any of these (or is given the ability while it has one), cancelled when it has none of them any more.</summary>
    [Export]
    public TagContainer TriggerWhileTagPresent { get; set; }

    /// <summary>Activates once when given (passive abilities; starting abilities activate when the component enters the tree).</summary>
    [Export]
    public bool ActivateOnGranted { get; set; }

    /// <summary>
    /// Pressing it activates the ability, or reaches its tasks while active (<see cref="AbilityContainer.PressInput"/>).
    /// Change it per actor with <see cref="AbilityContainer.SetInput"/>.
    /// </summary>
    [ExportGroup("Input")]
    [Export(PropertyHint.InputName)]
    public StringName InputAction { get; set; }

    /// <summary>Lasting cues shown on the actor while the ability is active (e.g. "Cue.Cast.Fire" for a glowing hand).</summary>
    [ExportGroup("Cues")]
    [Export]
    public TagContainer ActivationCues { get; set; }

    /// <summary>The .tres this copy was made from; null on the definition itself.</summary>
    public Ability Definition { get; private set; }

    public AbilityHandle Handle { get; private set; }

    /// <summary>
    /// Set when given (default 1), changed with <see cref="AbilityContainer.SetLevel"/>. Passed to the specs this
    /// ability makes (cost and cooldown too), so level values and curves of their magnitudes follow it.
    /// </summary>
    public float Level { get; private set; }

    /// <summary>From activation until the end (including <see cref="OnEnd"/>).</summary>
    public bool IsActive { get; private set; }

    /// <summary>True between <see cref="AbilityContainer.PressInput"/> and <see cref="AbilityContainer.ReleaseInput"/> of its action.</summary>
    public bool IsInputPressed { get; internal set; }

    /// <summary>The actor that owns the ability (the parent of its <see cref="AbilitySystemComponent"/>); may be null.</summary>
    public Node Actor { get; private set; }

    /// <summary>The actor's ability system component (<c>as AbilitySystemComponent</c>); may be null.</summary>
    public Node AbilitySystem { get; private set; }

    public AttributeContainer Attributes { get; private set; }

    public TagCountContainer Tags { get; private set; }

    public ActiveEffectContainer Effects { get; private set; }

    /// <summary>The actor's cues; may be null (an ability container made without one).</summary>
    public CueContainer Cues { get; private set; }

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

    internal bool HasTagTriggers => HasTags(TriggerOnTagAdded) || HasTags(TriggerWhileTagPresent);

    /// <summary>What the ability does. Call <see cref="End"/> when it is done; it may end right here.</summary>
    protected abstract void Activate();

    /// <summary>Activation by one of <see cref="TriggerEventTags"/>; calls <see cref="Activate"/> unless overridden.</summary>
    protected virtual void ActivateFromEvent(in AbilityEventData data) => Activate();

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

    /// <summary>A spec of <paramref name="data"/> coming from this actor (instigator, its attributes and tags) at this ability's level, to apply to anyone.</summary>
    protected EffectSpec MakeEffectSpec(EffectData data, Node causer = null) =>
        new(data, Actor, causer, AbilitySystem) { SourceAttributes = Attributes, SourceTags = Tags, Level = Level };

    protected EffectHandle ApplyEffectToSelf(EffectData data)
    {
        if (Effects != null)
            return Effects.Apply(MakeEffectSpec(data));

        GD.PushError($"Ability '{ResourcePath}': ApplyEffectToSelf on an ability that was not given to an actor.");
        return EffectHandle.None;
    }

    /// <summary>
    /// Applies <paramref name="data"/> from this actor to every entry of <paramref name="targets"/> whose actor has an
    /// ability system. Entries with a point get their own copy of the spec with that hit, so the effect's executed cues
    /// open where each target was hit; the others share one spec. Returns how many accepted it (application tags or
    /// immunity may refuse).
    /// </summary>
    protected int ApplyEffectToTargets(EffectData data, TargetData targets, Node causer = null)
    {
        if (data == null || targets == null)
            return 0;

        EffectSpec spec = null;
        int applied = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            TargetHit hit = targets[i];
            ActiveEffectContainer effects = IAbilitySystem.Find(hit.Actor)?.Effects;
            if (effects == null)
                continue;

            spec ??= MakeEffectSpec(data, causer);
            EffectSpec applying = hit.HasPoint ? spec.CopyWithHit(hit.Point, hit.Normal) : spec;
            if (effects.Apply(applying).IsValid)
                applied++;
        }

        return applied;
    }

    /// <summary>Opens the short lived scene of <paramref name="tag"/> at the actor (e.g. a cast flash).</summary>
    protected void ExecuteCue(Tag tag) => Cues?.Execute(tag, MakeCueParameters(tag));

    /// <summary>Opens the short lived scene of <paramref name="tag"/> at <paramref name="location"/> (global).</summary>
    protected void ExecuteCue(Tag tag, Vector2 location, Vector2 normal = default) =>
        Cues?.Execute(tag, MakeCueParameters(tag).WithLocation(location, normal));

    protected void ExecuteCue(TagContainer tags)
    {
        for (int i = 0; tags != null && i < tags.Count; i++)
            ExecuteCue(tags[i]);
    }

    protected void ExecuteCue(TagContainer tags, Vector2 location, Vector2 normal = default)
    {
        for (int i = 0; tags != null && i < tags.Count; i++)
            ExecuteCue(tags[i], location, normal);
    }

    /// <summary>
    /// Shows the lasting scene of <paramref name="tag"/> on the actor until <see cref="RemoveCue(Tag)"/> or until the
    /// ability ends. Only while active.
    /// </summary>
    protected void AddCue(Tag tag)
    {
        if (!IsActive || IsEnding)
        {
            GD.PushError($"Ability '{(Definition ?? this).ResourcePath}': a cue can only be added while the ability is active.");
            return;
        }

        if (Cues == null || !tag.IsValid)
            return;

        // Listed before adding: a hook of this cue may end the ability, which must take this cue back too.
        _addedCues ??= new List<Tag>(2);
        _addedCues.Add(tag);
        Cues.Add(tag, MakeCueParameters(tag));
    }

    protected void AddCue(TagContainer tags)
    {
        for (int i = 0; tags != null && i < tags.Count && IsActive && !IsEnding; i++)
            AddCue(tags[i]);
    }

    /// <summary>Takes back one <see cref="AddCue(Tag)"/> of this ability; cues it did not add are left alone.</summary>
    protected void RemoveCue(Tag tag)
    {
        int index = _addedCues?.LastIndexOf(tag) ?? -1;
        if (index < 0)
            return;

        _addedCues.RemoveAt(index);
        Cues?.Remove(tag, MakeCueParameters(tag));
    }

    protected void RemoveCue(TagContainer tags)
    {
        for (int i = 0; tags != null && i < tags.Count; i++)
            RemoveCue(tags[i]);
    }

    /// <summary>Applies <paramref name="data"/> from this actor to <paramref name="target"/>; None if it has no ability system or refused it.</summary>
    protected EffectHandle ApplyEffectToTarget(EffectData data, Node target, Node causer = null)
    {
        ActiveEffectContainer effects = IAbilitySystem.Find(target)?.Effects;
        return data == null || effects == null ? EffectHandle.None : effects.Apply(MakeEffectSpec(data, causer));
    }

    /// <summary>Runs <paramref name="completed"/> after <paramref name="seconds"/>, unless the ability ends first.</summary>
    protected WaitDelayTask WaitDelay(float seconds, Action completed)
    {
        var task = new WaitDelayTask(seconds);
        task.Completed += completed;
        return RunTask(task);
    }

    /// <summary>
    /// Runs <paramref name="received"/> when an event matching <paramref name="tag"/> is sent to the actor; only for
    /// the first one unless <paramref name="onlyOnce"/> is off. Not for the event that activated the ability.
    /// </summary>
    protected WaitEventTask WaitEvent(Tag tag, AbilityEventHandler received, bool onlyOnce = true)
    {
        var task = new WaitEventTask(tag, onlyOnce);
        task.Received += received;
        return RunTask(task);
    }

    /// <summary>Runs <paramref name="completed"/> once the actor has <paramref name="tag"/> (with <paramref name="present"/> off: no longer has it); right away if it already is so.</summary>
    protected WaitTagTask WaitTag(Tag tag, Action completed, bool present = true)
    {
        var task = new WaitTagTask(Tags, tag, present);
        task.Completed += completed;
        return RunTask(task);
    }

    /// <summary>Runs <paramref name="released"/> with the seconds held once the input is released; right away with 0 if it is not held.</summary>
    protected WaitInputReleaseTask WaitInputRelease(Action<float> released)
    {
        var task = new WaitInputReleaseTask(!IsInputPressed);
        task.Released += released;
        return RunTask(task);
    }

    /// <summary>
    /// Lets the player aim from the actor (a <see cref="Node2D"/>) with <paramref name="reticle"/> and fills
    /// <paramref name="into"/> on confirm: the aimed point, or with <paramref name="query"/> the targets it finds
    /// there. For other settings (e.g. <see cref="WaitTargetDataTask.QueryFromSource"/>) make the task yourself.
    /// </summary>
    protected WaitTargetDataTask WaitTargetData(PackedScene reticle, float range, TargetData into, Action<TargetData> confirmed,
        Action cancelled = null, TargetQuery2D query = null, TargetConfirmation confirmation = TargetConfirmation.Confirm)
    {
        var task = new WaitTargetDataTask(Actor as Node2D, reticle, range, into, query, confirmation) { InputHeld = IsInputPressed };
        task.Confirmed += confirmed;
        task.Cancelled += cancelled;
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
        if (_deliveryDepth == 0)
            _tasks.RemoveAll(static t => !t.IsRunning);

        _tasks.Add(task);
        task.Start();
        return task;
    }

    internal void Grant(Ability definition, AbilityHandle handle, float level, AttributeContainer attributes, TagCountContainer tags,
        ActiveEffectContainer effects, CueContainer cues, Node actor, Node abilitySystem, Action<Ability, bool> ended)
    {
        Definition = definition;
        Handle = handle;
        Attributes = attributes;
        Tags = tags;
        Effects = effects;
        Cues = cues;
        Actor = actor;
        AbilitySystem = abilitySystem;
        _ended = ended;
        SetLevel(level);

        if (Cost != null && !Cost.IsInstant)
            GD.PushWarning($"Ability '{definition.ResourcePath}': the cost effect should be instant.");

        if (Cooldown != null && (Cooldown.IsInstant || Cooldown.GrantedTags == null || Cooldown.GrantedTags.Count == 0))
            GD.PushWarning($"Ability '{definition.ResourcePath}': the cooldown effect needs a duration and granted tags; otherwise it never blocks activation.");
    }

    /// <summary>Effects already applied keep the level they were applied at.</summary>
    internal void SetLevel(float level)
    {
        Level = level;

        // Built once per level, so cost and cooldown checks (polled by UI every frame) allocate nothing.
        _costSpec = Cost == null ? null : MakeEffectSpec(Cost);
        _cooldownSpec = Cooldown == null ? null : MakeEffectSpec(Cooldown);
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

        if (IsActive && !IsEnding)
            AddCue(ActivationCues);
    }

    internal void RunActivate(bool fromEvent, in AbilityEventData data)
    {
        try
        {
            if (fromEvent)
                ActivateFromEvent(data);
            else
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
            RemoveAddedCues();

            int count = _ownedTagCount;
            _ownedTagCount = 0;
            for (int i = 0; i < count; i++)
                Tags.Remove(ActivationOwnedTags[i]);

            IsActive = false;
            IsEnding = false;
        }

        _ended?.Invoke(this, cancelled);
    }

    /// <summary>Passes an event to the running tasks; tasks started meanwhile wait for the next one.</summary>
    internal void ReceiveEvent(in AbilityEventData data)
    {
        if (!IsActive || _tasks == null)
            return;

        _deliveryDepth++;
        try
        {
            int count = _tasks.Count;
            for (int i = 0; i < count && i < _tasks.Count; i++)
            {
                if (_tasks[i].IsRunning)
                    _tasks[i].DeliverEvent(data);
            }
        }
        finally
        {
            _deliveryDepth--;
        }
    }

    /// <summary>Passes a press or release of the input action to the running tasks.</summary>
    internal void ReceiveInput(bool pressed)
    {
        if (!IsActive || _tasks == null)
            return;

        _deliveryDepth++;
        try
        {
            int count = _tasks.Count;
            for (int i = 0; i < count && i < _tasks.Count; i++)
            {
                if (_tasks[i].IsRunning)
                    _tasks[i].DeliverInput(pressed);
            }
        }
        finally
        {
            _deliveryDepth--;
        }
    }

    /// <summary>Passes a confirm or cancel to the running tasks; true if one of them took it.</summary>
    internal bool ReceiveConfirm(bool confirm)
    {
        if (!IsActive || _tasks == null)
            return false;

        bool taken = false;
        _deliveryDepth++;
        try
        {
            int count = _tasks.Count;
            for (int i = 0; i < count && i < _tasks.Count; i++)
            {
                if (_tasks[i].IsRunning && _tasks[i].DeliverConfirm(confirm))
                    taken = true;
            }
        }
        finally
        {
            _deliveryDepth--;
        }

        return taken;
    }

    private CueParameters MakeCueParameters(Tag tag) => new(tag, Actor, instigator: Actor, level: Level);

    /// <summary>Last added first; each is taken off the list before its hook runs.</summary>
    private void RemoveAddedCues()
    {
        if (_addedCues == null)
            return;

        for (int i = _addedCues.Count - 1; i >= 0; i--)
        {
            Tag tag = _addedCues[i];
            _addedCues.RemoveAt(i);
            Cues?.Remove(tag, MakeCueParameters(tag));
        }
    }

    private static bool HasTags(TagContainer tags) => tags != null && tags.Count > 0;
}
