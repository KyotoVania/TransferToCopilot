using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Set Guard Post (Wait for Valid Tile)",
    story: "Waits for the unit to be on a valid tile, then saves its position as a guard post if not already set.",
    category: "Enemy Actions",
    id: "EnemyAction_SetGuardPost_v3" // Version 3
)]
public class SetGuardPostNode : Unity.Behavior.Action
{
    // Clés Blackboard
    private const string BB_SELF_UNIT = "SelfUnit";
    private const string BB_GUARD_POST_POS = "GuardPostPosition";
    private const string BB_IS_GUARD_POST_SET = "IsGuardPostSet";

    // Cache des variables
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Vector2Int> bbGuardPostPosition;
    private BlackboardVariable<bool> bbIsGuardPostSet;
    private bool blackboardVariablesCached = false;

    /// <summary>
    /// Appelé une seule fois au début de l'exécution du nœud.
    /// </summary>
    protected override Status OnStart()
    {
        if (!CacheBlackboardVariables())
        {
            Debug.LogError($"[{GameObject?.name}] SetGuardPostNode: Échec critique du cache des variables Blackboard.", GameObject);
            return Status.Failure;
        }

        // Si le poste est déjà défini, notre travail est terminé.
        if (bbIsGuardPostSet.Value)
        {
            return Status.Success;
        }

        // Sinon, on commence à attendre une position valide.
        return Status.Running;
    }

    /// <summary>
    /// Appelé à chaque frame tant que le nœud est en statut "Running".
    /// </summary>
    protected override Status OnUpdate()
    {
        // Si entre-temps le poste a été défini, on termine.
        if (bbIsGuardPostSet.Value)
        {
            return Status.Success;
        }

        var selfUnit = bbSelfUnit.Value;
        if (selfUnit == null)
        {
            Debug.LogError($"[{GameObject?.name}] SetGuardPostNode: SelfUnit est devenu null pendant l'attente.", GameObject);
            return Status.Failure;
        }

        // On essaie de récupérer la tuile
        Tile currentTile = selfUnit.GetOccupiedTile();

        // Si la tuile est valide (non nulle), on sauvegarde la position
        if (currentTile != null)
        {
            bbGuardPostPosition.Value = new Vector2Int(currentTile.column, currentTile.row);
            bbIsGuardPostSet.Value = true; // IMPORTANT : On met le flag à true

            Debug.Log($"[{selfUnit.name}] Poste de garde sauvegardé sur une tuile valide : ({currentTile.column}, {currentTile.row})");

            // Le nœud a réussi sa mission.
            return Status.Success;
        }

        // Si la tuile est encore nulle, on continue d'attendre.
        return Status.Running;
    }

    protected override void OnEnd()
    {
        // On réinitialise le cache pour la prochaine exécution potentielle.
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
        if (!blackboard.GetVariable(BB_GUARD_POST_POS, out bbGuardPostPosition)) success = false;
        if (!blackboard.GetVariable(BB_IS_GUARD_POST_SET, out bbIsGuardPostSet)) success = false;

        blackboardVariablesCached = success;
        return success;
    }
}