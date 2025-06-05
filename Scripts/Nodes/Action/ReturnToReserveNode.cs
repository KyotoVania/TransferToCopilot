using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;


[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Return To Defense Position",
    story: "Return to defensive position after eliminating threat",
    category: "Ally Actions",
    id: "AllyAction_ReturnToDefensePosition_v1"
)]
public class ReturnToDefensePositionNode : Unity.Behavior.Action
{
    // Blackboard Variables
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string FINAL_DESTINATION_POS_VAR = "FinalDestinationPosition";
    private const string IS_IN_DEFENSIVE_MODE_VAR = "IsInDefensiveMode";
    private const string SELECTED_ACTION_TYPE_VAR = "SelectedActionType";
    private const string IS_DEFENDING_VAR = "IsDefending";

    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    private BlackboardVariable<bool> bbIsInDefensiveMode;
    private BlackboardVariable<AIActionType> bbSelectedActionType;
    private BlackboardVariable<bool> bbIsDefending;
    
    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;

    protected override Status OnStart()
    {
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        
        if (!CacheBlackboardVariables())
        {
            Debug.LogError("[ReturnToDefensePositionNode] Failed to cache blackboard variables.", GameObject);
            return Status.Failure;
        }

        AllyUnit selfUnit = bbSelfUnit?.Value as AllyUnit;
        if (selfUnit == null)
        {
            Debug.LogError("[ReturnToDefensePositionNode] SelfUnit is null or not AllyUnit.", GameObject);
            return Status.Failure;
        }

        Vector2Int reservePos = bbFinalDestinationPosition?.Value ?? new Vector2Int(-1, -1);
        if (reservePos.x == -1)
        {
            Debug.LogError("[ReturnToDefensePositionNode] No reserve position set.", GameObject);
            return Status.Failure;
        }

        Tile currentTile = selfUnit.GetOccupiedTile();
        if (currentTile != null && 
            currentTile.column == reservePos.x && 
            currentTile.row == reservePos.y)
        {
            // Déjà sur la position de défense, reprendre la défense
            if (bbIsInDefensiveMode != null) bbIsInDefensiveMode.Value = true;
            if (bbIsDefending != null) bbIsDefending.Value = true;
            
            Debug.Log("[ReturnToDefensePositionNode] Already at defense position, resuming defense.", GameObject);
            return Status.Success;
        }

        // Pas sur la position, il faut y retourner
        if (bbSelectedActionType != null) bbSelectedActionType.Value = AIActionType.MoveToBuilding;
        if (bbIsDefending != null) bbIsDefending.Value = false; // Pas encore en défense active
        
        Debug.Log($"[ReturnToDefensePositionNode] Moving back to defense position ({reservePos.x},{reservePos.y})", GameObject);
        return Status.Success;
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        if (agent?.BlackboardReference == null) return false;
        
        var blackboard = agent.BlackboardReference;
        bool success = true;

        success &= blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit);
        success &= blackboard.GetVariable(FINAL_DESTINATION_POS_VAR, out bbFinalDestinationPosition);
        success &= blackboard.GetVariable(IS_IN_DEFENSIVE_MODE_VAR, out bbIsInDefensiveMode);
        success &= blackboard.GetVariable(SELECTED_ACTION_TYPE_VAR, out bbSelectedActionType);
        success &= blackboard.GetVariable(IS_DEFENDING_VAR, out bbIsDefending);

        blackboardVariablesCached = success;
        return success;
    }

    protected override Status OnUpdate()
    {
        return Status.Success; // Action instantanée
    }

    protected override void OnEnd()
    {
        blackboardVariablesCached = false;
        bbSelfUnit = null;
        bbFinalDestinationPosition = null;
        bbIsInDefensiveMode = null;
        bbSelectedActionType = null;
        bbIsDefending = null;
    }
}