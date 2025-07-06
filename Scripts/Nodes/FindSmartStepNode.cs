using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Find Smart Step (Generic)",
    story: "Find Smart Step (Generic)",
    category: "Pathfinding Actions",
    id: "Pathfinding_FindSmartStep_v3"
)]
public partial class FindSmartStepNode : Unity.Behavior.Action
{
    // --- Noms des variables Blackboard (fusionnés) ---
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string FINAL_DESTINATION_POS_VAR = "FinalDestinationPosition";
    private const string INTERACTION_TARGET_UNIT_VAR = "InteractionTargetUnit";
    private const string INTERACTION_TARGET_BUILDING_VAR = "InteractionTargetBuilding";
    private const string SELECTED_ACTION_TYPE_VAR = "SelectedActionType";

    private const string CURRENT_PATH_VAR = "CurrentPath";
    private const string MOVEMENT_TARGET_POS_VAR = "MovementTargetPosition";
    private const string PATHFINDING_FAILED_VAR = "PathfindingFailed";

    // --- Variables Blackboard (fusionnées) ---
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    private BlackboardVariable<Unit> bbInteractionTargetUnit;
    private BlackboardVariable<Building> bbInteractionTargetBuilding;
    private BlackboardVariable<AIActionType> bbSelectedActionType;
    private BlackboardVariable<List<Vector2Int>> bbCurrentPath;
    private BlackboardVariable<Vector2Int> bbMovementTargetPosition;
    private BlackboardVariable<bool> bbPathfindingFailed;

    private bool blackboardVariablesCached = false;
    private Unit selfUnitInstance;
    private HexGridManager gridManager;
    private string nodeInstanceId;

    [Header("Node Debug Options")]
    [SerializeField] private bool debugStandableCheckLog = false;
    [SerializeField] private bool debugEngagementTileSearchLog = false;
    [SerializeField] private bool debugAStarStepsLog = false;

    // Classe interne pour A* (inchangée)
    private class PathNode : IComparable<PathNode>
    {
        public Tile TileNode { get; }
        public Vector2Int Coords => new Vector2Int(TileNode.column, TileNode.row);
        public float GCost { get; set; }
        public float HCost { get; set; }
        public float FCost => GCost + HCost;
        public PathNode Parent { get; set; }
        public PathNode(Tile tile) { TileNode = tile; GCost = float.MaxValue; HCost = float.MaxValue; }
        public int CompareTo(PathNode other) { if (other == null) return 1; int c = FCost.CompareTo(other.FCost); return c == 0 ? HCost.CompareTo(other.HCost) : c; }
        public override bool Equals(object obj) => obj is PathNode other && TileNode == other.TileNode;
        public override int GetHashCode() => TileNode != null ? TileNode.GetHashCode() : 0;
    }

    #region Main Logic

