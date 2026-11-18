using Godot;

namespace AbilitySystem;

/// <summary>
/// One entry of a <see cref="TargetData"/>: an actor, a point, or both. Points are stored in 3D; 2D games read
/// <see cref="Point2D"/> and <see cref="Normal2D"/>.
/// </summary>
public readonly struct TargetHit
{
    public TargetHit(Node actor, Vector3 point, Vector3 normal, bool hasPoint)
    {
        Actor = actor;
        Point = point;
        Normal = normal;
        HasPoint = hasPoint;
    }

    /// <summary>The targeted actor; null for a point only (e.g. a ground target). May be freed by the time it is read.</summary>
    public Node Actor { get; }

    /// <summary>Global position of the hit; valid when <see cref="HasPoint"/>.</summary>
    public Vector3 Point { get; }

    /// <summary>Surface normal at the hit; zero when not known.</summary>
    public Vector3 Normal { get; }

    public bool HasPoint { get; }

    public Vector2 Point2D => new(Point.X, Point.Y);

    public Vector2 Normal2D => new(Normal.X, Normal.Y);
}
