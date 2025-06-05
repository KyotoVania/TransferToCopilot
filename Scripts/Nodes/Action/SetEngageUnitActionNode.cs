using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Set Engage Unit Action",
    story: "Sets up engagement with detected enemy unit",
    category: "My Actions",
    id: "AllyAction_SetEngageUnitAction_v1"
)]
public class SetEngageUnitActionNode : Unity.Behavior.Action
{
    private const string BB_DETECTED_ENEMY_UNIT = "DetectedEnemyUnit";
    private const string BB_INTERACTION_TARGET_UNIT = "InteractionTargetUnit";
    private const string BB_FINAL_DESTINATION_POSITION = "FinalDestinationPosition";
    private const string BB_SELECTED_ACTION_TYPE = "SelectedActionType";
    private BlackboardVariable<Unit> bbDetectedEnemyUnit;
    private BlackboardVariable<Unit> bbInteractionTargetUnit;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    private BlackboardVariable<string> bbSelectedActionType;
    private BlackboardVariable<Unit> bbSelfUnit;
    private const string BB_SELF_UNIT = "SelfUnit";
    private bool blackboardVariablesCached = false;

    protected override Status OnStart()
    {
        if (!CacheBlackboardVariables()) return Status.Failure;
        if (bbDetectedEnemyUnit.Value == null) return Status.Failure;

        // Set InteractionTargetUnit
        bbInteractionTargetUnit.Value = bbDetectedEnemyUnit.Value;

        // Set FinalDestinationPosition to enemy's tile position
        var enemyTile = bbDetectedEnemyUnit.Value.GetOccupiedTile();
        if (enemyTile != null)
        {
            bbFinalDestinationPosition.Value = new Vector2Int(enemyTile.column, enemyTile.row);
        }
        else
        {
            Debug.LogWarning("[SetEngageUnitActionNode] Enemy unit has no occupied tile.");
            return Status.Failure;
        }

        // Decide action type
        var selfUnit = bbSelfUnit.Value;
        bool inRange = selfUnit != null && selfUnit.IsUnitInRange(bbDetectedEnemyUnit.Value);
        bbSelectedActionType.Value = inRange ? "AttackUnit" : "MoveToUnit";

        return Status.Success;
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;
        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null) return false;
        var blackboard = agent.BlackboardReference;
        bool ok = true;
        ok &= blackboard.GetVariable(BB_DETECTED_ENEMY_UNIT, out bbDetectedEnemyUnit);
        ok &= blackboard.GetVariable(BB_INTERACTION_TARGET_UNIT, out bbInteractionTargetUnit);
        ok &= blackboard.GetVariable(BB_FINAL_DESTINATION_POSITION, out bbFinalDestinationPosition);
        ok &= blackboard.GetVariable(BB_SELECTED_ACTION_TYPE, out bbSelectedActionType);
        ok &= blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit);
        blackboardVariablesCached = ok;
        return ok;
    }
}