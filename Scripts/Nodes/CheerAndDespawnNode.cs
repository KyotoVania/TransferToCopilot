// File: Scripts/Nodes/CheerAndDespawnNode.cs
using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using System.Collections; // For IEnumerator
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Cheer And Despawn",
    story: "Cheer And Despawn",
    category: "My Actions",
    id: "YOUR_UNIQUE_ID_CheerAndDespawn" // Generate a new GUID
)]
public class CheerAndDespawnNode : Unity.Behavior.Action
{
    private const string SELF_UNIT_VAR = "SelfUnit";
    private BlackboardVariable<Unit> bbSelfUnit;
    private bool blackboardVariablesCached = false;
    private Unit selfUnitInstance = null;
    private Coroutine cheerCoroutine;

    protected override Status OnStart()
    {
        if (!CacheBlackboardVariables() || bbSelfUnit == null) return Status.Failure;

        selfUnitInstance = bbSelfUnit.Value;
        if (selfUnitInstance == null)
        {
            LogFailure($"'{SELF_UNIT_VAR}' value is null.", true);
            return Status.Failure;
        }

        // Stop any other unit actions explicitly if needed (e.g. movement)
        // Though the graph structure should prevent this if Cheer is high priority.
        // selfUnitInstance.StopAllMovementOrActions(); // Hypothetical method

        cheerCoroutine = selfUnitInstance.StartCoroutine(selfUnitInstance.PerformCheerAndDespawnCoroutine());
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        // The coroutine handles its own lifetime and destruction.
        // If the unit still exists, the coroutine is likely running.
        // If the unit is destroyed by the coroutine, this node will effectively stop.
        // For robustness, we can check if the GameObject is still active.
        if (selfUnitInstance == null || !selfUnitInstance.gameObject.activeInHierarchy)
        {
            // Unit has been despawned by the coroutine
            if (selfUnitInstance == null && Debug.isDebugBuild) Debug.LogWarning($"[CheerNode] SelfUnitInstance became null, assuming success.");
            return Status.Success;
        }
        return Status.Running; // Coroutine is managing the timing
    }

    protected override void OnEnd()
    {
        if (selfUnitInstance != null && cheerCoroutine != null)
        {
            // If the node is ended prematurely (e.g. graph abort), stop the cheer
            // This might be unlikely if it's a terminal action for the unit.
            selfUnitInstance.StopCoroutine(cheerCoroutine);
        }
        cheerCoroutine = null;
        selfUnitInstance = null;
        blackboardVariablesCached = false;
        bbSelfUnit = null;
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;
        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null)
        {
            LogFailure("Agent or BlackboardRef null.", true); return false;
        }
        var blackboard = agent.BlackboardReference;
        bool success = blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit);
        if(!success) LogFailure($"BBVar '{SELF_UNIT_VAR}' not found.", true);
        blackboardVariablesCached = success;
        return success;
    }
}