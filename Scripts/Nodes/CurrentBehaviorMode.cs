using System;
using Unity.Behavior;

/// <summary>
/// Enumeration of AI behavior modes for Unity Behavior Graph system.
/// Determines the overall strategy and decision-making approach of AI units.
/// </summary>
[BlackboardEnum]
public enum CurrentBehaviorMode
{
    /// <summary>Unit prioritizes defensive actions and area control.</summary>
    Defensive,
    /// <summary>Unit focuses on completing specific objectives.</summary>
    ObjectiveFocused
}
