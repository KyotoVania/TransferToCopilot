using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages enemy unit coordination to prevent multiple units from targeting the same tile.
/// Acts as a central registry for planned destinations.
/// </summary>
public class EnemyUnitManager : MonoBehaviour
{
    // Singleton instance
    public static EnemyUnitManager Instance { get; private set; }

    // Dictionary mapping tile positions to the units targeting them
    private Dictionary<Vector2Int, EnemyUnit> plannedDestinations = new Dictionary<Vector2Int, EnemyUnit>();

    // Debug setting
    [SerializeField] private bool enableDebugLogging = false;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            Debug.LogWarning("[EnemyUnitManager] Multiple instances detected. Destroying duplicate.", gameObject);
        }
    }

    /// <summary>
    /// Registers a planned destination for an enemy unit
    /// </summary>
    /// <param name="unit">The enemy unit</param>
    /// <param name="position">The target position</param>
    /// <returns>True if registration was successful, false if already reserved</returns>
    public static bool RegisterPlannedDestination(EnemyUnit unit, Vector2Int position)
    {
        if (Instance == null)
        {
            // Create instance if none exists
            new GameObject("EnemyUnitManager").AddComponent<EnemyUnitManager>();
        }

        // Check if position is already reserved by another unit
        if (Instance.plannedDestinations.TryGetValue(position, out EnemyUnit existingUnit))
        {
            if (existingUnit != unit && existingUnit != null)
            {
                if (Instance.enableDebugLogging)
                {
                    Debug.Log($"Position ({position.x}, {position.y}) already reserved by {existingUnit.name}");
                }
                return false;
            }
        }

        // Unregister any previous targets for this unit
        UnregisterUnit(unit);

        // Register the new target
        Instance.plannedDestinations[position] = unit;

        if (Instance.enableDebugLogging)
        {
            Debug.Log($"{unit.name} registered target at ({position.x}, {position.y})");
        }

        return true;
    }

    /// <summary>
    /// Unregisters a planned destination for an enemy unit
    /// </summary>
    /// <param name="unit">The enemy unit</param>
    /// <param name="position">The target position</param>
    public static void UnregisterPlannedDestination(EnemyUnit unit, Vector2Int position)
    {
        if (Instance == null)
            return;

        // Only remove if this unit is the one that registered it
        if (Instance.plannedDestinations.TryGetValue(position, out EnemyUnit existingUnit) && existingUnit == unit)
        {
            Instance.plannedDestinations.Remove(position);

            if (Instance.enableDebugLogging)
            {
                Debug.Log($"{unit.name} unregistered target at ({position.x}, {position.y})");
            }
        }
    }

    /// <summary>
    /// Unregisters all destinations for a specific unit
    /// </summary>
    /// <param name="unit">The enemy unit</param>
    public static void UnregisterUnit(EnemyUnit unit)
    {
        if (Instance == null)
            return;

        List<Vector2Int> keysToRemove = new List<Vector2Int>();

        // Find all positions registered by this unit
        foreach (var kvp in Instance.plannedDestinations)
        {
            if (kvp.Value == unit)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        // Remove them all
        foreach (var key in keysToRemove)
        {
            Instance.plannedDestinations.Remove(key);

            if (Instance.enableDebugLogging)
            {
                Debug.Log($"{unit.name} unregistered target at ({key.x}, {key.y})");
            }
        }
    }

    /// <summary>
    /// Checks if a position is reserved by any enemy unit
    /// </summary>
    /// <param name="position">The position to check</param>
    /// <param name="excludeUnit">Optional unit to exclude from the check</param>
    /// <returns>True if the position is reserved, false otherwise</returns>
    public static bool IsTileReserved(Vector2Int position, EnemyUnit excludeUnit = null)
    {
        if (Instance == null)
            return false;

        if (Instance.plannedDestinations.TryGetValue(position, out EnemyUnit unit))
        {
            // Position is reserved by a different unit
            return unit != null && unit != excludeUnit;
        }

        return false;
    }

    /// <summary>
    /// Gets all currently reserved positions for debugging
    /// </summary>
    /// <returns>Dictionary of positions and units</returns>
    public static Dictionary<Vector2Int, EnemyUnit> GetAllReservedPositions()
    {
        if (Instance == null)
            return new Dictionary<Vector2Int, EnemyUnit>();

        return new Dictionary<Vector2Int, EnemyUnit>(Instance.plannedDestinations);
    }

    /// <summary>
    /// Clears all reservations (useful when changing scenes or resetting)
    /// </summary>
    public static void ClearAllReservations()
    {
        if (Instance == null)
            return;

        Instance.plannedDestinations.Clear();

        if (Instance.enableDebugLogging)
        {
            Debug.Log("Cleared all enemy unit reservations");
        }
    }

    // Cleanup on destroy
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}