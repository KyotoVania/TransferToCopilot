using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;
/*
[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Check Current Action State",
    story: "Check if unit is currently performing any action",
    category: "Condition Nodes",
    id: "Condition_CheckCurrentActionState_v1"
)]
public partial class CheckCurrentActionStateNode : Unity.Behavior.Condition
{
    private const string BB_IS_ATTACKING = "IsAttacking";
    private const string BB_IS_CAPTURING = "IsCapturing";
    private const string BB_IS_MOVING = "IsMoving";
    private const string BB_IS_DEFENDING = "IsDefending";

    private BlackboardVariable<bool> bbIsAttacking;
    private BlackboardVariable<bool> bbIsCapturing;
    private BlackboardVariable<bool> bbIsMoving;
    private BlackboardVariable<bool> bbIsDefending;

    private bool blackboardVariablesCached = false;

    public override bool IsTrue()
    {
        if (!CacheBlackboardVariables()) return false;

        bool isCurrentlyAttacking = bbIsAttacking?.Value ?? false;
        bool isCurrentlyCapturing = bbIsCapturing?.Value ?? false;
        bool isCurrentlyMoving = bbIsMoving?.Value ?? false;
        bool isCurrentlyDefending = bbIsDefending?.Value ?? false;

        bool isPerformingAction = isCurrentlyAttacking || isCurrentlyCapturing || 
                                 isCurrentlyMoving || isCurrentlyDefending;

        if (isPerformingAction)
        {
            Debug.Log($"Unit is currently performing action: Attack:{isCurrentlyAttacking}, " +
                     $"Capture:{isCurrentlyCapturing}, Move:{isCurrentlyMoving}, Defend:{isCurrentlyDefending}");
        }

        return isPerformingAction;
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null) return false;

        var blackboard = agent.BlackboardReference;
        
        blackboard.GetVariable(BB_IS_ATTACKING, out bbIsAttacking);
        blackboard.GetVariable(BB_IS_CAPTURING, out bbIsCapturing);
        blackboard.GetVariable(BB_IS_MOVING, out bbIsMoving);
        blackboard.GetVariable(BB_IS_DEFENDING, out bbIsDefending);

        blackboardVariablesCached = true;
        return true;
    }

    protected override void OnEnd()
    {
        blackboardVariablesCached = false;
    }
}*/