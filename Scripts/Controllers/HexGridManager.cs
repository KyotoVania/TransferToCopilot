using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class HexGridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int columns = 10;
    public int rows = 10;
    public float tileWidth = 1f;  // Assumed to be the distance between the centers of adjacent tiles horizontally (long diameter for pointy top)
    public float tileHeight = 1f; // Assumed to be the full height of the hex (short diameter for pointy top)

    [Tooltip("If true, will use tile's existing row/column values from editor instead of calculating them based on world position.")]
    public bool respectExistingCoordinates = true;

    [Header("Debug Options")]
    [SerializeField] private bool showPathfindingLogs = false;

    // Instance for global access
    public static HexGridManager Instance { get; private set; }

    private List<Tile> allTilesInScene = new List<Tile>(); // Renamed for clarity
    private List<IMapObserver> mapObservers = new List<IMapObserver>(); // Renamed for clarity

    // 2D array to store tiles by their grid coordinates for quick access
    private Tile[,] tileGrid;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        tileGrid = new Tile[columns, rows]; // Initialize with configured dimensions
    }

    private void Start()
    {
        InitializeExistingTiles();
        SetupNeighborsForAllTiles(); // Renamed for clarity
        InitializeTileStatesAfterSetup(); // Renamed for clarity
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

        // If not respecting existing coordinates, we need a robust way to assign them.
        // For now, we'll assume if respectExistingCoordinates is false, tiles are dynamically generated or need recalculation.
        // If tiles are pre-placed and respectExistingCoordinates is true, their column/row should be set in the editor.

        foreach (Tile tile in allTilesInScene)
        {
            if (tile.gameObject.activeInHierarchy) // Process only active tiles
            {
                tile.SetGridManager(this); // Link tile to this manager

                if (!respectExistingCoordinates)
                {
                    // This world-to-grid calculation needs to be accurate for your hex layout
                    AssignGridCoordinatesFromWorldPosition(tile);
                }

                // Validate and clamp coordinates
                if (tile.column < 0 || tile.column >= columns || tile.row < 0 || tile.row >= rows)
                {
                    Debug.LogWarning($"[HexGridManager] Tile '{tile.name}' at ({tile.transform.position}) has out-of-bounds coordinates ({tile.column},{tile.row}). Clamping or ignoring. Ensure 'respectExistingCoordinates' is set correctly or coordinates are valid.", tile.gameObject);
                    // Option: Clamp or skip adding to tileGrid if invalid
                    tile.column = Mathf.Clamp(tile.column, 0, columns - 1);
                    tile.row = Mathf.Clamp(tile.row, 0, rows - 1);
                }

                // Store in grid array for quick lookup, potentially overwriting if multiple tiles claim same spot
                if (tileGrid[tile.column, tile.row] != null && tileGrid[tile.column, tile.row] != tile)
                {
                    Debug.LogWarning($"[HexGridManager] Multiple tiles assigned to grid position ({tile.column},{tile.row}). Overwriting '{tileGrid[tile.column, tile.row].name}' with '{tile.name}'. Check tile coordinate setup.", tile.gameObject);
                }
                tileGrid[tile.column, tile.row] = tile;
            }
        }
         if(showPathfindingLogs) Debug.Log($"[HexGridManager] Initialized {allTilesInScene.Count} tiles. Grid size: {columns}x{rows}.");
    }

    // Example for pointy-topped hexagons
    private void AssignGridCoordinatesFromWorldPosition(Tile tile)
    {
        // This is a simplified conversion and highly depends on your hex orientation and origin.
        // For pointy-topped hexagons where 'x' increases to the right and 'z' (world) is 'y' (axial)
        // This requires a proper axial or cube coordinate conversion.
        // The current calculation in your original script is for a more "square grid" like assignment.
        // For a robust solution, you'd convert world to axial/cube, then to offset.
        // Placeholder - you'll need to replace this with accurate hex grid coordinate conversion.

        // Simple, potentially inaccurate conversion based on your original:
        float hexWidthApproximation = tileWidth * 0.75f; // For horizontal spacing of pointy-topped hexes

        int col = Mathf.RoundToInt(tile.transform.position.x / hexWidthApproximation);

        // For pointy-topped, row calculation depends on if it's an odd or even column (offset rows)
        float yOffsetForRowCalc = (col % 2 == 1) ? tileHeight * 0.5f : 0f;
        int r = Mathf.RoundToInt((tile.transform.position.z - yOffsetForRowCalc) / tileHeight);

        tile.column = col;
        tile.row = r;

        // Debug.Log($"[HexGridManager] Assigned coords ({col},{r}) to tile {tile.name} at world pos {tile.transform.position}");
    }


    private void SetupNeighborsForAllTiles()
    {
        foreach (Tile tile in allTilesInScene)
        {
            if (tileGrid[tile.column, tile.row] == tile) // Ensure we are processing the tile registered at this grid coordinate
            {
                List<Tile> neighbors = FindNeighborsForTile(tile.column, tile.row);
                tile.SetNeighbors(neighbors);
            }
        }
    }

    public List<Tile> GetAdjacentTiles(Tile centerTile) // Public wrapper
    {
        if (centerTile == null) return new List<Tile>();
        return FindNeighborsForTile(centerTile.column, centerTile.row);
    }

    // Renamed to be more descriptive
    private List<Tile> FindNeighborsForTile(int col, int row)
    {
        List<Tile> neighbors = new List<Tile>();
        // Directions for pointy-topped hexagons
        // Parity (even/odd column) determines the offset for diagonal neighbors
        int[][] directions = (col % 2 == 0) ?
            new int[][] { new int[]{0, -1}, new int[]{1, -1}, new int[]{1, 0}, new int[]{0, 1}, new int[]{-1, 0}, new int[]{-1, -1} } : // Even column
            new int[][] { new int[]{0, -1}, new int[]{1, 0}, new int[]{1, 1}, new int[]{0, 1}, new int[]{-1, 1}, new int[]{-1, 0} };   // Odd column

        foreach (int[] dir in directions)
        {
            int neighborCol = col + dir[0];
            int neighborRow = row + dir[1];

            if (neighborCol >= 0 && neighborCol < columns &&
                neighborRow >= 0 && neighborRow < rows)
            {
                Tile neighbor = tileGrid[neighborCol, neighborRow];
                if (neighbor != null)
                {
                    neighbors.Add(neighbor);
                }
            }
        }
        return neighbors;
    }

    private List<Tile> FindNeighborsForTile_FlatTop_OddQ(int col, int row)
    {
        List<Tile> neighbors = new List<Tile>();
        int[][] directions;
        if (col % 2 == 0) // Colonnes PAIRES (0, 2, 4...)
        {
            directions = new int[][] {
                new int[]{1, 0},   // Est
                new int[]{0, 1},   // Sud-Est
                new int[]{-1, 1},  // Sud-Ouest
                new int[]{-1, 0},  // Ouest
                new int[]{-1, -1}, // Nord-Ouest
                new int[]{0, -1}   // Nord-Est
            };
        }
        else // Colonnes IMPAIRES (1, 3, 5...)
        {
            directions = new int[][] {
                new int[]{1, 0},   // Est
                new int[]{1, 1},   // Sud-Est
                new int[]{0, 1},   // Sud-Ouest
                new int[]{-1, 0},  // Ouest
                new int[]{0, -1},  // Nord-Ouest
                new int[]{1, -1}   // Nord-Est
            };
        }

        foreach (int[] dir in directions)
        {
            int neighborCol = col + dir[0];
            int neighborRow = row + dir[1];

            if (neighborCol >= 0 && neighborCol < columns &&
                neighborRow >= 0 && neighborRow < rows)
            {
                Tile neighbor = tileGrid[neighborCol, neighborRow];
                if (neighbor != null)
                {
                    neighbors.Add(neighbor);
                }
            }
        }
        return neighbors;
    }

    private void InitializeTileStatesAfterSetup()
    {
        StartCoroutine(DelayedTileInitialization());
    }

    private IEnumerator DelayedTileInitialization()
    {
        yield return new WaitForEndOfFrame(); // Wait for other Start methods to potentially complete
        foreach (Tile tile in allTilesInScene)
        {
            if (tileGrid[tile.column, tile.row] == tile) // Check it's the correct tile in the grid
            {
                MusicReactiveTile musicTile = tile as MusicReactiveTile;
                if (musicTile != null) // No need to check MusicManager.Instance here, MusicReactiveTile handles it
                {
                    musicTile.InitializeReactiveState();
                }
            }
        }
    }

    public Tile GetClosestTile(Vector3 position)
    {
        Tile closest = null;
        float minDistSq = Mathf.Infinity; // Use squared distance for efficiency
        foreach (Tile tile in allTilesInScene)
        {
            if (tile == null || !tile.gameObject.activeInHierarchy) continue;

            float distSq = (tile.transform.position - position).sqrMagnitude;
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                closest = tile;
            }
        }
        if (closest == null && allTilesInScene.Count > 0) {
             Debug.LogWarning($"[HexGridManager] GetClosestTile to {position} returned null, but there are {allTilesInScene.Count} tiles. Check tile states.");
        }
        return closest;
    }

    public Tile GetNextNeighborTowardsTarget(int currentCol, int currentRow, int targetCol, int targetRow, Unit requestingUnit = null)
    {
        if (showPathfindingLogs)
            Debug.Log($"[HexGridManager] Pathfinding: From ({currentCol},{currentRow}) to ({targetCol},{targetRow}) for unit '{requestingUnit?.name ?? "N/A"}'");

        if (currentCol == targetCol && currentRow == targetRow)
        {
            if (showPathfindingLogs) Debug.Log("[HexGridManager] Pathfinding: Already at target.");
            return null; // Already at the target
        }

        Tile currentTile = GetTileAt(currentCol, currentRow);
        if (currentTile == null)
        {
            Debug.LogError($"[HexGridManager] Pathfinding: Current tile at ({currentCol},{currentRow}) is null!");
            return null;
        }

        List<Tile> neighbors = currentTile.Neighbors;
        if (neighbors.Count == 0)
        {
            if (showPathfindingLogs) Debug.LogWarning($"[HexGridManager] Pathfinding: Tile ({currentCol},{currentRow}) has no neighbors.");
            return null;
        }

        Tile bestCandidate = null;
        float minDistanceToTarget = float.MaxValue;

        // Check if the target tile itself is a neighbor and is available
        foreach (Tile neighbor in neighbors)
        {
            if (neighbor.column == targetCol && neighbor.row == targetRow)
            {
                if (!neighbor.IsOccupied && // Not physically occupied
                    (TileReservationController.Instance == null || !TileReservationController.Instance.IsTileReservedByOtherUnit(new Vector2Int(neighbor.column, neighbor.row), requestingUnit))) // Not reserved by another unit
                {
                    if (showPathfindingLogs) Debug.Log($"[HexGridManager] Pathfinding: Target ({targetCol},{targetRow}) is a direct, available neighbor.");
                    return neighbor; // Prefer direct path to target if available
                }
                else
                {
                    if (showPathfindingLogs) Debug.Log($"[HexGridManager] Pathfinding: Target ({targetCol},{targetRow}) is a neighbor but occupied/reserved.");
                    // Don't return null yet, explore other neighbors that might lead towards target.
                }
                break; // Found the target among neighbors, no need to check its distance further for this loop
            }
        }

        // If target is not a direct available neighbor, find the best step among other neighbors
        foreach (Tile neighbor in neighbors)
        {
            if (neighbor.IsOccupied || (TileReservationController.Instance != null && TileReservationController.Instance.IsTileReservedByOtherUnit(new Vector2Int(neighbor.column, neighbor.row), requestingUnit)))
            {
                if (showPathfindingLogs) Debug.Log($"[HexGridManager] Pathfinding: Neighbor ({neighbor.column},{neighbor.row}) is occupied or reserved by other. Skipping.");
                continue;
            }

            float distance = HexDistance(neighbor.column, neighbor.row, targetCol, targetRow);
            if (distance < minDistanceToTarget)
            {
                minDistanceToTarget = distance;
                bestCandidate = neighbor;
            }
            // Optional: Tie-breaking (e.g., prefer straight lines or less "costly" tiles if you add costs)
            else if (distance == minDistanceToTarget && bestCandidate != null)
            {
                // Simple tie-breaking: prefer lower column, then lower row, or random
                if (neighbor.column < bestCandidate.column || (neighbor.column == bestCandidate.column && neighbor.row < bestCandidate.row))
                {
                    bestCandidate = neighbor;
                }
            }
        }

        if (bestCandidate != null)
        {
            if (showPathfindingLogs) Debug.Log($"[HexGridManager] Pathfinding: Best next step is ({bestCandidate.column},{bestCandidate.row}), dist to target: {minDistanceToTarget}.");
        }
        else
        {
            if (showPathfindingLogs) Debug.LogWarning($"[HexGridManager] Pathfinding: No available (unoccupied/unreserved by others) neighbor found to move towards target.");
        }
        return bestCandidate;
    }

    private Vector3Int ConvertOffsetToCube_OddQ(int col, int row)
    {
        int cube_x = col;
        int cube_z = row - (col - (col & 1)) / 2; // Correct pour odd-q (pointy ET flat)
        int cube_y = -cube_x - cube_z;
        return new Vector3Int(cube_x, cube_y, cube_z);
    }

    public int HexDistance(int col1, int row1, int col2, int row2)
    {
        Vector3Int cube1 = ConvertOffsetToCube_OddQ(col1, row1); // Utilise la même conversion
        Vector3Int cube2 = ConvertOffsetToCube_OddQ(col2, row2); // Utilise la même conversion

        return (Mathf.Abs(cube1.x - cube2.x) +
                Mathf.Abs(cube1.y - cube2.y) +
                Mathf.Abs(cube1.z - cube2.z)) / 2; // La formule de distance reste la même
    }

    public Tile GetTileAt(int column, int row)
    {
        if (column >= 0 && column < columns && row >= 0 && row < rows)
        {
            return tileGrid[column, row];
        }
        Debug.LogWarning($"[HexGridManager] Attempted to get tile out of bounds: ({column},{row})");
        return null;
    }

    public List<Tile> GetTilesWithinRange(int centerColumn, int centerRow, int range)
    {
        List<Tile> tilesInRange = new List<Tile>();
        if (range < 0) { Debug.LogError("Range must be non-negative."); return tilesInRange; }

        Tile centerTile = GetTileAt(centerColumn, centerRow);
        if (centerTile == null) { Debug.LogError($"Center tile ({centerColumn},{centerRow}) not found for GetTilesWithinRange."); return tilesInRange; }

        // For range 0, only the center tile
        if (range == 0) {
            tilesInRange.Add(centerTile);
            return tilesInRange;
        }

        Queue<Tile> queue = new Queue<Tile>();
        HashSet<Tile> visited = new HashSet<Tile>(); // To prevent reprocessing tiles and cycles
        Dictionary<Tile, int> distances = new Dictionary<Tile, int>(); // To store distance from center

        queue.Enqueue(centerTile);
        visited.Add(centerTile);
        distances[centerTile] = 0;
        tilesInRange.Add(centerTile); // Center tile is within range 0 of itself

        while (queue.Count > 0)
        {
            Tile current = queue.Dequeue();
            int currentDist = distances[current];

            if (currentDist >= range) continue; // Stop exploring from this tile if max range reached

            foreach (Tile neighbor in current.Neighbors)
            {
                if (neighbor != null && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    distances[neighbor] = currentDist + 1;
                    tilesInRange.Add(neighbor);
                    queue.Enqueue(neighbor); // Add to queue to explore its neighbors
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

    public int GetTilesCount() => allTilesInScene.Count;
    #endregion
}