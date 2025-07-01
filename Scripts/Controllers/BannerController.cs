using UnityEngine;
using System.Collections.Generic;
using Game.Observers;
using UnityEngine.InputSystem;
using System.Linq;

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

    // Targeting system for gamepad controls
    [Header("Targeting Settings")]
    [SerializeField] private bool isTargetingMode = false;
    [SerializeField] private int currentTargetIndex = 0;
    private List<Building> targetableBuildings = new List<Building>();
    private Building currentlyHighlightedBuilding;
    
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
    //BuildingSelectionFeedback
    private void OnEnable()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += HandleBeat;
        }

        // Subscribe to camera lock toggle requests
        RhythmGameCameraController.OnToggleCameraLockRequested += HandleToggleCameraLockRequest;

        // Subscribe to gamepad targeting inputs
        if (InputManager.Instance != null)
        {
            InputManager.Instance.GameplayActions.CycleTarget.performed += OnCycleTargetPressed;
            InputManager.Instance.GameplayActions.PlaceBanner.performed += OnPlaceBannerPressed;
        }
    }

    private void OnDisable()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleBeat;
        }

        // Unsubscribe from camera events
        RhythmGameCameraController.OnToggleCameraLockRequested -= HandleToggleCameraLockRequest;

        // Unsubscribe from gamepad inputs
        if (InputManager.Instance != null)
        {
            InputManager.Instance.GameplayActions.CycleTarget.performed -= OnCycleTargetPressed;
            InputManager.Instance.GameplayActions.PlaceBanner.performed -= OnPlaceBannerPressed;
        }
    }

    private void HandleBeat(float beatDuration)
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
        if (building == null) return false;

        Tile occupiedTile = building.GetOccupiedTile();
        if (occupiedTile == null) return false;

        // Avant de changer, sauvegardons l'ancien bâtiment pour nettoyer son outline.
        Building previousBuildingWithBanner = _currentBuilding;

        // Appel à la méthode de placement principale
        bool placementSuccess = PlaceBanner(occupiedTile);

        if (placementSuccess)
        {
            // Si le placement réussit, mettons à jour les outlines.

            // 1. Nettoyer l'outline de l'ancien bâtiment s'il était différent.
            if (previousBuildingWithBanner != null && previousBuildingWithBanner != building)
            {
                var oldFeedback = previousBuildingWithBanner.GetComponent<BuildingSelectionFeedback>();
                if (oldFeedback != null)
                {
                    oldFeedback.SetOutlineState(OutlineState.Default);
                }
            }

            // 2. Mettre le nouveau bâtiment en état "Selected".
            var newFeedback = building.GetComponent<BuildingSelectionFeedback>();
            if (newFeedback != null)
            {
                newFeedback.SetOutlineState(OutlineState.Selected);
            }
        }
        
        return placementSuccess;
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
            if (_currentBuilding != null)
            {
                var feedback = _currentBuilding.GetComponent<BuildingSelectionFeedback>();
                if (feedback != null)
                {
                    // Remettre à l'état par défaut quand la bannière est retirée
                    feedback.SetOutlineState(OutlineState.Default);
                }
            }

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

    #region Targeting System for Gamepad

    /// <summary>
    /// Handles the toggle camera lock request from the camera controller
    /// </summary>
    private void HandleToggleCameraLockRequest()
    {
        if (isTargetingMode)
        {
            ExitTargetingMode();
        }
        else
        {
            EnterTargetingMode();
        }
    }

    /// <summary>
    /// Enters targeting mode: scans for buildings, selects first target, locks camera
    /// </summary>
    private void EnterTargetingMode()
    {
        Debug.Log("[BannerController] Entering targeting mode");
        
        // Scan for all targetable buildings
        ScanForTargetableBuildings();
        
        if (targetableBuildings.Count == 0)
        {
            Debug.LogWarning("[BannerController] No targetable buildings found!");
            return;
        }

        isTargetingMode = true;
        currentTargetIndex = 0;

        // Find the closest building to current camera position as default target
        SelectClosestBuildingAsDefault();
        
        // Lock camera on the selected target and highlight it
        UpdateCurrentTarget();

        Debug.Log($"[BannerController] Targeting mode activated with {targetableBuildings.Count} targetable buildings");
    }

    /// <summary>
    /// Exits targeting mode: unlocks camera, clears highlights
    /// </summary>
    private void ExitTargetingMode()
    {
        Debug.Log("[BannerController] Exiting targeting mode");
        
        isTargetingMode = false;
        
        // Clear building highlight
        ClearBuildingHighlight();
        
        // Unlock camera
        var cameraController = FindFirstObjectByType<RhythmGameCameraController>();
        if (cameraController != null)
        {
            cameraController.UnlockCamera();
        }

        // Clear targetable buildings list
        targetableBuildings.Clear();
        currentTargetIndex = 0;
        currentlyHighlightedBuilding = null;
    }

    /// <summary>
    /// Scans the scene for all targetable buildings and organizes them predictably
    /// </summary>
    private void ScanForTargetableBuildings()
    {
        // Find all buildings in the scene
        Building[] allBuildings = FindObjectsOfType<Building>();
        
        // Filter for targetable buildings and sort them (left to right by X position)
        targetableBuildings = allBuildings
            .Where(building => building.IsTargetable)
            .OrderBy(building => building.transform.position.x)
            .ThenBy(building => building.transform.position.z)
            .ToList();

        Debug.Log($"[BannerController] Found {targetableBuildings.Count} targetable buildings");
    }

    /// <summary>
    /// Finds the building closest to the camera's current position and sets it as default
    /// </summary>
    private void SelectClosestBuildingAsDefault()
    {
        if (targetableBuildings.Count == 0) return;

        var cameraController = FindFirstObjectByType<RhythmGameCameraController>();
        if (cameraController == null) return;

        Vector3 cameraPosition = cameraController.transform.position;
        float closestDistance = float.MaxValue;
        int closestIndex = 0;

        for (int i = 0; i < targetableBuildings.Count; i++)
        {
            float distance = Vector3.Distance(cameraPosition, targetableBuildings[i].transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        currentTargetIndex = closestIndex;
    }

    /// <summary>
    /// Updates the current target, camera lock, and visual feedback
    /// </summary>
   private void UpdateCurrentTarget()
    {
        if (targetableBuildings.Count == 0) return;
        
        // 1. Nettoyer l'highlight de l'ancienne cible
        ClearBuildingHighlight();

        // 2. Définir la nouvelle cible
        Building targetBuilding = targetableBuildings[currentTargetIndex];
        currentlyHighlightedBuilding = targetBuilding;

        // 3. Mettre la nouvelle cible en surbrillance (état Hover)
        HighlightBuilding(targetBuilding);

        var cameraController = FindFirstObjectByType<RhythmGameCameraController>();
        if (cameraController != null)
        {
            cameraController.LockOnTarget(targetBuilding.transform);
        }
    }

    /// <summary>
    /// Highlights a building using its BuildingSelectionFeedback component
    /// </summary>
    private void HighlightBuilding(Building building)
    {
        if (building == null) return;
        var feedback = building.GetComponent<BuildingSelectionFeedback>();
        if (feedback != null)
        {
            // Ne passe en mode Hover que si le bâtiment n'est pas déjà sélectionné
            if (feedback.CurrentState != OutlineState.Selected)
            {
                feedback.SetOutlineState(OutlineState.Hover);
            }
        }
    }
    /// <summary>
    /// Clears the highlight from the currently highlighted building
    /// </summary>
    private void ClearBuildingHighlight()
    {
        if (currentlyHighlightedBuilding != null)
        {
            var feedback = currentlyHighlightedBuilding.GetComponent<BuildingSelectionFeedback>();
            if (feedback != null)
            {
                // Ne remet à Default que si l'état actuel était Hover.
                // Cela empêche d'effacer l'état "Selected" en changeant de cible.
                if (feedback.CurrentState == OutlineState.Hover)
                {
                    feedback.SetOutlineState(OutlineState.Default);
                }
            }
            currentlyHighlightedBuilding = null;
        }
    }

    /// <summary>
    /// Handles target cycling input from gamepad - now supports bi-directional cycling and works in both lock/unlock modes
    /// </summary>
    private void OnCycleTargetPressed(InputAction.CallbackContext context)
    {
        // NEW: Allow cycling even when camera is unlocked
        // If not in targeting mode, initialize targeting without locking camera
        if (!isTargetingMode)
        {
            InitializeTargetingWithoutCameraLock();
            if (targetableBuildings.Count == 0) return;
        }
        
        if (targetableBuildings.Count <= 1) return;

        // Read the axis value to determine direction
        float axisValue = context.ReadValue<float>();
        
        // Only proceed if the input is significant enough (avoid noise)
        if (Mathf.Abs(axisValue) < 0.5f) return;
        
        // Determine direction based on axis value
        if (axisValue > 0.5f)
        {
            // Positive value (D-Pad Right) - cycle forward
            currentTargetIndex = (currentTargetIndex + 1) % targetableBuildings.Count;
            Debug.Log($"[BannerController] Cycled FORWARD to target {currentTargetIndex}: {targetableBuildings[currentTargetIndex].name}");
        }
        else if (axisValue < -0.5f)
        {
            // Negative value (D-Pad Left) - cycle backward
            currentTargetIndex = (currentTargetIndex - 1 + targetableBuildings.Count) % targetableBuildings.Count;
            Debug.Log($"[BannerController] Cycled BACKWARD to target {currentTargetIndex}: {targetableBuildings[currentTargetIndex].name}");
        }
        
        // Update target and visual feedback (camera lock is now conditional)
        UpdateCurrentTargetConditional();
    }

    /// <summary>
    /// NEW: Initialize targeting system without locking the camera
    /// </summary>
    private void InitializeTargetingWithoutCameraLock()
    {
        Debug.Log("[BannerController] Initializing targeting without camera lock");
        
        // Scan for all targetable buildings
        ScanForTargetableBuildings();
        
        if (targetableBuildings.Count == 0)
        {
            Debug.LogWarning("[BannerController] No targetable buildings found!");
            return;
        }

        // Set targeting mode but don't lock camera yet
        isTargetingMode = true;
        currentTargetIndex = 0;

        // Find the closest building to current camera position as default target
        SelectClosestBuildingAsDefault();
        
        Debug.Log($"[BannerController] Free targeting mode activated with {targetableBuildings.Count} targetable buildings");
    }

    /// <summary>
    /// NEW: Updates current target with conditional camera locking
    /// </summary>
    private void UpdateCurrentTargetConditional()
    {
        if (targetableBuildings.Count == 0) return;
        
        // 1. Clear highlight from previous target
        ClearBuildingHighlight();

        // 2. Set new target
        Building targetBuilding = targetableBuildings[currentTargetIndex];
        currentlyHighlightedBuilding = targetBuilding;

        // 3. Highlight the new target
        HighlightBuilding(targetBuilding);

        // 4. NEW: Only lock camera if it's already locked
        var cameraController = FindFirstObjectByType<RhythmGameCameraController>();
        if (cameraController != null && cameraController.IsLocked)
        {
            // Camera is already locked, so follow the new target
            cameraController.LockOnTarget(targetBuilding.transform);
            Debug.Log($"[BannerController] Camera following new target: {targetBuilding.name}");
        }
        else
        {
            // Camera is free, only update visual feedback
            Debug.Log($"[BannerController] Free targeting - highlighting {targetBuilding.name} without camera movement");
        }
    }

    /// <summary>
    /// Handles banner placement input from gamepad - now works in both lock/unlock modes
    /// </summary>
    private void OnPlaceBannerPressed(InputAction.CallbackContext context)
    {
        // NEW: Allow placement even when camera is unlocked, as long as we have a highlighted target
        if (!isTargetingMode || targetableBuildings.Count == 0 || currentTargetIndex >= targetableBuildings.Count)
        {
            Debug.LogWarning("[BannerController] No valid target selected for banner placement");
            return;
        }

        Building targetBuilding = targetableBuildings[currentTargetIndex];
        
        // Place banner on the selected building
        bool placementSuccess = PlaceBannerOnBuilding(targetBuilding);
        
        if (placementSuccess)
        {
            var cameraController = FindFirstObjectByType<RhythmGameCameraController>();
            string mode = (cameraController != null && cameraController.IsLocked) ? "lock" : "free";
            Debug.Log($"[BannerController] Banner placed on {targetBuilding.name} via gamepad in {mode} mode");
        }
        else
        {
            Debug.LogWarning($"[BannerController] Failed to place banner on {targetBuilding.name}");
        }
    }

    #endregion
}