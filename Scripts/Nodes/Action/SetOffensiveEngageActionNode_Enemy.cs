using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;
using ScriptableObjects; 
using System.Collections.Generic; 


[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Set Offensive Engage Action (Enemy)",
    story: "Set Offensive Engage Action (Enemy)",
    category: "Enemy Actions",
    id: "EnemyAction_SetOffensiveEngage_v1" // ID unique pour les ennemis
)]
public class SetOffensiveEngageActionNode_Enemy : Unity.Behavior.Action
{
    // Clés pour les variables Blackboard
    private const string BB_SELF_UNIT = "SelfUnit";
    private const string BB_DETECTED_PLAYER_UNIT = "DetectedPlayerUnit"; // Pour les ennemis, on cherche les joueurs

    private const string BB_INTERACTION_TARGET_UNIT = "InteractionTargetUnit";
    private const string BB_FINAL_DESTINATION_POSITION = "FinalDestinationPosition";
    private const string BB_SELECTED_ACTION_TYPE = "SelectedActionType";
    private const string BB_INTERACTION_TARGET_BUILDING = "InteractionTargetBuilding"; // Pour le nettoyer

    // Cache des variables
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbDetectedPlayerUnit; // Type spécifique pour les joueurs
    private BlackboardVariable<Unit> bbInteractionTargetUnit;
    private BlackboardVariable<Building> bbInteractionTargetBuilding;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    private BlackboardVariable<AIActionType> bbSelectedActionType;

    private bool blackboardVariablesCached = false;

    protected override Status OnStart()
    {
        if (!CacheBlackboardVariables())
        {
            Debug.LogError($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Échec du cache des variables Blackboard.", GameObject);
            return Status.Failure;
        }

        var selfUnit = bbSelfUnit?.Value as EnemyUnit;
        var detectedPlayer = bbDetectedPlayerUnit?.Value;

        // Échoue si l'unité elle-même est invalide ou si aucun joueur n'est détecté
        if (selfUnit == null || detectedPlayer == null || detectedPlayer.Health <= 0)
        {
            // Ce n'est pas une erreur, juste que la condition pour ce nœud n'est pas remplie
            return Status.Failure;
        }

        // C'est bien une cible valide, on prépare l'engagement
        Debug.Log($"[{selfUnit.name}] Engagement offensif ennemi ! Cible : {detectedPlayer.name}");

        // 1. Définir la cible d'interaction sur le joueur
        bbInteractionTargetUnit.Value = detectedPlayer;
        bbInteractionTargetBuilding.Value = null; // Nettoyer la cible de bâtiment

        // 2. Vérifier si le joueur est à portée d'attaque
        bool isPlayerInRange = selfUnit.IsUnitInRange(detectedPlayer);
        
        // 3. Définir l'action appropriée
        bbSelectedActionType.Value = isPlayerInRange ? AIActionType.AttackUnit : AIActionType.MoveToUnit;
        Debug.Log($"[{selfUnit.name}] Action sélectionnée : {bbSelectedActionType.Value}");
        
        // Ce nœud a terminé sa tâche (mettre à jour le BB), il retourne donc Success.
        return Status.Success;
    }

    protected override Status OnUpdate()
    {
        // Action instantanée, ne reste jamais en "Running"
        return Status.Success;
    }

    protected override void OnEnd()
    {
        blackboardVariablesCached = false;
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null)
        {
            Debug.LogError($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: BehaviorGraphAgent est null!", GameObject);
            return false;
        }
        
        if (agent.BlackboardReference == null)
        {
            Debug.LogError($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: BlackboardReference est null!", GameObject);
            return false;
        }

        var blackboard = agent.BlackboardReference;
        bool success = true;

        Debug.Log($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Début du cache des variables...");

        if (!blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit))
        {
            Debug.LogError($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Variable '{BB_SELF_UNIT}' non trouvée dans le Blackboard!", GameObject);
            success = false;
        }
        else
        {
            Debug.Log($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Variable '{BB_SELF_UNIT}' trouvée, valeur: {bbSelfUnit?.Value?.name ?? "null"}");
        }

        if (!blackboard.GetVariable(BB_DETECTED_PLAYER_UNIT, out bbDetectedPlayerUnit))
        {
            Debug.LogError($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Variable '{BB_DETECTED_PLAYER_UNIT}' non trouvée dans le Blackboard!", GameObject);
            success = false;
        }
        else
        {
            Debug.Log($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Variable '{BB_DETECTED_PLAYER_UNIT}' trouvée, valeur: {bbDetectedPlayerUnit?.Value?.name ?? "null"}");
        }

        if (!blackboard.GetVariable(BB_INTERACTION_TARGET_UNIT, out bbInteractionTargetUnit))
        {
            Debug.LogError($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Variable '{BB_INTERACTION_TARGET_UNIT}' non trouvée dans le Blackboard!", GameObject);
            success = false;
        }
        else
        {
            Debug.Log($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Variable '{BB_INTERACTION_TARGET_UNIT}' trouvée");
        }

        if (!blackboard.GetVariable(BB_FINAL_DESTINATION_POSITION, out bbFinalDestinationPosition))
        {
            Debug.LogError($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Variable '{BB_FINAL_DESTINATION_POSITION}' non trouvée dans le Blackboard!", GameObject);
            success = false;
        }
        else
        {
            Debug.Log($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Variable '{BB_FINAL_DESTINATION_POSITION}' trouvée");
        }

        if (!blackboard.GetVariable(BB_SELECTED_ACTION_TYPE, out bbSelectedActionType))
        {
            Debug.LogError($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Variable '{BB_SELECTED_ACTION_TYPE}' non trouvée dans le Blackboard!", GameObject);
            success = false;
        }
        else
        {
            Debug.Log($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Variable '{BB_SELECTED_ACTION_TYPE}' trouvée");
        }

        if (!blackboard.GetVariable(BB_INTERACTION_TARGET_BUILDING, out bbInteractionTargetBuilding))
        {
            Debug.LogError($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Variable '{BB_INTERACTION_TARGET_BUILDING}' non trouvée dans le Blackboard!", GameObject);
            success = false;
        }
        else
        {
            Debug.Log($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Variable '{BB_INTERACTION_TARGET_BUILDING}' trouvée");
        }

        blackboardVariablesCached = success;
        
        if (success)
        {
            Debug.Log($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Cache des variables réussi!");
        }
        else
        {
            Debug.LogError($"[{GameObject?.name}] SetOffensiveEngageActionNode_Enemy: Échec du cache des variables. Vérifiez que toutes les variables sont bien configurées dans le Blackboard.", GameObject);
        }
        
        return success;
    }
}
