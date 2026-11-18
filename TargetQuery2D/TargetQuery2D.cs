using System;
using System.Collections.Generic;
using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// Finds actors in an area (2D physics), set up in the inspector: a shape, collision layers and filters. An ability
/// keeps one as an export and runs it when it acts:
/// <code>
/// [Export] public TargetQuery2D Area { get; set; }   // Circle 150, BlockedTags: State.Dead
///
/// _targets.Clear();
/// Area.Run((Node2D)Actor, facing, _targets);
/// ApplyEffectToTargets(Damage, _targets);
/// </code>
/// A collider counts as the actor whose ability system it or its nearest ancestor has, so a hitbox under the actor
/// works; each actor is added once. Physics state is most accurate inside _PhysicsProcess; called elsewhere, it is
/// that of the last physics frame. Each run asks the physics server, which allocates (Godot API): run it when the
/// ability acts, not every frame.
/// </summary>
[Tool]
[GlobalClass]
public partial class TargetQuery2D : Resource
{
    // Colliders asked from the physics server per run; beyond this many in the area the rest are not seen.
    private const int MaxColliders = 64;

    private static readonly Variant ColliderKey = "collider";
    private static readonly Variant PositionKey = "position";
    private static readonly Variant NormalKey = "normal";

    private TargetShape _shape;

    // Built on first use and reused; the resource is shared, runs happen one at a time on the main thread.
    private CircleShape2D _circle;
    private RectangleShape2D _rectangle;
    private PhysicsShapeQueryParameters2D _shapeQuery;
    private PhysicsRayQueryParameters2D _rayQuery;
    private Godot.Collections.Array<Rid> _exclude;
    private List<Candidate> _candidates;
    private bool _running;

    [ExportGroup("Shape")]
    [Export]
    public TargetShape Shape
    {
        get => _shape;
        set
        {
            _shape = value;
            NotifyPropertyListChanged();
        }
    }

    /// <summary>Radius of Circle and Cone, length of Box and Ray.</summary>
    [Export(PropertyHint.Range, "0,2000,1,or_greater,suffix:px")]
    public float Range { get; set; } = 100f;

    /// <summary>Cone: full opening in degrees, centered on the direction.</summary>
    [Export(PropertyHint.Range, "1,360,1,suffix:°")]
    public float Angle { get; set; } = 90f;

    /// <summary>Box: width across the direction.</summary>
    [Export(PropertyHint.Range, "0,2000,1,or_greater,suffix:px")]
    public float Width { get; set; } = 50f;

    /// <summary>Moves the origin, turned with the direction: X forward, Y to the side (down when facing right).</summary>
    [Export]
    public Vector2 Offset { get; set; }

    [ExportGroup("Collision")]
    [Export(PropertyHint.Layers2DPhysics)]
    public uint CollisionMask { get; set; } = 1;

    [Export]
    public bool CollideWithBodies { get; set; } = true;

    [Export]
    public bool CollideWithAreas { get; set; } = true;

    /// <summary>Leaves out the source (and the actor it belongs to).</summary>
    [ExportGroup("Filter")]
    [Export]
    public bool ExcludeSource { get; set; } = true;

    /// <summary>Only actors with an ability system; off, any collider counts (walls, props).</summary>
    [Export]
    public bool RequireAbilitySystem { get; set; } = true;

    /// <summary>The target must have all of these.</summary>
    [Export]
    public TagContainer RequiredTags { get; set; }

    /// <summary>Targets with any of these are left out (e.g. "State.Dead").</summary>
    [Export]
    public TagContainer BlockedTags { get; set; }

    /// <summary>Most targets added; 0 for no limit. Applied after sorting (Nearest + 1 = the closest target).</summary>
    [Export(PropertyHint.Range, "0,100,1,or_greater")]
    public int MaxTargets { get; set; }

    [Export]
    public TargetSort Sort { get; set; }

