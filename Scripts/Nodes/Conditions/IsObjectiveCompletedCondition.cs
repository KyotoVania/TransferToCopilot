using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Is Objective Completed",
    story: "Checks if the objective is completed",
    category: "Ally Conditions",
    id: "AllyCondition_IsObjectiveCompleted_v1"
)]
public partial class IsObjectiveCompletedCondition : Unity.Behavior.Condition
{
    private const string BB_IS_OBJECTIVE_COMPLETED = "IsObjectiveCompleted";

    private BlackboardVariable<bool> bbIsObjectiveCompleted;
    private bool blackboardVariableCached = false;
    private BehaviorGraphAgent agent;

    public override void OnStart()
    {
        base.OnStart();
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        CacheBlackboardVariable();
    }

    private void CacheBlackboardVariable()
    {
        if (blackboardVariableCached) return;

        if (agent == null || agent.BlackboardReference == null)
        {
            if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
            if (agent == null || agent.BlackboardReference == null)
            {
                Debug.LogError("[IsObjectiveCompletedCondition] BehaviorGraphAgent or Blackboard not found.", GameObject);
                return;
            }
        }

        var blackboard = agent.BlackboardReference;
        if (!blackboard.GetVariable(BB_IS_OBJECTIVE_COMPLETED, out bbIsObjectiveCompleted))
        {
            Debug.LogWarning($"[IsObjectiveCompletedCondition] Blackboard variable '{BB_IS_OBJECTIVE_COMPLETED}' not found.", GameObject);
        }

        blackboardVariableCached = true;
    }

    public override bool IsTrue()
    {
        if (bbIsObjectiveCompleted == null)
        {
            CacheBlackboardVariable();
            if (bbIsObjectiveCompleted == null) return false;
        }

        return bbIsObjectiveCompleted.Value;
    }

    public override void OnEnd()
    {
        blackboardVariableCached = false;
        bbIsObjectiveCompleted = null;
        base.OnEnd();
    }
}