    protected override Status OnStart()
    {
        nodeInstanceId = Guid.NewGuid().ToString("N").Substring(0, 6);
        LogNodeMessage("OnStart BEGIN", isVerboseOverride: true);

        if (!CacheBlackboardVariables() || selfUnitInstance == null || gridManager == null)
        {
            LogNodeMessage("Échec des prérequis (Cache, SelfUnit ou GridManager).", isError: true, isVerboseOverride: true);
            SetPathfindingOutputs(true, new List<Vector2Int>(), new Vector2Int(-1, -1));
            return Status.Failure;
        }

        Tile startTile = selfUnitInstance.GetOccupiedTile();
        if (startTile == null)
        {
            LogNodeMessage("L'unité n'est pas sur une tuile de départ valide.", isError: true, isVerboseOverride: true);
            SetPathfindingOutputs(true, new List<Vector2Int>(), new Vector2Int(-1, -1));
            return Status.Failure;
        }

        List<Tile> targetTiles = GetTargetTiles();
        if (targetTiles.Count == 0 || targetTiles.All(t => t == null))
        {
            LogNodeMessage("Aucune tuile cible valide trouvée sur le blackboard (Unité, Bâtiment ou Position).", isError: true, isVerboseOverride: true);
            SetPathfindingOutputs(true, new List<Vector2Int>(), new Vector2Int(startTile.column, startTile.row));
            return Status.Failure;
        }

        float attackRange = selfUnitInstance.AttackRange;
        AIActionType currentAction = bbSelectedActionType?.Value ?? AIActionType.None;
        LogNodeMessage($"--- Début FindSmartPath depuis ({startTile.column},{startTile.row}) vers {targetTiles.Count} tuile(s) cible(s). Action: {currentAction}, Portée d'attaque de base: {attackRange} ---", isVerboseOverride: true);

        List<Vector2Int> path = CalculateAStarPathToEngagement(startTile, targetTiles, attackRange, currentAction);

        if (path != null && path.Count > 0)
        {
            SetPathfindingOutputs(false, path, path[0]);
            LogNodeMessage($"Chemin trouvé. Prochain pas: ({path[0].x},{path[0].y}). Longueur: {path.Count}", isVerboseOverride: true);
            return Status.Success;
        }
        else if (path != null && path.Count == 0)
        {
            SetPathfindingOutputs(false, new List<Vector2Int>(), new Vector2Int(startTile.column, startTile.row));
            LogNodeMessage("Déjà en position d'engagement. Aucun mouvement nécessaire.", isVerboseOverride: true);
            return Status.Success;
        }
        else
        {
            LogNodeMessage("Aucun chemin trouvé vers une position d'engagement.", isError: false, isVerboseOverride: true);
            SetPathfindingOutputs(true, new List<Vector2Int>(), new Vector2Int(startTile.column, startTile.row));
            return Status.Failure;
        }
    }

    private List<Tile> GetTargetTiles()
    {
        var targetUnit = bbInteractionTargetUnit?.Value;
        if (targetUnit != null) return targetUnit.GetOccupiedTiles();

        var targetBuilding = bbInteractionTargetBuilding?.Value;
        if (targetBuilding != null)
        {
            Tile buildingTile = targetBuilding.GetOccupiedTile();
            if (buildingTile != null) return new List<Tile> { buildingTile };
        }

        if (bbFinalDestinationPosition != null)
        {
            Vector2Int finalDestPos = bbFinalDestinationPosition.Value;
            Tile finalDestTile = gridManager.GetTileAt(finalDestPos.x, finalDestPos.y);
            if (finalDestTile != null) return new List<Tile> { finalDestTile };
        }

        return new List<Tile>();
    }

    #endregion

    #region A* and Engagement Logic

    private List<Vector2Int> CalculateAStarPathToEngagement(Tile startTile, List<Tile> targetTiles, float unitAttackRange, AIActionType currentAction)
    {
        LogNodeMessage($"CalculateAStarPathToEngagement: start=({startTile.column},{startTile.row}), {targetTiles.Count} tuile(s) cible(s), range={unitAttackRange}", isVerboseOverride: debugEngagementTileSearchLog);

        List<Tile> engagementTiles = GetValidEngagementTiles(targetTiles, unitAttackRange, currentAction);

        if (engagementTiles.Count == 0)
        {
            LogNodeMessage("Aucune tuile d'engagement VALIDE trouvée.", isError: false, isVerboseOverride: true);
            return null;
        }

        if (engagementTiles.Contains(startTile))
        {
            LogNodeMessage("L'unité est déjà sur une tuile d'engagement valide.", isVerboseOverride: true);
            return new List<Vector2Int>();
        }

        List<PathNode> openList = new List<PathNode>();
        HashSet<Tile> closedSet = new HashSet<Tile>();
        Dictionary<Tile, PathNode> allNodes = new Dictionary<Tile, PathNode>();

        PathNode startNode = GetPathNode(startTile, allNodes);
        startNode.GCost = 0;
        startNode.HCost = CalculateMinHeuristicToEngagementTiles(startNode.TileNode, engagementTiles);
        openList.Add(startNode);

        PathNode bestEngagementNodeFound = null;
        while (openList.Count > 0)
        {
            openList.Sort();
            PathNode currentNode = openList[0];
            openList.RemoveAt(0);

            if (closedSet.Contains(currentNode.TileNode)) continue;
            closedSet.Add(currentNode.TileNode);

            if (engagementTiles.Contains(currentNode.TileNode))
            {
                bestEngagementNodeFound = currentNode;
                break;
            }

            foreach (Tile neighborTile in currentNode.TileNode.Neighbors)
            {
                if (neighborTile == null || closedSet.Contains(neighborTile) || !IsTileStandableForUnit(neighborTile, selfUnitInstance)) continue;

                PathNode neighborPathNode = GetPathNode(neighborTile, allNodes);
                float tentativeGCost = currentNode.GCost + 1;

                if (tentativeGCost < neighborPathNode.GCost)
                {
                    neighborPathNode.Parent = currentNode;
                    neighborPathNode.GCost = tentativeGCost;
                    neighborPathNode.HCost = CalculateMinHeuristicToEngagementTiles(neighborPathNode.TileNode, engagementTiles);
                    if (!openList.Contains(neighborPathNode)) openList.Add(neighborPathNode);
                }
            }
        }

        if (bestEngagementNodeFound != null) return ReconstructPath(bestEngagementNodeFound, startTile);

        LogNodeMessage("Aucun chemin n'a pu être tracé vers une tuile d'engagement valide.", isVerboseOverride: true);
        return null;
    }

