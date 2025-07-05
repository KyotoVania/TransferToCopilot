using UnityEngine;
using System.Collections.Generic;
using Game.Observers;

/// <summary>
/// Point central de la gestion de la bannière.
/// Gère l'état logique (quelle tuile est sélectionnée) et visuel (affichage des prefabs).
/// S'abonne à InputTargetingManager pour réagir aux intentions du joueur.
/// Ce script intègre désormais les responsabilités de l'ancien MouseManager liées à l'affichage.
/// </summary>
public class BannerController : MonoBehaviour
{
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
    private Tile _currentTile;

    [Header("Visuals & Prefabs")]
    [SerializeField] private GameObject bannerPrefab;
    [SerializeField] private GameObject previewBannerPrefab;
    
    [Header("Debugging")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private bool debugVisuals = true;
    [SerializeField] private Color previewDebugColor = Color.blue;
    [SerializeField] private Color bannerDebugColor = Color.red;

    // --- Objets persistants pour les visuels ---
    private GameObject persistentBanner;
    private GameObject persistentPreviewBanner;
    private GameObject debugPreviewSphere;
    private GameObject debugBannerSphere;

    private readonly List<IBannerObserver> observers = new List<IBannerObserver>();

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
        // S'abonner aux événements du Manager d'Input
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
        // Initialize banner on ally base after a short delay to ensure all buildings are loaded
        StartCoroutine(InitializeBannerOnAllyBase());
    }

    /// <summary>
    /// Automatically places the banner on the player's ally building at game start
    /// </summary>
    private System.Collections.IEnumerator InitializeBannerOnAllyBase()
    {
        // Wait a bit for all buildings to be initialized
        yield return new WaitForSeconds(0.5f);
        
        // Find the first ally building (PlayerBuilding)
        PlayerBuilding allyBase = FindFirstObjectByType<PlayerBuilding>();
        
        if (allyBase != null && allyBase.Team == TeamType.Player)
        {
            if (debugLogs) Debug.Log($"[BannerController] Auto-placing banner on ally base: {allyBase.name}");
            PlaceBannerOnBuilding(allyBase);
        }
        else
        {
            if (debugLogs) Debug.LogWarning("[BannerController] No ally base found for initial banner placement.");
        }
    }

    private void OnDisable()
    {
        // Se désabonner pour éviter les erreurs
        InputTargetingManager.OnBuildingHovered -= HandleBuildingHovered;
        InputTargetingManager.OnHoverEnded -= HandleHoverEnded;
        InputTargetingManager.OnBuildingSelected -= HandleBuildingSelected;

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleBeat;
        }
        
