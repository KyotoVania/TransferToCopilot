using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Game.Observers;

/// <summary>
/// Central point for banner management, merging input logic and visual placement.
/// Can place the banner on a Building or a Unit via InputTargetingManager.
/// </summary>
public class BannerController : MonoBehaviour
{
    // --- Singleton Pattern ---
    public static bool Exists => instance != null;
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

    [Header("State")]
    public Vector2Int CurrentBannerPosition { get; private set; }
    public bool HasActiveBanner { get; private set; }
    public Building CurrentBuilding { get; private set; }
    public Unit CurrentTargetedUnit { get; private set; }
    private Tile _currentTile;

    [Header("Visuals & Prefabs")]
    [SerializeField] private GameObject bannerPrefab;
    [SerializeField] private GameObject previewBannerPrefab;

    [Header("Debugging")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private bool debugVisuals = true;
    [SerializeField] private Color previewDebugColor = new Color(0, 0, 1, 0.5f);
    [SerializeField] private Color bannerDebugColor = new Color(1, 0, 0, 0.5f);

    // --- Persistent Visual Objects ---
    private GameObject persistentBanner;
    private GameObject persistentPreviewBanner;
    private GameObject debugPreviewSphere;
    private GameObject debugBannerSphere;

    private readonly List<IBannerObserver> observers = new List<IBannerObserver>();

    #region Unity Lifecycle

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        InitializePersistentPrefabs();
        if (debugVisuals) CreateDebugVisuals();
    }

