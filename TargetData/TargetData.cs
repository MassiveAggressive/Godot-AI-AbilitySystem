using System.Collections.Generic;
using Godot;

namespace AbilitySystem;

/// <summary>
/// The result of targeting: actors and/or points an ability acts on. Filled by code or by a query
/// (<see cref="TargetQuery2D"/>), then used with e.g. <c>ApplyEffectToTargets</c>. Keep one per ability and
/// <see cref="Clear"/> it before each use; once its list has grown, reusing it allocates nothing.
/// <code>
/// _targets.Clear();
/// Area.Run((Node2D)Actor, facing, _targets);
/// ApplyEffectToTargets(Damage, _targets);
/// </code>
/// Data received with an event belongs to the sender, who may reuse it; copy it (<see cref="CopyFrom"/>) to keep it.
/// </summary>
public sealed class TargetData
{
    private readonly List<TargetHit> _hits = new(4);

    public int Count => _hits.Count;

    public TargetHit this[int index] => _hits[index];

    /// <summary>An actor without a point.</summary>
    public void Add(Node actor) => _hits.Add(new TargetHit(actor, Vector3.Zero, Vector3.Zero, false));

    public void Add(Node actor, Vector2 point, Vector2 normal = default) =>
        _hits.Add(new TargetHit(actor, new Vector3(point.X, point.Y, 0f), new Vector3(normal.X, normal.Y, 0f), true));

    public void Add(Node actor, Vector3 point, Vector3 normal = default) => _hits.Add(new TargetHit(actor, point, normal, true));

    public void Add(in TargetHit hit) => _hits.Add(hit);

    /// <summary>A point without an actor (e.g. where an area spell lands).</summary>
    public void AddPoint(Vector2 point) => Add(null, point);

    public void AddPoint(Vector3 point) => Add(null, point);

    /// <summary>True if an entry has <paramref name="actor"/>.</summary>
    public bool Contains(Node actor)
    {
        if (actor == null)
            return false;

        for (int i = 0; i < _hits.Count; i++)
        {
            if (_hits[i].Actor == actor)
                return true;
        }

        return false;
    }

    public void Clear() => _hits.Clear();

    /// <summary>Replaces the entries with <paramref name="other"/>'s.</summary>
    public void CopyFrom(TargetData other)
    {
        _hits.Clear();
        if (other != null && other != this)
            _hits.AddRange(other._hits);
    }
}
