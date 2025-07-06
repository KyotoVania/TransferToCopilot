using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;
using Vector2Int = UnityEngine.Vector2Int;

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
    // --- Clés des variables du Blackboard ---
    private const string BB_SELF_UNIT = "SelfUnit";
    private const string BB_HAS_INITIAL_OBJECTIVE_SET = "HasInitialObjectiveSet";
    private const string BB_INITIAL_TARGET_BUILDING = "InitialTargetBuilding";
    private const string BB_INTERACTION_TARGET_UNIT = "InteractionTargetUnit";
    private const string BB_IS_IN_DEFENSIVE_MODE = "IsInDefensiveMode";
    private const string BB_FINAL_DESTINATION_POSITION = "FinalDestinationPosition";

    // --- Cache des variables du Blackboard ---
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<bool> bbHasInitialObjectiveSet;
    private BlackboardVariable<Building> bbInitialTargetBuilding;
    private BlackboardVariable<Unit> bbInteractionTargetUnit;
    private BlackboardVariable<bool> bbIsInDefensiveMode;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;

    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;

    protected override Status OnStart()
    {
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();

        if (!CacheBlackboardVariables())
        {
            Debug.LogError("[InitializeObjectiveFromBannerNode] Échec du cache des variables Blackboard.", GameObject);
            return Status.Failure;
        }

        var selfUnit = bbSelfUnit?.Value;
        if (selfUnit == null) return Status.Failure;

        if (!BannerController.Exists || !BannerController.Instance.HasActiveBanner)
        {
            Debug.LogWarning($"[{selfUnit.name}] Ne trouve pas de bannière active pour initialiser l'objectif.");
            return Status.Failure;
        }

        // Réinitialiser les anciennes cibles pour éviter les conflits
        bbInitialTargetBuilding.Value = null;
        bbInteractionTargetUnit.Value = null;

        // Priorité 1: Tenter de cibler une UNITÉ (le boss)
        Unit targetedUnit = BannerController.Instance.CurrentTargetedUnit;
        if (targetedUnit != null)
        {
            Tile unitTile = targetedUnit.GetOccupiedTile();
            if (unitTile != null)
            {
                Debug.Log($"[IA - {selfUnit.name}] Objectif initialisé sur UNITÉ : '{targetedUnit.name}' à la position ({unitTile.column}, {unitTile.row})");

                bbInteractionTargetUnit.Value = targetedUnit;
                bbFinalDestinationPosition.Value = new Vector2Int(unitTile.column, unitTile.row);
                bbIsInDefensiveMode.Value = false; // L'attaque d'une unité est toujours offensive
                bbHasInitialObjectiveSet.Value = true;

                return Status.Success;
            }
        }

        // Priorité 2: Tenter de cibler un BÂTIMENT
        Building targetedBuilding = BannerController.Instance.CurrentBuilding;
        if (targetedBuilding != null)
        {
            Tile buildingTile = targetedBuilding.GetOccupiedTile();
            if(buildingTile != null)
            {
                Debug.Log($"[IA - {selfUnit.name}] Objectif initialisé sur BÂTIMENT : '{targetedBuilding.name}' à la position ({buildingTile.column}, {buildingTile.row})");

                bbInitialTargetBuilding.Value = targetedBuilding;
                bbFinalDestinationPosition.Value = new Vector2Int(buildingTile.column, buildingTile.row);
                bbIsInDefensiveMode.Value = (targetedBuilding.Team == TeamType.Player); // Le mode défensif s'active si le bâtiment est allié
                bbHasInitialObjectiveSet.Value = true;

                return Status.Success;
            }
        }

        Debug.LogError("[InitializeObjectiveFromBannerNode] La bannière est active mais aucune cible valide (Unité ou Bâtiment) n'a été trouvée.", GameObject);
        return Status.Failure;
    }

    protected override Status OnUpdate()
    {
        // Ce noeud n'a besoin que d'un tick pour s'exécuter.
        return Status.Success;
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;
        if (agent == null || agent.BlackboardReference == null) return false;

        var blackboard = agent.BlackboardReference;
        bool success = true;

        if (!blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit)) success = false;
        if (!blackboard.GetVariable(BB_HAS_INITIAL_OBJECTIVE_SET, out bbHasInitialObjectiveSet)) success = false;
        if (!blackboard.GetVariable(BB_INITIAL_TARGET_BUILDING, out bbInitialTargetBuilding)) success = false;
        if (!blackboard.GetVariable(BB_IS_IN_DEFENSIVE_MODE, out bbIsInDefensiveMode)) success = false;
        if (!blackboard.GetVariable(BB_INTERACTION_TARGET_UNIT, out bbInteractionTargetUnit)) success = false;
        if (!blackboard.GetVariable(BB_FINAL_DESTINATION_POSITION, out bbFinalDestinationPosition))
        {
            Debug.LogError($"[InitializeObjectiveFromBannerNode] La variable Blackboard '{BB_FINAL_DESTINATION_POSITION}' est introuvable.", GameObject);
            success = false;
        }

        blackboardVariablesCached = success;
        return success;
    }

    protected override void OnEnd()
    {
        // Nettoyage pour la prochaine utilisation
        blackboardVariablesCached = false;
        agent = null;
    }
}