        // Cacher les visuels si le controller est désactivé
        HideBanner(persistentPreviewBanner);
        HideBanner(persistentBanner);
    }
    
    private void OnDestroy()
    {
        if (persistentPreviewBanner != null) Destroy(persistentPreviewBanner);
        if (persistentBanner != null) Destroy(persistentBanner);
        if (debugPreviewSphere != null) Destroy(debugPreviewSphere);
        if (debugBannerSphere != null) Destroy(debugBannerSphere);
        
        if (instance == this) instance = null;
    }

    #region Event Handlers

    /// <summary>
    /// Réagit au survol d'un bâtiment pour afficher la prévisualisation.
    /// </summary>
    private void HandleBuildingHovered(Building building)
    {
        if (building == null) return;
        
        if (debugLogs) Debug.Log($"[BannerController] Hover detected on {building.name}. Showing preview.");
        float topY = GetTopOfBuilding(building);
        ShowBanner(persistentPreviewBanner, building.transform.position, topY, true);
    }

    /// <summary>
    /// Réagit à la fin du survol pour cacher la prévisualisation.
    /// </summary>
    private void HandleHoverEnded()
    {
        if (debugLogs) Debug.Log($"[BannerController] Hover ended. Hiding preview.");
        HideBanner(persistentPreviewBanner);
    }

    /// <summary>
    /// Réagit à la sélection d'un bâtiment (clic ou bouton manette).
    /// </summary>
    private void HandleBuildingSelected(Building building)
    {
        HideBanner(persistentPreviewBanner); // Cacher la prévisualisation dans tous les cas

        if (building != null)
        {
            // Si on sélectionne le bâtiment qui a déjà la bannière, on l'enlève.
            if (HasActiveBanner && building == CurrentBuilding)
            {
                if(debugLogs) Debug.Log($"[BannerController] Same building selected. Clearing banner.");
                ClearBanner();
            }
            else // Sinon, on essaie de la placer.
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
    
    #endregion
    
    #region Banner Logic

    public bool PlaceBannerOnBuilding(Building building)
    {
        if (building == null) return false;
        Tile occupiedTile = building.GetOccupiedTile();
        if (occupiedTile == null) return false;

        Building previousBuildingWithBanner = CurrentBuilding;
        bool placementSuccess = PlaceBanner(occupiedTile);

        if (placementSuccess)
        {
            // Gérer l'outline : enlever l'ancien, mettre le nouveau en "Selected"
            if (previousBuildingWithBanner != null && previousBuildingWithBanner != building)
            {
                previousBuildingWithBanner.GetComponent<BuildingSelectionFeedback>()?.SetOutlineState(OutlineState.Default);
            }
            building.GetComponent<BuildingSelectionFeedback>()?.SetOutlineState(OutlineState.Selected);
            
            // --- C'EST ICI LA CORRECTION MAJEURE ---
            // On affiche le visuel de la bannière principale après que la logique soit validée.
            float topY = GetTopOfBuilding(building);
            ShowBanner(persistentBanner, building.transform.position, topY, false);
        }
        
        return placementSuccess;
    }

    private bool PlaceBanner(Tile tile)
    {
        if (tile.currentBuilding == null || !tile.currentBuilding.IsTargetable) return false;

        CurrentBuilding = tile.currentBuilding;
        _currentTile = tile;
        CurrentBannerPosition = new Vector2Int(tile.column, tile.row);
        HasActiveBanner = true;

        if(debugLogs) Debug.Log($"[BannerController] Banner logic placed on Tile({tile.column}, {tile.row}). Notifying observers.");
        NotifyObservers(tile.column, tile.row);
        return true;
    }

    public void ClearBanner()
    {
        if (HasActiveBanner)
        {
            if (CurrentBuilding != null)
            {
                CurrentBuilding.GetComponent<BuildingSelectionFeedback>()?.SetOutlineState(OutlineState.Default);
            }

            HasActiveBanner = false;
            CurrentBannerPosition = Vector2Int.zero;
            CurrentBuilding = null;
            _currentTile = null;
            
            // Cacher le visuel et notifier que la bannière est retirée (avec des coordonnées invalides ou null)
            HideBanner(persistentBanner);
            NotifyObservers(-1, -1); // ou une autre convention pour "removed"
            if(debugLogs) Debug.Log($"[BannerController] Banner cleared.");
        }
    }
    
    #endregion
    
    #region Visuals Management (from MouseManager)
    
    private void InitializePersistentPrefabs()
    {
        if (bannerPrefab == null) Debug.LogError("[BannerController] Banner Prefab is not assigned!");
        if (previewBannerPrefab == null) Debug.LogWarning("[BannerController] Preview Banner Prefab is not assigned! Using normal banner as fallback.");

        if (persistentBanner == null && bannerPrefab != null)
        {
            persistentBanner = Instantiate(bannerPrefab);
            persistentBanner.name = "Persistent_Banner_Visual";
            persistentBanner.SetActive(false);
        }
        
        GameObject prefabToUseForPreview = previewBannerPrefab != null ? previewBannerPrefab : bannerPrefab;
        if (persistentPreviewBanner == null && prefabToUseForPreview != null)
        {
            persistentPreviewBanner = Instantiate(prefabToUseForPreview);
            persistentPreviewBanner.name = "Persistent_Preview_Visual";
            persistentPreviewBanner.SetActive(false);
        }
    }

    private void ShowBanner(GameObject banner, Vector3 buildingWorldPosition, float buildingTopY, bool isPreview)
    {
        if (banner == null) return;
        
        banner.SetActive(false);

        BannerMovement bannerMovement = banner.GetComponent<BannerMovement>();
        float heightOffset = bannerMovement != null ? bannerMovement.FinalHeightOffset : 1.0f;
        Vector3 bannerPosition = new Vector3(buildingWorldPosition.x, buildingTopY + heightOffset, buildingWorldPosition.z);

        if (bannerMovement != null)
        {
            banner.transform.position = bannerPosition;
            banner.SetActive(true);
            bannerMovement.UpdatePosition(bannerPosition);
        }
        else
        {
            banner.transform.position = bannerPosition;
            banner.SetActive(true);
        }
        
        if (debugVisuals)
        {
            GameObject sphere = isPreview ? debugPreviewSphere : debugBannerSphere;
            if (sphere != null)
            {
                sphere.transform.position = bannerPosition;
                sphere.SetActive(true);
            }
        }
    }

    private void HideBanner(GameObject bannerInstance)
    {
        if (bannerInstance != null) bannerInstance.SetActive(false);
        
        if (debugVisuals)
        {
            if (bannerInstance == persistentPreviewBanner && debugPreviewSphere != null) debugPreviewSphere.SetActive(false);
            else if (bannerInstance == persistentBanner && debugBannerSphere != null) debugBannerSphere.SetActive(false);
        }
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
        
        // Fallback si aucun renderer n'est trouvé
        if (!hasBounds)
        {
            Collider[] colliders = building.GetComponentsInChildren<Collider>();
            foreach (Collider collider in colliders)
            {
                 if (!hasBounds)
                {
                    combinedBounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(collider.bounds);
                }
            }
        }
        
        return hasBounds ? combinedBounds.max.y : building.transform.position.y;
    }

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
    
    #endregion
    
    #region Observer Pattern

    private void HandleBeat(float beatDuration)
    {
        if (HasActiveBanner)
        {
            NotifyObservers(CurrentBannerPosition.x, CurrentBannerPosition.y);
        }
    }
    
    public void AddObserver(IBannerObserver observer)
    {
        if (!observers.Contains(observer)) observers.Add(observer);
    }

    public void RemoveObserver(IBannerObserver observer)
    {
        if (observers.Contains(observer)) observers.Remove(observer);
    }

    private void NotifyObservers(int column, int row)
    {
        foreach (var observer in new List<IBannerObserver>(observers))
        {
            observer?.OnBannerPlaced(column, row);
        }
    }
    
    #endregion
}

