using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class HexGridManager : MonoBehaviour
{
    [Header("Grid Logical Settings")] // MODIFIÉ: Titre de section
    [Tooltip("La coordonnée de colonne logique la plus à gauche (peut être négative).")]
    public int minColumn = 0; // NOUVEAU
    [Tooltip("La coordonnée de colonne logique la plus à droite.")]
    public int maxColumn = 9; // ANCIENNEMENT 'columns', renommé pour clarté
    [Tooltip("La coordonnée de ligne logique la plus en bas (peut être négative).")]
    public int minRow = 0;    // NOUVEAU
    [Tooltip("La coordonnée de ligne logique la plus en haut.")]
    public int maxRow = 9;    // ANCIENNEMENT 'rows', renommé pour clarté

    [Header("Tile Physical Settings")] // MODIFIÉ: Titre de section
    public float tileWidth = 1f;
    public float tileHeight = 1f;

    [Tooltip("If true, will use tile's existing row/column values from editor instead of calculating them based on world position.")]
    public bool respectExistingCoordinates = true;

    [Header("Debug Options")]
    [SerializeField] private bool showPathfindingLogs = false;

    public static HexGridManager Instance { get; private set; }

    private List<Tile> allTilesInScene = new List<Tile>();
    private List<IMapObserver> mapObservers = new List<IMapObserver>();

    private Tile[,] tileGrid; // Le tableau interne reste basé sur 0
    private int gridArrayWidth;  // NOUVEAU: Largeur réelle du tableau
    private int gridArrayHeight; // NOUVEAU: Hauteur réelle du tableau

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // NOUVEAU: Calculer les dimensions réelles du tableau
        gridArrayWidth = maxColumn - minColumn + 1;
        gridArrayHeight = maxRow - minRow + 1;

        if (gridArrayWidth <= 0 || gridArrayHeight <= 0)
        {
            Debug.LogError("[HexGridManager] Grid dimensions (maxColumn/maxRow - minColumn/minRow + 1) result in non-positive size. Check your min/max coordinate settings!", this);
            enabled = false;
            return;
        }
        tileGrid = new Tile[gridArrayWidth, gridArrayHeight];
        if (showPathfindingLogs) Debug.Log($"[HexGridManager] Awake: tileGrid initialized with array dimensions {gridArrayWidth}x{gridArrayHeight} to cover logical cols [{minColumn}..{maxColumn}] and rows [{minRow}..{maxRow}].");
    }

    private void Start()
    {
        InitializeExistingTiles();
        SetupNeighborsForAllTiles();
        InitializeTileStatesAfterSetup();
    }

    // NOUVEAU: Méthode pour convertir les coordonnées logiques en indices de tableau
    private Vector2Int ToArrayIndex(int logicalColumn, int logicalRow)
    {
        return new Vector2Int(logicalColumn - minColumn, logicalRow - minRow);
    }

    // NOUVEAU: Vérifie si les coordonnées logiques sont dans les limites définies
    private bool IsLogicalCoordInBounds(int logicalColumn, int logicalRow)
    {
        return logicalColumn >= minColumn && logicalColumn <= maxColumn &&
               logicalRow >= minRow && logicalRow <= maxRow;
    }

    private void InitializeExistingTiles()
    {
        // Correction de l'avertissement CS0618
        Tile[] sceneTilesArray = FindObjectsByType<Tile>(FindObjectsSortMode.None);
        allTilesInScene.AddRange(sceneTilesArray);

        if (allTilesInScene.Count == 0)
        {
            Debug.LogWarning("[HexGridManager] No Tile objects found in the scene during InitializeExistingTiles.");
            return;
        }

        int validTilesInitialized = 0; // NOUVEAU: Compteur pour le log

        foreach (Tile tile in allTilesInScene)
        {
            if (tile.gameObject.activeInHierarchy)
            {
                tile.SetGridManager(this);

                if (!respectExistingCoordinates)
                {
                    AssignGridCoordinatesFromWorldPosition(tile);
                    // Rappel: AssignGridCoordinatesFromWorldPosition doit être revu si vous utilisez
                    // des grilles hexagonales complexes ou des origines de monde arbitraires.
                    // Il doit assigner des tile.column et tile.row LOGIQUES.
                }

                // MODIFIÉ: Valider par rapport aux limites logiques min/max
                if (!IsLogicalCoordInBounds(tile.column, tile.row))
                {
                    Debug.LogWarning($"[HexGridManager] Tile '{tile.name}' at ({tile.transform.position}) has out-of-bounds logical coordinates ({tile.column},{tile.row}). It will be ignored. Logical Bounds: Cols [{minColumn}..{maxColumn}], Rows [{minRow}..{maxRow}]", tile.gameObject);
                    continue; // Ignorer cette tuile si elle est hors des limites logiques définies
                }

                // MODIFIÉ: Utiliser ToArrayIndex pour stocker dans tileGrid
                Vector2Int arrayIndices = ToArrayIndex(tile.column, tile.row);

                if (tileGrid[arrayIndices.x, arrayIndices.y] != null && tileGrid[arrayIndices.x, arrayIndices.y] != tile)
                {
                    Debug.LogWarning($"[HexGridManager] Multiple tiles assigned to logical grid position ({tile.column},{tile.row}) which maps to array index ({arrayIndices.x},{arrayIndices.y}). Overwriting '{tileGrid[arrayIndices.x, arrayIndices.y].name}' with '{tile.name}'. Check tile coordinate setup.", tile.gameObject);
                }
                tileGrid[arrayIndices.x, arrayIndices.y] = tile;
                validTilesInitialized++; // NOUVEAU
            }
        }
        // MODIFIÉ: Log de fin
        if(showPathfindingLogs) Debug.Log($"[HexGridManager] InitializeExistingTiles: Processed {allTilesInScene.Count} tiles from scene. Initialized {validTilesInitialized} valid tiles within logical bounds. Array size: {gridArrayWidth}x{gridArrayHeight}. Logical bounds: Col({minColumn} to {maxColumn}), Row({minRow} to {maxRow}).");
    }

    // IMPORTANT: Cette méthode doit assigner des coordonnées LOGIQUES à tile.column et tile.row.
    // La logique actuelle est très basique et pourrait ne pas fonctionner correctement pour des grilles hexagonales
    // complexes ou si l'origine de votre monde n'est pas (0,0) pour la tuile (minColumn, minRow).
    // Vous devrez peut-être implémenter un algorithme de conversion de coordonnées monde vers axial/cube puis vers offset logique.
    private void AssignGridCoordinatesFromWorldPosition(Tile tile)
    {
        // Exemple simplifié (POINTY-TOPPED, origine de la grille logique (minColumn, minRow) à l'origine du monde (0,0,0) )
        // CECI EST UN PSEUDO-CODE APPROXIMATIF ET DEVRA ÊTRE ADAPTÉ À VOTRE GÉOMÉTRIE EXACTE.
        float approxHexWidth = tileWidth * 0.75f; // Espacement horizontal pour pointy-topped
        float worldX = tile.transform.position.x;
        float worldZ = tile.transform.position.z;

        // Conversion grossière en coordonnées "offset" logiques.
        // Vous aurez besoin d'une conversion plus robuste pour les hexagones.
        // Par exemple, en utilisant des coordonnées axiales ou cubiques comme intermédiaires.
        // Pour l'instant, cela ressemble plus à une grille carrée.

        // Exemple:
        // int q = Mathf.RoundToInt((worldX * Mathf.Sqrt(3)/3 - worldZ / 3) / (tileWidth/2)); // Conversion vers axial q
        // int r = Mathf.RoundToInt((worldZ * 2/3) / (tileHeight/2)); // Conversion vers axial r
        // tile.column = q; // Ou une conversion q,r vers offset si vous préférez les offsets logiques
        // tile.row = r;    // (ce qui est le cas ici avec minColumn, minRow)

        // Placeholder (ancienne logique, à remplacer par une vraie conversion hex vers logique):
        int col = minColumn + Mathf.RoundToInt(worldX / approxHexWidth);
        int r = minRow + Mathf.RoundToInt(worldZ / tileHeight);
        // La logique ci-dessus est très probablement incorrecte pour une grille hexagonale.
        // Vous DEVEZ la remplacer par un algorithme correct de conversion world-to-hex-offset.

        tile.column = col;
        tile.row = r;

        // Debug.Log($"[HexGridManager] AssignGridCoordinatesFromWorldPosition: Assigned logical coords ({col},{r}) to tile {tile.name} at world pos {tile.transform.position}");
    }


    private void SetupNeighborsForAllTiles()
    {
        foreach (Tile tile in allTilesInScene)
        {
            // MODIFIÉ: S'assurer que la tuile est dans les limites avant de chercher des voisins
            if (tile != null && IsLogicalCoordInBounds(tile.column, tile.row))
            {
                // S'assurer aussi que c'est bien CETTE tuile qui est enregistrée aux bonnes coordonnées
                Vector2Int arrayIndices = ToArrayIndex(tile.column, tile.row);
                if (tileGrid[arrayIndices.x, arrayIndices.y] == tile)
                {
                    List<Tile> neighbors = FindNeighborsForTile(tile.column, tile.row);
                    tile.SetNeighbors(neighbors);
                }
            }
        }
    }

    public List<Tile> GetAdjacentTiles(Tile centerTile)
    {
        if (centerTile == null || !IsLogicalCoordInBounds(centerTile.column, centerTile.row)) return new List<Tile>();
        return FindNeighborsForTile(centerTile.column, centerTile.row);
    }

    // Les paramètres logicalCol et logicalRow sont des coordonnées logiques
    private List<Tile> FindNeighborsForTile(int logicalCol, int logicalRow)
    {
        List<Tile> neighbors = new List<Tile>();
        // Directions pour pointy-topped (basées sur les coordonnées logiques)
        int[][] directions = (logicalCol % 2 == 0) ? // Ou parité de (logicalCol - minColumn) si la structure offset dépend de l'index 0 du tableau
            new int[][] { new int[]{0, -1}, new int[]{1, -1}, new int[]{1, 0}, new int[]{0, 1}, new int[]{-1, 0}, new int[]{-1, -1} } :
            new int[][] { new int[]{0, -1}, new int[]{1, 0}, new int[]{1, 1}, new int[]{0, 1}, new int[]{-1, 1}, new int[]{-1, 0} };

        foreach (int[] dir in directions)
        {
            int neighborLogicalCol = logicalCol + dir[0];
            int neighborLogicalRow = logicalRow + dir[1];

            // MODIFIÉ: Valider les coordonnées logiques du voisin et utiliser GetTileAt
            Tile neighbor = GetTileAt(neighborLogicalCol, neighborLogicalRow); // GetTileAt gère la conversion et les limites
            if (neighbor != null)
            {
                neighbors.Add(neighbor);
            }
        }
        return neighbors;
    }

    // FindNeighborsForTile_FlatTop_OddQ serait à adapter de la même manière si vous l'utilisez.

    private void InitializeTileStatesAfterSetup()
    {
        StartCoroutine(DelayedTileInitialization());
    }

    private IEnumerator DelayedTileInitialization()
    {
        yield return new WaitForEndOfFrame();
        foreach (Tile tile in allTilesInScene)
        {
            // MODIFIÉ: Vérifier si la tuile est valide et à sa place dans la grille logique
            if (tile != null && IsLogicalCoordInBounds(tile.column, tile.row))
            {
                Vector2Int arrayIndices = ToArrayIndex(tile.column, tile.row);
                if (tileGrid[arrayIndices.x, arrayIndices.y] == tile)
                {
                    MusicReactiveTile musicTile = tile as MusicReactiveTile;
                    if (musicTile != null)
                    {
                        musicTile.InitializeReactiveVisualState(); // Nom de méthode corrigé
                    }
                }
            }
        }
    }

    public Tile GetClosestTile(Vector3 position)
    {
        Tile closest = null;
        float minDistSq = Mathf.Infinity;
        foreach (Tile tile in allTilesInScene)
        {
            if (tile == null || !tile.gameObject.activeInHierarchy) continue;
            // NOUVEAU: S'assurer que la tuile est dans les limites logiques avant de la considérer
            if (!IsLogicalCoordInBounds(tile.column, tile.row)) continue;

            float distSq = (tile.transform.position - position).sqrMagnitude;
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                closest = tile;
            }
        }
        if (closest == null && allTilesInScene.Count > 0 && showPathfindingLogs) {
             Debug.LogWarning($"[HexGridManager] GetClosestTile to {position} returned null, but there are tiles in scene. Check tile states and logical bounds setup.");
        }
        return closest;
    }

    // Les paramètres currentCol, currentRow, targetCol, targetRow sont LOGIQUES
    public Tile GetNextNeighborTowardsTarget(int currentCol, int currentRow, int targetCol, int targetRow, Unit requestingUnit = null)
    {
        // La logique interne de cette méthode (A*, distances, etc.) devrait déjà fonctionner avec
        // des coordonnées logiques (positives ou négatives).
        // Il faut juste s'assurer que tous les appels à GetTileAt et les accès aux voisins
        // utilisent bien des coordonnées logiques et que les bornes sont respectées.

        if (showPathfindingLogs)
            Debug.Log($"[HexGridManager] Pathfinding: From logical ({currentCol},{currentRow}) to logical ({targetCol},{targetRow}) for unit '{requestingUnit?.name ?? "N/A"}'");

        if (currentCol == targetCol && currentRow == targetRow)
        {
            if (showPathfindingLogs) Debug.Log("[HexGridManager] Pathfinding: Already at target.");
            return null;
        }

        Tile currentTile = GetTileAt(currentCol, currentRow); // Utilise la version modifiée
        if (currentTile == null)
        {
            Debug.LogError($"[HexGridManager] Pathfinding: Current tile at logical ({currentCol},{currentRow}) is null!", this);
            return null;
        }

        List<Tile> neighbors = currentTile.Neighbors; // Neighbors sont déjà des objets Tile valides
        if (neighbors.Count == 0)
        {
            if (showPathfindingLogs) Debug.LogWarning($"[HexGridManager] Pathfinding: Tile logical ({currentCol},{currentRow}) has no neighbors.");
            return null;
        }

        Tile bestCandidate = null;
        float minDistanceToTarget = float.MaxValue;

        foreach (Tile neighbor in neighbors) // neighbor.column et neighbor.row sont déjà logiques
        {
            if (neighbor.column == targetCol && neighbor.row == targetRow)
            {
                if (!neighbor.IsOccupied &&
                    (TileReservationController.Instance == null || !TileReservationController.Instance.IsTileReservedByOtherUnit(new Vector2Int(neighbor.column, neighbor.row), requestingUnit)))
                {
                    if (showPathfindingLogs) Debug.Log($"[HexGridManager] Pathfinding: Target logical ({targetCol},{targetRow}) is a direct, available neighbor.");
                    return neighbor;
                }
                else
                {
                    if (showPathfindingLogs) Debug.Log($"[HexGridManager] Pathfinding: Target logical ({targetCol},{targetRow}) is a neighbor but occupied/reserved.");
                }
                break;
            }
        }

        foreach (Tile neighbor in neighbors)
        {
            if (neighbor.IsOccupied || (TileReservationController.Instance != null && TileReservationController.Instance.IsTileReservedByOtherUnit(new Vector2Int(neighbor.column, neighbor.row), requestingUnit)))
            {
                if (showPathfindingLogs) Debug.Log($"[HexGridManager] Pathfinding: Neighbor logical ({neighbor.column},{neighbor.row}) is occupied or reserved by other. Skipping.");
                continue;
            }

            float distance = HexDistance(neighbor.column, neighbor.row, targetCol, targetRow); // HexDistance utilise des coords logiques
            if (distance < minDistanceToTarget)
            {
                minDistanceToTarget = distance;
                bestCandidate = neighbor;
            }
            else if (distance == minDistanceToTarget && bestCandidate != null)
            {
                if (neighbor.column < bestCandidate.column || (neighbor.column == bestCandidate.column && neighbor.row < bestCandidate.row))
                {
                    bestCandidate = neighbor;
                }
            }
        }

        if (bestCandidate != null)
        {
            if (showPathfindingLogs) Debug.Log($"[HexGridManager] Pathfinding: Best next step is logical ({bestCandidate.column},{bestCandidate.row}), dist to target: {minDistanceToTarget}.");
        }
        else
        {
            if (showPathfindingLogs) Debug.LogWarning($"[HexGridManager] Pathfinding: No available (unoccupied/unreserved by others) neighbor found to move towards target.");
        }
        return bestCandidate;
    }

    // ConvertOffsetToCube_OddQ et HexDistance devraient fonctionner correctement avec des coordonnées logiques négatives
    // car les maths sous-jacentes des coordonnées cubiques le permettent.
    private Vector3Int ConvertOffsetToCube_OddQ(int col, int row)
    {
        int cube_x = col;
        int cube_z = row - (col - (col & 1)) / 2;
        int cube_y = -cube_x - cube_z;
        return new Vector3Int(cube_x, cube_y, cube_z);
    }

    public int HexDistance(int col1, int row1, int col2, int row2)
    {
        Vector3Int cube1 = ConvertOffsetToCube_OddQ(col1, row1);
        Vector3Int cube2 = ConvertOffsetToCube_OddQ(col2, row2);
        return (Mathf.Abs(cube1.x - cube2.x) + Mathf.Abs(cube1.y - cube2.y) + Mathf.Abs(cube1.z - cube2.z)) / 2;
    }

    // MODIFIÉ: Les paramètres logicalColumn, logicalRow sont des coordonnées logiques
    public Tile GetTileAt(int logicalColumn, int logicalRow)
    {
        // MODIFIÉ: Valider par rapport aux limites logiques et convertir en indices de tableau
        if (IsLogicalCoordInBounds(logicalColumn, logicalRow))
        {
            Vector2Int arrayIndices = ToArrayIndex(logicalColumn, logicalRow);
            return tileGrid[arrayIndices.x, arrayIndices.y];
        }
        // Optionnel: Log verbeux pour les accès hors limites
        // if (showPathfindingLogs) Debug.LogWarning($"[HexGridManager] Attempted to get tile at logical ({logicalColumn},{logicalRow}) which is out of defined logical bounds: Cols [{minColumn}..{maxColumn}], Rows [{minRow}..{maxRow}]");
        return null;
    }

    // Les paramètres centerColumn, centerRow sont LOGIQUES
    public List<Tile> GetTilesWithinRange(int centerLogicalColumn, int centerLogicalRow, int range)
    {
        List<Tile> tilesInRange = new List<Tile>();
        if (range < 0) { Debug.LogError("Range must be non-negative."); return tilesInRange; }

        Tile centerTile = GetTileAt(centerLogicalColumn, centerLogicalRow); // Utilise la version modifiée
        if (centerTile == null)
        {
            // MODIFIÉ: Log si la tuile centrale n'est pas trouvée avec des coordonnées logiques
            Debug.LogError($"[HexGridManager] Center tile at logical ({centerLogicalColumn},{centerLogicalRow}) not found for GetTilesWithinRange.");
            return tilesInRange;
        }

        if (range == 0) {
            tilesInRange.Add(centerTile);
            return tilesInRange;
        }

        Queue<Tile> queue = new Queue<Tile>();
        HashSet<Tile> visited = new HashSet<Tile>();
        Dictionary<Tile, int> distances = new Dictionary<Tile, int>();

        queue.Enqueue(centerTile);
        visited.Add(centerTile);
        distances[centerTile] = 0;
        tilesInRange.Add(centerTile);

        while (queue.Count > 0)
        {
            Tile current = queue.Dequeue();
            int currentDist = distances[current];

            if (currentDist >= range) continue;

            foreach (Tile neighbor in current.Neighbors) // Les voisins sont déjà des tuiles valides
            {
                if (neighbor != null && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    distances[neighbor] = currentDist + 1;
                    tilesInRange.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
        return tilesInRange;
    }

    #region Observer Pattern Management
    public void RegisterObserver(IMapObserver observer)
    {
        if (!mapObservers.Contains(observer)) mapObservers.Add(observer);
    }

    public void UnregisterObserver(IMapObserver observer)
    {
        mapObservers.Remove(observer);
    }

    public void NotifyTileChanged(Tile tile)
    {
        foreach (var observer in mapObservers)
        {
            observer.OnTileStateChanged(tile);
        }
    }

    public int GetTilesCount() => allTilesInScene.Count(t => t != null && IsLogicalCoordInBounds(t.column, t.row)); // Compte seulement les tuiles valides
    #endregion
}