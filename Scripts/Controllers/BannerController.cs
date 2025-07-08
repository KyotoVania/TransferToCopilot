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

    // MODIFIED: Method signature now correctly accepts a GameObject.
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

    // MODIFIED: Method signature now correctly accepts a GameObject.
    private void HandleTargetSelected(GameObject targetObject)
    {
        HideVisual(persistentPreviewBanner, true); // Always hide preview on selection.

        // Case 1: Clicked on empty space.
        if (targetObject == null)
        {
            if (HasActiveBanner)
            {
                if(debugLogs) Debug.Log("[BannerController] Clicked on empty space. Clearing banner.");
                ClearBanner();
            }
            return;
        }

        // MODIFIED: Check for components to determine what was selected.
        // Case 2: A Building was selected.
        if (targetObject.TryGetComponent<Building>(out Building selectedBuilding))
        {
            if (HasActiveBanner && selectedBuilding == CurrentBuilding)
            {
                if(debugLogs) Debug.Log($"[BannerController] Same building selected. Clearing banner.");
                ClearBanner();
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
                 if(debugLogs) Debug.Log($"[BannerController] Same unit selected. Clearing banner.");
                 ClearBanner();
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
            }
            else
            {
                ClearBanner();
                return;
            }
        }
        else if (_currentTile != null)
        {
            positionToNotify = new Vector2Int(_currentTile.column, _currentTile.row);
        }
        else
        {
            ClearBanner();
            return;
        }

        NotifyObservers(positionToNotify.x, positionToNotify.y);
    }

    private void HandleTargetedUnitDestroyed()
    {
        if (debugLogs) Debug.Log("[BannerController] Targeted unit was destroyed. Clearing banner.");
        ClearBanner();
    }

    #endregion

    #region Banner Logic

    public bool PlaceBannerOnBuilding(Building building)
    {
        if (building == null) return false;
        Tile occupiedTile = building.GetOccupiedTile();
        // Assuming your Building script has an IsTargetable property.
        if (occupiedTile == null || !building.IsTargetable) return false;

        ClearBanner();

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
        // Add a check for IsTargetable if your Unit script has this property.
        if (unit == null) return false;

        ClearBanner();

        CurrentTargetedUnit = unit;
        HasActiveBanner = true;
        CurrentTargetedUnit.OnUnitDestroyed += HandleTargetedUnitDestroyed;

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

    public void ClearBanner()
    {
        if (!HasActiveBanner) return;

        if (CurrentBuilding != null)
        {
            CurrentBuilding.GetComponent<BuildingSelectionFeedback>()?.SetOutlineState(OutlineState.Default);
        }
        if (CurrentTargetedUnit != null)
        {
            CurrentTargetedUnit.OnUnitDestroyed -= HandleTargetedUnitDestroyed;
        }

        HasActiveBanner = false;
        CurrentBannerPosition = Vector2Int.zero;
        CurrentBuilding = null;
        CurrentTargetedUnit = null;
        _currentTile = null;

        HideVisual(persistentBanner, false);
        NotifyObservers(-1, -1);
        if(debugLogs) Debug.Log("[BannerController] Banner cleared.");
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

        // MODIFIED: The 'true' parameter keeps the banner's original world scale.
        visual.transform.SetParent(unit.transform, true); // Attacher à l'unité

        visual.transform.localPosition = Vector3.zero; // Réinitialiser la position locale
        visual.SetActive(true);

        BannerMovement bannerMovement = visual.GetComponent<BannerMovement>();
        bannerMovement?.AttachToUnit(unit); // Laisser le script de mouvement gérer l'offset

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

        Bounds combinedBounds = new Bounds(renderers[0].bounds.center, Vector3.zero);
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

    private IEnumerator InitializeBannerOnAllyBase()
    {
        yield return new WaitForSeconds(0.5f);
        PlayerBuilding allyBase = FindFirstObjectByType<PlayerBuilding>();
        if (allyBase != null)
        {
            if (debugLogs) Debug.Log($"[BannerController] Auto-placing banner on ally base: {allyBase.name}");
            PlaceBannerOnBuilding(allyBase);
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