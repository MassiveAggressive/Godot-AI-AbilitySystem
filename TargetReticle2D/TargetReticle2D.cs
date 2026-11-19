using Godot;

namespace AbilitySystem;

/// <summary>
/// The aiming marker of <see cref="WaitTargetDataTask"/>: follows the mouse in the world, limited by the ability's
/// range, until the player confirms or cancels. Make a scene whose root is this node and put the visuals under it
/// (e.g. a circle sprite); no code is needed. Subclass it to change the aim or react to the player:
/// <code>
/// [GlobalClass]
/// public partial class GridReticle : TargetReticle2D
/// {
///     protected override Vector2 CalculateAim(Vector2 mouse) => mouse.Snapped(new Vector2(32f, 32f));
///     public override bool IsAimValid => base.IsAimValid &amp;&amp; !IsOverWall(AimPoint);
/// }
/// </code>
/// The task adds it as a child of the source (top level, so it stays in world coordinates) and frees it when done.
/// </summary>
[GlobalClass]
public partial class TargetReticle2D : Node2D
{
    // A point pulled to the range limit must still count as inside it despite float rounding.
    private const float RangeTolerance = 0.01f;

    private bool _aiming;

    /// <summary>Who aims (the ability's actor); the range is measured from it. Null until aiming starts.</summary>
    public Node2D Source { get; private set; }

    /// <summary>Most distance from <see cref="Source"/>; 0 for no limit.</summary>
    public float Range { get; private set; }

    /// <summary>On: the aim is pulled back to the range limit. Off: aiming beyond it is not valid.</summary>
    public bool ClampToRange { get; private set; }

    /// <summary>Where the player aims (global), updated every frame while aiming.</summary>
    public Vector2 AimPoint { get; private set; }

    /// <summary>True while waiting for the player; false once confirmed or cancelled.</summary>
    public bool IsAiming => _aiming;

    /// <summary>Confirming does nothing while false (the player keeps aiming). By default: within range.</summary>
    public virtual bool IsAimValid =>
        GodotObject.IsInstanceValid(Source) && IsWithinRange(AimPoint, Source.GlobalPosition, Range);

    public override void _Process(double delta)
    {
        if (_aiming)
            RefreshAim();
    }

    /// <summary>The aim from the mouse position (global); e.g. snap to a grid. Range is applied afterwards.</summary>
    protected virtual Vector2 CalculateAim(Vector2 mouse) => mouse;

    /// <summary>The reticle is in the tree and <see cref="AimPoint"/> is set.</summary>
    protected virtual void OnAimStarted()
    {
    }

    /// <summary>The player confirmed. Return true to free the node yourself (e.g. after an animation); false frees it now.</summary>
    protected virtual bool OnConfirmed() => false;

    /// <summary>Aiming was cancelled or the ability ended. Return true to free the node yourself.</summary>
    protected virtual bool OnCancelled() => false;

    internal static bool IsWithinRange(Vector2 point, Vector2 origin, float range)
    {
        if (range <= 0f)
            return true;

        float limit = range + RangeTolerance;
        return point.DistanceSquaredTo(origin) <= limit * limit;
    }

    internal static Vector2 LimitToRange(Vector2 point, Vector2 origin, float range, bool clamp) =>
        clamp && range > 0f ? origin + (point - origin).LimitLength(range) : point;

    /// <summary>Before it is added to the tree.</summary>
    internal void Setup(Node2D source, float range, bool clampToRange)
    {
        Source = source;
        Range = range;
        ClampToRange = clampToRange;
        TopLevel = true;
    }

    /// <summary>After it is added to the tree.</summary>
    internal void Begin()
    {
        _aiming = true;
        RefreshAim();
        OnAimStarted();
    }

    /// <summary>Reads the mouse again; input arrives before this frame's _Process, so a click uses where it happened.</summary>
    internal void RefreshAim()
    {
        if (!GodotObject.IsInstanceValid(Source))
            return;

        Vector2 origin = Source.GlobalPosition;
        AimPoint = LimitToRange(CalculateAim(GetGlobalMousePosition()), origin, Range, ClampToRange);
        GlobalPosition = AimPoint;
    }

    /// <summary>Stops aiming; true if the reticle frees itself.</summary>
    internal bool Close(bool confirmed)
    {
        _aiming = false;
        return confirmed ? OnConfirmed() : OnCancelled();
    }
}
