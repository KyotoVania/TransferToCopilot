using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Is At Guard Post",
    story: "Checks if the unit is on its saved guard post tile. Includes debug logs.",
    category: "Enemy Conditions",
    id: "EnemyCondition_IsAtGuardPost_v2" // Version 2
)]
public class IsAtGuardPostCondition : Unity.Behavior.Condition
{
    [Tooltip("Activer pour voir les positions comparées dans la console.")]
    public bool enableDebugLogs = false;

    private const string BB_SELF_UNIT = "SelfUnit";
    private const string BB_GUARD_POST_POS = "GuardPostPosition";

    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Vector2Int> bbGuardPostPosition;

    public override bool IsTrue()
    {
        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null) return false;
        var blackboard = agent.BlackboardReference;

        // On récupère les variables à chaque fois pour s'assurer d'avoir les dernières valeurs.
        if (!blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit) ||
            !blackboard.GetVariable(BB_GUARD_POST_POS, out bbGuardPostPosition))
        {
            if(enableDebugLogs) Debug.LogWarning($"[{GameObject.name}] IsAtGuardPost: Variables Blackboard manquantes.");
            return false;
        }

        var selfUnit = bbSelfUnit.Value;
        var guardPost = bbGuardPostPosition.Value;
        Tile currentTile = selfUnit?.GetOccupiedTile();

        if (currentTile == null)
        {
            if(enableDebugLogs) Debug.Log($"[{selfUnit.name}] IsAtGuardPost: Unité non attachée à une tuile. Résultat: Faux.");
            return false;
        }

        bool isAtPost = (currentTile.column == guardPost.x && currentTile.row == guardPost.y);

        if (enableDebugLogs)
        {
            Debug.Log($"[{selfUnit.name}] Vérification IsAtGuardPost: Position Actuelle=({currentTile.column}, {currentTile.row}), Position de Garde Sauvegardée=({guardPost.x}, {guardPost.y}). Résultat: {isAtPost}");
        }

        return isAtPost;
    }
}