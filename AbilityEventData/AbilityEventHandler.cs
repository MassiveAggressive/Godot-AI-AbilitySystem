namespace AbilitySystem;

/// <summary>Receives an ability event; <c>in</c>, so the struct is not copied.</summary>
public delegate void AbilityEventHandler(in AbilityEventData data);
