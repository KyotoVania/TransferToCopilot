using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using System.Collections.Generic;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[Condition(name: "Is Unit In Range",
           story: "Is Unit In Range",
           category: "My Conditions",
           id: "YOUR_UNIQUE_GUID_HERE_IsInUnitRange")] // Optional: Replace with a new GUID
public partial class IsInUnitInteractionRangeCondition : Unity.Behavior.Condition // Fully qualify
{
    private const string BB_SELF_UNIT = "SelfUnit";
    private const string BB_INTERACTION_TARGET_UNIT = "InteractionTargetUnit";

    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbInteractionTargetUnit;

    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;

    public override void OnStart()
    {
        base.OnStart();
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        CacheBlackboardVariables();
    }

    public override bool IsTrue()
    {
        if (!blackboardVariablesCached)
        {
            if (!CacheBlackboardVariables()) return false;
        }

        var selfUnit = bbSelfUnit?.Value;
        var targetUnit = bbInteractionTargetUnit?.Value;

        if (selfUnit == null || targetUnit == null) return false;

        // Use the unit's standard AttackRange. No special bonuses.
        int attackRange = selfUnit.AttackRange;

        Tile selfTile = selfUnit.GetOccupiedTile();
        // This correctly gets all tiles for multi-tile units like the boss.
        List<Tile> targetTiles = targetUnit.GetOccupiedTiles();

        if (selfTile == null || targetTiles.Count == 0 || HexGridManager.Instance == null) return false;

        foreach (var targetTile in targetTiles)
        {
            if (targetTile != null)
            {
                int distance = HexGridManager.Instance.HexDistance(selfTile.column, selfTile.row, targetTile.column, targetTile.row);
                if (distance <= attackRange)
                {
                    return true; // As soon as one part of the target is in range, we're good.
                }
            }
        }

        return false;
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;
        if (agent == null || agent.BlackboardReference == null) return false;

        var blackboard = agent.BlackboardReference;
        bool success = true;

        if (!blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit)) success = false;
        if (!blackboard.GetVariable(BB_INTERACTION_TARGET_UNIT, out bbInteractionTargetUnit)) success = false;

        blackboardVariablesCached = success;
        return success;
    }

    public override void OnEnd()
    {
        blackboardVariablesCached = false;
        agent = null;
        base.OnEnd();
    }
}