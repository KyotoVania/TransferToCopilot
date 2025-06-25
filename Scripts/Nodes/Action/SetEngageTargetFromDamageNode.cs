using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Set Engage Target From Damage v2",
    story: "Instantanément, lit LastAttackerInfo et met à jour le Blackboard pour endqsdqgager l'attaquant.",
    category: "AI Actions",
    id: "AIActionsdf_SetEngageTargetFromDamage_v32"
)]
public partial class SetEngageTargetFromDamageNode : Unity.Behavior.Action
{
    private bool blackboardVariablesCached = false;

    // Variables à lire et écrire
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbInteractionTargetUnit;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;

    // Toute la logique est déplacée dans OnStart pour une exécution instantanée.
    protected override Status OnStart()
    {
        if (!CacheBlackboardVariables())
        {
            Debug.LogError($"[{GameObject?.name}] SetEngageTargetFromDamageNode: Échec de la mise en cache des variables Blackboard critiques.", GameObject);
            return Status.Failure;
        }

        var self = bbSelfUnit?.Value;
        if (self == null || !self.LastAttackerInfo.HasValue)
        {
            // Sécurité : si on arrive ici sans info de dégâts, on échoue.
            Debug.LogWarning($"[{self?.name}] SetEngageTargetFromDamageNode: SelfUnit ou LastAttackerInfo manquant. Le nœud échoue.", GameObject);
            return Status.Failure;
        }

        var attacker = self.LastAttackerInfo.Value.Attacker;
        if (attacker == null || attacker.Health <= 0)
        {
            // La cible n'est plus valide.
            Debug.LogWarning($"[{self.name}] SetEngageTargetFromDamageNode: Attacker {attacker?.name} n'est plus valide ou est mort. Le nœud échoue.", GameObject);
            return Status.Failure;
        }

        Tile attackerTile = attacker.GetOccupiedTile();
        if (attackerTile == null)
        {
            // L'attaquant n'est plus sur la carte.
            Debug.LogWarning($"[{self.name}] SetEngageTargetFromDamageNode: Attacker {attacker.name} n'est pas sur une tuile valide. Le nœud échoue.", GameObject);
            return Status.Failure;
        }

        // --- Cœur de la logique : Mise à jour du Blackboard ---
        bbInteractionTargetUnit.Value = attacker;
        bbFinalDestinationPosition.Value = new Vector2Int(attackerTile.column, attackerTile.row);
        Debug.Log($"[{self.name} | SetEngageTargetFromDamageNode] Engagement préparé. Cible: {attacker.name}. Destination: ({attackerTile.column},{attackerTile.row}).");

        // Le nœud a fait son travail et réussit instantanément dans la même frame.
        return Status.Success;
    }
    
    // OnUpdate ne sera jamais appelé car OnStart ne retourne jamais Running.
    protected override Status OnUpdate()
    {
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
        if (agent == null || agent.BlackboardReference == null)
        {
            Debug.LogError($"[{GameObject?.name} - SetEngageTargetFromDamageNode] Agent ou BlackboardReference manquant.", GameObject);
            return false;
        }
        
        var blackboard = agent.BlackboardReference;
        bool success = true;

        if (!blackboard.GetVariable("SelfUnit", out bbSelfUnit)) { Debug.LogError("BBVar 'SelfUnit' manquant.", GameObject); success = false; }
        if (!blackboard.GetVariable("InteractionTargetUnit", out bbInteractionTargetUnit)) { Debug.LogError("BBVar 'InteractionTargetUnit' manquant.", GameObject); success = false; }
        if (!blackboard.GetVariable("FinalDestinationPosition", out bbFinalDestinationPosition)) { Debug.LogError("BBVar 'FinalDestinationPosition' manquant.", GameObject); success = false; }

        blackboardVariablesCached = success;
        return success;
    }
}