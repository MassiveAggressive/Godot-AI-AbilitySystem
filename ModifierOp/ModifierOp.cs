namespace AbilitySystem;

/// <summary>
/// How a modifier combines with an attribute inside its <see cref="ModifierEvaluationChannel"/>.
/// The formula is on <see cref="Aggregator"/>.
/// </summary>
public enum ModifierOp
{
    // Explicit values: resources store the number, so existing entries must never be renumbered.

    /// <summary>Added to the channel's base value.</summary>
    Add = 0,

    /// <summary>Ratio (1.5 = +50%). Bonuses are summed: two 1.5 give ×2.0.</summary>
    Multiply = 1,

    /// <summary>Divisor (2 = half). Summed like <see cref="Multiply"/>: two 2 give ÷3.</summary>
    Divide = 2,

    /// <summary>Ratio (1.5 = +50%). Factors are multiplied: two 1.5 give ×2.25.</summary>
    MultiplyCompound = 3,

    /// <summary>Added after every multiplier and divisor of the channel.</summary>
    AddFinal = 4,

    /// <summary>Replaces the channel's result. The most recently added override of the channel wins.</summary>
    Override = 5,
}
