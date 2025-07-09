using System;
using System.Collections.Generic; 
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using UnityEngine;
using Unity.Properties;
using ScriptableObjects; 

[Serializable, GeneratePropertyBag]
[Condition(name: "Is Unit In Range",
           story: "Is Unit In Range",
           category: "My Conditions",
           id: "a0a3a7d2-7b19-4b8a-9c7c-1e6e9f1e1f1e")] // GUID unique pour cette nouvelle version
public partial class IsInUnitInteractionRangeCondition : Unity.Behavior.Condition
{
    // Les noms des variables du Blackboard restent les mêmes
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string TARGET_UNIT_VAR = "InteractionTargetUnit";

    // Cache pour les références aux variables du Blackboard
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbTargetUnit;
    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;

    public override void OnStart()
    {
        if (agent == null && GameObject != null)
        {
            agent = GameObject.GetComponent<BehaviorGraphAgent>();
        }
        // Réinitialiser le cache pour obtenir les données les plus récentes
        blackboardVariablesCached = false;
    }

    /// <summary>
    /// Met en cache les variables du Blackboard pour un accès plus rapide.
    /// </summary>
    private void CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return;

        if (agent == null || agent.BlackboardReference == null)
        {
            Debug.LogError("[IsInUnitInteractionRangeCondition] BehaviorGraphAgent ou Blackboard non trouvé.", GameObject);
            return;
        }
        var blackboard = agent.BlackboardReference;

        bool foundAll = true;
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit))
        {
            Debug.LogWarning($"[IsInUnitInteractionRangeCondition] La variable Blackboard '{SELF_UNIT_VAR}' est introuvable.", GameObject);
            foundAll = false;
        }
        if (!blackboard.GetVariable(TARGET_UNIT_VAR, out bbTargetUnit))
        {
            Debug.LogWarning($"[IsInUnitInteractionRangeCondition] La variable Blackboard '{TARGET_UNIT_VAR}' est introuvable.", GameObject);
            foundAll = false;
        }
        
        // Le cache est considéré comme réussi uniquement si les deux variables essentielles sont trouvées
        if (foundAll)
        {
            blackboardVariablesCached = true;
        }
    }

    /// <summary>
    /// Évalue la condition.
    /// </summary>
    public override bool IsTrue()
    {
        // Tente de mettre les variables en cache si ce n'est pas déjà fait
        if (!blackboardVariablesCached)
        {
            CacheBlackboardVariables();
        }

        // Si, après la tentative de cache, les références sont toujours nulles, la condition échoue
        if (bbSelfUnit == null || bbTargetUnit == null)
        {
            return false;
        }
        
        var selfUnit = bbSelfUnit.Value;
        var targetUnit = bbTargetUnit.Value;

        // Échoue proprement si les unités ne sont pas définies dans le Blackboard
        if (selfUnit == null || targetUnit == null)
        {
            return false;
        }

        // --- LOGIQUE FUSIONNÉE ---
        // Pour les unités de type Boss, nous utilisons une vérification spéciale qui itère sur toutes leurs tuiles.
        // Pour les unités standards, nous nous fions à la méthode originale et stable IsUnitInRange.
        if (targetUnit.GetUnitType() == UnitType.Boss)
        {
            // C'est la logique robuste de V2, maintenant utilisée spécifiquement pour les boss multi-tuiles.
            Tile selfTile = selfUnit.GetOccupiedTile();
            List<Tile> targetTiles = targetUnit.GetOccupiedTiles(); // Récupère correctement toutes les tuiles du boss

            if (selfTile == null || targetTiles.Count == 0 || HexGridManager.Instance == null)
            {
                return false;
            }

            // Vérifie la distance par rapport à chaque tuile du boss
            foreach (var targetTile in targetTiles)
            {
                if (targetTile != null)
                {
                    int distance = HexGridManager.Instance.HexDistance(selfTile.column, selfTile.row, targetTile.column, targetTile.row);
                    if (distance <= selfUnit.AttackRange)
                    {
                        return true; // Succès dès qu'une partie du boss est à portée
                    }
                }
            }
            return false; // Aucune partie du boss n'était à portée
        }
        else
        {
            bool isInRange = selfUnit.IsUnitInRange(targetUnit);
            if (!isInRange)
            {
                Debug.LogWarning($"[IsInUnitInteractionRangeCondition] {selfUnit.name} n'est pas dans la portée de {targetUnit.name}.", GameObject);
            }
            
            return selfUnit.IsUnitInRange(targetUnit);
        }
    }

    public override void OnEnd()
    {
        // Réinitialise l'état pour la prochaine exécution du nœud
        blackboardVariablesCached = false;
        bbSelfUnit = null;
        bbTargetUnit = null;
    }
}