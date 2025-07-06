using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Set Pursue Objective Action",
    story: "Définit la cible et l'action (déplacement, attaque, capture) en fonction de l'objectif prioritaire (Unité ou Bâtiment) du Blackboard.",
    category: "Ally Actions",
    id: "AllyAction_SetPursueObjective_v1" // ID unifié
)]
public partial class SetPursueObjectiveActionNode : Unity.Behavior.Action
{
    // --- Noms des variables Blackboard (fusionnés et clarifiés) ---
    // ENTRÉES (ce que le nœud lit)
    private const string BB_SELF_UNIT = "SelfUnit";
    private const string BB_OBJECTIVE_UNIT = "InteractionTargetUnit"; // Cible prioritaire (ex: le Boss)
    private const string BB_OBJECTIVE_BUILDING = "InitialTargetBuilding"; // Cible secondaire

    // SORTIES (ce que le nœud écrit)
    private const string BB_INTERACTION_TARGET_BUILDING_OUT = "InteractionTargetBuilding"; // Cible de bâtiment confirmée
    private const string BB_FINAL_DESTINATION_POSITION = "FinalDestinationPosition";
    private const string BB_SELECTED_ACTION_TYPE = "SelectedActionType";

    // --- Cache pour les variables ---
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbObjectiveUnit;
    private BlackboardVariable<Building> bbObjectiveBuilding;
    private BlackboardVariable<Building> bbInteractionTargetBuildingOut;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    private BlackboardVariable<AIActionType> bbSelectedActionType;

    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;

    /// <summary>
    /// Logique principale du nœud, exécutée une seule fois.
    /// </summary>
    protected override Status OnStart()
    {
        if (!CacheBlackboardVariables())
        {
            Debug.LogError($"[{GameObject?.name}] SetPursueObjectiveActionNode: Échec du cache des variables Blackboard.", GameObject);
            return Status.Failure;
        }

        var selfUnit = bbSelfUnit?.Value as AllyUnit;
        if (selfUnit == null)
        {
            Debug.LogError($"[{GameObject?.name}] SetPursueObjectiveActionNode: SelfUnit est null ou n'est pas un AllyUnit.", GameObject);
            return Status.Failure;
        }

        // --- FEATURE FUSIONNÉE : Logique de Priorisation ---
        // Priorité 1: L'objectif est une UNITE (le Boss)
        var priorityUnitTarget = bbObjectiveUnit?.Value;
        if (priorityUnitTarget != null)
        {
            bbSelectedActionType.Value = AIActionType.AttackUnit;
            // On s'assure que la cible de bâtiment est nulle pour ne pas créer de confusion pour les nœuds suivants
            bbInteractionTargetBuildingOut.Value = null;
            // La destination est la tuile de l'unité (cette info est déjà sur le BB, mais on la confirme)
            Tile targetTile = priorityUnitTarget.GetOccupiedTile();
            if (targetTile != null)
            {
                bbFinalDestinationPosition.Value = new Vector2Int(targetTile.column, targetTile.row);
            }

            Debug.Log($"[{selfUnit.name}] Décision Prioritaire: ATTAQUER l'unité '{priorityUnitTarget.name}'.", selfUnit);
            return Status.Success;
        }

        // Priorité 2: L'objectif est un BATIMENT (logique détaillée)
        var buildingObjective = bbObjectiveBuilding?.Value;
        if (buildingObjective == null)
        {
            // Pas d'objectif du tout, le nœud échoue pour laisser d'autres branches de l'IA s'exécuter.
            return Status.Failure;
        }

        // --- FEATURE FUSIONNÉE : Logique Tactique Détaillée pour les Bâtiments ---

        // Définir les cibles communes
        bbInteractionTargetBuildingOut.Value = buildingObjective;
        Tile objectiveTile = buildingObjective.GetOccupiedTile();
        if (objectiveTile == null)
        {
            Debug.LogWarning($"[{selfUnit.name}] L'objectif '{buildingObjective.name}' n'a pas de tuile occupée. Action annulée.", selfUnit);
            return Status.Failure;
        }
        bbFinalDestinationPosition.Value = new Vector2Int(objectiveTile.column, objectiveTile.row);

        // Logique de décision pour déterminer l'action
        AIActionType decidedAction;

        bool isCapturable = buildingObjective is NeutralBuilding neutralBuilding &&
                            neutralBuilding.IsRecapturable &&
                            buildingObjective.Team != TeamType.Player;

        if (isCapturable)
        {
            if (selfUnit.IsBuildingInCaptureRange(buildingObjective))
            {
                decidedAction = AIActionType.CaptureBuilding;
            }
            else
            {
                decidedAction = AIActionType.MoveToBuilding;
            }
        }
        else if (buildingObjective.Team == TeamType.Enemy)
        {
            if (selfUnit.IsBuildingInRange(buildingObjective))
            {
                decidedAction = AIActionType.AttackBuilding;
            }
            else
            {
                decidedAction = AIActionType.MoveToBuilding;
            }
        }
        else if (buildingObjective.Team == TeamType.Player)
        {
            // Le bâtiment est déjà à nous, l'objectif est accompli
            decidedAction = AIActionType.CheerAndDespawn;
        }
        else
        {
            // Cas par défaut (ex: bâtiment neutre non-capturable)
            decidedAction = AIActionType.None;
        }

        bbSelectedActionType.Value = decidedAction;
        Debug.Log($"[{selfUnit.name}] Décision Bâtiment: {decidedAction} sur '{buildingObjective.name}'.", selfUnit);

        return Status.Success;
    }

    protected override Status OnUpdate() => Status.Success;

    protected override void OnEnd()
    {
        blackboardVariablesCached = false;
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        if (agent == null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null) return false;

        var blackboard = agent.BlackboardReference;
        bool success = true;

        // Entrées
        if (!blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit)) success = false;
        if (!blackboard.GetVariable(BB_OBJECTIVE_UNIT, out bbObjectiveUnit)) success = false;
        if (!blackboard.GetVariable(BB_OBJECTIVE_BUILDING, out bbObjectiveBuilding)) success = false;

        // Sorties
        if (!blackboard.GetVariable(BB_INTERACTION_TARGET_BUILDING_OUT, out bbInteractionTargetBuildingOut)) success = false;
        if (!blackboard.GetVariable(BB_FINAL_DESTINATION_POSITION, out bbFinalDestinationPosition)) success = false;
        if (!blackboard.GetVariable(BB_SELECTED_ACTION_TYPE, out bbSelectedActionType)) success = false;

        blackboardVariablesCached = success;
        return success;
    }
}