    private List<Tile> GetValidEngagementTiles(List<Tile> targetEntityTiles, float unitAttackRange, AIActionType currentAction)
    {
        var allEngagementTiles = new HashSet<Tile>();
        if (targetEntityTiles.Count == 0) return new List<Tile>();

        foreach(var targetTile in targetEntityTiles.Where(t => t != null))
        {
            bool forceCaptureRange = (currentAction == AIActionType.CaptureBuilding && targetTile.currentBuilding != null);
            int effectiveRange = forceCaptureRange ? 1 : Mathf.Max(0, Mathf.FloorToInt(unitAttackRange));

            // *** CORRECTION 1: Check for 'MoveToBuilding' only. ***
            if (currentAction == AIActionType.MoveToBuilding && !forceCaptureRange)
            {
                if (IsTileStandableForUnit(targetTile, selfUnitInstance))
                {
                    allEngagementTiles.Add(targetTile);
                    continue;
                }
            }

            List<Tile> tilesPotentiallyInRange = gridManager.GetTilesWithinRange(targetTile.column, targetTile.row, effectiveRange);
            foreach (Tile potentialStandpoint in tilesPotentiallyInRange)
            {
                if (potentialStandpoint == null) continue;
                if (effectiveRange > 0 && potentialStandpoint == targetTile) continue;
                if (effectiveRange == 0 && potentialStandpoint != targetTile) continue;
                if (IsTileStandableForUnit(potentialStandpoint, selfUnitInstance))
                {
                    allEngagementTiles.Add(potentialStandpoint);
                }
            }
        }

        if (allEngagementTiles.Count == 0) LogNodeMessage("Aucune tuile d'engagement PRATICABLE trouvée.", false, true);
        return allEngagementTiles.ToList();
    }

    private bool IsTileStandableForUnit(Tile tile, Unit unit)
    {
        if (tile == null || unit == null) return false;

        if (tile.tileType != TileType.Ground) return false;
        if (tile.currentEnvironment != null && tile.currentEnvironment.IsBlocking) return false;
        if (tile.currentUnit != null && tile.currentUnit != unit) return false;
        if (TileReservationController.Instance != null && TileReservationController.Instance.IsTileReservedByOtherUnit(new Vector2Int(tile.column, tile.row), unit)) return false;

        if (tile.currentBuilding != null)
        {
            // *** CORRECTION 2: Determine unit's team by its type, not from StatSheet_SO. ***
            TeamType unitTeam = (unit is AllyUnit) ? TeamType.Player : TeamType.Enemy;

            if (tile.currentBuilding.Team != TeamType.Neutral && tile.currentBuilding.Team != unitTeam)
            {
                // Tile has an enemy building on it, so it's not standable.
                return false;
            }

            var finalDestTiles = GetTargetTiles();
            if (tile.currentBuilding.Team == unitTeam && !finalDestTiles.Contains(tile))
            {
                 // Tile has a friendly building, can't stand on it unless it's the final destination.
                 return false;
            }
        }

        return true;
    }

