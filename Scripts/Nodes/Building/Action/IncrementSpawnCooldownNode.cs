using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Increment Spawn Cooldown",
    story: "Augmente la variable de cooldown de spawn de 1.",
    category: "Building Actions",
    id: "BuildingAction_IncrementCooldown_v1"
)]
public partial class IncrementSpawnCooldownNode : Unity.Behavior.Action
{
    // Clé pour la variable du Blackboard
    private const string BB_CURRENT_SPAWN_COOLDOWN = "CurrentSpawnCooldown";

    // Cache de la variable
    private BlackboardVariable<int> bbCurrentSpawnCooldown;
    
    private bool blackboardVariablesCached = false;

    // Action instantanée, toute la logique est dans OnStart.
    protected override Status OnStart()
    {
        if (!CacheBlackboardVariables())
        {
            Debug.LogError($"[{GameObject?.name}] IncrementSpawnCooldownNode: Échec du cache de la variable '{BB_CURRENT_SPAWN_COOLDOWN}'.", GameObject);
            return Status.Failure;
        }

        // Lire la valeur actuelle, l'incrémenter et la sauvegarder.
        int currentValue = bbCurrentSpawnCooldown.Value;
        bbCurrentSpawnCooldown.Value = currentValue + 1;
        
        // Optionnel : décommenter pour un débogage très verbeux
        // Debug.Log($"[{GameObject?.name}] IncrementSpawnCooldownNode: Cooldown incrémenté à {bbCurrentSpawnCooldown.Value}.", GameObject);
        
        return Status.Success;
    }

    protected override Status OnUpdate()
    {
        return Status.Success; // Action instantanée
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
        bool success = blackboard.GetVariable(BB_CURRENT_SPAWN_COOLDOWN, out bbCurrentSpawnCooldown);
        
        blackboardVariablesCached = success;
        return success;
    }
}