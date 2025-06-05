using System;
using Unity.Behavior;             // For base Condition class
using Unity.Behavior.GraphFramework; // For BlackboardVariable and BehaviorGraphAgent
using UnityEngine;
using Unity.Properties;           // For GeneratePropertyBag

[Serializable, GeneratePropertyBag]
[Condition(name: "Is Building In Range",
           story: "Is Building In Range",
           category: "My Conditions", // Ensure this category matches how you want to organize
           id: "YOUR_UNIQUE_GUID_HERE_IsInBuildingRange")] // Optional: Replace with a new GUID
public partial class IsInBuildingInteractionRangeCondition : Unity.Behavior.Condition // Fully qualify if needed
{
    // Blackboard variable names
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string TARGET_BUILDING_VAR = "InteractionTargetBuilding";

    // Cached Blackboard variables
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Building> bbTargetBuilding;
    private bool blackboardVariablesCached = false;

    // Store a reference to the agent for efficiency
    private BehaviorGraphAgent agent;

    public override void OnStart()
    {
        // It's good practice to ensure the agent is fetched once.
        // The GameObject property in the base Condition class refers to the agent's GameObject.
        if (agent == null && GameObject != null)
        {
            agent = GameObject.GetComponent<BehaviorGraphAgent>();
        }

        blackboardVariablesCached = false; // Reset cache flag for fresh fetch
        CacheBlackboardVariables();
    }

    private void CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return;

        if (agent == null || agent.BlackboardReference == null)
        {
            // Try to get it one last time if OnStart hasn't set it (e.g. if GameObject was null then)
            if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();

            if (agent == null || agent.BlackboardReference == null)
            {
                Debug.LogError("[IsInBuildingInteractionRangeCondition] BehaviorGraphAgent or Blackboard not found.");
                // Variables will remain null, IsTrue will fail safely.
                // Set blackboardVariablesCached to true ONLY if we consider this a non-recoverable setup error
                // for this evaluation cycle, to prevent spamming logs.
                // However, if the agent might appear later, we might want to keep trying.
                // For simplicity, let's assume if it's not here by now, it's an issue.
                blackboardVariablesCached = true; // Prevent further attempts if fundamental setup is missing
                return;
            }
        }
        var blackboard = agent.BlackboardReference;

        bool foundAll = true;
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit))
        {
            Debug.LogWarning($"[IsInBuildingInteractionRangeCondition] Blackboard variable '{SELF_UNIT_VAR}' not found.");
            foundAll = false;
        }
        if (!blackboard.GetVariable(TARGET_BUILDING_VAR, out bbTargetBuilding))
        {
            Debug.LogWarning($"[IsInBuildingInteractionRangeCondition] Blackboard variable '{TARGET_BUILDING_VAR}' not found.");
            foundAll = false;
        }
        blackboardVariablesCached = foundAll; // Only true if all *required* variables were found
    }

    public override bool IsTrue()
    {
        // Try to cache if not done yet (e.g., if OnStart was skipped or failed partially)
        if (!blackboardVariablesCached)
        {
            CacheBlackboardVariables();
            // If still not successfully cached (e.g., BB vars missing), treat as failure.
            if (!blackboardVariablesCached || bbSelfUnit == null || bbTargetBuilding == null)
            {
                 // Log only if essential variables are missing after trying to cache
                 if (bbSelfUnit == null) Debug.LogWarning($"[IsInBuildingInteractionRangeCondition] SelfUnit not available from Blackboard in IsTrue.");
                 if (bbTargetBuilding == null) Debug.LogWarning($"[IsInBuildingInteractionRangeCondition] InteractionTargetBuilding not available from Blackboard in IsTrue.");
                 return false; // Condition fails if setup is incomplete
            }
        }

        var selfUnit = bbSelfUnit.Value; // Safe to access .Value if bbSelfUnit itself is not null
        var targetBuilding = bbTargetBuilding.Value;

        if (selfUnit == null)
        {
             Debug.LogWarning("[IsInBuildingInteractionRangeCondition] SelfUnit is null on Blackboard in IsTrue.");
            return false;
        }

        if (targetBuilding == null)
        {
            // No building targeted, so not in range of "no building".
             Debug.LogWarning("[IsInBuildingInteractionRangeCondition] InteractionTargetBuilding is null on Blackboard in IsTrue.");
            return false;
        }

        // Your AllyUnit or Unit class should have IsBuildingInRange.
        // Ensure it's public or protected internal and accessible.
        bool isInRange = selfUnit.IsBuildingInRange(targetBuilding);
        if(isInRange) Debug.Log($"[IsInBuildingInteractionRangeCondition] Unit '{selfUnit.name}' IS in range of building '{targetBuilding.name}'. Result: true");
        else Debug.Log($"[IsInBuildingInteractionRangeCondition] Unit '{selfUnit.name}' is NOT in range of building '{targetBuilding.name}'. Result: false");
        return isInRange;
    }

    public override void OnEnd()
    {
        // Reset cache flag and variable references for next time
        blackboardVariablesCached = false;
        bbSelfUnit = null;
        bbTargetBuilding = null;
        // agent = null; // Agent reference can persist as it's tied to the GameObject
    }
}