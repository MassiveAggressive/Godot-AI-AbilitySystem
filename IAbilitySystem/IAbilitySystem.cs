using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// What an actor's ability system offers to code that only has the actor's node (targeting, hits): its attributes,
/// tags and effects. <see cref="AbilitySystemComponent"/> implements it; lower layers use this instead of the
/// component, which would make them depend on everything the component holds.
/// </summary>
public interface IAbilitySystem
{
    /// <summary>The node the system belongs to.</summary>
    Node Actor { get; }

    AttributeContainer Attributes { get; }

    TagCountContainer Tags { get; }

    ActiveEffectContainer Effects { get; }

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
