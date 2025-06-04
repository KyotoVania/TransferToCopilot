using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Initialize Decision Context",
    story: "Initialize decision context and determine if defensive or offensive mode",
    category: "Action Nodes",
    id: "Action_InitializeDecisionContext_v1"
)]
public partial class InitializeDecisionContextNode : Unity.Behavior.Action
{
    // Input Blackboard variables
    private const string BB_SELF_UNIT = "SelfUnit";
    private const string BB_HAS_INITIAL_OBJECTIVE_SET = "HasInitialObjectiveSet";
    private const string BB_INITIAL_TARGET_BUILDING = "InitialTargetBuilding";
    private const string BB_IS_OBJECTIVE_COMPLETED = "IsObjectiveCompleted";
    private const string BB_HAS_BANNER_TARGET = "HasBannerTarget";
    private const string BB_BANNER_TARGET_POSITION = "BannerTargetPosition";

    // Output Blackboard variables
    private const string BB_IS_IN_DEFENSIVE_MODE = "IsInDefensiveMode";
    private const string BB_CURRENT_PRIORITY_TARGET = "CurrentPriorityTarget"; // Building à traiter
    private const string BB_CURRENT_TARGET_POSITION = "CurrentTargetPosition"; // Position du target
    private const string BB_HAS_PRIORITY_TARGET = "HasPriorityTarget"; // Boolean pour savoir si on a une cible

    // Cached variables
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<bool> bbHasInitialObjectiveSet;
    private BlackboardVariable<Building> bbInitialTargetBuilding;
    private BlackboardVariable<bool> bbIsObjectiveCompleted;
    private BlackboardVariable<bool> bbHasBannerTarget;
    private BlackboardVariable<Vector2Int> bbBannerTargetPosition;
    
    private BlackboardVariable<bool> bbIsInDefensiveMode;
    private BlackboardVariable<Building> bbCurrentPriorityTarget;
    private BlackboardVariable<Vector2Int> bbCurrentTargetPosition;
    private BlackboardVariable<bool> bbHasPriorityTarget;

    private bool blackboardVariablesCached = false;

    protected override Status OnStart()
    {
        if (!CacheBlackboardVariables())
        {
            Debug.LogError("[InitializeDecisionContext] Failed to cache Blackboard variables", GameObject);
            return Status.Failure;
        }

        return DetermineContext();
    }

    private Status DetermineContext()
    {
        Unit selfUnit = bbSelfUnit?.Value;
        if (selfUnit == null)
        {
            Debug.LogError("[InitializeDecisionContext] SelfUnit is null", GameObject);
            return Status.Failure;
        }

        AllyUnit allyUnit = selfUnit as AllyUnit;
        if (allyUnit == null)
        {
            Debug.LogError("[InitializeDecisionContext] SelfUnit is not AllyUnit", GameObject);
            return Status.Failure;
        }

        // Réinitialiser les outputs
        ClearOutputs();

        // PRIORITÉ 1: Objectif Initial
        if (TryHandleInitialObjective(allyUnit))
        {
            Debug.Log($"[InitializeDecisionContext] Using Initial Objective", GameObject);
            return Status.Success;
        }

        // PRIORITÉ 2: Bannière
        if (TryHandleBannerTarget(allyUnit))
        {
            Debug.Log($"[InitializeDecisionContext] Using Banner Target", GameObject);
            return Status.Success;
        }

        // PRIORITÉ 3: Aucune cible prioritaire
        Debug.Log($"[InitializeDecisionContext] No priority target found", GameObject);
        if (bbHasPriorityTarget != null) bbHasPriorityTarget.Value = false;
        if (bbIsInDefensiveMode != null) bbIsInDefensiveMode.Value = false;
        
        return Status.Success;
    }

