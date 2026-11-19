using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// What an actor's ability system offers to code that only has the actor's node (targeting, hits): its attributes,
/// tags, effects, cues and team. <see cref="AbilitySystemComponent"/> implements it; lower layers use this instead of the
/// component, which would make them depend on everything the component holds.
/// </summary>
public interface IAbilitySystem
{
    /// <summary>The node the system belongs to.</summary>
    Node Actor { get; }

    AttributeContainer Attributes { get; }

    TagCountContainer Tags { get; }

    ActiveEffectContainer Effects { get; }

    /// <summary>The actor's cues, e.g. to show a hit spark on a target without an effect.</summary>
    CueContainer Cues { get; }

    /// <summary>The actor's team (e.g. "Team.Player"); <see cref="Tag.None"/> for none. See <see cref="GetAttitude(IAbilitySystem, IAbilitySystem)"/>.</summary>
    Tag Team { get; }

    /// <summary>
    /// How <paramref name="from"/> stands toward <paramref name="to"/>: the same team (or the same system) is
    /// friendly, two different teams are hostile, and without a team on either side (or without a system) neutral.
    /// Teams match exactly: "Team.Monster.Orc" and "Team.Monster.Goblin" are hostile.
    /// </summary>
    static TeamAttitude GetAttitude(IAbilitySystem from, IAbilitySystem to)
    {
        if (from != null && from == to)
            return TeamAttitude.Friendly;

        Tag fromTeam = from?.Team ?? Tag.None;
        Tag toTeam = to?.Team ?? Tag.None;
        if (!fromTeam.IsValid || !toTeam.IsValid)
            return TeamAttitude.Neutral;

        return fromTeam.Id == toTeam.Id ? TeamAttitude.Friendly : TeamAttitude.Hostile;
    }

    /// <summary>The attitude between the systems of two nodes (or of their nearest ancestors with one, e.g. a hitbox).</summary>
    static TeamAttitude GetAttitude(Node from, Node to) => GetAttitude(FindInParents(from), FindInParents(to));

    /// <summary><paramref name="node"/> itself or its first direct child that implements this; null if none.</summary>
    static IAbilitySystem Find(Node node)
    {
        if (!GodotObject.IsInstanceValid(node))
            return null;

        if (node is IAbilitySystem self)
            return self;

        int count = node.GetChildCount();
        for (int i = 0; i < count; i++)
        {
            if (node.GetChild(i) is IAbilitySystem child)
                return child;
        }

        return null;
    }

    /// <summary>
    /// The system of <paramref name="node"/> or of its nearest ancestor that has one; null if none. For colliders:
    /// a hitbox <c>Area2D</c> under the actor finds the actor's system.
    /// </summary>
    static IAbilitySystem FindInParents(Node node)
    {
        for (Node current = node; GodotObject.IsInstanceValid(current); current = current.GetParent())
        {
            IAbilitySystem system = Find(current);
            if (system != null)
                return system;
        }

        return null;
    }
}
