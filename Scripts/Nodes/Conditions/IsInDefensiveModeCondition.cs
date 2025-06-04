using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Is In Defensive Mode",
    story: "Is In Defensive Mode",
    category: "Ally Conditions",
    id: "AllyCondition_IsInDefensiveMode_v1"
)]
public partial class IsInDefensiveModeCondition : Unity.Behavior.Condition
{
    private const string BB_IS_IN_DEFENSIVE_MODE = "IsInDefensiveMode";
    
    private BlackboardVariable<bool> bbIsInDefensiveMode;
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
                Debug.LogError("[IsInDefensiveModeCondition] BehaviorGraphAgent or Blackboard not found.", GameObject);
                return;
            }
        }
        
        var blackboard = agent.BlackboardReference;
        if (!blackboard.GetVariable(BB_IS_IN_DEFENSIVE_MODE, out bbIsInDefensiveMode))
        {
            Debug.LogWarning($"[IsInDefensiveModeCondition] Blackboard variable '{BB_IS_IN_DEFENSIVE_MODE}' not found.", GameObject);
        }
        
        blackboardVariableCached = true;
    }

    public override bool IsTrue()
    {
        if (bbIsInDefensiveMode == null)
        {
            CacheBlackboardVariable();
            if (bbIsInDefensiveMode == null) return false;
        }
        
        return bbIsInDefensiveMode.Value;
    }

    public override void OnEnd()
    {
        blackboardVariableCached = false;
        bbIsInDefensiveMode = null;
        base.OnEnd();
    }
}