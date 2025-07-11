using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System.Collections.Generic;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Scan For Targets (Enemy)",
    story: "Scan For Targets (Enemy)",
    category: "Enemy Actions", // Nouvelle catégorie
    id: "EnemyAction_ScanForTargets_v1"
)]
public partial class ScanForTargetsNode_Enemy : Unity.Behavior.Action
{
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbDetectedPlayerUnit;
    private BlackboardVariable<Building> bbDetectedTargetableBuilding;
    // Optionnel : Flags booléens si vous préférez les utiliser dans les conditions
    // private BlackboardVariable<bool> bbHasDetectedPlayer;
    // private BlackboardVariable<bool> bbHasDetectedBuilding;

    private bool blackboardVariablesCached = false;
    private Unit selfUnitInstance;
    private BehaviorGraphAgent agent;
    private string nodeInstanceId;


    protected override Status OnStart()
    {
        nodeInstanceId = Guid.NewGuid().ToString("N").Substring(0,6);
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (!CacheBlackboardVariables())
        {
            LogNodeMessage("Failed to cache BB variables.", true);
            return Status.Failure;
        }

        selfUnitInstance = bbSelfUnit?.Value;
        if (selfUnitInstance == null)
        {
            LogNodeMessage($"'{EnemyUnit.BB_SELF_UNIT}' is null.", true);
            ClearDetectedTargets();
            return Status.Failure;
        }
        return Status.Running; // Scan is performed in OnUpdate, returns Success immediately
    }

    private void ClearDetectedTargets()
    {
        if (bbDetectedPlayerUnit != null) bbDetectedPlayerUnit.Value = null;
        if (bbDetectedTargetableBuilding != null) bbDetectedTargetableBuilding.Value = null;
        // if (bbHasDetectedPlayer != null) bbHasDetectedPlayer.Value = false;
        // if (bbHasDetectedBuilding != null) bbHasDetectedBuilding.Value = false;
    }

    private void LogNodeMessage(string message, bool isError = false, bool forceLog = false)
    {
        string unitName = selfUnitInstance != null ? selfUnitInstance.name : (bbSelfUnit?.Value != null ? bbSelfUnit.Value.name : "NoSelfUnit");
        string logPrefix = $"[{nodeInstanceId} | {unitName} | ScanForTargets_Enemy]";

        if (isError) Debug.LogError($"{logPrefix} {message}", GameObject);
    }


    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;
        if (agent == null || agent.BlackboardReference == null)
        {
             if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
             if (agent == null || agent.BlackboardReference == null)
             {
                Debug.LogError("[ScanForTargetsNode_Enemy] BehaviorGraphAgent or Blackboard not found.", GameObject);
                return false;
             }
        }
        var blackboard = agent.BlackboardReference;
        bool success = true;

        if (!blackboard.GetVariable(EnemyUnit.BB_SELF_UNIT, out bbSelfUnit))
            { LogNodeMessage($"BBVar '{EnemyUnit.BB_SELF_UNIT}' missing.", true); success = false; }
        if (!blackboard.GetVariable(EnemyUnit.BB_DETECTED_PLAYER_UNIT, out bbDetectedPlayerUnit))
            { LogNodeMessage($"BBVar '{EnemyUnit.BB_DETECTED_PLAYER_UNIT}' missing.", true); success = false; }
        if (!blackboard.GetVariable(EnemyUnit.BB_DETECTED_TARGETABLE_BUILDING, out bbDetectedTargetableBuilding))
            { LogNodeMessage($"BBVar '{EnemyUnit.BB_DETECTED_TARGETABLE_BUILDING}' missing.", true); success = false; }

        // blackboard.GetVariable("HasDetectedPlayerUnit", out bbHasDetectedPlayer); // Optionnel
        // blackboard.GetVariable("HasDetectedTargetableBuilding", out bbHasDetectedBuilding); // Optionnel

        blackboardVariablesCached = success;
        return success;
    }

    protected override Status OnUpdate()
    {
        if (selfUnitInstance == null) // Vérification de sécurité
        {
            LogNodeMessage("SelfUnit instance became null during OnUpdate.", true);
            ClearDetectedTargets();
            return Status.Failure;  
        }

        Tile currentTile = selfUnitInstance.GetOccupiedTile();
        if (currentTile == null)
        {
            LogNodeMessage("Unit is not on a valid tile. Cannot scan.", false); // Pas une erreur, mais un état de fait
            ClearDetectedTargets();
            return Status.Success; // Scan effectué, rien trouvé car pas de position de scan
        }

        int detectionRange = selfUnitInstance.DetectionRange; //
        List<Tile> tilesInRange = HexGridManager.Instance.GetTilesWithinRange(currentTile.column, currentTile.row, detectionRange); //

        AllyUnit closestPlayerUnit = null;
        Building closestTargetableBuilding = null;
        float minPlayerUnitDistSq = float.MaxValue;
        float minBuildingDistSq = float.MaxValue;
        Vector3 selfPosition = selfUnitInstance.transform.position;

        foreach (Tile tile in tilesInRange)
        {
            if (tile == null) continue;

            // Scan pour unités Joueur (AllyUnit)
            if (tile.currentUnit != null && tile.currentUnit is AllyUnit && tile.currentUnit.Health > 0)
            {
                AllyUnit potentialTargetUnit = tile.currentUnit as AllyUnit;
                float distSq = (potentialTargetUnit.transform.position - selfPosition).sqrMagnitude;
                if (distSq < minPlayerUnitDistSq)
                {
                    minPlayerUnitDistSq = distSq;
                    closestPlayerUnit = potentialTargetUnit;
                }
            }

            // Scan pour bâtiments ciblables (Joueur ou Neutre)
            if (tile.currentBuilding != null && tile.currentBuilding.CurrentHealth > 0 &&
                (tile.currentBuilding.Team == TeamType.Player || tile.currentBuilding.Team == TeamType.Neutral || tile.currentBuilding.Team == TeamType.NeutralPlayer))
            {
                Building potentialTargetBuilding = tile.currentBuilding;
                float distSq = (potentialTargetBuilding.transform.position - selfPosition).sqrMagnitude;
                if (distSq < minBuildingDistSq)
                {
                    minBuildingDistSq = distSq;
                    closestTargetableBuilding = potentialTargetBuilding;
                }
            }
        }

        // Écrire les résultats sur le Blackboard
        bbDetectedPlayerUnit.Value = closestPlayerUnit;
        bbDetectedTargetableBuilding.Value = closestTargetableBuilding;

        // if (bbHasDetectedPlayer != null) bbHasDetectedPlayer.Value = (closestPlayerUnit != null);
        // if (bbHasDetectedBuilding != null) bbHasDetectedBuilding.Value = (closestTargetableBuilding != null);

        return Status.Success; // Le scan est une action instantanée dans ce modèle
    }

    protected override void OnEnd()
    {
        // Pas de nettoyage spécifique nécessaire pour ce nœud car il est instantané
        // et les variables Blackboard sont destinées à persister pour d'autres nœuds.
        // blackboardVariablesCached = false; // Peut être réinitialisé si vous voulez forcer un recache à chaque exécution.
        // selfUnitInstance = null;
    }
}