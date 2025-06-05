using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Check Defensive Threats",
    story: "Check if there are enemies in perimeter or building under attack",
    category: "Ally Actions",
    id: "AllyAction_CheckDefensiveThreats_v1"
)]
public class CheckDefensiveThreatsNode : Unity.Behavior.Condition
{
    // Blackboard Variables (utilise seulement les variables existantes)
    private const string DETECTED_ENEMY_UNIT_VAR = "DetectedEnemyUnit";
    private const string DEFENDED_BUILDING_UNDER_ATTACK_VAR = "DefendedBuildingIsUnderAttack";
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string BB_FINAL_DESTINATION_POSITION = "FinalDestinationPosition";
    private const string SELECTED_ACTION_TYPE_VAR = "SelectedActionType";
    private const string BB_INTERACTION_TARGET_UNIT = "InteractionTargetUnit"; // <-- Add this line

    private BlackboardVariable<Unit> bbDetectedEnemyUnit;
    private BlackboardVariable<bool> bbDefendedBuildingUnderAttack;
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    private BlackboardVariable<AIActionType> bbSelectedActionType;
    private BlackboardVariable<Unit> bbInteractionTargetUnit; // <-- Add this line
    
    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;

    public override void OnStart()
    {
        base.OnStart();
        Debug.Log("[CheckDefensiveThreatsNode] Starting Check Defensive Threats Node");
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        CacheBlackboardVariables();
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        if (agent?.BlackboardReference == null) 
        {
            Debug.LogError("[CheckDefensiveThreatsNode] Agent or BlackboardReference is null");
            return false;
        }
        
        var blackboard = agent.BlackboardReference;
        bool success = true;

        // Vérification une par une pour debug
        if (!blackboard.GetVariable(DETECTED_ENEMY_UNIT_VAR, out bbDetectedEnemyUnit))
        {
            Debug.LogWarning($"[CheckDefensiveThreatsNode] Variable '{DETECTED_ENEMY_UNIT_VAR}' not found on blackboard");
            success = false;
        }
        if (!blackboard.GetVariable(BB_FINAL_DESTINATION_POSITION, out bbFinalDestinationPosition))
        {
            Debug.LogError($"[RequestReservePositionNode] '{BB_FINAL_DESTINATION_POSITION}' not found on blackboard");
            success = false;
        }
        if (!blackboard.GetVariable(DEFENDED_BUILDING_UNDER_ATTACK_VAR, out bbDefendedBuildingUnderAttack))
        {
            Debug.LogWarning($"[CheckDefensiveThreatsNode] Variable '{DEFENDED_BUILDING_UNDER_ATTACK_VAR}' not found on blackboard");
            success = false;
        }
        
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit))
        {
            Debug.LogWarning($"[CheckDefensiveThreatsNode] Variable '{SELF_UNIT_VAR}' not found on blackboard");
            success = false;
        }
        if (!blackboard.GetVariable(SELECTED_ACTION_TYPE_VAR, out bbSelectedActionType))
        {
            Debug.LogWarning($"[CheckDefensiveThreatsNode] Variable '{SELECTED_ACTION_TYPE_VAR}' not found on blackboard");
            success = false;
        }
        if (!blackboard.GetVariable(BB_INTERACTION_TARGET_UNIT, out bbInteractionTargetUnit)) // <-- Add this block
        {
            Debug.LogWarning($"[CheckDefensiveThreatsNode] Variable '{BB_INTERACTION_TARGET_UNIT}' not found on blackboard");
            // Not critical, don't set success = false
        }

        blackboardVariablesCached = success;
        Debug.Log($"[CheckDefensiveThreatsNode] Cache result: {success}");
        return success;
    }

    public override bool IsTrue()
    {
        Debug.Log("[CheckDefensiveThreatsNode] Checking defensive threats...");
        
        if (!CacheBlackboardVariables()) 
        {
            Debug.LogError("[CheckDefensiveThreatsNode] Failed to cache blackboard variables");
            return false;
        }
        
        Debug.Log("[CheckDefensiveThreatsNode] Blackboard variables cached successfully.");
        
        AllyUnit selfUnit = bbSelfUnit?.Value as AllyUnit;
        if (selfUnit == null) 
        {
            Debug.LogWarning("[CheckDefensiveThreatsNode] SelfUnit is null or not AllyUnit");
            return false;
        }
        
        Debug.Log($"[CheckDefensiveThreatsNode] Self unit: {selfUnit.name}");

        // Priorité 1: Bâtiment défendu sous attaque
        bool buildingUnderAttack = bbDefendedBuildingUnderAttack?.Value ?? false;
        Debug.Log($"[CheckDefensiveThreatsNode] Building under attack: {buildingUnderAttack}");
        Unit detectedEnemy = bbDetectedEnemyUnit?.Value;
        if (buildingUnderAttack)
        {
            Debug.Log($"[CheckDefensiveThreatsNode] Building under attack detected for {selfUnit.name}");
            //on set FinalDestinationPosition avec la position de l'ennemi

            if (bbDetectedEnemyUnit != null)
            {
                if (bbFinalDestinationPosition != null)
                {
                    //pour avoir la position de l'ennemi on use GetOccupiedTile
                    Tile enemyTile = detectedEnemy.GetOccupiedTile();
                    if (enemyTile != null)
                    {
                        //MoveToBuilding pour le I
                        if (bbSelectedActionType != null)
                        {
                            bbSelectedActionType.Value = AIActionType.MoveToUnit;
                            bbFinalDestinationPosition.Value = new Vector2Int(enemyTile.column, enemyTile.row);
                            if (bbInteractionTargetUnit != null)
                                bbInteractionTargetUnit.Value = detectedEnemy; // <-- Set interaction target
                        }
                    }
                    else
                        Debug.LogWarning(
                            $"[CheckDefensiveThreatsNode] Detected enemy {detectedEnemy.name} has no occupied tile.");
                }

                Debug.Log($"[CheckDefensiveThreatsNode] Detected enemy set for {selfUnit.name}: {detectedEnemy.name}");
            }
            return true;
        }

        // Priorité 2: Ennemi dans le périmètre ET à portée d'attaque
        Debug.Log($"[CheckDefensiveThreatsNode] Detected enemy: {detectedEnemy?.name ?? "null"}");
        
        if (detectedEnemy != null && detectedEnemy.Health > 0)
        {
            Debug.Log($"[CheckDefensiveThreatsNode] Threat detected: {detectedEnemy.name} is in range of {selfUnit.name}");
            //on set FinalDestinationPosition avec la position de l'ennemi
            if (bbDetectedEnemyUnit != null)
            {
                bbDetectedEnemyUnit.Value = detectedEnemy;
                if (bbFinalDestinationPosition != null)
                {
                    //pour avoir la position de l'ennemi on use GetOccupiedTile
                    Tile enemyTile = detectedEnemy.GetOccupiedTile();
                    if (enemyTile != null)
                    {
                        //MoveToBuilding pour le I
                        if (bbSelectedActionType != null)
                        {
                            bbSelectedActionType.Value = AIActionType.MoveToUnit;
                            bbFinalDestinationPosition.Value = new Vector2Int(enemyTile.column, enemyTile.row);
                            if (bbInteractionTargetUnit != null)
                                bbInteractionTargetUnit.Value = detectedEnemy; // <-- Set interaction target
                        }
                    }
                    else
                        Debug.LogWarning($"[CheckDefensiveThreatsNode] Detected enemy {detectedEnemy.name} has no occupied tile.");
                }
                Debug.Log($"[CheckDefensiveThreatsNode] Detected enemy set for {selfUnit.name}: {detectedEnemy.name}");
            }
            return true;
        }

        // Aucune menace immédiate
        Debug.Log("[CheckDefensiveThreatsNode] No immediate threats detected.");
        return false;
    }

    public override void OnEnd()
    {
        blackboardVariablesCached = false;
        bbDetectedEnemyUnit = null;
        bbDefendedBuildingUnderAttack = null;
        bbSelfUnit = null;
        bbFinalDestinationPosition = null;
        bbSelectedActionType = null;
        bbInteractionTargetUnit = null; // <-- Add this line
        base.OnEnd();
    }
}
