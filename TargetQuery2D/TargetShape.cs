namespace AbilitySystem;

/// <summary>The area a <see cref="TargetQuery2D"/> searches, placed at the origin and turned toward the direction.</summary>
public enum TargetShape
{
    /// <summary>Everything within <see cref="TargetQuery2D.Range"/> around the origin.</summary>
    Circle = 0,

    /// <summary>A circle cut to <see cref="TargetQuery2D.Angle"/> degrees around the direction (by the target's position).</summary>
    Cone = 1,

    /// <summary>A rectangle <see cref="TargetQuery2D.Range"/> long and <see cref="TargetQuery2D.Width"/> wide, starting at the origin.</summary>
    Box = 2,

    /// <summary>A line <see cref="TargetQuery2D.Range"/> long; only the first thing it hits, with the hit point and normal.</summary>
    Ray = 3,
}
