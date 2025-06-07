using UnityEngine;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;
using Unity.Behavior;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Defensive Attacke",
    story: "Setup target and trigger attack while preserving defensive mode",
    category: "Ally Actions",
    id: "AllyAction_DefensiveAttack_v1"
)]
public class DefensiveAttackNode : Unity.Behavior.Action
{
    // Variables blackboard existantes
    private const string DETECTED_ENEMY_UNIT_VAR = "DetectedEnemyUnit";
    private const string DEFENDED_BUILDING_UNDER_ATTACK_VAR = "DefendedBuildingIsUnderAttack";
    private const string SELF_UNIT_VAR = "SelfUnit";

    private BlackboardVariable<Unit> bbDetectedEnemyUnit;
    private BlackboardVariable<bool> bbDefendedBuildingUnderAttack;
    private BlackboardVariable<Unit> bbSelfUnit;
    
    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;

    protected override Status OnStart()
    {
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
    
        if (!CacheBlackboardVariables())
        {
            Debug.LogError("[DefensiveAttackNode] Failed to cache blackboard variables.", GameObject);
            return Status.Failure;
        }

        AllyUnit selfUnit = bbSelfUnit?.Value as AllyUnit;
        if (selfUnit == null)
        {
            Debug.LogError("[DefensiveAttackNode] SelfUnit is null or not AllyUnit.", GameObject);
            return Status.Failure;
        }

        // Vérifier si l'ennemi est encore valide
        Unit enemy = bbDetectedEnemyUnit?.Value;
        if (enemy == null || enemy.Health <= 0)
        {
            Debug.Log("[DefensiveAttackNode] Enemy is dead or null, cleaning threat flags.", GameObject);
        
            // Nettoyer TOUS les flags de menace
            if (bbDetectedEnemyUnit != null)
                bbDetectedEnemyUnit.Value = null;
            if (bbDefendedBuildingUnderAttack != null)
                bbDefendedBuildingUnderAttack.Value = false;
            
            return Status.Success; // Mission accomplie, menace éliminée
        }

        // Si l'ennemi est vivant, configurer l'attaque
        if (selfUnit.IsUnitInRange(enemy))
        {
            Debug.Log($"[DefensiveAttackNode] Valid enemy detected for defensive attack: {enemy.name}.", GameObject);
        
            // Nettoyer le flag d'attaque du bâtiment car on prend en charge la menace
            if (bbDefendedBuildingUnderAttack != null) 
            {
                bbDefendedBuildingUnderAttack.Value = false;
                Debug.Log("[DefensiveAttackNode] Cleared building under attack flag.", GameObject);
            }
        
            return Status.Success; // L'AttackUnitNode prendra le relais
        }
    
        Debug.LogWarning("[DefensiveAttackNode] No valid target for defensive attack.", GameObject);
        return Status.Failure;
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        if (agent?.BlackboardReference == null) return false;
        
        var blackboard = agent.BlackboardReference;
        bool success = true;

        if (!blackboard.GetVariable(DETECTED_ENEMY_UNIT_VAR, out bbDetectedEnemyUnit))
        {
            Debug.LogWarning($"[DefensiveAttackNode] Variable '{DETECTED_ENEMY_UNIT_VAR}' not found");
            success = false;
        }
        
        if (!blackboard.GetVariable(DEFENDED_BUILDING_UNDER_ATTACK_VAR, out bbDefendedBuildingUnderAttack))
        {
            Debug.LogWarning($"[DefensiveAttackNode] Variable '{DEFENDED_BUILDING_UNDER_ATTACK_VAR}' not found");
            success = false;
        }
        
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit))
        {
            Debug.LogWarning($"[DefensiveAttackNode] Variable '{SELF_UNIT_VAR}' not found");
            success = false;
        }

        blackboardVariablesCached = success;
        return success;
    }

    protected override Status OnUpdate()
    {
        return Status.Success; // Action instantanée
    }

    protected override void OnEnd()
    {
        blackboardVariablesCached = false;
        bbDetectedEnemyUnit = null;
        bbDefendedBuildingUnderAttack = null;
        bbSelfUnit = null;
    }
}
