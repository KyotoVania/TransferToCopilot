using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Reset Spawn Cooldown",
    story: "Remet la variable de cooldown de spawn à zéro.",
    category: "Building Actions",
    id: "BuildingAction_ResetCooldown_v1"
)]
public partial class ResetCooldownNode : Unity.Behavior.Action
{
    // --- CLÉS BLACKBOARD ---
    private const string BB_CURRENT_SPAWN_COOLDOWN = "CurrentSpawnCooldown";

    // --- CACHE DES VARIABLES ---
    private BlackboardVariable<int> bbCurrentSpawnCooldown;
    
    // DÉCLARATION DU CHAMP AU NIVEAU DE LA CLASSE
    // C'est cette ligne qui doit être visible par toutes les méthodes ci-dessous.
    private bool blackboardVariablesCached = false;

    // L'action est instantanée, la logique est donc dans OnStart.
    protected override Status OnStart()
    {
        if (!CacheBlackboardVariables())
        {
            Debug.LogError($"[{GameObject?.name}] ResetCooldownNode: Échec du cache de la variable '{BB_CURRENT_SPAWN_COOLDOWN}'.", GameObject);
            return Status.Failure;
        }

        // Remettre le cooldown à 0
        bbCurrentSpawnCooldown.Value = 0;
        
        return Status.Success;
    }

    protected override Status OnUpdate()
    {
        // Cette action est instantanée, elle réussit donc immédiatement dans OnStart.
        // OnUpdate ne devrait pas être appelé, mais par sécurité, on retourne Success.
        return Status.Success;
    }

    protected override void OnEnd()
    {
        // Réinitialise le flag de cache pour la prochaine exécution du nœud.
        blackboardVariablesCached = false;
    }

    /// <summary>
    /// Met en cache les références aux variables du Blackboard.
    /// </summary>
    private bool CacheBlackboardVariables()
    {
        // Si déjà mis en cache, ne rien faire.
        if (blackboardVariablesCached) return true;

        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null) return false;

        var blackboard = agent.BlackboardReference;
        bool success = blackboard.GetVariable(BB_CURRENT_SPAWN_COOLDOWN, out bbCurrentSpawnCooldown);
        
        // Mettre à jour le statut du cache.
        blackboardVariablesCached = success;
        return success;
    }
}