using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// What a cue scene is told when its cue runs (<see cref="ICueNotify"/>). The nodes may be null or freed; check them
/// with <see cref="GodotObject.IsInstanceValid"/>. Cues from code (<see cref="CueContainer.Execute"/>, abilities)
/// have no spec or effect:
/// <code>
/// var parameters = new CueParameters(sparkTag, enemy, instigator: Actor).WithLocation(point, normal);
/// IAbilitySystem.Find(enemy)?.Cues.Execute(sparkTag, parameters);
/// </code>
/// </summary>
public readonly struct CueParameters
{
    /// <summary>For an effect's cue: instigator, causer, level and hit come from <paramref name="spec"/>.</summary>
    public CueParameters(Tag tag, Node target, EffectSpec spec, ActiveEffect effect, int stackCount)
    {
        Tag = tag;
        Target = target;
        Spec = spec;
        Effect = effect;
        StackCount = stackCount;
        Instigator = spec?.Instigator;
        Causer = spec?.Causer;
        Level = spec?.Level ?? 1f;
        HasLocation = spec != null && spec.HasHit;
        Location = HasLocation ? spec.HitPoint : Vector3.Zero;
        Normal = HasLocation ? spec.HitNormal : Vector3.Zero;
    }

    /// <summary>For a cue from code: no spec, no effect, one stack. Add a place with <see cref="WithLocation(Vector2, Vector2)"/>.</summary>
    public CueParameters(Tag tag, Node target, Node instigator = null, Node causer = null, float level = 1f)
    {
        Tag = tag;
        Target = target;
        Spec = null;
        Effect = null;
        StackCount = 1;
        Instigator = instigator;
        Causer = causer;
        Level = level;
        HasLocation = false;
        Location = Vector3.Zero;
        Normal = Vector3.Zero;
    }

    private CueParameters(in CueParameters other, Tag tag, Vector3 location, Vector3 normal, bool hasLocation)
    {
        Tag = tag;
        Target = other.Target;
        Spec = other.Spec;
        Effect = other.Effect;
        StackCount = other.StackCount;
        Instigator = other.Instigator;
        Causer = other.Causer;
        Level = other.Level;
        HasLocation = hasLocation;
        Location = location;
        Normal = normal;
    }

    /// <summary>The cue tag on the effect, e.g. "Cue.Fire.Burn" (also when the scene comes from a parent tag's entry).</summary>
    public Tag Tag { get; }

    /// <summary>The actor showing the cue.</summary>
    public Node Target { get; }

    /// <summary>Null for cues from code.</summary>
    public EffectSpec Spec { get; }

    /// <summary>The active effect; null for instant effects and cues from code.</summary>
    public ActiveEffect Effect { get; }

    /// <summary>The effect's stacks when the cue ran; 1 for instant effects and cues from code.</summary>
    public int StackCount { get; }

    public Node Instigator { get; }

    public Node Causer { get; }

    public float Level { get; }

    /// <summary>Where it happened (global), e.g. the hit point; an executed scene opens there instead of at the actor.</summary>
    public Vector3 Location { get; }

    /// <summary>The surface direction at <see cref="Location"/>; zero if not known.</summary>
    public Vector3 Normal { get; }

    public bool HasLocation { get; }

    public Vector2 Location2D => new(Location.X, Location.Y);

    public Vector2 Normal2D => new(Normal.X, Normal.Y);

    public CueParameters WithLocation(Vector2 location, Vector2 normal = default) =>
        WithLocation(new Vector3(location.X, location.Y, 0f), new Vector3(normal.X, normal.Y, 0f));

    public CueParameters WithLocation(Vector3 location, Vector3 normal = default) => new(this, Tag, location, normal, true);

    /// <summary>The same parameters for another cue tag (one call showing several cue tags).</summary>
    internal CueParameters WithTag(Tag tag) => new(this, tag, Location, Normal, HasLocation);
}
