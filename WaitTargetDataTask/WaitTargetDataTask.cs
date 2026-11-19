using System;
using Godot;

namespace AbilitySystem;

/// <summary>
/// Lets the player aim: shows a <see cref="TargetReticle2D"/> scene following the mouse (within range) and, on
/// confirm, fills the target data: the aimed point, or with a query the targets it finds there. Cancel (or the
/// ability ending) closes the reticle. Usually started with the ability's <c>WaitTargetData</c> helper:
/// <code>
/// _targets.Clear();
/// WaitTargetData(Reticle, Range, _targets, Strike, Cancel, Area);
/// </code>
/// Set the properties and subscribe before <c>RunTask</c>: <see cref="TargetConfirmation.Instant"/> completes
/// while starting. The query runs on confirm, i.e. while handling input, so it sees the last physics frame.
/// AI does not aim: it picks targets itself and activates with them (<c>TryActivate(handle, data)</c>).
/// </summary>
public sealed class WaitTargetDataTask : AbilityTask
{
    private readonly Node2D _source;
    private readonly PackedScene _reticleScene;
    private readonly float _range;
    private readonly TargetData _into;
    private readonly TargetQuery2D _query;
    private readonly TargetConfirmation _confirmation;
    private TargetReticle2D _reticle;

    /// <param name="source">Who aims; the range is measured from it and the reticle is added under it.</param>
    /// <param name="reticle">A scene whose root is a <see cref="TargetReticle2D"/>; null for an invisible one.</param>
    /// <param name="range">Most distance from the source; 0 for no limit.</param>
    /// <param name="into">Filled on confirm, without clearing it.</param>
    /// <param name="query">Run on confirm; null to take only the aimed point.</param>
    public WaitTargetDataTask(Node2D source, PackedScene reticle, float range, TargetData into, TargetQuery2D query = null,
        TargetConfirmation confirmation = TargetConfirmation.Confirm)
    {
        _source = source;
        _reticleScene = reticle;
        _range = range;
        _into = into;
        _query = query;
        _confirmation = confirmation;
    }

    /// <summary>The player confirmed; passes the filled target data (the one given to the constructor).</summary>
    public event Action<TargetData> Confirmed;

    /// <summary>Cancelled by the player, out of range without clamping, or the source is gone. Not raised when the ability ends.</summary>
    public event Action Cancelled;

    /// <summary>On: aiming beyond the range is pulled back to it. Off: confirming out of range is ignored (<see cref="TargetConfirmation.Instant"/> and a release cancel).</summary>
    public bool ClampToRange { get; init; } = true;

    /// <summary>The query runs from the source toward the aim (cone, line); off, at the aimed point (area spell).</summary>
    public bool QueryFromSource { get; init; }

    /// <summary>With <see cref="TargetConfirmation.Confirm"/>: pressing the ability's input again confirms too.</summary>
    public bool AbilityInputConfirms { get; init; } = true;

    /// <summary>With <see cref="TargetConfirmation.InputRelease"/>: false when the input is not held now (activated by code); it then works like <see cref="TargetConfirmation.Instant"/>.</summary>
    public bool InputHeld { get; init; } = true;

    /// <summary>Where the player aimed (global); set on confirm.</summary>
    public Vector2 AimPoint { get; private set; }

    protected override void OnStart()
    {
        if (!GodotObject.IsInstanceValid(_source) || !_source.IsInsideTree() || _into == null)
        {
            GD.PushError("WaitTargetDataTask: needs a source Node2D inside the tree and a TargetData to fill.");
            Complete(false);
            return;
        }

        if (_confirmation == TargetConfirmation.Instant || (_confirmation == TargetConfirmation.InputRelease && !InputHeld))
        {
            TakeMouseAim();
            return;
        }

        OpenReticle();
    }

    protected override void OnEnd() => CloseReticle(false);

    protected override bool OnConfirm() => TryConfirm();

    protected override bool OnCancel()
    {
        Complete(false);
        return true;
    }

    protected override void OnInputPressed()
    {
        if (_confirmation == TargetConfirmation.Confirm && AbilityInputConfirms)
            TryConfirm();
    }

    protected override void OnInputReleased()
    {
        if (_confirmation == TargetConfirmation.InputRelease && !TryConfirm())
            Complete(false);
    }

    private void TakeMouseAim()
    {
        Vector2 origin = _source.GlobalPosition;
        AimPoint = TargetReticle2D.LimitToRange(_source.GetGlobalMousePosition(), origin, _range, ClampToRange);
        Complete(TargetReticle2D.IsWithinRange(AimPoint, origin, _range));
    }

    private void OpenReticle()
    {
        if (_reticleScene == null)
        {
            _reticle = new TargetReticle2D();
        }
        else
        {
            Node node = _reticleScene.Instantiate();
            _reticle = node as TargetReticle2D;
            if (_reticle == null)
            {
                GD.PushError($"WaitTargetDataTask: the root of '{_reticleScene.ResourcePath}' is not a TargetReticle2D.");
                node.Free();
                Complete(false);
                return;
            }
        }

        _reticle.Setup(_source, _range, ClampToRange);
        _source.AddChild(_reticle);
        _reticle.Begin();
    }

    /// <summary>False while the aim is not valid: the player keeps aiming.</summary>
    private bool TryConfirm()
    {
        if (!IsRunning)
            return false;

        if (!GodotObject.IsInstanceValid(_source) || !GodotObject.IsInstanceValid(_reticle))
        {
            Complete(false);
            return true;
        }

        _reticle.RefreshAim();
        AimPoint = _reticle.AimPoint;
        if (!_reticle.IsAimValid)
            return false;

        Complete(true);
        return true;
    }

    private void Complete(bool confirmed)
    {
        if (confirmed)
            CollectTargets();

        CloseReticle(confirmed);
        End();

        if (confirmed)
            Confirmed?.Invoke(_into);
        else
            Cancelled?.Invoke();
    }

    private void CollectTargets()
    {
        if (_query == null)
        {
            _into.AddPoint(AimPoint);
            return;
        }

        Vector2 direction = AimPoint - _source.GlobalPosition;
        if (QueryFromSource)
            _query.Run(_source, direction, _into);
        else
            _query.RunAt(_source, AimPoint, direction, _into);
    }

    private void CloseReticle(bool confirmed)
    {
        TargetReticle2D reticle = _reticle;
        _reticle = null;
        if (!GodotObject.IsInstanceValid(reticle))
            return;

        if (!reticle.Close(confirmed))
            reticle.QueueFree();
    }
}
