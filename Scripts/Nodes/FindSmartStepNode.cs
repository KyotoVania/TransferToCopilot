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
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string FINAL_DESTINATION_POS_VAR = "FinalDestinationPosition";
    private const string CURRENT_PATH_VAR = "CurrentPath";
    private const string MOVEMENT_TARGET_POS_VAR = "MovementTargetPosition";
    private const string PATHFINDING_FAILED_VAR = "PathfindingFailed";


    // --- Cache pour les variables du Blackboard ---
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    private BlackboardVariable<List<Vector2Int>> bbCurrentPath;
    private BlackboardVariable<Vector2Int> bbMovementTargetPosition;
    private BlackboardVariable<bool> bbPathfindingFailed;

    private bool blackboardVariablesCached = false;
    private Unit selfUnitInstance;
    private HexGridManager gridManager;
    private string nodeInstanceId;

    // Ajout d'un flag pour déboguer IsTileStandableForUnit
    [Header("Node Debug Options")] // Pourrait être mis dans une section Odin si vous l'utilisez
    [SerializeField] private bool debugStandableCheckLog = false;
    [SerializeField] private bool debugEngagementTileSearchLog = false;


    private class PathNode : IComparable<PathNode>
    {
        public Tile TileNode { get; }
        public Vector2Int Coords => new Vector2Int(TileNode.column, TileNode.row);
        public float GCost { get; set; }
        public float HCost { get; set; }
        public float FCost => GCost + HCost;
        public PathNode Parent { get; set; }

        public PathNode(Tile tile) { TileNode = tile; GCost = float.MaxValue; HCost = float.MaxValue; }

        public int CompareTo(PathNode other)
        {
            if (other == null) return 1;
            int compare = FCost.CompareTo(other.FCost);
            if (compare == 0) compare = HCost.CompareTo(other.HCost);
            return compare;
        }
        public override bool Equals(object obj) => obj is PathNode other && TileNode == other.TileNode;
        public override int GetHashCode() => TileNode != null ? TileNode.GetHashCode() : 0;
    }


    // Méthode de log améliorée
    private void LogNodeMessage(string message, bool isError = false, bool isVerboseOverride = false)
    {
        /*
        Unit unitForLog = selfUnitInstance ?? bbSelfUnit?.Value;
        string unitName = unitForLog != null ? unitForLog.name : (GameObject != null ? GameObject.name : "FindSmartStepNode");
        bool enableGeneralVerboseLogging = false;
        if (unitForLog is EnemyUnit enemyLog) enableGeneralVerboseLogging = enemyLog.enableVerboseLogging;
        else if (unitForLog is AllyUnit allyLog) enableGeneralVerboseLogging = allyLog.enableVerboseLogging;

        // Toujours logger les erreurs. Sinon, logger si le flag verbose du noeud est activé OU le flag verbose général de l'unité.
        if (isError || isVerboseOverride || enableGeneralVerboseLogging) // isVerboseOverride permet de forcer un log non-erreur
        {
            string logPrefix = $"[{nodeInstanceId} | {unitName} | FindSmartStepNode]";
            if (isError) Debug.LogError($"{logPrefix} {message}", GameObject);
            else Debug.Log($"{logPrefix} {message}", GameObject);
        }
        */
    }

    protected override Status OnStart()
    {
        nodeInstanceId = Guid.NewGuid().ToString("N").Substring(0, 6);
        LogNodeMessage("OnStart BEGIN", isVerboseOverride: true);

        if (!CacheBlackboardVariables())
        {
            LogNodeMessage("Échec du cache des variables Blackboard.", isError: true, isVerboseOverride: true);
            SetPathfindingOutputs(true, new List<Vector2Int>(), new Vector2Int(-1, -1));
            return Status.Failure;
        }

        selfUnitInstance = bbSelfUnit?.Value;
        gridManager = HexGridManager.Instance;

        if (selfUnitInstance == null || gridManager == null)
        {
            LogNodeMessage($"SelfUnit ({(selfUnitInstance == null ? "NULL" : selfUnitInstance.name)}) ou HexGridManager ({(gridManager == null ? "NULL" : "OK")}) est null.", isError: true, isVerboseOverride: true);
            SetPathfindingOutputs(true, new List<Vector2Int>(), new Vector2Int(-1, -1));
            return Status.Failure;
        }

        Tile startTile = selfUnitInstance.GetOccupiedTile();
        if (startTile == null)
        {
            LogNodeMessage("L'unité n'est pas sur une tuile de départ valide (GetOccupiedTile a retourné null).", isError: true, isVerboseOverride: true); // Log forcé car c'est une condition d'échec majeure
            SetPathfindingOutputs(true, new List<Vector2Int>(), new Vector2Int(-1, -1));
            return Status.Failure;
        }

        LogNodeMessage($"Unité {selfUnitInstance.name} démarre depuis la tuile ({startTile.column},{startTile.row}).", isVerboseOverride: true);


        if (bbFinalDestinationPosition == null)
        {
            LogNodeMessage("FinalDestinationPosition n'est pas définie sur le Blackboard.", isError: true, isVerboseOverride: true);
            SetPathfindingOutputs(true, new List<Vector2Int>(), new Vector2Int(startTile.column, startTile.row));
            return Status.Failure;
        }
        Vector2Int finalDestCoords = bbFinalDestinationPosition.Value;
        Tile finalTargetEntityTile = gridManager.GetTileAt(finalDestCoords.x, finalDestCoords.y);

        if (finalTargetEntityTile == null)
        {
            LogNodeMessage($"La tuile de destination finale ({finalDestCoords.x},{finalDestCoords.y}) est invalide/non trouvée.", isError: true, isVerboseOverride: true);
            SetPathfindingOutputs(true, new List<Vector2Int>(), new Vector2Int(startTile.column, startTile.row));
            return Status.Failure;
        }
         LogNodeMessage($"Destination finale: Tuile ({finalDestCoords.x},{finalDestCoords.y}), Bâtiment: {finalTargetEntityTile.currentBuilding?.name ?? "Aucun"}, Unité: {finalTargetEntityTile.currentUnit?.name ?? "Aucune"}", isVerboseOverride: true);


        if (startTile == finalTargetEntityTile)
        {
            LogNodeMessage($"Déjà sur la tuile de destination finale ({finalDestCoords.x},{finalDestCoords.y}). S'il s'agit d'une cible à attaquer, nous devrions être adjacents ou à portée.", isVerboseOverride: true);
            // Si la cible est sur la même tuile que l'attaquant, cela dépend des règles du jeu.
            // Typiquement pour une attaque, on veut être sur une tuile *adjacente* ou *à portée*.
            // Ici, on considère qu'aucun chemin n'est nécessaire, mais la logique d'engagement doit être vérifiée.
            // La méthode GetValidEngagementTiles déterminera si la position actuelle est une position d'engagement.
        }

        float attackRange = selfUnitInstance.AttackRange;
        LogNodeMessage($"--- Début FindSmartPath depuis ({startTile.column},{startTile.row}) vers entité sur tuile {finalTargetEntityTile.name} ({finalDestCoords.x},{finalDestCoords.y}) avec portée {attackRange} ---", isVerboseOverride: true);

        List<Vector2Int> path = CalculateAStarPathToEngagement(startTile, finalTargetEntityTile, attackRange);

        if (path != null && path.Count > 0)
        {
            SetPathfindingOutputs(false, path, path[0]);
            LogNodeMessage($"Chemin trouvé. Prochain pas: ({path[0].x},{path[0].y}). Longueur totale du chemin: {path.Count} pas.", isVerboseOverride: true);
            return Status.Success;
        }
        else if (path != null && path.Count == 0)
        {
            SetPathfindingOutputs(false, new List<Vector2Int>(), new Vector2Int(startTile.column, startTile.row));
            LogNodeMessage("Déjà en position d'engagement ou la cible est sur la même tuile et engageable. Aucun mouvement nécessaire.", isVerboseOverride: true);
            return Status.Success;
        }
        else // path == null (signifie qu'aucune tuile d'engagement n'a été trouvée ou qu'aucun chemin vers elles n'existe)
        {
            // Le log "Aucune tuile d'engagement valide trouvée." est déjà dans CalculateAStarPathToEngagement si c'est le cas.
            LogNodeMessage("Aucun chemin trouvé vers une position d'engagement.", isError: false, isVerboseOverride: true); // Forcer le log si échec
            SetPathfindingOutputs(true, new List<Vector2Int>(), new Vector2Int(startTile.column, startTile.row));
            return Status.Failure;
        }
    }

    private List<Vector2Int> CalculateAStarPathToEngagement(Tile startTile, Tile finalTargetEntityTile, float unitAttackRange)
    {
        LogNodeMessage($"CalculateAStarPathToEngagement: start=({startTile.column},{startTile.row}), targetEntityTile=({finalTargetEntityTile.column},{finalTargetEntityTile.row}), range={unitAttackRange}", isVerboseOverride: debugEngagementTileSearchLog);
        List<Tile> engagementTiles = GetValidEngagementTiles(finalTargetEntityTile, unitAttackRange);

        if (engagementTiles.Count == 0)
        {
            LogNodeMessage($"Aucune tuile d'engagement VALIDE trouvée pour la cible '{finalTargetEntityTile.name}' ({finalTargetEntityTile.column},{finalTargetEntityTile.row}) avec portée {unitAttackRange}. Vérifiez les logs de IsTileStandableForUnit si activés, ou la configuration de la carte/unités environnantes.", isError: false, isVerboseOverride: true); // Forcer ce log crucial
            string debugNeighbors = "";
            if (finalTargetEntityTile.Neighbors != null) {
                foreach(var n in finalTargetEntityTile.Neighbors) {
                    debugNeighbors += $" ({n.column},{n.row} Occ:{n.IsOccupied} Type:{n.tileType} EnvBlock:{n.currentEnvironment?.IsBlocking} Res:{n.IsReserved})";
                }
            }
            LogNodeMessage($"Détails cible: {finalTargetEntityTile.name} Occ:{finalTargetEntityTile.IsOccupied} Type:{finalTargetEntityTile.tileType} B:{finalTargetEntityTile.currentBuilding?.name} U:{finalTargetEntityTile.currentUnit?.name}. Voisins: {debugNeighbors}", false, isVerboseOverride: true);
            return null; // Échec si aucune tuile d'engagement.
        }
         LogNodeMessage($"Found {engagementTiles.Count} potential engagement tiles.", isVerboseOverride: debugEngagementTileSearchLog);
         if (debugEngagementTileSearchLog) {
            foreach(var et in engagementTiles) LogNodeMessage($" - Engagement Tile Candidate: ({et.column},{et.row})", false, true);
         }


        if (engagementTiles.Contains(startTile))
        {
            LogNodeMessage("L'unité est déjà sur une tuile d'engagement valide. Aucun chemin nécessaire.", isVerboseOverride: true);
            return new List<Vector2Int>(); // Chemin vide signifie "pas besoin de bouger"
        }

        List<PathNode> openList = new List<PathNode>();
        HashSet<Tile> closedSet = new HashSet<Tile>();
        Dictionary<Tile, PathNode> allNodes = new Dictionary<Tile, PathNode>();

        PathNode startNode = GetPathNode(startTile, allNodes);
        startNode.GCost = 0;
        startNode.HCost = CalculateMinHeuristicToEngagementTiles(startNode.TileNode, engagementTiles);
        openList.Add(startNode);

        PathNode bestEngagementNodeFound = null;

        int iterations = 0;
        int maxIterations = gridManager.columns * gridManager.rows * 2; // Safety break

        while (openList.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            openList.Sort();
            PathNode currentNode = openList[0];
            openList.RemoveAt(0);

            if (closedSet.Contains(currentNode.TileNode)) continue;
            closedSet.Add(currentNode.TileNode);

            if (engagementTiles.Contains(currentNode.TileNode))
            {
                bestEngagementNodeFound = currentNode;
                LogNodeMessage($"Chemin vers la tuile d'engagement ({currentNode.Coords.x},{currentNode.Coords.y}) trouvé. Coût G: {currentNode.GCost}", isVerboseOverride: debugEngagementTileSearchLog);
                break; // Chemin le plus court vers *une* des tuiles d'engagement trouvé
            }

            foreach (Tile neighborTile in currentNode.TileNode.Neighbors)
            {
                if (neighborTile == null || closedSet.Contains(neighborTile)) continue;

                if (!IsTileStandableForUnit(neighborTile, selfUnitInstance) && !engagementTiles.Contains(neighborTile)) // Si c'est une tuile d'engagement, on peut vouloir s'y arrêter même si on ne peut pas la "traverser" ensuite.
                {
                    //  LogNodeMessage($"Voisin ({neighborTile.column},{neighborTile.row}) non praticable pour A* pathing (sauf si c'est une tuile d'engagement finale).", false, debugAStarStepLog);
                    continue;
                }


                PathNode neighborPathNode = GetPathNode(neighborTile, allNodes);
                float tentativeGCost = currentNode.GCost + 1; // Coût de 1 par tuile

                if (tentativeGCost < neighborPathNode.GCost)
                {
                    neighborPathNode.Parent = currentNode;
                    neighborPathNode.GCost = tentativeGCost;
                    neighborPathNode.HCost = CalculateMinHeuristicToEngagementTiles(neighborPathNode.TileNode, engagementTiles);
                    if (!openList.Contains(neighborPathNode))
                    {
                        openList.Add(neighborPathNode);
                    }
                }
            }
        }
        if(iterations >= maxIterations) LogNodeMessage("A* a atteint le nombre maximum d'itérations.", true, true);

        if (bestEngagementNodeFound != null)
        {
            return ReconstructPath(bestEngagementNodeFound, startTile);
        }
        else
        {
            LogNodeMessage("Aucun chemin n'a pu être tracé vers une tuile d'engagement valide (A* n'a pas atteint de tuile d'engagement).", isVerboseOverride: true); // Forcer ce log aussi
            return null;
        }
    }


    // AMÉLIORATION : Logique plus claire pour trouver les tuiles d'où attaquer.
    private List<Tile> GetValidEngagementTiles(Tile actualTargetEntityTile, float unitAttackRange)
    {
        List<Tile> validStandpoints = new List<Tile>();
        if (actualTargetEntityTile == null || gridManager == null || selfUnitInstance == null) {
            LogNodeMessage("GetValidEngagementTiles: Préconditions non remplies (cible, grille ou unité null).", true, true);
            return validStandpoints;
        }

        int attackRangeInTiles = Mathf.Max(0, Mathf.FloorToInt(unitAttackRange)); // Assurer une portée positive ou nulle.

        // Note: HexGridManager.GetTilesWithinRange inclut la tuile centrale.
        List<Tile> tilesPotentiallyInRange = gridManager.GetTilesWithinRange(actualTargetEntityTile.column, actualTargetEntityTile.row, attackRangeInTiles);

        if (debugEngagementTileSearchLog) LogNodeMessage($"GetValidEngagementTiles: Cible {actualTargetEntityTile.name} ({actualTargetEntityTile.column},{actualTargetEntityTile.row}). Portée brute en tuiles: {attackRangeInTiles}. {tilesPotentiallyInRange.Count} tuiles à vérifier.", false, true);

        foreach (Tile potentialStandpoint in tilesPotentiallyInRange)
        {
            if (potentialStandpoint == null) continue;

            // Règle 1: On ne peut pas se tenir SUR la tuile de l'entité cible pour l'attaquer à distance.
            // Pour la mêlée (portée 1), on cherche une tuile adjacente. Si portée 0, on doit être sur la même tuile (cas spécial).
            if (attackRangeInTiles > 0 && potentialStandpoint == actualTargetEntityTile)
            {
                 if (debugEngagementTileSearchLog) LogNodeMessage($"  - Skipped: {potentialStandpoint.name} (is target tile, range > 0)", false, true);
                continue;
            }
            // Si portée 0, la seule tuile d'engagement est la tuile cible elle-même.
            if (attackRangeInTiles == 0 && potentialStandpoint != actualTargetEntityTile) {
                if (debugEngagementTileSearchLog) LogNodeMessage($"  - Skipped: {potentialStandpoint.name} (not target tile, range == 0)", false, true);
                continue;
            }


            // Règle 2: La tuile depuis laquelle on attaque doit être praticable par l'unité.
            if (!IsTileStandableForUnit(potentialStandpoint, selfUnitInstance))
            {
                 if (debugEngagementTileSearchLog) LogNodeMessage($"  - Skipped: {potentialStandpoint.name} (not standable)", false, true);
                continue;
            }

            // Règle 3: Vérifier si l'entité cible est réellement à portée DEPUIS ce "potentialStandpoint".
            // Ceci est important car GetTilesWithinRange autour de la CIBLE donne des tuiles.
            // Il faut ensuite vérifier si depuis CHACUNE de ces tuiles, la CIBLE est à portée de l'ATTAQUANT.
            // Pour une grille hexagonale, la distance est symétrique, donc si A est à X portée de B, B est à X portée de A.
            // Donc, si potentialStandpoint est dans les tilesPotentiallyInRange (autour de la cible),
            // alors la cible est bien à portée depuis potentialStandpoint.
            // On pourrait ajouter ici un check Line-of-Sight si nécessaire.

            validStandpoints.Add(potentialStandpoint);
            if (debugEngagementTileSearchLog) LogNodeMessage($"  - Added: {potentialStandpoint.name} as valid engagement standpoint.", false, true);
        }

        if (validStandpoints.Count == 0) {
            LogNodeMessage($"Aucune tuile d'engagement PRATICABLE trouvée pour {actualTargetEntityTile.name} à portée {unitAttackRange}.", false, true);
        }
        return validStandpoints;
    }

    // AMÉLIORATION : Logique plus claire pour savoir si une tuile est praticable.
    private bool IsTileStandableForUnit(Tile tileToStandOn, Unit attacker)
    {
        if (tileToStandOn == null || attacker == null) return false;

        string reason = "";
        bool isStandable = true;

        if (tileToStandOn.tileType != TileType.Ground) {
            isStandable = false; reason = $"Not Ground (Type: {tileToStandOn.tileType})";
        } else if (tileToStandOn.currentEnvironment != null && tileToStandOn.currentEnvironment.IsBlocking) {
            isStandable = false; reason = $"Blocking env '{tileToStandOn.currentEnvironment.EnvironmentName}'";
        } else if (tileToStandOn.currentUnit != null && tileToStandOn.currentUnit != attacker) {
            isStandable = false; reason = $"Occupied by OTHER unit '{tileToStandOn.currentUnit.name}'";
        } else if (tileToStandOn.currentBuilding != null) {
            // On ne peut généralement pas se tenir sur une tuile qui a un bâtiment pour attaquer une *autre* cible.
            // Si la cible finale EST ce bâtiment, c'est géré par GetValidEngagementTiles (on se mettra à côté).
            isStandable = false; reason = $"Has building '{tileToStandOn.currentBuilding.name}'";
        } else {
            Vector2Int tilePos = new Vector2Int(tileToStandOn.column, tileToStandOn.row);
            if (TileReservationController.Instance != null &&
                TileReservationController.Instance.IsTileReservedByOtherUnit(tilePos, attacker)) {
                isStandable = false; reason = $"Reserved by OTHER unit";
            }
        }

        return isStandable;
    }

    // ... (ReconstructPath, GetPathNode, CalculateMinHeuristicToEngagementTiles, SetPathfindingOutputs, CacheBlackboardVariables) ...
    // Assurez-vous que ces méthodes utilisent LogNodeMessage pour la verbosité contrôlée.

    protected override Status OnUpdate() { return Status.Success; } // Le calcul est fait dans OnStart

    private List<Vector2Int> ReconstructPath(PathNode targetNode, Tile startTile)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        PathNode current = targetNode;
        int safetyBreak = 0;
        while (current != null && current.TileNode != startTile && safetyBreak < (gridManager.columns * gridManager.rows))
        {
            path.Add(current.Coords);
            current = current.Parent;
            safetyBreak++;
        }
        if (safetyBreak >= (gridManager.columns * gridManager.rows)) {
            LogNodeMessage("Erreur dans ReconstructPath: boucle infinie ou chemin trop long.", isError:true, isVerboseOverride: true);
            return new List<Vector2Int>();
        }
        path.Reverse();
        return path;
    }
     private PathNode GetPathNode(Tile tile, Dictionary<Tile, PathNode> dict)
    {
        if (!dict.TryGetValue(tile, out PathNode node))
        {
            node = new PathNode(tile);
            dict[tile] = node;
        }
        return node;
    }

    private float CalculateMinHeuristicToEngagementTiles(Tile fromTile, List<Tile> targetEngagementTiles)
    {
        if (targetEngagementTiles.Count == 0) return float.MaxValue;
        float minHDist = float.MaxValue;
        foreach (Tile engagementTile in targetEngagementTiles)
        {
            minHDist = Mathf.Min(minHDist, gridManager.HexDistance(fromTile.column, fromTile.row, engagementTile.column, engagementTile.row));
        }
        return minHDist;
    }

    private void SetPathfindingOutputs(bool failed, List<Vector2Int> path, Vector2Int nextStep) {
        if (bbPathfindingFailed != null) bbPathfindingFailed.Value = failed;
        else LogNodeMessage("bbPathfindingFailed est null, ne peut pas écrire sur le Blackboard.", isError:true, isVerboseOverride: true);

        if (bbCurrentPath != null)
        {
            bbCurrentPath.Value = path ?? new List<Vector2Int>();
        } else LogNodeMessage("bbCurrentPath est null, ne peut pas écrire sur le Blackboard.", isError:true, isVerboseOverride: true);

        if (bbMovementTargetPosition != null)
        {
            bbMovementTargetPosition.Value = nextStep;
        } else LogNodeMessage("bbMovementTargetPosition est null, ne peut pas écrire sur le Blackboard.", isError:true, isVerboseOverride: true);
    }


    protected override void OnEnd()
    {
        blackboardVariablesCached = false;
        selfUnitInstance = null;
        LogNodeMessage("OnEnd", isVerboseOverride: true);
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;
        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null) {
            Debug.LogError($"[{GameObject?.name} - FindSmartStepNode] Critical: Agent or BlackboardRef missing.", GameObject);
            return false;
        }
        var blackboard = agent.BlackboardReference;
        bool success = true;

        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit)) { LogNodeMessage($"BBVar IN '{SELF_UNIT_VAR}' (type Unit) missing.", true, isVerboseOverride:true); success = false; }
        if (!blackboard.GetVariable(FINAL_DESTINATION_POS_VAR, out bbFinalDestinationPosition)) { LogNodeMessage($"BBVar IN '{FINAL_DESTINATION_POS_VAR}' missing.", true, isVerboseOverride:true); success = false; }

        if (!blackboard.GetVariable(CURRENT_PATH_VAR, out bbCurrentPath)) { LogNodeMessage($"BBVar OUT '{CURRENT_PATH_VAR}' missing.", true, isVerboseOverride:true); success = false; }
        if (!blackboard.GetVariable(MOVEMENT_TARGET_POS_VAR, out bbMovementTargetPosition)) { LogNodeMessage($"BBVar OUT '{MOVEMENT_TARGET_POS_VAR}' missing.", true, isVerboseOverride:true); success = false; }
        if (!blackboard.GetVariable(PATHFINDING_FAILED_VAR, out bbPathfindingFailed)) { LogNodeMessage($"BBVar OUT '{PATHFINDING_FAILED_VAR}' missing.", true, isVerboseOverride:true); success = false; }

        blackboardVariablesCached = success;
        if (!success) Debug.LogError($"[{GameObject?.name} - FindSmartStepNode] CRITICAL Blackboard variable(s) missing during cache. Check full log.", GameObject);
        return success;
    }
}