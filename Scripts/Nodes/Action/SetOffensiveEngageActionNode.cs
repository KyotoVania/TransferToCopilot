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
    name: "Set Offensive Engage Action",
    story: "Set Offensive Engage Action",
    category: "Ally Actions",
    id: "AllyAction_SetOffensiveEngage_v1" // ID unique
)]
public class SetOffensiveEngageActionNode : Unity.Behavior.Action
{
    // Clés pour les variables Blackboard
    private const string BB_SELF_UNIT = "SelfUnit";
    private const string BB_DETECTED_ENEMY_UNIT = "DetectedEnemyUnit";

    private const string BB_INTERACTION_TARGET_UNIT = "InteractionTargetUnit";
    private const string BB_FINAL_DESTINATION_POSITION = "FinalDestinationPosition";
    private const string BB_SELECTED_ACTION_TYPE = "SelectedActionType";
    private const string BB_INTERACTION_TARGET_BUILDING = "InteractionTargetBuilding"; // Pour le nettoyer

    // Cache des variables
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbDetectedEnemyUnit;
    private BlackboardVariable<Unit> bbInteractionTargetUnit;
    private BlackboardVariable<Building> bbInteractionTargetBuilding;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    private BlackboardVariable<AIActionType> bbSelectedActionType;

    private bool blackboardVariablesCached = false;

    protected override Status OnStart()
    {
        if (!CacheBlackboardVariables())
        {
            Debug.LogError($"[{GameObject?.name}] SetOffensiveEngageActionNode: Échec du cache des variables Blackboard.", GameObject);
            return Status.Failure;
        }

        var selfUnit = bbSelfUnit?.Value as AllyUnit;
        var detectedEnemy = bbDetectedEnemyUnit?.Value;

        // Échoue si l'unité elle-même est invalide ou si aucun ennemi n'est détecté
        if (selfUnit == null || detectedEnemy == null || detectedEnemy.Health <= 0)
        {
            // Ce n'est pas une erreur, juste que la condition pour ce nœud n'est pas remplie
            return Status.Failure;
        }

        // C'est bien une cible valide, on prépare l'engagement
        Debug.Log($"[{selfUnit.name}] Engagement offensif ! Cible : {detectedEnemy.name}");

        // 1. Définir la cible d'interaction sur l'ennemi
        bbInteractionTargetUnit.Value = detectedEnemy;
        bbInteractionTargetBuilding.Value = null; // Nettoyer la cible de bâtiment

        // 2. Définir la destination sur la position de l'ennemi

        bool isEnemyInRange;
        if (detectedEnemy.GetUnitType() == UnitType.Boss)
        {
            // C'est la logique robuste de V2, maintenant utilisée spécifiquement pour les boss multi-tuiles.
            Tile selfTile = selfUnit.GetOccupiedTile();
            List<Tile> targetTiles = detectedEnemy.GetOccupiedTiles(); // Récupère correctement toutes les tuiles du boss
            if (selfTile == null || targetTiles.Count == 0 || HexGridManager.Instance == null)
            {
                return Status.Failure; // Sécurité, si on n'a pas de tuile ou de gestionnaire de grille
            }
            isEnemyInRange = false; // Aucune partie du boss n'était à portée

            // Vérifie la distance par rapport à chaque tuile du boss
            foreach (var targetTile in targetTiles)
            {
                if (targetTile != null)
                {
                    int distance = HexGridManager.Instance.HexDistance(selfTile.column, selfTile.row, targetTile.column, targetTile.row);
                    if (distance <= selfUnit.AttackRange)
                    {
                        isEnemyInRange = true;
                        Debug.Log($"[{selfUnit.name}] Boss {detectedEnemy.name} est à portée d'attaque sur la tuile ({targetTile.column}, {targetTile.row}).");
                    }
                }
            }
        }
        else
        {
            isEnemyInRange = selfUnit.IsUnitInRange(detectedEnemy);
        }
        
        bbSelectedActionType.Value = isEnemyInRange ? AIActionType.AttackUnit : AIActionType.MoveToUnit;
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
        if (agent == null || agent.BlackboardReference == null) return false;

        var blackboard = agent.BlackboardReference;
        bool success = true;

        if (!blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit)) success = false;
        if (!blackboard.GetVariable(BB_DETECTED_ENEMY_UNIT, out bbDetectedEnemyUnit)) success = false;
        if (!blackboard.GetVariable(BB_INTERACTION_TARGET_UNIT, out bbInteractionTargetUnit)) success = false;
        if (!blackboard.GetVariable(BB_FINAL_DESTINATION_POSITION, out bbFinalDestinationPosition)) success = false;
        if (!blackboard.GetVariable(BB_SELECTED_ACTION_TYPE, out bbSelectedActionType)) success = false;
        if (!blackboard.GetVariable(BB_INTERACTION_TARGET_BUILDING, out bbInteractionTargetBuilding)) success = false;

        blackboardVariablesCached = success;
        return success;
    }
}