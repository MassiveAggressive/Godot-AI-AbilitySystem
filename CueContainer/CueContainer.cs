using System;
using System.Collections.Generic;
using Godot;
using Tags;
using Timers;

namespace AbilitySystem;

/// <summary>
/// Shows an actor's effect cues (<see cref="EffectData.Cues"/>) with the scenes of a <see cref="CueLibrary"/>, by
/// following the effects' events; the effects know nothing about cues. An execution opens a short lived scene each
/// time. A duration or infinite effect keeps one scene per cue tag on the actor while it is active and not inhibited;
/// effects with the same cue tag share it, and it goes with the last of them. Code and abilities show cues without an
/// effect with <see cref="Execute"/>, <see cref="Add"/> and <see cref="Remove(Tag)"/>. Cues are only for looks: game
/// logic does not wait on them.
/// </summary>
public sealed class CueContainer
{
    private readonly Node _target;
    private readonly ActiveEffectContainer _effects;
    private readonly CueLibrary _library;

    // Bound once, so subscribing allocates no new delegate.
    private readonly Action<ActiveEffect> _onEffectAdded;
    private readonly Action<ActiveEffect> _onEffectRemoved;
    private readonly Action<EffectSpec> _onEffectExecuted;
    private readonly Action<ActiveEffect, bool> _onInhibitionChanged;

    // Effects whose cues are shown, with how many of their cue tags were added.
    private readonly List<ShownEffect> _shownEffects = new(4);

    // The lasting scene of each cue tag (by id) and how many shown effects share it.
    private readonly Dictionary<int, ActiveCue> _activeCues = new(4);

    private bool _attached;

    /// <param name="target">The actor; lasting scenes become its children.</param>
    /// <param name="effects">The actor's effects, whose cues are shown.</param>
    /// <param name="library">Null for <see cref="CueLibrary.Default"/>.</param>
    public CueContainer(Node target, ActiveEffectContainer effects, CueLibrary library = null)
    {
        _target = target;
        _effects = effects;
        _library = library ?? CueLibrary.Default;
        _onEffectAdded = OnEffectAdded;
        _onEffectRemoved = OnEffectRemoved;
        _onEffectExecuted = OnEffectExecuted;
        _onInhibitionChanged = OnInhibitionChanged;

        if (target == null || effects == null)
        {
            GD.PushError("CueContainer: target or effects is null; no cues are shown.");
            return;
        }

        effects.EffectAdded += _onEffectAdded;
        effects.EffectRemoved += _onEffectRemoved;
        effects.EffectExecuted += _onEffectExecuted;
        effects.InhibitionChanged += _onInhibitionChanged;
        _attached = true;
    }

    /// <summary>
    /// Opens the short lived scene of <paramref name="tag"/> once, without an effect (e.g. a spark where a spell hit):
    /// at <paramref name="parameters"/>' location if it has one, otherwise at the actor.
    /// </summary>
    public void Execute(Tag tag, in CueParameters parameters)
    {
        if (!_attached || !tag.IsValid)
            return;

        CueEntry entry = _library.Find(tag);
        if (entry != null)
            OpenExecuted(entry, parameters.WithTag(tag));
    }

    /// <summary>
    /// Shows the lasting scene of <paramref name="tag"/> on the actor until a matching <see cref="Remove(Tag)"/>.
    /// Shared with effects that have the same cue tag: one scene, and it goes with the last of them. Every
    /// <c>Add</c> needs its own <c>Remove</c>; an extra <c>Remove</c> may take down an effect's scene.
    /// </summary>
    public void Add(Tag tag, in CueParameters parameters)
    {
        if (_attached && tag.IsValid)
            AddCue(tag, parameters.WithTag(tag));
    }

    /// <summary>Takes back one <see cref="Add"/>.</summary>
    public void Remove(Tag tag) => Remove(tag, new CueParameters(tag, _target));

    /// <summary>Takes back one <see cref="Add"/>; <paramref name="parameters"/> go to the scene's <c>OnCueRemoved</c>.</summary>
    public void Remove(Tag tag, in CueParameters parameters)
    {
        if (_attached && tag.IsValid)
            RemoveCue(tag, parameters.WithTag(tag));
    }

    /// <summary>
    /// Stops following the effects and forgets the lasting scenes without calling their hooks. Called when the actor
    /// is freed: its scenes go with it, and a node being freed should not run cue code.
    /// </summary>
    public void Detach()
    {
        if (!_attached)
            return;

        _attached = false;
        _effects.EffectAdded -= _onEffectAdded;
        _effects.EffectRemoved -= _onEffectRemoved;
        _effects.EffectExecuted -= _onEffectExecuted;
        _effects.InhibitionChanged -= _onInhibitionChanged;

        // A scene still waiting to be added would otherwise be added later and stay.
        foreach (ActiveCue cue in _activeCues.Values)
        {
            if (GodotObject.IsInstanceValid(cue.Scene) && cue.Scene.GetParent() == null)
                cue.Scene.Free();
        }

        _activeCues.Clear();
        _shownEffects.Clear();
    }

