using System;
using System.Collections.Generic;
using UnityEngine;

// Interface for observers that need tile reservation updates
public interface ITileReservationObserver
{
    void OnTileReservationChanged(Vector2Int tilePos, Unit reservingUnit, bool isReserved);
}

public class TileReservationController : MonoBehaviour
{
    // Singleton instance
    public static TileReservationController Instance { get; private set; }

    // Map of tile positions to their reserving units
    public Dictionary<Vector2Int, Unit> reservations = new Dictionary<Vector2Int, Unit>();

    // List of observers
    public List<ITileReservationObserver> observers = new List<ITileReservationObserver>();

    // Debug flag
    [SerializeField] private bool enableDebugLogs = false;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (enableDebugLogs)
            Debug.Log("TileReservationController initialized");
    }

    // Register an observer
    public void AddObserver(ITileReservationObserver observer)
    {
        if (!observers.Contains(observer))
        {
            observers.Add(observer);
            if (enableDebugLogs)
                Debug.Log($"Observer added: {observer}");
        }
    }

    // Unregister an observer
    public void RemoveObserver(ITileReservationObserver observer)
    {
        if (observers.Contains(observer))
        {
            observers.Remove(observer);
        }
    }

    // Notify all observers of reservation changes
    private void NotifyObservers(Vector2Int tilePos, Unit reservingUnit, bool isReserved)
    {
        foreach (var observer in observers)
        {
            observer.OnTileReservationChanged(tilePos, reservingUnit, isReserved);
        }
    }

    // Try to reserve a tile for a unit
    public bool TryReserveTile(Vector2Int tilePos, Unit requestingUnit)
    {
        // If tile is already reserved, check if it's by the same unit
        if (reservations.TryGetValue(tilePos, out Unit existingUnit))
        {
            if (existingUnit == requestingUnit)
            {
                // Already reserved by this unit
                return true;
            }

            if (enableDebugLogs)
                Debug.Log($"Tile at ({tilePos.x}, {tilePos.y}) already reserved by {existingUnit.name}, " +
                          $"denied for {requestingUnit.name}");

            return false;
        }

        // Reserve the tile
        reservations[tilePos] = requestingUnit;

        if (enableDebugLogs)
            Debug.Log($"Tile at ({tilePos.x}, {tilePos.y}) reserved by {requestingUnit.name}");

        // Notify observers
        NotifyObservers(tilePos, requestingUnit, true);

        return true;
    }

    // Release a tile reservation
    public void ReleaseTileReservation(Vector2Int tilePos, Unit releasingUnit)
    {
        if (reservations.TryGetValue(tilePos, out Unit existingUnit))
        {
            // Only allow the reserving unit to release the reservation
            if (existingUnit == releasingUnit)
            {
                reservations.Remove(tilePos);

                if (enableDebugLogs)
                    Debug.Log($"Tile at ({tilePos.x}, {tilePos.y}) reservation released by {releasingUnit.name}");

                // Notify observers
                NotifyObservers(tilePos, releasingUnit, false);
            }
            else if (enableDebugLogs)
            {
                Debug.LogWarning($"Unit {releasingUnit.name} attempted to release tile ({tilePos.x}, {tilePos.y}) " +
                                 $"reserved by {existingUnit.name}");
            }
        }
    }

    // Check if a tile is reserved
    public bool IsTileReserved(Vector2Int tilePos)
    {
        return reservations.ContainsKey(tilePos);
    }

    // Check if a tile is reserved by a specific unit
    public bool IsTileReservedBy(Vector2Int tilePos, Unit unit)
    {
        return reservations.TryGetValue(tilePos, out Unit reservingUnit) && reservingUnit == unit;
    }

    // Check if a tile is reserved by another unit
    public bool IsTileReservedByOtherUnit(Vector2Int tilePos, Unit requestingUnit)
    {
        return reservations.TryGetValue(tilePos, out Unit reservingUnit) && reservingUnit != requestingUnit;
    }

    // Get the unit that reserved a tile
    public Unit GetReservingUnit(Vector2Int tilePos)
    {
        reservations.TryGetValue(tilePos, out Unit reservingUnit);
        return reservingUnit;
    }

    // Get all current reservations
    public Dictionary<Vector2Int, Unit> GetAllReservations()
    {
        return new Dictionary<Vector2Int, Unit>(reservations);
    }

    // Clear all reservations (e.g., when restarting level)
    public void ClearAllReservations()
    {
        if (enableDebugLogs)
            Debug.Log("Clearing all tile reservations");

        // Store the reservations to notify about
        var oldReservations = new Dictionary<Vector2Int, Unit>(reservations);

        // Clear the dictionary
        reservations.Clear();

        // Notify about each cleared reservation
        foreach (var kvp in oldReservations)
        {
            NotifyObservers(kvp.Key, kvp.Value, false);
        }
    }
}