    /// <summary>Searches from <paramref name="source"/>'s position toward <paramref name="direction"/>. See <see cref="RunAt"/>.</summary>
    public int Run(Node2D source, Vector2 direction, TargetData into, Func<Node, bool> filter = null) =>
        RunAt(source, GodotObject.IsInstanceValid(source) ? source.GlobalPosition : Vector2.Zero, direction, into, filter);

    /// <summary>
    /// Searches from <paramref name="origin"/> (e.g. where an area spell lands) toward <paramref name="direction"/>
    /// and adds the targets to <paramref name="into"/> without clearing it; actors already in it are skipped.
    /// <paramref name="filter"/> may leave out more actors. Returns how many were added. The source gives the world
    /// to search and is left out with <see cref="ExcludeSource"/>.
    /// </summary>
    public int RunAt(Node2D source, Vector2 origin, Vector2 direction, TargetData into, Func<Node, bool> filter = null)
    {
        if (into == null || !GodotObject.IsInstanceValid(source) || !source.IsInsideTree())
        {
            GD.PushError("TargetQuery2D: needs a source inside the tree and a TargetData to fill.");
            return 0;
        }

        if (_running)
        {
            GD.PushError("TargetQuery2D: run again from its own filter; use another query resource for that.");
            return 0;
        }

        _running = true;
        _candidates ??= new List<Candidate>(8);
        try
        {
            Vector2 forward = direction == Vector2.Zero ? Vector2.Right : direction.Normalized();
            float angle = forward.Angle();
            Vector2 center = origin + Offset.Rotated(angle);
            PhysicsDirectSpaceState2D space = source.GetWorld2D().DirectSpaceState;

            if (_shape == TargetShape.Ray)
                CollectRay(space, source, center, forward, into, filter);
            else
                CollectShape(space, source, center, forward, angle, into, filter);

            SortCandidates();
            int count = MaxTargets > 0 ? Math.Min(MaxTargets, _candidates.Count) : _candidates.Count;
            for (int i = 0; i < count; i++)
                into.Add(_candidates[i].Actor, _candidates[i].Point, _candidates[i].Normal);

            return count;
        }
        finally
        {
            // Not kept: the nodes may be freed before the next run.
            _candidates.Clear();
            _running = false;
        }
    }

    private void CollectShape(PhysicsDirectSpaceState2D space, Node2D source, Vector2 center, Vector2 forward, float angle,
        TargetData into, Func<Node, bool> filter)
    {
        _shapeQuery ??= new PhysicsShapeQueryParameters2D();
        if (_shape == TargetShape.Box)
        {
            _rectangle ??= new RectangleShape2D();
            _rectangle.Size = new Vector2(Range, Width);
            _shapeQuery.Shape = _rectangle;
            _shapeQuery.Transform = new Transform2D(angle, center + forward * (Range * 0.5f));
        }
        else
        {
            _circle ??= new CircleShape2D();
            _circle.Radius = Range;
            _shapeQuery.Shape = _circle;
            _shapeQuery.Transform = new Transform2D(0f, center);
        }

        _shapeQuery.CollisionMask = CollisionMask;
        _shapeQuery.CollideWithBodies = CollideWithBodies;
        _shapeQuery.CollideWithAreas = CollideWithAreas;

        float halfCone = Mathf.DegToRad(Angle) * 0.5f;
        Godot.Collections.Array<Godot.Collections.Dictionary> results = space.IntersectShape(_shapeQuery, MaxColliders);
        for (int i = 0; i < results.Count; i++)
        {
            var collider = results[i][ColliderKey].AsGodotObject() as Node2D;
            if (!Accepts(collider, source, into, filter, out Node actor))
                continue;

            Vector2 point = actor is Node2D actor2D ? actor2D.GlobalPosition : collider.GlobalPosition;
            if (_shape == TargetShape.Cone && point != center && Math.Abs(forward.AngleTo(point - center)) > halfCone)
                continue;

            _candidates.Add(new Candidate(actor, point, Vector2.Zero, center.DistanceSquaredTo(point)));
        }
    }