    private void OnEffectAdded(ActiveEffect effect)
    {
        // An effect starting inhibited shows its cues once it is no longer inhibited.
        if (effect.Data.HasCues && _attached && !effect.IsRemoved && !effect.IsInhibited)
            ShowEffect(effect);
    }

    private void OnEffectRemoved(ActiveEffect effect)
    {
        if (effect.Data.HasCues)
            HideEffect(effect);
    }

    private void OnInhibitionChanged(ActiveEffect effect, bool inhibited)
    {
        if (!effect.Data.HasCues || !_attached)
            return;

        if (inhibited)
            HideEffect(effect);
        else if (!effect.IsRemoved)
            ShowEffect(effect);
    }

    private void OnEffectExecuted(EffectSpec spec)
    {
        EffectData data = spec.Data;
        if (!data.HasCues || !_attached)
            return;

        ActiveEffect effect = data.IsInstant ? null : FindActive(spec);
        int stackCount = effect?.StackCount ?? 1;
        TagContainer cues = data.Cues;
        for (int i = 0; i < cues.Count; i++)
        {
            CueEntry entry = _library.Find(cues[i]);
            if (entry != null)
                OpenExecuted(entry, new CueParameters(cues[i], _target, spec, effect, stackCount));
        }
    }

    private ActiveEffect FindActive(EffectSpec spec)
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            if (_effects[i].Spec == spec)
                return _effects[i];
        }

        return null;
    }

    private void ShowEffect(ActiveEffect effect)
    {
        if (IndexOfShown(effect) >= 0)
            return;

        _shownEffects.Add(new ShownEffect(effect, 0));
        TagContainer cues = effect.Data.Cues;
        for (int i = 0; i < cues.Count; i++)
        {
            // A hook may have removed the effect (or detached this container); then it is no longer listed.
            int index = IndexOfShown(effect);
            if (index < 0)
                return;

            // Counted before adding: a hook of this very cue may remove the effect, which must take this cue back too.
            _shownEffects[index] = new ShownEffect(effect, i + 1);
            AddCue(cues[i], new CueParameters(cues[i], _target, effect.Spec, effect, effect.StackCount));
        }
    }

    /// <summary>Takes back exactly the cues that were added.</summary>
    private void HideEffect(ActiveEffect effect)
    {
        int index = IndexOfShown(effect);
        if (index < 0)
            return;

        int count = _shownEffects[index].CueCount;
        _shownEffects.RemoveAt(index);
        TagContainer cues = effect.Data.Cues;
        for (int i = 0; i < count; i++)
            RemoveCue(cues[i], new CueParameters(cues[i], _target, effect.Spec, effect, effect.StackCount));
    }

    private int IndexOfShown(ActiveEffect effect)
    {
        for (int i = 0; i < _shownEffects.Count; i++)
        {
            if (_shownEffects[i].Effect == effect)
                return i;
        }

        return -1;
    }

    private void AddCue(Tag tag, in CueParameters parameters)
    {
        if (_activeCues.TryGetValue(tag.Id, out ActiveCue cue))
        {
            _activeCues[tag.Id] = new ActiveCue(cue.Scene, cue.Count + 1);
            return;
        }

        CueEntry entry = _library.Find(tag);
        if (entry == null)
            return;

        Node scene = entry.Scene.Instantiate();
        _activeCues[tag.Id] = new ActiveCue(scene, 1);
        if (CanAddChild(_target))
            PlaceAdded(scene, parameters);
        else
            PlaceAddedLater(scene, parameters);
    }

    private void PlaceAddedLater(Node scene, CueParameters parameters) =>
        Callable.From(() => PlaceAdded(scene, parameters)).CallDeferred();

    private void PlaceAdded(Node scene, in CueParameters parameters)
    {
        // Removed (or detached) before a deferred add came round: the scene was freed.
        if (!GodotObject.IsInstanceValid(scene))
            return;

        if (!GodotObject.IsInstanceValid(_target))
        {
            scene.Free();
            return;
        }

        _target.AddChild(scene);
        if (scene is ICueNotify notify)
            notify.OnCueAdded(in parameters);
    }

    private void RemoveCue(Tag tag, in CueParameters parameters)
    {
        if (!_activeCues.TryGetValue(tag.Id, out ActiveCue cue))
            return;

        if (cue.Count > 1)
        {
            _activeCues[tag.Id] = new ActiveCue(cue.Scene, cue.Count - 1);
            return;
        }

        _activeCues.Remove(tag.Id);
        Node scene = cue.Scene;
        if (!GodotObject.IsInstanceValid(scene))
            return;

        // Never added (its deferred add is still waiting), so it never got OnCueAdded either.
        if (scene.GetParent() == null)
        {
            scene.Free();
            return;
        }

        if (scene is ICueNotify notify && notify.OnCueRemoved(in parameters))
            return;

        scene.QueueFree();
    }

    private void OpenExecuted(CueEntry entry, in CueParameters parameters)
    {
        Node parent = entry.AttachExecuted ? _target : _target.GetParent() ?? _target;
        Node scene = entry.Scene.Instantiate();
        if (CanAddChild(parent))
            PlaceExecuted(scene, parent, entry, parameters);
        else
            PlaceExecutedLater(scene, parent, entry, parameters);
    }

    private void PlaceExecutedLater(Node scene, Node parent, CueEntry entry, CueParameters parameters) =>
        Callable.From(() => PlaceExecuted(scene, parent, entry, parameters)).CallDeferred();

    private void PlaceExecuted(Node scene, Node parent, CueEntry entry, in CueParameters parameters)
    {
        if (!GodotObject.IsInstanceValid(parent))
        {
            scene.Free();
            return;
        }

        parent.AddChild(scene);
        if (parameters.HasLocation)
            Place(scene, entry, parameters);
        else if (!entry.AttachExecuted && GodotObject.IsInstanceValid(_target))
            MoveTo(scene, _target);

        if (scene is ICueNotify notify)
            notify.OnCueExecuted(in parameters);

        if (entry.ExecutedLifetime > 0f)
            FreeAfter(scene, entry.ExecutedLifetime);
    }

    /// <summary>On game time, so a paused game keeps its hit effects.</summary>
    private static void FreeAfter(Node scene, float seconds) => TimerManager.Start(seconds, () => FreeScene(scene));

    /// <summary>Places a 2D scene at a 2D actor's position, a 3D one at a 3D actor's; others are left as they are.</summary>
    private static void MoveTo(Node scene, Node target)
    {
        if (!scene.IsInsideTree() || !target.IsInsideTree())
            return;

        if (scene is Node2D scene2D && target is Node2D target2D)
            scene2D.GlobalPosition = target2D.GlobalPosition;
        else if (scene is Node3D scene3D && target is Node3D target3D)
            scene3D.GlobalPosition = target3D.GlobalPosition;
    }

    /// <summary>Places a 2D or 3D scene at the cue's location, turned to its normal if the entry asks for it.</summary>
    private static void Place(Node scene, CueEntry entry, in CueParameters parameters)
    {
        if (!scene.IsInsideTree())
            return;

        bool rotate = entry.RotateToNormal && parameters.Normal != Vector3.Zero;
        if (scene is Node2D scene2D)
        {
            scene2D.GlobalPosition = parameters.Location2D;
            if (rotate && parameters.Normal2D != Vector2.Zero)
                scene2D.GlobalRotation = parameters.Normal2D.Angle();
        }
        else if (scene is Node3D scene3D)
        {
            scene3D.GlobalPosition = parameters.Location;
            if (rotate)
                scene3D.GlobalBasis = UpAlong(parameters.Normal.Normalized(), scene3D.GlobalBasis.Scale);
        }
    }

    /// <summary>A basis whose +Y is <paramref name="up"/>, keeping <paramref name="scale"/>.</summary>
    private static Basis UpAlong(Vector3 up, Vector3 scale)
    {
        // Any direction not parallel to up gives the other two axes.
        Vector3 reference = Mathf.Abs(up.Dot(Vector3.Forward)) > 0.99f ? Vector3.Right : Vector3.Forward;
        Vector3 x = reference.Cross(up).Normalized();
        Vector3 z = x.Cross(up);
        return new Basis(x * scale.X, up * scale.Y, z * scale.Z);
    }

    private static void FreeScene(Node scene)
    {
        if (GodotObject.IsInstanceValid(scene) && !scene.IsQueuedForDeletion())
            scene.QueueFree();
    }

    // A node still setting up its children refuses new ones, e.g. the actor while it enters the tree and its
    // component applies the starting effects; the scene is then added at the end of the frame.
    private static bool CanAddChild(Node parent) => !parent.IsInsideTree() || parent.IsNodeReady();

    private readonly struct ShownEffect
    {
        public readonly ActiveEffect Effect;
        public readonly int CueCount;

        public ShownEffect(ActiveEffect effect, int cueCount)
        {
            Effect = effect;
            CueCount = cueCount;
        }
    }

    private readonly struct ActiveCue
    {
        public readonly Node Scene;
        public readonly int Count;

        public ActiveCue(Node scene, int count)
        {
            Scene = scene;
            Count = count;
        }
    }
}
