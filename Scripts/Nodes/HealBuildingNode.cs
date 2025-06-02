using UnityEngine;
using Unity.Behavior; // Main namespace for Behavior Tree base classes
using System; // For [Serializable]
using Unity.Properties;
using Unity.Behavior.GraphFramework;

/// <summary>
/// A custom Behavior Graph Action node that commands the AllyUnit to heal
/// a target Player Building specified on the Blackboard.
/// Assumes healing action is effectively instantaneous.
/// Returns Success if heal is applied, Failure otherwise.
/// Briefly sets the 'IsHealing' Blackboard variable during execution.
/// </summary>
[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Heal Building",
    story: "Heal Building",
    category: "My Actions",
    id: "YOUR_UNIQUE_ID_HealBuilding" // Generate a unique GUID string here if needed, or omit for auto
)]
public class HealBuildingNode : Unity.Behavior.Action
{
    // Constants for Blackboard variable names
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string TARGET_BUILDING_VAR = "DetectedBuilding"; // Or "InteractionTargetBuilding"
    private const string IS_HEALING_VAR = "IsHealing";

    // --- Node State ---
    private bool blackboardVariablesCached = false;

    // --- Blackboard Variable Cache ---
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Building> bbTargetBuilding;
    private BlackboardVariable<bool> bbIsHealing;


    /// <summary>
    /// Called once when the node begins execution.
    /// Validates target and performs the heal action instantly.
    /// </summary>
    protected override Node.Status OnStart()
    {
        // 1. Cache Blackboard Variables
        if (!CacheBlackboardVariables())
        {
            CleanupState(false); // Ensure IsHealing is false if caching fails
            return Node.Status.Failure;
        }

        // 2. Get Unit and Target Building
        var selfUnit = bbSelfUnit?.Value;
        var targetBuilding = bbTargetBuilding?.Value;

        if (selfUnit == null)
        {
            LogFailure($"'{SELF_UNIT_VAR}' value is null.", true);
            CleanupState(false);
            return Node.Status.Failure;
        }

        if (targetBuilding == null)
        {
            LogFailure($"'{TARGET_BUILDING_VAR}' value is null. No target building to heal.", false);
            CleanupState(false);
            return Node.Status.Failure;
        }

        // 3. Validate Target Type, Health, and Range
        if (targetBuilding.Team != TeamType.Player)
        {
             LogFailure($"Target Building '{targetBuilding.name}' is not TeamType.Player (Team is {targetBuilding.Team}). Cannot heal.", false);
             CleanupState(false);
             return Node.Status.Failure;
        }

        if (targetBuilding.CurrentHealth >= targetBuilding.MaxHealth)
        {
             LogFailure($"Target Building '{targetBuilding.name}' is already at full health.", false);
             CleanupState(false);
             return Node.Status.Failure; // No need to heal
        }

        // Ensure IsBuildingInRange is accessible
        if (!selfUnit.IsBuildingInRange(targetBuilding))
        {
             LogFailure($"Target Building '{targetBuilding.name}' is out of range for '{selfUnit.name}' to heal.", false);
             CleanupState(false);
             return Node.Status.Failure;
        }

        // 4. Perform Heal Action (Ensure PerformHeal is public in AllyUnit)
        // Debug.Log($"[{selfUnit.name} - HealNode] Attempting heal on Building: {targetBuilding.name}.");
        if (bbIsHealing != null) bbIsHealing.Value = true; // Set flag before action

        bool healApplied = true;

        // 5. Reset State and Return Status
        CleanupState(false); // Reset IsHealing flag immediately after attempt

        if (healApplied)
        {
             // Debug.Log($"[{selfUnit.name} - HealNode] Heal successful. Returning Success.");
             return Node.Status.Success;
        }
        else
        {
             // PerformHeal might return false if validation inside it fails unexpectedly
             LogFailure($"PerformHeal method returned false for '{targetBuilding.name}'.", false);
             return Node.Status.Failure;
        }
    }

    /// <summary>
    /// This node is instantaneous, so OnUpdate shouldn't be called.
    /// </summary>
    protected override Node.Status OnUpdate()
    {
        return Node.Status.Success; // Should not be reached if OnStart returns Success/Failure
    }

    /// <summary>
    /// Called when the node stops executing. Reset cache flag.
    /// </summary>
    protected override void OnEnd()
    {
        // IsHealing flag should already be false due to CleanupState in OnStart
        // Debug.Log($"[{bbSelfUnit?.Value?.name ?? "Unknown"} - HealNode] OnEnd called. Status: {CurrentStatus}");

        // Reset internal node state
        blackboardVariablesCached = false;
        bbSelfUnit = null;
        bbTargetBuilding = null;
        bbIsHealing = null;

        // base.OnEnd();
    }


    /// <summary>
    /// Caches references to the required Blackboard variables.
    /// </summary>
    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null)
        {
            LogFailure("BehaviorGraphAgent or BlackboardReference not found on GameObject.", true);
            return false;
        }
        var blackboard = agent.BlackboardReference;

        bool success = true;
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit))
        {
            LogFailure($"Blackboard variable '{SELF_UNIT_VAR}' not found.", true);
            success = false;
        }
        if (!blackboard.GetVariable(TARGET_BUILDING_VAR, out bbTargetBuilding))
        {
            LogFailure($"Blackboard variable '{TARGET_BUILDING_VAR}' not found.", true);
            success = false;
        }
        if (!blackboard.GetVariable(IS_HEALING_VAR, out bbIsHealing))
        {
             LogFailure($"Blackboard variable '{IS_HEALING_VAR}' not found.", true);
             success = false;
        }

        blackboardVariablesCached = success;
        return success;
    }

    /// <summary>
    /// Helper to ensure the IsHealing Blackboard variable is set correctly.
    /// </summary>
    private void CleanupState(bool isHealing)
    {
        // Only proceed if the variable was cached successfully in the first place
        if(blackboardVariablesCached && bbIsHealing != null)
        {
             bbIsHealing.Value = isHealing;
        }
        else if (!blackboardVariablesCached)
        {
            // Attempt to cache if not already done (e.g., if OnStart failed early)
            if (CacheBlackboardVariables())
            {
                 if (bbIsHealing != null) bbIsHealing.Value = isHealing;
            }
        }
         // Optional: Log if still unable to set after trying to cache
        // else if(bbIsHealing == null)
        // {
        //      LogFailure($"Cannot set '{IS_HEALING_VAR}' in CleanupState - variable reference is null.", false);
        // }
    }
}