    #endregion

    #region Helpers & Blackboard

    protected override Status OnUpdate() { return Status.Success; }

    private List<Vector2Int> ReconstructPath(PathNode targetNode, Tile startTile)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        PathNode current = targetNode;
        while (current != null && current.TileNode != startTile)
        {
            path.Add(current.Coords);
            current = current.Parent;
        }
        path.Reverse();
        return path;
    }

    private PathNode GetPathNode(Tile tile, Dictionary<Tile, PathNode> dict)
    {
        if (!dict.TryGetValue(tile, out PathNode node)) { node = new PathNode(tile); dict[tile] = node; }
        return node;
    }

    private float CalculateMinHeuristicToEngagementTiles(Tile from, List<Tile> targets)
    {
        if (targets.Count == 0) return float.MaxValue;
        float minH = float.MaxValue;
        foreach (Tile target in targets)
        {
            minH = Mathf.Min(minH, gridManager.HexDistance(from.column, from.row, target.column, target.row));
        }
        return minH;
    }

    private void SetPathfindingOutputs(bool failed, List<Vector2Int> path, Vector2Int nextStep)
    {
        if(bbPathfindingFailed != null) bbPathfindingFailed.Value = failed;
        if(bbCurrentPath != null) bbCurrentPath.Value = path ?? new List<Vector2Int>();
        if(bbMovementTargetPosition != null) bbMovementTargetPosition.Value = nextStep;
    }

    protected override void OnEnd()
    {
        blackboardVariablesCached = false;
        selfUnitInstance = null;
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;
        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null) return false;

        var bb = agent.BlackboardReference;
        bool success = true;

        if (!bb.GetVariable(SELF_UNIT_VAR, out bbSelfUnit)) success = false;
        if (!bb.GetVariable(FINAL_DESTINATION_POS_VAR, out bbFinalDestinationPosition)) { /* Optional */ }
        if (!bb.GetVariable(SELECTED_ACTION_TYPE_VAR, out bbSelectedActionType)) success = false;
        if (!bb.GetVariable(INTERACTION_TARGET_UNIT_VAR, out bbInteractionTargetUnit)) { /* Optional */ }
        if (!bb.GetVariable(INTERACTION_TARGET_BUILDING_VAR, out bbInteractionTargetBuilding)) { /* Optional */ }
        if (!bb.GetVariable(CURRENT_PATH_VAR, out bbCurrentPath)) success = false;
        if (!bb.GetVariable(MOVEMENT_TARGET_POS_VAR, out bbMovementTargetPosition)) success = false;
        if (!bb.GetVariable(PATHFINDING_FAILED_VAR, out bbPathfindingFailed)) success = false;

        if(success)
        {
            selfUnitInstance = bbSelfUnit.Value;
            gridManager = HexGridManager.Instance;
        }
        else Debug.LogError($"[{GameObject?.name} - FindSmartStepNode] Une ou plusieurs variables Blackboard critiques sont manquantes !", GameObject);

        blackboardVariablesCached = success;
        return success;
    }

    private void LogNodeMessage(string message, bool isError = false, bool isVerboseOverride = false)
    {
        Unit unitForLog = selfUnitInstance ?? bbSelfUnit?.Value;
        bool enableGeneralVerboseLogging = false;
        if (unitForLog is EnemyUnit eu) enableGeneralVerboseLogging = eu.enableVerboseLogging;
        else if (unitForLog is AllyUnit au) enableGeneralVerboseLogging = au.enableVerboseLogging;

        if (isError || isVerboseOverride || enableGeneralVerboseLogging)
        {
            string logPrefix = $"[{nodeInstanceId} | FindSmartStep]";
            if (isError) Debug.LogError($"{logPrefix} {message}", GameObject);
            else Debug.Log($"{logPrefix} {message}", GameObject);
        }
    }

    #endregion
}