    private void CollectRay(PhysicsDirectSpaceState2D space, Node2D source, Vector2 center, Vector2 forward, TargetData into,
        Func<Node, bool> filter)
    {
        _rayQuery ??= new PhysicsRayQueryParameters2D();
        _rayQuery.From = center;
        _rayQuery.To = center + forward * Range;
        _rayQuery.CollisionMask = CollisionMask;
        _rayQuery.CollideWithBodies = CollideWithBodies;
        _rayQuery.CollideWithAreas = CollideWithAreas;

        // The ray stops at the first thing it hits, so the source must not be that thing.
        _exclude ??= new Godot.Collections.Array<Rid>();
        _exclude.Clear();
        if (ExcludeSource && source is CollisionObject2D sourceBody)
            _exclude.Add(sourceBody.GetRid());

        _rayQuery.Exclude = _exclude;

        using Godot.Collections.Dictionary hit = space.IntersectRay(_rayQuery);
        if (hit.Count == 0)
            return;

        var collider = hit[ColliderKey].AsGodotObject() as Node2D;
        if (!Accepts(collider, source, into, filter, out Node actor))
            return;

        Vector2 point = hit[PositionKey].AsVector2();
        _candidates.Add(new Candidate(actor, point, hit[NormalKey].AsVector2(), center.DistanceSquaredTo(point)));
    }

    /// <summary>Finds the collider's actor and checks the filters; false to leave it out.</summary>
    private bool Accepts(Node2D collider, Node2D source, TargetData into, Func<Node, bool> filter, out Node actor)
    {
        actor = null;
        if (collider == null)
            return false;

        IAbilitySystem system = IAbilitySystem.FindInParents(collider);
        if (system == null && RequireAbilitySystem)
            return false;

        actor = system?.Actor ?? collider;
        if (ExcludeSource && (actor == source || collider == source))
            return false;

        if (into.Contains(actor) || HasCandidate(actor))
            return false;

        TagCountContainer tags = system?.Tags;
        if (HasTags(RequiredTags) && (tags == null || !tags.HasAll(RequiredTags)))
            return false;

        if (HasTags(BlockedTags) && tags != null && tags.HasAny(BlockedTags))
            return false;

        return filter == null || filter(actor);
    }

    private bool HasCandidate(Node actor)
    {
        for (int i = 0; i < _candidates.Count; i++)
        {
            if (_candidates[i].Actor == actor)
                return true;
        }

        return false;
    }

    // Insertion sort: few targets, and no comparer allocation.
    private void SortCandidates()
    {
        if (Sort == TargetSort.None)
            return;

        bool nearest = Sort == TargetSort.Nearest;
        for (int i = 1; i < _candidates.Count; i++)
        {
            Candidate current = _candidates[i];
            int j = i - 1;
            while (j >= 0 && (nearest ? _candidates[j].DistanceSquared > current.DistanceSquared : _candidates[j].DistanceSquared < current.DistanceSquared))
            {
                _candidates[j + 1] = _candidates[j];
                j--;
            }

            _candidates[j + 1] = current;
        }
    }

    private static bool HasTags(TagContainer tags) => tags != null && tags.Count > 0;

    // Godot API: the property info arrives as a Godot Dictionary.
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (!Engine.IsEditorHint())
            return;

        bool visible = property["name"].AsString() switch
        {
            nameof(Angle) => _shape == TargetShape.Cone,
            nameof(Width) => _shape == TargetShape.Box,
            _ => true,
        };

        // Hidden fields keep the storage flag so their value is still saved.
        if (!visible)
            property["usage"] = (int)PropertyUsageFlags.Storage;
    }

    private readonly struct Candidate
    {
        public readonly Node Actor;
        public readonly Vector2 Point;
        public readonly Vector2 Normal;
        public readonly float DistanceSquared;

        public Candidate(Node actor, Vector2 point, Vector2 normal, float distanceSquared)
        {
            Actor = actor;
            Point = point;
            Normal = normal;
            DistanceSquared = distanceSquared;
        }
    }
}
