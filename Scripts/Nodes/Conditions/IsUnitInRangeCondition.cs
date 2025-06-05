using System;
using Unity.Behavior;             // For base Condition class
using Unity.Behavior.GraphFramework; // For BlackboardVariable and BehaviorGraphAgent
using UnityEngine;
using Unity.Properties;           // For GeneratePropertyBag

[Serializable, GeneratePropertyBag]
[Condition(name: "Is Unit In Range",
           story: "Is Unit In Range",
           category: "My Conditions",
           id: "YOUR_UNIQUE_GUID_HERE_IsInUnitRange")] // Optional: Replace with a new GUID
public partial class IsInUnitInteractionRangeCondition : Unity.Behavior.Condition // Fully qualify
{
    // Blackboard variable names
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string TARGET_UNIT_VAR = "InteractionTargetUnit";

    // Cached Blackboard variables
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbTargetUnit; // Target can be any Unit
    private bool blackboardVariablesCached = false;

    // Store a reference to the agent for efficiency
    private BehaviorGraphAgent agent;

    public override void OnStart()
    {
        if (agent == null && GameObject != null)
        {
            agent = GameObject.GetComponent<BehaviorGraphAgent>();
        }
        blackboardVariablesCached = false;
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
                Debug.LogError("[IsInUnitInteractionRangeCondition] BehaviorGraphAgent or Blackboard not found.");
                blackboardVariablesCached = true; 
                return;
            }
        }
        var blackboard = agent.BlackboardReference;

        bool foundAll = true;
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit))
        {
            Debug.LogWarning($"[IsInUnitInteractionRangeCondition] Blackboard variable '{SELF_UNIT_VAR}' not found.");
            foundAll = false;
        }
        if (!blackboard.GetVariable(TARGET_UNIT_VAR, out bbTargetUnit))
        {
            Debug.LogWarning($"[IsInUnitInteractionRangeCondition] Blackboard variable '{TARGET_UNIT_VAR}' not found.");
            foundAll = false;
        }
        blackboardVariablesCached = foundAll;
    }

    public override bool IsTrue()
    {
        if (!blackboardVariablesCached)
        {
            CacheBlackboardVariables();
            if (!blackboardVariablesCached || bbSelfUnit == null || bbTargetUnit == null)
            {
                if (bbSelfUnit == null) Debug.LogWarning($"[IsInUnitInteractionRangeCondition] SelfUnit not available from Blackboard in IsTrue.");
                if (bbTargetUnit == null) Debug.LogWarning($"[IsInUnitInteractionRangeCondition] InteractionTargetUnit not available from Blackboard in IsTrue.");
                return false;
            }
        }
        
        var selfUnit = bbSelfUnit.Value;
        var targetUnit = bbTargetUnit.Value;

        if (selfUnit == null)
        {
             Debug.LogWarning("[IsInUnitInteractionRangeCondition] SelfUnit is null on Blackboard in IsTrue.");
            return false;
        }

        if (targetUnit == null)
        {
             Debug.LogWarning("[IsInUnitInteractionRangeCondition] InteractionTargetUnit is null on Blackboard in IsTrue.");
            return false;
        }
        
        bool isInRange = selfUnit.IsUnitInRange(targetUnit);
        if(isInRange) Debug.Log($"[IsInUnitInteractionRangeCondition] Unit '{selfUnit.name}' IS in range of unit '{targetUnit.name}'. Result: true");
         else Debug.Log($"[IsInUnitInteractionRangeCondition] Unit '{selfUnit.name}' is NOT in range of unit '{targetUnit.name}'. Result: false");
        return isInRange;
    }

    public override void OnEnd()
    {
        blackboardVariablesCached = false;
        bbSelfUnit = null;
        bbTargetUnit = null;
        // agent = null; 
    }
}