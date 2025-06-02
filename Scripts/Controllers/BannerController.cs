using UnityEngine;
using System.Collections.Generic;
using Game.Observers;

public class BannerController : MonoBehaviour
{
    public static bool Exists => instance != null;
    public Vector2Int CurrentBannerPosition { get; private set; }
    public bool HasActiveBanner { get; private set; }

    // Added references to the current building and tile for MouseManager
    private Building _currentBuilding;
    private Tile _currentTile;
    public Building CurrentBuilding => _currentBuilding;
    public Tile CurrentTile => _currentTile;

    private readonly List<IBannerObserver> observers = new List<IBannerObserver>();

    private static BannerController instance;
    public static BannerController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<BannerController>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("BannerController");
                    instance = obj.AddComponent<BannerController>();
                }
            }
            return instance;
        }
    }

    private void OnEnable()
    {
        RhythmManager.OnBeat += HandleBeat;
    }

    private void OnDisable()
    {
        RhythmManager.OnBeat -= HandleBeat;
    }

    private void HandleBeat()
    {
        if (HasActiveBanner)
        {
            // Broadcast position on every beat
            NotifyObservers(CurrentBannerPosition.x, CurrentBannerPosition.y);
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Multiple BannerController instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public void AddObserver(IBannerObserver observer)
    {
        if (observer == null) return;

        if (!observers.Contains(observer))
        {
            observers.Add(observer);

            // Immediately notify new observers if there's an active banner
            if (HasActiveBanner)
            {
                observer.OnBannerPlaced(CurrentBannerPosition.x, CurrentBannerPosition.y);
            }
        }
    }

    public void RemoveObserver(IBannerObserver observer)
    {
        if (observer == null) return;

        if (observers.Contains(observer))
        {
            observers.Remove(observer);
        }
    }

    /// <summary>
    /// New method that allows placing a banner by clicking on a building.
    /// Gets the tile from the building and passes it to the original PlaceBanner method.
    /// </summary>
    public bool PlaceBannerOnBuilding(Building building)
    {
        if (building == null)
        {
            return false;
        }

        // Store the current building reference
        _currentBuilding = building;

        // Get the tile that this building occupies
        Tile occupiedTile = building.GetOccupiedTile();
        if (occupiedTile == null)
        {
            _currentBuilding = null;
            return false;
        }

        // Call the original PlaceBanner method with the tile
        return PlaceBanner(occupiedTile);
    }

    /// <summary>
    /// Attempts to place the banner on the specified tile.
    /// Returns true if placement succeeded, false otherwise.
    /// </summary>
    public bool PlaceBanner(Tile tile)
    {
        // Ensure there is a building attached to the tile.
        if (tile.currentBuilding == null)
        {
            _currentBuilding = null;
            _currentTile = null;
            return false;
        }

        // Store the current tile reference
        _currentTile = tile;

        // If we didn't come through PlaceBannerOnBuilding, set the building reference too
        if (_currentBuilding == null || _currentBuilding != tile.currentBuilding)
        {
            _currentBuilding = tile.currentBuilding;
        }

        Building building = tile.currentBuilding;
        // Check if the building is targetable.
        if (!building.IsTargetable)
        {
            _currentBuilding = null;
            _currentTile = null;
            return false;
        }

        // Check if it's the same position as current
        Vector2Int newPosition = new Vector2Int(tile.column, tile.row);
        bool samePosition = HasActiveBanner && CurrentBannerPosition == newPosition;

        // Valid target: place the banner.
        CurrentBannerPosition = newPosition;
        HasActiveBanner = true;

        // Notify even if it's the same position - this helps with retriggering movement
        NotifyObservers(tile.column, tile.row);

        return true;
    }

    public void ClearBanner()
    {
        if (HasActiveBanner)
        {
            HasActiveBanner = false;
            CurrentBannerPosition = Vector2Int.zero;
            _currentBuilding = null;
            _currentTile = null;
        }
    }

    public void ForceNotifyObservers()
    {
        if (HasActiveBanner)
        {
            NotifyObservers(CurrentBannerPosition.x, CurrentBannerPosition.y);
        }
    }

    private void NotifyObservers(int column, int row)
    {
        // Create a copy of the observers list to safely iterate
        var observersCopy = new List<IBannerObserver>(observers);

        foreach (var observer in observersCopy)
        {
            if (observer != null)
            {
                try
                {
                    observer.OnBannerPlaced(column, row);
                }
                catch (System.Exception e)
                {
                    observers.Remove(observer);
                }
            }
        }

        // Clean up null observers after notification
        observers.RemoveAll(item => item == null);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}