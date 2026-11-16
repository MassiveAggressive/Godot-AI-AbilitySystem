namespace AbilitySystem;

/// <summary>
/// Evaluation step of a modifier. Channels are evaluated in ascending order and each channel's result is the
/// base value of the next one, e.g. Channel0 for gear bonuses and Channel1 for a buff that scales the geared value.
/// </summary>
public enum ModifierEvaluationChannel
{
    // Explicit values: resources store the number and the order defines the evaluation order.
    Channel0 = 0,
    Channel1 = 1,
    Channel2 = 2,
    Channel3 = 3,
    Channel4 = 4,
    Channel5 = 5,
    Channel6 = 6,
    Channel7 = 7,
    Channel8 = 8,
    Channel9 = 9,
}
