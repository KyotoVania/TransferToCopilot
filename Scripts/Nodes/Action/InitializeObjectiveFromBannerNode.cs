using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Initialize Objective From Banner",
    story: "Initialize the unit's objective and mode from the current banner position",
    category: "Ally Actions",
    id: "AllyAction_InitializeObjectiveFromBanner_v1"
)]
public partial class InitializeObjectiveFromBannerNode : Unity.Behavior.Action
{
    // Input Blackboard variables
    private const string BB_BANNER_TARGET_POSITION = "BannerTargetPosition";
    private const string BB_HAS_BANNER_TARGET = "HasBannerTarget";
    private const string BB_SELF_UNIT = "SelfUnit";
    
    // Output Blackboard variables
    private const string BB_HAS_INITIAL_OBJECTIVE_SET = "HasInitialObjectiveSet";
    private const string BB_INITIAL_TARGET_BUILDING = "InitialTargetBuilding";
    private const string BB_IS_IN_DEFENSIVE_MODE = "IsInDefensiveMode";
    private const string BB_IS_OBJECTIVE_COMPLETED = "IsObjectiveCompleted";

    // Cached Blackboard variables
    private BlackboardVariable<Vector2Int> bbBannerTargetPosition;
    private BlackboardVariable<bool> bbHasBannerTarget;
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<bool> bbHasInitialObjectiveSet;
    private BlackboardVariable<Building> bbInitialTargetBuilding;
    private BlackboardVariable<bool> bbIsInDefensiveMode;
    private BlackboardVariable<bool> bbIsObjectiveCompleted;
    
    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;
    private AllyUnit selfUnit;

    protected override Status OnStart()
    {
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        
        if (!CacheBlackboardVariables())
        {
            Debug.LogError("[InitializeObjectiveFromBannerNode] Failed to cache blackboard variables.", GameObject);
            return Status.Failure;
        }

        selfUnit = bbSelfUnit?.Value as AllyUnit;
        if (selfUnit == null)
        {
            Debug.LogError("[InitializeObjectiveFromBannerNode] SelfUnit is null or not an AllyUnit.", GameObject);
            return Status.Failure;
        }

        // Vérifier si l'objectif est déjà initialisé (sécurité)
        bool hasObjective = bbHasInitialObjectiveSet?.Value ?? false;
        if (hasObjective)
        {
            Debug.LogWarning("[InitializeObjectiveFromBannerNode] Objective already set, skipping initialization.", GameObject);
            return Status.Success;
        }

        // Récupérer la position de la bannière
        bool hasBanner = bbHasBannerTarget?.Value ?? false;
        if (!hasBanner)
        {
            Debug.LogError("[InitializeObjectiveFromBannerNode] No banner target available for initialization.", GameObject);
            return Status.Failure;
        }

        Vector2Int bannerPos = bbBannerTargetPosition?.Value ?? new Vector2Int(-1, -1);
        if (bannerPos.x == -1 || bannerPos.y == -1)
        {
            Debug.LogError("[InitializeObjectiveFromBannerNode] Invalid banner position.", GameObject);
            return Status.Failure;
        }

        // Trouver le bâtiment à la position de la bannière
        Building buildingAtBanner = selfUnit.FindBuildingAtPosition(bannerPos);
        if (buildingAtBanner == null)
        {
            Debug.LogError($"[InitializeObjectiveFromBannerNode] No building found at banner position ({bannerPos.x}, {bannerPos.y}).", GameObject);
            return Status.Failure;
        }

        // Définir l'objectif et le mode selon le type de bâtiment
        bool isDefensiveMode = (buildingAtBanner.Team == TeamType.Player);
        
        // Écrire sur le Blackboard
        if (bbInitialTargetBuilding != null) bbInitialTargetBuilding.Value = buildingAtBanner;
        if (bbIsInDefensiveMode != null) bbIsInDefensiveMode.Value = isDefensiveMode;
        if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = false;
        if (bbHasInitialObjectiveSet != null) bbHasInitialObjectiveSet.Value = true;

        Debug.Log($"[InitializeObjectiveFromBannerNode] Objective initialized: Building='{buildingAtBanner.name}', Mode={( isDefensiveMode ? "Defensive" : "Offensive")}", GameObject);
        
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
            Debug.LogError("[InitializeObjectiveFromBannerNode] Agent or BlackboardReference missing.", GameObject);
            return false;
        }

        var blackboard = agent.BlackboardReference;
        bool success = true;

        // Input variables
        if (!blackboard.GetVariable(BB_BANNER_TARGET_POSITION, out bbBannerTargetPosition))
        {
            Debug.LogWarning($"[InitializeObjectiveFromBannerNode] '{BB_BANNER_TARGET_POSITION}' not found.", GameObject);
            success = false;
        }
        
        if (!blackboard.GetVariable(BB_HAS_BANNER_TARGET, out bbHasBannerTarget))
        {
            Debug.LogWarning($"[InitializeObjectiveFromBannerNode] '{BB_HAS_BANNER_TARGET}' not found.", GameObject);
            success = false;
        }
        
        if (!blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit))
        {
            Debug.LogError($"[InitializeObjectiveFromBannerNode] '{BB_SELF_UNIT}' not found.", GameObject);
            success = false;
        }

        // Output variables
        if (!blackboard.GetVariable(BB_HAS_INITIAL_OBJECTIVE_SET, out bbHasInitialObjectiveSet))
        {
            Debug.LogError($"[InitializeObjectiveFromBannerNode] '{BB_HAS_INITIAL_OBJECTIVE_SET}' not found.", GameObject);
            success = false;
        }
        
        if (!blackboard.GetVariable(BB_INITIAL_TARGET_BUILDING, out bbInitialTargetBuilding))
        {
            Debug.LogError($"[InitializeObjectiveFromBannerNode] '{BB_INITIAL_TARGET_BUILDING}' not found.", GameObject);
            success = false;
        }
        
        if (!blackboard.GetVariable(BB_IS_IN_DEFENSIVE_MODE, out bbIsInDefensiveMode))
        {
            Debug.LogError($"[InitializeObjectiveFromBannerNode] '{BB_IS_IN_DEFENSIVE_MODE}' not found.", GameObject);
            success = false;
        }
        
        if (!blackboard.GetVariable(BB_IS_OBJECTIVE_COMPLETED, out bbIsObjectiveCompleted))
        {
            Debug.LogError($"[InitializeObjectiveFromBannerNode] '{BB_IS_OBJECTIVE_COMPLETED}' not found.", GameObject);
            success = false;
        }

        blackboardVariablesCached = success;
        return success;
    }

    protected override void OnEnd()
    {
        blackboardVariablesCached = false;
        bbBannerTargetPosition = null;
        bbHasBannerTarget = null;
        bbSelfUnit = null;
        bbHasInitialObjectiveSet = null;
        bbInitialTargetBuilding = null;
        bbIsInDefensiveMode = null;
        bbIsObjectiveCompleted = null;
        selfUnit = null;
    }
}