    private bool TryHandleInitialObjective(AllyUnit unit)
    {
        bool hasInitialObjective = bbHasInitialObjectiveSet?.Value ?? false;
        bool isCompleted = bbIsObjectiveCompleted?.Value ?? false;
        Building initialBuilding = bbInitialTargetBuilding?.Value;

        if (!hasInitialObjective || isCompleted || initialBuilding == null || initialBuilding.CurrentHealth <= 0)
        {
            return false;
        }

        return SetPriorityTarget(initialBuilding, unit);
    }

    private bool TryHandleBannerTarget(AllyUnit unit)
    {
        bool hasBanner = bbHasBannerTarget?.Value ?? false;
        if (!hasBanner) return false;

        Vector2Int bannerPos = bbBannerTargetPosition?.Value ?? new Vector2Int(-1, -1);
        if (bannerPos.x == -1) return false;

        Building buildingAtBanner = unit.FindBuildingAtPosition(bannerPos);
        if (buildingAtBanner == null || buildingAtBanner.CurrentHealth <= 0)
        {
            return false;
        }

        return SetPriorityTarget(buildingAtBanner, unit);
    }

    private bool SetPriorityTarget(Building building, AllyUnit unit)
    {
        Tile buildingTile = building.GetOccupiedTile();
        if (buildingTile == null) return false;

        // Déterminer si c'est défensif ou offensif
        bool isDefensive = (building.Team == TeamType.Player);

        // Écrire sur le Blackboard
        if (bbCurrentPriorityTarget != null) bbCurrentPriorityTarget.Value = building;
        if (bbCurrentTargetPosition != null) bbCurrentTargetPosition.Value = new Vector2Int(buildingTile.column, buildingTile.row);
        if (bbHasPriorityTarget != null) bbHasPriorityTarget.Value = true;
        if (bbIsInDefensiveMode != null) bbIsInDefensiveMode.Value = isDefensive;

        Debug.Log($"[InitializeDecisionContext] Set priority target: {building.name} (Defensive: {isDefensive})", GameObject);
        return true;
    }

    private void ClearOutputs()
    {
        if (bbCurrentPriorityTarget != null) bbCurrentPriorityTarget.Value = null;
        if (bbCurrentTargetPosition != null) bbCurrentTargetPosition.Value = new Vector2Int(-1, -1);
        if (bbHasPriorityTarget != null) bbHasPriorityTarget.Value = false;
        if (bbIsInDefensiveMode != null) bbIsInDefensiveMode.Value = false;
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null) return false;

        var blackboard = agent.BlackboardReference;
        bool success = true;

        // Inputs
        if (!blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit)) success = false;
        if (!blackboard.GetVariable(BB_HAS_INITIAL_OBJECTIVE_SET, out bbHasInitialObjectiveSet)) success = false;
        if (!blackboard.GetVariable(BB_INITIAL_TARGET_BUILDING, out bbInitialTargetBuilding)) success = false;
        if (!blackboard.GetVariable(BB_IS_OBJECTIVE_COMPLETED, out bbIsObjectiveCompleted)) success = false;
        if (!blackboard.GetVariable(BB_HAS_BANNER_TARGET, out bbHasBannerTarget)) success = false;
        if (!blackboard.GetVariable(BB_BANNER_TARGET_POSITION, out bbBannerTargetPosition)) success = false;

        // Outputs
        if (!blackboard.GetVariable(BB_IS_IN_DEFENSIVE_MODE, out bbIsInDefensiveMode)) success = false;
        if (!blackboard.GetVariable(BB_CURRENT_PRIORITY_TARGET, out bbCurrentPriorityTarget)) success = false;
        if (!blackboard.GetVariable(BB_CURRENT_TARGET_POSITION, out bbCurrentTargetPosition)) success = false;
        if (!blackboard.GetVariable(BB_HAS_PRIORITY_TARGET, out bbHasPriorityTarget)) success = false;

        blackboardVariablesCached = success;
        return success;
    }

    protected override Status OnUpdate()
    {
        return Status.Success;
    }

    protected override void OnEnd()
    {
        blackboardVariablesCached = false;
    }
}