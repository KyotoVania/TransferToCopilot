using System;
using Unity.Behavior;

/// <summary>
/// Enumeration defining all possible AI action types for the Unity Behavior Graph system.
/// Used to communicate the current action state of AI units through the blackboard system.
/// This enum enables coordination between different behavior nodes and external systems.
/// </summary>
[BlackboardEnum]
public enum AIActionType
{
    /// <summary>
    /// No action is currently being performed by the AI unit.
    /// Default state when the unit is idle or waiting for orders.
    /// </summary>
    None,
    
    /// <summary>
    /// Unit is actively moving towards a building target.
    /// Used for capture, healing, or positioning operations involving structures.
    /// </summary>
    MoveToBuilding,
    
    /// <summary>
    /// Unit is moving towards another unit target.
    /// Used for combat engagement, support actions, or formation positioning.
    /// </summary>
    MoveToUnit,
    
    /// <summary>
    /// Unit is actively attacking another unit in combat.
    /// Includes melee attacks, ranged attacks, and special combat abilities.
    /// </summary>
    AttackUnit,
    
    /// <summary>
    /// Unit is attacking a building structure.
    /// Used for siege warfare, building destruction, or area denial tactics.
    /// </summary>
    AttackBuilding,
    
    /// <summary>
    /// Unit is in the process of capturing a neutral or enemy building.
    /// Involves channeling time and potential interruption by enemy forces.
    /// </summary>
    CaptureBuilding,
    
    /// <summary>
    /// Unit is performing a victory celebration and will be removed from the battlefield.
    /// Final action for units that have completed their objectives successfully.
    /// </summary>
    CheerAndDespawn,
    
    /// <summary>
    /// Unit is actively defending a strategic position or area.
    /// Involves staying in position and engaging nearby threats while maintaining formation.
    /// </summary>
    DefendPosition
}
