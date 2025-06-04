using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Is At Reserve Position",
    story: "Is At Reserve Position",
    category: "Ally Conditions",
    id: "AllyCondition_IsAtReservePosition_v1"
)]
public partial class IsAtReservePositionCondition : Unity.Behavior.Condition
{
    private const string BB_SELF_UNIT = "SelfUnit";
    private const string BB_RESERVE_POSITION_ASSIGNED = "ReservePositionAssigned";
    
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<bool> bbReservePositionAssigned;
    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;

    public override void OnStart()
    {
        base.OnStart();
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        CacheBlackboardVariables();
    }

    private void CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return;
        
        if (agent == null || agent.BlackboardReference == null)
        {
            if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
            if (agent == null || agent.BlackboardReference == null)
            {
                Debug.LogError("[IsAtReservePositionCondition] BehaviorGraphAgent or Blackboard not found.", GameObject);
                return;
            }
        }
        
        var blackboard = agent.BlackboardReference;
        
        if (!blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit))
        {
            Debug.LogWarning($"[IsAtReservePositionCondition] Blackboard variable '{BB_SELF_UNIT}' not found.", GameObject);
        }
        
        if (!blackboard.GetVariable(BB_RESERVE_POSITION_ASSIGNED, out bbReservePositionAssigned))
        {
            Debug.LogWarning($"[IsAtReservePositionCondition] Blackboard variable '{BB_RESERVE_POSITION_ASSIGNED}' not found.", GameObject);
        }
        
        blackboardVariablesCached = true;
    }

    public override bool IsTrue()
    {
        if (bbSelfUnit == null || bbReservePositionAssigned == null)
        {
            CacheBlackboardVariables();
            if (bbSelfUnit == null || bbReservePositionAssigned == null) return false;
        }
        
        AllyUnit allyUnit = bbSelfUnit.Value as AllyUnit;
        if (allyUnit == null) return false;
        
        // Vérifier si une position de réserve est assignée
        bool hasReserveAssigned = bbReservePositionAssigned.Value;
        if (!hasReserveAssigned) return false;
        
        // Vérifier si l'unité est effectivement à sa position de réserve
        if (allyUnit.currentReserveTile == null) return false;
        
        Tile currentTile = allyUnit.GetOccupiedTile();
        if (currentTile == null) return false;
        
        return (currentTile == allyUnit.currentReserveTile);
    }

    public override void OnEnd()
    {
        blackboardVariablesCached = false;
        bbSelfUnit = null;
        bbReservePositionAssigned = null;
        base.OnEnd();
    }
}