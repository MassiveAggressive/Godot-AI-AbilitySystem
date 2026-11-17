namespace AbilitySystem;

/// <summary>What happens when the duration of a stacked effect ends. Values are stored in resources; append new ones.</summary>
public enum StackExpirationPolicy
{
    /// <summary>The whole stack is removed.</summary>
    ClearStack = 0,

    /// <summary>One stack is removed and the duration starts again; the effect ends with its last stack.</summary>
    RemoveOneAndRefresh = 1,

    /// <summary>The duration starts again and the stack stays; remove it in code (e.g. on <c>StackCountChanged</c>).</summary>
    RefreshDuration = 2,
}
