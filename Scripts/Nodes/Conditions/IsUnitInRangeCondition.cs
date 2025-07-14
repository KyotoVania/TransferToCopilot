using System;
using System.Collections.Generic; 
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using UnityEngine;
using Unity.Properties;
using ScriptableObjects; 

/// <summary>
/// Unity Behavior Graph condition node that checks if a unit is within interaction range of another unit.
/// Handles both standard single-tile units and multi-tile boss units with special range checking logic.
/// </summary>
[Serializable, GeneratePropertyBag]
[Condition(name: "Is Unit In Range",
           story: "Is Unit In Range",
           category: "My Conditions",
           id: "a0a3a7d2-7b19-4b8a-9c7c-1e6e9f1e1f1e")]
public partial class IsInUnitInteractionRangeCondition : Unity.Behavior.Condition
{
    // BLACKBOARD VARIABLE NAMES
    /// <summary>Blackboard variable name for the unit performing the range check.</summary>
    private const string SELF_UNIT_VAR = "SelfUnit";
    /// <summary>Blackboard variable name for the target unit to check range against.</summary>
    private const string TARGET_UNIT_VAR = "InteractionTargetUnit";

    // CACHED BLACKBOARD VARIABLES
    /// <summary>Cached blackboard reference to the unit performing the range check.</summary>
    private BlackboardVariable<Unit> bbSelfUnit;
    /// <summary>Cached blackboard reference to the target unit.</summary>
    private BlackboardVariable<Unit> bbTargetUnit;
    /// <summary>Whether blackboard variables have been successfully cached.</summary>
    private bool blackboardVariablesCached = false;
    /// <summary>Cached reference to the behavior graph agent.</summary>
    private BehaviorGraphAgent agent;

    /// <summary>
    /// Initializes the condition node and resets variable cache.
    /// </summary>
    public override void OnStart()
    {
        if (agent == null && GameObject != null)
        {
            agent = GameObject.GetComponent<BehaviorGraphAgent>();
        }
        // Reset cache to get the most recent data
        blackboardVariablesCached = false;
    }

    /// <summary>
    /// Caches blackboard variables for faster access.
    /// </summary>
    private void CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return;

        if (agent == null || agent.BlackboardReference == null)
        {
            Debug.LogError("[IsInUnitInteractionRangeCondition] BehaviorGraphAgent or Blackboard not found.", GameObject);
            return;
        }
        var blackboard = agent.BlackboardReference;

        bool foundAll = true;
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit))
        {
            Debug.LogWarning($"[IsInUnitInteractionRangeCondition] Blackboard variable '{SELF_UNIT_VAR}' not found.", GameObject);
            foundAll = false;
        }
        if (!blackboard.GetVariable(TARGET_UNIT_VAR, out bbTargetUnit))
        {
            Debug.LogWarning($"[IsInUnitInteractionRangeCondition] Blackboard variable '{TARGET_UNIT_VAR}' not found.", GameObject);
            foundAll = false;
        }
        
        // Cache is considered successful only if both essential variables are found
        if (foundAll)
        {
            blackboardVariablesCached = true;
        }
    }

    /// <summary>
    /// Evaluates the condition to determine if the unit is within range.
    /// </summary>
    /// <returns>True if the unit is within interaction range of the target.</returns>
    public override bool IsTrue()
    {
        // Attempt to cache variables if not already done
        if (!blackboardVariablesCached)
        {
            CacheBlackboardVariables();
        }

        // If references are still null after cache attempt, condition fails
        if (bbSelfUnit == null || bbTargetUnit == null)
        {
            return false;
        }
        
        var selfUnit = bbSelfUnit.Value;
        var targetUnit = bbTargetUnit.Value;

        // Fail gracefully if units are not defined in the blackboard
        if (selfUnit == null || targetUnit == null)
        {
            return false;
        }

        // --- MERGED LOGIC ---
        // For Boss type units, we use special verification that iterates over all their tiles.
        // For standard units, we rely on the original and stable IsUnitInRange method.
        if (targetUnit.GetUnitType() == UnitType.Boss)
        {
            // Robust logic for multi-tile boss targets
            Tile selfTile = selfUnit.GetOccupiedTile();
            List<Tile> targetTiles = targetUnit.GetOccupiedTiles(); // Gets all boss tiles correctly

            if (selfTile == null || targetTiles.Count == 0 || HexGridManager.Instance == null)
            {
                return false;
            }

            // Check distance to each boss tile
            foreach (var targetTile in targetTiles)
            {
                if (targetTile != null)
                {
                    int distance = HexGridManager.Instance.HexDistance(selfTile.column, selfTile.row, targetTile.column, targetTile.row);
                    if (distance <= selfUnit.AttackRange)
                    {
                        return true; // Success as soon as any part of the boss is in range
                    }
                }
            }
            return false; // No part of the boss was in range
        }
        else
        {
            bool isInRange = selfUnit.IsUnitInRange(targetUnit);
            if (!isInRange)
            {
                Debug.LogWarning($"[IsInUnitInteractionRangeCondition] {selfUnit.name} is not in range of {targetUnit.name}.", GameObject);
            }
            
            return selfUnit.IsUnitInRange(targetUnit);
        }
    }

    /// <summary>
    /// Cleans up the condition node state for the next execution.
    /// </summary>
    public override void OnEnd()
    {
        // Reset state for next node execution
        blackboardVariablesCached = false;
        bbSelfUnit = null;
        bbTargetUnit = null;
    }
}