using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Set Pursue Objective Action",
    story: "Définit la cible et l'action (déplacement, attaque, capture) en fonction de l'objectif principal du Blackboard.",
    category: "Ally Actions",
    id: "AllyAction_SetPursueObjective_v1" // ID unique pour ce nouveau nœud
)]
public partial class SetPursueObjectiveActionNode : Unity.Behavior.Action
{
    // --- Noms des variables Blackboard (constantes pour la propreté) ---
    // Entrées lues
    private const string BB_SELF_UNIT = "SelfUnit";
    private const string BB_INITIAL_TARGET_BUILDING = "InitialTargetBuilding";

    // Sorties écrites
    private const string BB_INTERACTION_TARGET_BUILDING = "InteractionTargetBuilding";
    private const string BB_FINAL_DESTINATION_POSITION = "FinalDestinationPosition";
    private const string BB_SELECTED_ACTION_TYPE = "SelectedActionType";

    // --- Cache pour les variables ---
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Building> bbInitialTargetBuilding;
    private BlackboardVariable<Building> bbInteractionTargetBuilding;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    private BlackboardVariable<AIActionType> bbSelectedActionType;

    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;

    /// <summary>
    /// Logique principale du nœud, exécutée une seule fois.
    /// </summary>
    protected override Status OnStart()
    {
        // 1. Mise en cache des variables du Blackboard
        if (!CacheBlackboardVariables())
        {
            Debug.LogError($"[{GameObject?.name}] SetPursueObjectiveActionNode: Échec du cache des variables Blackboard.", GameObject);
            return Status.Failure;
        }

        // 2. Récupération des valeurs d'entrée
        var selfUnit = bbSelfUnit?.Value as AllyUnit;
        var initialObjective = bbInitialTargetBuilding?.Value;

        // 3. Validation des données
        if (selfUnit == null)
        {
            Debug.LogError($"[{GameObject?.name}] SetPursueObjectiveActionNode: SelfUnit est null ou n'est pas un AllyUnit.", GameObject);
            return Status.Failure;
        }
        if (initialObjective == null)
        {
            // Ce n'est pas une erreur, cela signifie simplement que cette branche de l'arbre ne doit pas s'exécuter.
            // Le nœud échoue pour que le Try In Order passe à la priorité suivante.
            return Status.Failure; 
        }

        // 4. Définir les cibles communes
        bbInteractionTargetBuilding.Value = initialObjective;
        Tile objectiveTile = initialObjective.GetOccupiedTile();
        if (objectiveTile == null)
        {
            Debug.LogWarning($"[{selfUnit.name}] L'objectif '{initialObjective.name}' n'a pas de tuile occupée. Action annulée.", selfUnit);
            return Status.Failure;
        }
        bbFinalDestinationPosition.Value = new Vector2Int(objectiveTile.column, objectiveTile.row);

        // 5. Logique de décision pour déterminer l'action
        AIActionType decidedAction;

        if (selfUnit.IsBuildingInRange(initialObjective))
        {
            // Si à portée, on décide si on attaque ou on capture
            if (initialObjective is NeutralBuilding neutralBuilding && neutralBuilding.IsRecapturable && selfUnit.IsBuildingInCaptureRange(neutralBuilding))
            {
                // C'est un bâtiment capturable (Neutre ou Ennemi) et on est à portée de capture
                decidedAction = AIActionType.CaptureBuilding;
                Debug.Log($"[{selfUnit.name}] Décision: CAPTURER l'objectif '{initialObjective.name}'.", selfUnit);
            }
            else if (initialObjective.Team == TeamType.Enemy)
            {
                // C'est un bâtiment ennemi non-capturable (ou hors de portée de capture), on l'attaque
                decidedAction = AIActionType.AttackBuilding;
                Debug.Log($"[{selfUnit.name}] Décision: ATTAQUER l'objectif '{initialObjective.name}'.", selfUnit);
            }
            else
            {
                // Cas étrange : à portée d'un bâtiment allié ou neutre non capturable. On ne fait rien.
                decidedAction = AIActionType.None;
                Debug.Log($"[{selfUnit.name}] Décision: IGNORER l'objectif '{initialObjective.name}' (déjà allié ou non-capturable).", selfUnit);
            }
        }
        else
        {
            // Si hors de portée, on se déplace vers lui
            decidedAction = AIActionType.MoveToBuilding;
            Debug.Log($"[{selfUnit.name}] Décision: SE DÉPLACER vers l'objectif '{initialObjective.name}'.", selfUnit);
        }

        // 6. Écriture de l'action décidée sur le Blackboard
        bbSelectedActionType.Value = decidedAction;

        return Status.Success; // Le nœud a terminé sa tâche de décision.
    }

    protected override Status OnUpdate()
    {
        // Ce nœud est instantané, il ne devrait jamais rester en état "Running".
        return Status.Success;
    }
    
    protected override void OnEnd()
    {
        // Réinitialiser le cache pour la prochaine exécution
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
        if (!blackboard.GetVariable(BB_INITIAL_TARGET_BUILDING, out bbInitialTargetBuilding)) success = false;

        // Sorties
        if (!blackboard.GetVariable(BB_INTERACTION_TARGET_BUILDING, out bbInteractionTargetBuilding)) success = false;
        if (!blackboard.GetVariable(BB_FINAL_DESTINATION_POSITION, out bbFinalDestinationPosition)) success = false;
        if (!blackboard.GetVariable(BB_SELECTED_ACTION_TYPE, out bbSelectedActionType)) success = false;

        blackboardVariablesCached = success;
        return success;
    }
}