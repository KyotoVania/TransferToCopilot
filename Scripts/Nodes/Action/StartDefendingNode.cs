using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Start Defending",
    story: "Sets the unit into active defending mode (IsDefending = true)",
    category: "Ally Actions",
    id: "AllyAction_StartDefending_v1"
)]
public partial class StartDefendingNode : Unity.Behavior.Action
{
    private const string BB_IS_DEFENDING = "IsDefending";
    private const string BB_SELF_UNIT = "SelfUnit";
    
    private BlackboardVariable<bool> bbIsDefending;
    private BlackboardVariable<Unit> bbSelfUnit;
    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;

    protected override Status OnStart()
    {
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        
        if (!CacheBlackboardVariables())
        {
            Debug.LogError("[StartDefendingNode] Failed to cache blackboard variables.", GameObject);
            return Status.Failure;
        }

        AllyUnit selfUnit = bbSelfUnit?.Value as AllyUnit;
        if (selfUnit == null)
        {
            Debug.LogError("[StartDefendingNode] SelfUnit is null or not an AllyUnit.", GameObject);
            return Status.Failure;
        }

        // Activer le mode défense
        if (bbIsDefending != null)
        {
            bbIsDefending.Value = true;
            Debug.Log($"[StartDefendingNode] Unit '{selfUnit.name}' is now actively defending.", GameObject);
        }
        
        return Status.Success;
    }

    protected override Status OnUpdate()
    {
        return Status.Success; // Action instantanée
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        if (agent == null || agent.BlackboardReference == null)
        {
            Debug.LogError("[StartDefendingNode] Agent or BlackboardReference missing.", GameObject);
            return false;
        }

        var blackboard = agent.BlackboardReference;
        bool success = true;

        if (!blackboard.GetVariable(BB_IS_DEFENDING, out bbIsDefending))
        {
            Debug.LogError($"[StartDefendingNode] '{BB_IS_DEFENDING}' not found.", GameObject);
            success = false;
        }
        
        if (!blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit))
        {
            Debug.LogError($"[StartDefendingNode] '{BB_SELF_UNIT}' not found.", GameObject);
            success = false;
        }

        blackboardVariablesCached = success;
        return success;
    }

    protected override void OnEnd()
    {
        blackboardVariablesCached = false;
        bbIsDefending = null;
        bbSelfUnit = null;
    }
}