using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Game.Observers;

/// <summary>
/// Point central de la gestion de la bannière, fusionnant la logique d'input et le placement visuel.
/// Peut placer la bannière sur un Bâtiment (via InputTargetingManager) ou sur une Unité (via un appel externe).
/// Utilise des objets persistants pour les visuels pour de meilleures performances.
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

    // --- Objets persistants pour les visuels ---
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
        InputTargetingManager.OnBuildingHovered += HandleBuildingHovered;
        InputTargetingManager.OnHoverEnded += HandleHoverEnded;
        InputTargetingManager.OnBuildingSelected += HandleBuildingSelected;

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
        InputTargetingManager.OnBuildingHovered -= HandleBuildingHovered;
        InputTargetingManager.OnHoverEnded -= HandleHoverEnded;
        InputTargetingManager.OnBuildingSelected -= HandleBuildingSelected;

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleBeat;
        }

        // Sécurité : s'assurer de se désabonner si une unité était ciblée
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

    private void HandleBuildingHovered(Building building)
    {
        if (building == null) return;
        float topY = GetTopOfBuilding(building);
        ShowVisualAtPosition(persistentPreviewBanner, building.transform.position, topY, true);
    }

    private void HandleHoverEnded()
    {
        HideVisual(persistentPreviewBanner, true);
    }

    private void HandleBuildingSelected(Building building)
    {
        HideVisual(persistentPreviewBanner, true); // Cacher la prévisualisation dans tous les cas

        if (building != null)
        {
            // Si on sélectionne le bâtiment qui a déjà la bannière, on l'enlève.
            if (HasActiveBanner && building == CurrentBuilding)
            {
                if(debugLogs) Debug.Log($"[BannerController] Same building selected. Clearing banner.");
                ClearBanner();
            }
            else // Sinon, on la place.
            {
                if(debugLogs) Debug.Log($"[BannerController] New building selected ({building.name}). Placing banner.");
                PlaceBannerOnBuilding(building);
            }
        }
        else // Si on clique dans le vide, on retire la bannière.
        {
            if (HasActiveBanner)
            {
                if(debugLogs) Debug.Log($"[BannerController] Clicked on empty space. Clearing banner.");
                ClearBanner();
            }
        }
    }

    /// <summary>
    /// Gère la notification périodique aux observateurs. Si une unité est suivie, met à jour la position.
    /// </summary>
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
                CurrentBannerPosition = positionToNotify; // Mettre à jour la position logique
            }
            else // L'unité n'est plus sur une tuile valide
            {
                ClearBanner();
                return;
            }
        }
        else if (_currentTile != null) // Cas d'un bâtiment
        {
            positionToNotify = new Vector2Int(_currentTile.column, _currentTile.row);
        }
        else // Ni unité, ni bâtiment, état incohérent
        {
            ClearBanner();
            return;
        }

        NotifyObservers(positionToNotify.x, positionToNotify.y);
    }

    /// <summary>
    /// Se déclenche lorsque l'unité actuellement ciblée est détruite.
    /// </summary>
    private void HandleTargetedUnitDestroyed()
    {
        if (debugLogs) Debug.Log($"[BannerController] Targeted unit was destroyed. Clearing banner.");
        ClearBanner();
    }

    #endregion

    #region Banner Logic

    /// <summary>
    /// Place la bannière sur un Bâtiment. C'est la méthode principale pour le ciblage via input.
    /// </summary>
    public bool PlaceBannerOnBuilding(Building building)
    {
        if (building == null) return false;
        Tile occupiedTile = building.GetOccupiedTile();
        if (occupiedTile == null || !building.IsTargetable) return false;

        ClearBanner(); // Efface l'état précédent (unité ou autre bâtiment)

        CurrentBuilding = building;
        _currentTile = occupiedTile;
        CurrentBannerPosition = new Vector2Int(occupiedTile.column, occupiedTile.row);
        HasActiveBanner = true;

        building.GetComponent<BuildingSelectionFeedback>()?.SetOutlineState(OutlineState.Selected);

        float topY = GetTopOfBuilding(building);
        ShowVisualAtPosition(persistentBanner, building.transform.position, topY, false);

        if(debugLogs) Debug.Log($"[BannerController] Banner placed on Building {building.name}. Notifying observers.");
        NotifyObservers(occupiedTile.column, occupiedTile.row);
        return true;
    }

    /// <summary>
    /// Place la bannière sur une Unité. Méthode à appeler depuis un autre script.
    /// </summary>
    public bool PlaceBannerOnUnit(Unit unit)
    {
        if (unit == null) return false;

        ClearBanner(); // Efface l'état précédent

        CurrentTargetedUnit = unit;
        HasActiveBanner = true;

        // S'abonner à la destruction de l'unité pour nettoyer la bannière
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
        NotifyObservers(-1, -1); // Notification de retrait
        if(debugLogs) Debug.Log($"[BannerController] Banner cleared.");
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

        visual.transform.SetParent(null); // S'assurer qu'il n'est plus attaché à une unité

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

        visual.transform.SetParent(unit.transform, false); // Attacher à l'unité
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
            visualInstance.transform.SetParent(null); // Détacher pour éviter les problèmes
        }
        UpdateDebugVisual(isPreview, Vector3.zero, false);
    }

    private float GetTopOfBuilding(Building building)
    {
        if (building == null) return 0f;
        Bounds combinedBounds = new Bounds(building.transform.position, Vector3.zero);
        Renderer[] renderers = building.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;
            if (!hasBounds) { combinedBounds = renderer.bounds; hasBounds = true; }
            else { combinedBounds.Encapsulate(renderer.bounds); }
        }
        if (!hasBounds) return building.transform.position.y + 2f; // Fallback
        return hasBounds ? combinedBounds.max.y : building.transform.position.y;
    }

    #endregion

    #region Observer Pattern & Helpers

    private System.Collections.IEnumerator InitializeBannerOnAllyBase()
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