    private void OnEnable()
    {
        InputTargetingManager.OnTargetHovered += HandleTargetHovered;
        InputTargetingManager.OnHoverEnded += HandleHoverEnded;
        InputTargetingManager.OnTargetSelected += HandleTargetSelected;

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += HandleBeat;
        }
    }

    private void Start()
    {
        // The initial placement on the base is handled by this coroutine
        StartCoroutine(InitializeBannerOnAllyBase());
    }

    private void OnDisable()
    {
        InputTargetingManager.OnTargetHovered -= HandleTargetHovered;
        InputTargetingManager.OnHoverEnded -= HandleHoverEnded;
        InputTargetingManager.OnTargetSelected -= HandleTargetSelected;

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleBeat;
        }

        if (CurrentTargetedUnit != null)
        {
            CurrentTargetedUnit.OnUnitDestroyed -= HandleTargetedUnitDestroyed;
        }

        HideVisual(persistentPreviewBanner, true);
        HideVisual(persistentBanner, false);
    }

    private void OnDestroy()
    {
        if (persistentPreviewBanner != null) Destroy(persistentPreviewBanner);
        if (persistentBanner != null) Destroy(persistentBanner);
        if (debugPreviewSphere != null) Destroy(debugPreviewSphere);
        if (debugBannerSphere != null) Destroy(debugBannerSphere);

        if (instance == this) instance = null;
    }

    #endregion

    #region Event Handlers

    private void HandleTargetHovered(GameObject targetObject)
    {
        if (targetObject == null) return;

        float topY = GetTopOfTarget(targetObject);
        ShowVisualAtPosition(persistentPreviewBanner, targetObject.transform.position, topY, true);
    }

    private void HandleHoverEnded()
    {
        HideVisual(persistentPreviewBanner, true);
    }

    private void HandleTargetSelected(GameObject targetObject)
    {
        HideVisual(persistentPreviewBanner, true);

        // Case 1: Clicked on empty space, reset banner to base.
        if (targetObject == null)
        {
            if (debugLogs) Debug.Log("[BannerController] Clicked on empty space. Resetting banner to base.");
            return;
        }

        // Case 2: A Building was selected.
        if (targetObject.TryGetComponent<Building>(out Building selectedBuilding))
        {
            if (HasActiveBanner && selectedBuilding == CurrentBuilding)
            {
                if(debugLogs) Debug.Log($"[BannerController] Same building selected. Resetting banner to base.");
                ClearBanner(); // Reset to base
            }
            else
            {
                if(debugLogs) Debug.Log($"[BannerController] New building selected ({selectedBuilding.name}). Placing banner.");
                PlaceBannerOnBuilding(selectedBuilding);
            }
        }
        // Case 3: A Unit was selected.
        else if (targetObject.TryGetComponent<Unit>(out Unit selectedUnit))
        {
            if (HasActiveBanner && selectedUnit == CurrentTargetedUnit)
            {
                 if(debugLogs) Debug.Log($"[BannerController] Same unit selected. Resetting banner to base.");
                 ClearBanner(); // Reset to base
            }
            else
            {
                if(debugLogs) Debug.Log($"[BannerController] New unit selected ({selectedUnit.name}). Placing banner.");
                PlaceBannerOnUnit(selectedUnit);
            }
        }
    }

    private void HandleBeat(float beatDuration)
    {
        if (!HasActiveBanner) return;

        Vector2Int positionToNotify;

        if (CurrentTargetedUnit != null)
        {
            Tile unitTile = CurrentTargetedUnit.GetOccupiedTile();
            if (unitTile != null)
            {
                positionToNotify = new Vector2Int(unitTile.column, unitTile.row);
                CurrentBannerPosition = positionToNotify;
                NotifyObservers(positionToNotify.x, positionToNotify.y);
            }
            else
            {
                if(debugLogs) Debug.LogWarning($"[BannerController] Target unit '{CurrentTargetedUnit.name}' has a banner but its tile is temporarily unavailable. Waiting for next beat.");
                return;
            }
        }
        else if (_currentTile != null)
        {
            positionToNotify = new Vector2Int(_currentTile.column, _currentTile.row);
            NotifyObservers(positionToNotify.x, positionToNotify.y);
        }
        else
        {
            if (debugLogs) Debug.LogError("[BannerController] Banner is active but has no valid target (Unit or Tile). Resetting to base as a fallback.");
            ClearBanner();
            return;
        }
    }

    private void HandleTargetedUnitDestroyed()
    {
        if (debugLogs) Debug.Log("[BannerController] Targeted unit was destroyed. Resetting banner to base.");
        ClearBanner(); // Reset to base
    }

    #endregion

    #region Banner Logic

    public bool PlaceBannerOnBuilding(Building building)
    {
        if (building == null) return false;
        Tile occupiedTile = building.GetOccupiedTile();
        if (occupiedTile == null || !building.IsTargetable) return false;

        // Clean up previous target state without deactivating the banner
        _CleanUpCurrentTarget();

        // Set new target state
        CurrentBuilding = building;
        _currentTile = occupiedTile;
        CurrentBannerPosition = new Vector2Int(occupiedTile.column, occupiedTile.row);
        HasActiveBanner = true;

        building.GetComponent<BuildingSelectionFeedback>()?.SetOutlineState(OutlineState.Selected);

        float topY = GetTopOfTarget(building.gameObject);
        ShowVisualAtPosition(persistentBanner, building.transform.position, topY, false);

        if(debugLogs) Debug.Log($"[BannerController] Banner placed on Building {building.name}. Notifying observers.");
        NotifyObservers(occupiedTile.column, occupiedTile.row);
        return true;
    }

    public bool PlaceBannerOnUnit(Unit unit)
    {
        if (unit == null) return false;

        // Clean up previous target state without deactivating the banner
        _CleanUpCurrentTarget();

        // Set new target state
        CurrentTargetedUnit = unit;
        HasActiveBanner = true;
        CurrentTargetedUnit.OnUnitDestroyed += HandleTargetedUnitDestroyed;

        // Gérer l'outline pour les unités (équivalent à ce qui est fait pour les bâtiments)
        UnitSelectionFeedback unitFeedback = unit.GetComponent<UnitSelectionFeedback>();
        if (unitFeedback != null)
        {
            unitFeedback.SetOutlineState(OutlineState.Selected);
        }

        ShowAndAttachVisualToUnit(persistentBanner, unit, false);

        Tile unitTile = unit.GetOccupiedTile();
        if (unitTile != null)
        {
            CurrentBannerPosition = new Vector2Int(unitTile.column, unitTile.row);
            if(debugLogs) Debug.Log($"[BannerController] Banner placed on Unit {unit.name}. Notifying observers.");
            NotifyObservers(unitTile.column, unitTile.row);
        }
        return true;
    }

    /// <summary>
    /// MODIFIED: This method no longer deactivates the banner. Instead, it resets it to the allied base.
    /// This is the new default state for the banner.
    /// </summary>
    public void ClearBanner()
    {
        if (debugLogs) Debug.Log("[BannerController] Clearing current target and resetting banner to ally base.");

        // First, reset the state of the current target
        _CleanUpCurrentTarget();

        // Then, find the ally base and place the banner there
        StartCoroutine(InitializeBannerOnAllyBase());
    }

    /// <summary>
    /// NEW: Private helper to clean up the current target's state without changing HasActiveBanner.
    /// This prevents recursion and separates responsibilities.
    /// </summary>
    private void _CleanUpCurrentTarget()
    {
        if (CurrentBuilding != null)
        {
            CurrentBuilding.GetComponent<BuildingSelectionFeedback>()?.SetOutlineState(OutlineState.Default);
        }
        if (CurrentTargetedUnit != null)
        {
            CurrentTargetedUnit.OnUnitDestroyed -= HandleTargetedUnitDestroyed;
            // Remettre l'outline de l'unité à l'état par défaut
            UnitSelectionFeedback unitFeedback = CurrentTargetedUnit.GetComponent<UnitSelectionFeedback>();
            if (unitFeedback != null)
            {
                unitFeedback.SetOutlineState(OutlineState.Default);
            }
        }

        CurrentBuilding = null;
        CurrentTargetedUnit = null;
        _currentTile = null;
    }


    #endregion

    #region Visuals Management

    private void InitializePersistentPrefabs()
    {
        if (bannerPrefab != null)
        {
            persistentBanner = Instantiate(bannerPrefab);
            persistentBanner.name = "Persistent_Banner_Visual";
            persistentBanner.SetActive(false);
        }

        GameObject prefabToUseForPreview = previewBannerPrefab != null ? previewBannerPrefab : bannerPrefab;
        if (prefabToUseForPreview != null)
        {
            persistentPreviewBanner = Instantiate(prefabToUseForPreview);
            persistentPreviewBanner.name = "Persistent_Preview_Visual";
            persistentPreviewBanner.SetActive(false);
        }
    }

    private void ShowVisualAtPosition(GameObject visual, Vector3 worldPosition, float topY, bool isPreview)
    {
        if (visual == null) return;

        visual.transform.SetParent(null);

        BannerMovement bannerMovement = visual.GetComponent<BannerMovement>();
        float heightOffset = bannerMovement != null ? bannerMovement.FinalHeightOffset : 1.0f;
        Vector3 bannerPosition = new Vector3(worldPosition.x, topY + heightOffset, worldPosition.z);

        visual.transform.position = bannerPosition;
        visual.SetActive(true);
        bannerMovement?.UpdatePosition(bannerPosition);

        UpdateDebugVisual(isPreview, bannerPosition, true);
    }

    private void ShowAndAttachVisualToUnit(GameObject visual, Unit unit, bool isPreview)
    {
        if (visual == null || unit == null) return;

        visual.transform.SetParent(unit.transform, true);
        visual.transform.localPosition = Vector3.zero;
        visual.SetActive(true);

        BannerMovement bannerMovement = visual.GetComponent<BannerMovement>();
        bannerMovement?.AttachToUnit(unit);

        UpdateDebugVisual(isPreview, visual.transform.position, true);
    }

    private void HideVisual(GameObject visualInstance, bool isPreview)
    {
        if (visualInstance != null)
        {
            visualInstance.SetActive(false);
            visualInstance.transform.SetParent(null);
        }
        UpdateDebugVisual(isPreview, Vector3.zero, false);
    }

    private float GetTopOfTarget(GameObject targetObject)
    {
        if (targetObject == null) return 0f;

        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return targetObject.transform.position.y + 2f;

        Bounds combinedBounds = new Bounds();
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? combinedBounds.max.y : targetObject.transform.position.y + 2f;
    }

    #endregion

    #region Observer Pattern & Helpers

    /// <summary>
    /// Finds the player's base and places the banner on it.
    /// This is used for initialization and for resetting the banner.
    /// </summary>
    private IEnumerator InitializeBannerOnAllyBase()
    {
        // A short delay can prevent issues during scene loading
        yield return new WaitForSeconds(0.1f);

        PlayerBuilding allyBase = FindFirstObjectByType<PlayerBuilding>();
        if (allyBase != null)
        {
            if (debugLogs) Debug.Log($"[BannerController] Auto-placing/Resetting banner on ally base: {allyBase.name}");
            PlaceBannerOnBuilding(allyBase);
        }
        else
        {
            if (debugLogs) Debug.LogError("[BannerController] Could not find an ally base to place the banner!");
            // If no base, truly deactivate the banner
            _CleanUpCurrentTarget();
            HasActiveBanner = false;
            HideVisual(persistentBanner, false);
            NotifyObservers(-1, -1);
        }
    }

    public void AddObserver(IBannerObserver observer)
    {
        if (observer != null && !observers.Contains(observer))
        {
            observers.Add(observer);
        }
    }

    public void RemoveObserver(IBannerObserver observer)
    {
        if (observer != null) observers.Remove(observer);
    }

    private void NotifyObservers(int column, int row)
    {
        foreach (var observer in new List<IBannerObserver>(observers))
        {
            observer?.OnBannerPlaced(column, row);
        }
    }

    #endregion

    #region Debugging
    private void CreateDebugVisuals()
    {
        debugPreviewSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        debugPreviewSphere.name = "DebugPreviewSphere";
        debugPreviewSphere.transform.localScale = Vector3.one * 0.5f;
        debugPreviewSphere.GetComponent<Renderer>().material.color = previewDebugColor;
        Destroy(debugPreviewSphere.GetComponent<Collider>());
        debugPreviewSphere.SetActive(false);

        debugBannerSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        debugBannerSphere.name = "DebugBannerSphere";
        debugBannerSphere.transform.localScale = Vector3.one * 0.5f;
        debugBannerSphere.GetComponent<Renderer>().material.color = bannerDebugColor;
        Destroy(debugBannerSphere.GetComponent<Collider>());
        debugBannerSphere.SetActive(false);
    }

    private void UpdateDebugVisual(bool isPreview, Vector3 position, bool shouldShow)
    {
        if (!debugVisuals) return;
        GameObject sphere = isPreview ? debugPreviewSphere : debugBannerSphere;
        if (sphere != null)
        {
            sphere.transform.position = position;
            sphere.SetActive(shouldShow);
        }
    }
    #endregion
}