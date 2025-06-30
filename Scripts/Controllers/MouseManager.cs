using UnityEngine;
using System.Collections.Generic; 
using Game.Observers;
public class MouseManager : MonoBehaviour,IBannerObserver 
{
    [Header("Click Settings")]
    [SerializeField] private float clickCooldown = 0.01f;

    [Header("Banner Settings")]
    [SerializeField] private GameObject bannerPrefab;
    [SerializeField] private LayerMask tileLayerMask;
    [SerializeField] private LayerMask buildingLayerMask;

    [Header("Preview Settings")]
    [SerializeField] private GameObject previewBannerPrefab; // Prefab for the preview banner
    [SerializeField] private bool useSimplePreview = false; // Alternative simple preview method

    [Header("Cursor Settings")]
    [SerializeField] private Texture2D defaultCursorTexture; // Assign your cursor PNG in the inspector
    [SerializeField] private Texture2D hoverCursorTexture; // Optional different cursor for hover states
    [SerializeField] private Vector2 cursorHotspot = Vector2.zero; // Define the hotspot (click point) of your cursor

    [Header("Debugging")]
    [SerializeField] private bool debugClicks = true;
    [SerializeField] private float raycastDistance = 100f;
    [SerializeField] private bool debugVisuals = true;  // Enable visual debugging for banner positions
    [SerializeField] private Color previewDebugColor = Color.blue;
    [SerializeField] private Color bannerDebugColor = Color.red;

    private float lastClickTime;
    private GameObject currentBanner; // Active placed banner
    private GameObject persistentBanner; // Pre-instantiated normal banner for placement
    private GameObject persistentPreviewBanner; // Pre-instantiated preview banner for hover
    private GameObject debugPreviewSphere; // Debug visual for preview banner
    private GameObject debugBannerSphere; // Debug visual for placed banner
    private HexGridManager gridManager;
    private bool isHoveringValidTarget = false; // Track if we're hovering over a valid target
    private bool arePreabsInitialized = false; // Track if the persistent prefabs are initialized

    private BuildingSelectionFeedback currentlyHoveredBuildingFeedback;

    // Structure to store building information
    private struct BuildingInfo
    {
        public Building building;
        public Tile tile;
        public Vector3 position;
        public float height;

        public BuildingSelectionFeedback feedbackComponent;
    }

    private void Start()
    {
        gridManager = HexGridManager.Instance;
        lastClickTime = -clickCooldown;
        isHoveringValidTarget = false;

        SetDefaultCursor();

        if (debugVisuals)
        {
            CreateDebugVisuals();
        }
        InitializePersistentPrefabs();
    }

    private void SetDefaultCursor()
    {
        if (defaultCursorTexture != null)
        {
            Cursor.SetCursor(defaultCursorTexture, cursorHotspot, CursorMode.Auto);
        }
    }

    private void SetHoverCursor()
    {
        if (hoverCursorTexture != null)
        {
            Cursor.SetCursor(hoverCursorTexture, cursorHotspot, CursorMode.Auto);
        }
    }

    private void CreateDebugVisuals()
    {
        debugPreviewSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        debugPreviewSphere.name = "DebugPreviewSphere";
        debugPreviewSphere.transform.localScale = Vector3.one * 0.5f;
        Renderer previewRenderer = debugPreviewSphere.GetComponent<Renderer>();
        if (previewRenderer != null)
        {
            previewRenderer.material = new Material(Shader.Find("Standard"));
            previewRenderer.material.color = previewDebugColor;
        }
        Destroy(debugPreviewSphere.GetComponent<Collider>());

        debugBannerSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        debugBannerSphere.name = "DebugBannerSphere";
        debugBannerSphere.transform.localScale = Vector3.one * 0.5f;
        Renderer bannerRenderer = debugBannerSphere.GetComponent<Renderer>();
        if (bannerRenderer != null)
        {
            bannerRenderer.material = new Material(Shader.Find("Standard"));
            bannerRenderer.material.color = bannerDebugColor;
        }
        Destroy(debugBannerSphere.GetComponent<Collider>());

        debugPreviewSphere.SetActive(false);
        debugBannerSphere.SetActive(false);
        if (debugClicks) Debug.Log("Created debug visual spheres for banner positions");
    }

    private void InitializePersistentPrefabs()
    {
        if (arePreabsInitialized) return;
        if (debugClicks) Debug.Log("[MouseManager] Creating persistent banners");

        if (bannerPrefab != null)
        {
            if (persistentBanner != null) Destroy(persistentBanner);
            persistentBanner = Instantiate(bannerPrefab);
            persistentBanner.name = "Persistent_Banner";
            EnableAllRenderers(persistentBanner);
            BannerMovement bannerMovement = persistentBanner.GetComponent<BannerMovement>();
            if (bannerMovement != null) bannerMovement.enabled = true;
            else if (debugClicks) Debug.LogError("[MouseManager] Persistent banner is missing BannerMovement component!");
            persistentBanner.SetActive(false);
        }
        else if (debugClicks) Debug.LogError("[MouseManager] bannerPrefab is null!");

        if (previewBannerPrefab != null)
        {
            if (persistentPreviewBanner != null) Destroy(persistentPreviewBanner);
            persistentPreviewBanner = Instantiate(previewBannerPrefab);
            persistentPreviewBanner.name = "Persistent_Preview_Banner";
            EnableAllRenderers(persistentPreviewBanner);
            BannerMovement previewMovement = persistentPreviewBanner.GetComponent<BannerMovement>();
            if (previewMovement != null) previewMovement.enabled = true;
            else if (debugClicks) Debug.LogError("[MouseManager] Persistent preview banner is missing BannerMovement component!");
            persistentPreviewBanner.SetActive(false);
        }
        else if (bannerPrefab != null)
        {
            persistentPreviewBanner = Instantiate(bannerPrefab);
            persistentPreviewBanner.name = "Persistent_Preview_Banner";
            EnableAllRenderers(persistentPreviewBanner);
            BannerMovement previewMovement = persistentPreviewBanner.GetComponent<BannerMovement>();
            if (previewMovement != null) previewMovement.enabled = true;
            persistentPreviewBanner.SetActive(false);
        }
        else if (debugClicks) Debug.LogError("[MouseManager] previewBannerPrefab and bannerPrefab are null!");
        arePreabsInitialized = true;
    }

    private void EnableAllRenderers(GameObject obj)
    {
        if (obj == null) return;
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = true;
            // if (debugClicks) Debug.Log($"Enabled renderer on {renderer.gameObject.name}");
        }
    }

    private void Update()
    {
        HandleMouseHover(); // Renommé pour plus de clarté, gère maintenant l'outline et le preview
        if (Input.GetMouseButtonDown(0) && Time.time - lastClickTime > clickCooldown)
        {
            lastClickTime = Time.time;
            HandleBannerPlacement();
        }
    }

    private BuildingInfo GetBuildingInfoFromRay(Ray ray)
    {
        BuildingInfo info = new BuildingInfo();
        info.building = null;
        info.tile = null;
        info.position = Vector3.zero;
        info.height = 0f;
        info.feedbackComponent = null;

        RaycastHit[] buildingHits = Physics.RaycastAll(ray, raycastDistance, buildingLayerMask);
        float closestBuildingHitDistance = float.MaxValue;
        Building directlyHitBuilding = null;

        if (buildingHits.Length > 0)
        {
            foreach (RaycastHit hit in buildingHits)
            {
                Building buildingOnHitObject = hit.collider.GetComponent<Building>() ?? hit.collider.GetComponentInParent<Building>();
                if (buildingOnHitObject != null)
                {
                    if (hit.distance < closestBuildingHitDistance)
                    {
                        closestBuildingHitDistance = hit.distance;
                        directlyHitBuilding = buildingOnHitObject;
                    }
                }
            }
        }

        if (directlyHitBuilding != null) // Un bâtiment a été touché directement et est le plus proche
        {
            // Log AJOUTÉ pour le débogage du problème de bannière
            Tile occupiedTileForLog = directlyHitBuilding.GetOccupiedTile();
            if (debugClicks) Debug.Log($"[MouseManager/GetBuildingInfoFromRay] Touche DIRECTE sur Bâtiment: {directlyHitBuilding.name}. Sa tuile occupée est: {(occupiedTileForLog == null ? "NULL" : occupiedTileForLog.name)}");

            info.building = directlyHitBuilding;
            info.tile = occupiedTileForLog; // Utiliser la tuile récupérée ici
            info.position = directlyHitBuilding.transform.position;
            info.height = GetTopOfBuilding(directlyHitBuilding);
            info.feedbackComponent = directlyHitBuilding.GetComponent<BuildingSelectionFeedback>();
            // Log AJOUTÉ pour le débogage
            if (debugClicks) Debug.Log($"[MouseManager/GetBuildingInfoFromRay] RETOUR (Direct Hit): Building='{info.building?.name}', Tile='{info.tile?.name}', Pos='{info.position}'");
            return info;
        }

        RaycastHit tileHitInfo;
        if (Physics.Raycast(ray, out tileHitInfo, raycastDistance, tileLayerMask))
        {
            Tile tileComponent = tileHitInfo.collider.GetComponent<Tile>();
            if (tileComponent != null && tileComponent.currentBuilding != null)
            {
                 if (debugClicks) Debug.Log($"[MouseManager/GetBuildingInfoFromRay] Touche sur Tuile: {tileComponent.name}, qui a Bâtiment: {tileComponent.currentBuilding.name}");
                info.building = tileComponent.currentBuilding;
                info.tile = tileComponent;
                info.position = tileComponent.currentBuilding.transform.position;
                info.height = GetTopOfBuilding(tileComponent.currentBuilding);
                info.feedbackComponent = tileComponent.currentBuilding.GetComponent<BuildingSelectionFeedback>();
                // Log AJOUTÉ pour le débogage
                if (debugClicks) Debug.Log($"[MouseManager/GetBuildingInfoFromRay] RETOUR (Tile Hit): Building='{info.building?.name}', Tile='{info.tile?.name}', Pos='{info.position}'");
                return info;
            }
        }

        if (debugClicks) Debug.LogWarning("[MouseManager/GetBuildingInfoFromRay] RETOUR (Rien trouvé pour la bannière)");
        return info;
    }

    private float GetTopOfBuilding(Building building)
    {
        if (building == null)
        {
            if (debugClicks) Debug.LogWarning("[MouseManager/GetTopOfBuilding] Tentative d'obtenir la hauteur d'un bâtiment null.");
            return 0f; // Retourner 0 ou une hauteur par défaut appropriée
        }

        Renderer[] renderers = building.GetComponentsInChildren<Renderer>();
        Collider[] colliders = building.GetComponentsInChildren<Collider>(); // Peut aussi aider

        float maxY = building.transform.position.y; // Valeur de base si pas de renderers/colliders

        bool foundBounds = false;

        if (renderers.Length > 0)
        {
            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                // Ignorer les renderers qui pourraient être des particules ou des effets spéciaux très étendus
                if (renderers[i] is ParticleSystemRenderer || renderers[i] is TrailRenderer) continue;
                combinedBounds.Encapsulate(renderers[i].bounds);
            }
            maxY = combinedBounds.max.y;
            foundBounds = true;
            if (debugClicks) Debug.Log($"[MouseManager/GetTopOfBuilding] Hauteur du bâtiment '{building.name}' via Renderers: {maxY}. Bounds Center: {combinedBounds.center}, Size: {combinedBounds.size}");
        }
        else if (colliders.Length > 0) // Fallback sur les colliders si pas de renderers
        {
            Bounds combinedBounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                combinedBounds.Encapsulate(colliders[i].bounds);
            }
            maxY = combinedBounds.max.y;
            foundBounds = true;
            if (debugClicks) Debug.Log($"[MouseManager/GetTopOfBuilding] Hauteur du bâtiment '{building.name}' via Colliders: {maxY}. Bounds Center: {combinedBounds.center}, Size: {combinedBounds.size}");
        }

        if (!foundBounds && debugClicks)
        {
            Debug.LogWarning($"[MouseManager/GetTopOfBuilding] Aucun Renderer ou Collider trouvé pour le bâtiment '{building.name}'. Utilisation de sa position Y transform: {maxY}. La hauteur de la bannière pourrait être incorrecte.");
        }

        return maxY;
    }

    // Gère le survol de la souris pour l'outline et le preview de la bannière
    private void HandleMouseHover()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        BuildingInfo info = GetBuildingInfoFromRay(ray);

        BuildingSelectionFeedback newHoveredFeedback = info.feedbackComponent;

        // Si le bâtiment survolé a changé...
        if (newHoveredFeedback != currentlyHoveredBuildingFeedback)
        {
            // 1. Nettoyer l'ancien bâtiment survolé
            if (currentlyHoveredBuildingFeedback != null)
            {
                // On ne le remet par défaut que s'il était en survol, pour ne pas écraser l'état "Selected".
                if (currentlyHoveredBuildingFeedback.CurrentState == OutlineState.Hover)
                {
                    currentlyHoveredBuildingFeedback.SetOutlineState(OutlineState.Default);
                }
            }

            // 2. Mettre en surbrillance le nouveau bâtiment
            if (newHoveredFeedback != null)
            {
                // On ne le met en survol que s'il n'est pas déjà "Selected".
                if (newHoveredFeedback.CurrentState != OutlineState.Selected)
                {
                    newHoveredFeedback.SetOutlineState(OutlineState.Hover);
                }
            }
            
            // Mettre à jour la référence
            currentlyHoveredBuildingFeedback = newHoveredFeedback;
        }

        // Gérer le preview de la bannière et le curseur (logique existante)
        if (info.building != null)
        {
            ShowBanner(persistentPreviewBanner, info.position, info.height, true);
            if (!isHoveringValidTarget) SetHoverCursor();
            isHoveringValidTarget = true;
        }
        else
        {
            if (isHoveringValidTarget)
            {
                HideBanner(persistentPreviewBanner);
                SetDefaultCursor();
            }
            isHoveringValidTarget = false;
        }
    }

    private void ShowBanner(GameObject banner, Vector3 buildingWorldPosition, float buildingTopY, bool isPreview)
    {
        if (banner == null)
        {
            if (debugClicks) Debug.LogError($"[MouseManager] Banner is null! Cannot show {(isPreview ? "preview" : "normal")} banner.");
            return;
        }

        if (isPreview && useSimplePreview)
        {
            // ShowSimplePreview(buildingWorldPosition, buildingTopY); // Adapt this if needed
            return;
        }

        banner.SetActive(false); // Toujours désactiver en premier

        BannerMovement bannerMovement = banner.GetComponent<BannerMovement>();
        float heightOffset = bannerMovement != null ? bannerMovement.FinalHeightOffset : 1.0f; // Utiliser l'offset du bannerMovement

        // La position de la bannière est au-dessus du bâtiment
        Vector3 bannerPosition = new Vector3(buildingWorldPosition.x, buildingTopY + heightOffset, buildingWorldPosition.z);


        if (bannerMovement != null)
        {
            banner.transform.position = bannerPosition; // Définir la position avant d'activer
            banner.SetActive(true);
            bannerMovement.UpdatePosition(bannerPosition); // Dire au BannerMovement où il doit être
        }
        else
        {
            banner.transform.position = bannerPosition;
            banner.SetActive(true);
        }

        EnableAllRenderers(banner);

        if (debugVisuals)
        {
            GameObject sphere = isPreview ? debugPreviewSphere : debugBannerSphere;
            if (sphere != null)
            {
                sphere.transform.position = banner.transform.position;
                sphere.SetActive(true);
            }
        }
    }

    private void HideBanner(GameObject bannerInstance)
    {
        if (bannerInstance != null)
        {
            bannerInstance.SetActive(false);
            if (debugClicks) Debug.Log($"[MouseManager] HideBanner: {bannerInstance.name} désactivé.");
        }

        // Cacher aussi les sphères de débogage associées
        if (debugVisuals)
        {
            if (bannerInstance == persistentPreviewBanner && debugPreviewSphere != null)
            {
                debugPreviewSphere.SetActive(false);
            }
            else if (bannerInstance == persistentBanner && debugBannerSphere != null)
            {
                debugBannerSphere.SetActive(false);
            }
        }
    }

   
    private void HandleBannerPlacement()
    {
        // 1. Cacher la bannière de prévisualisation (ça ne change pas)
        HideBanner(persistentPreviewBanner);

        // 2. Lancer un rayon et obtenir les infos (ça ne change pas)
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        BuildingInfo info = GetBuildingInfoFromRay(ray);

        // Vos logs de débogage sont importants, on les garde !
        if (debugClicks)
        {
            if (info.building != null)
            {
                Debug.Log($"[MouseManager/HandleBannerPlacement] CLIC sur -> Bâtiment: {info.building.name}");
            }
            else
            {
                Debug.LogWarning("[MouseManager/HandleBannerPlacement] CLIC n'a retourné AUCUN bâtiment.");
            }
        }

        // 3. Agir en fonction du résultat du rayon
        if (info.building != null)
        {
            // CAS 1 : On a cliqué sur un bâtiment valide.
            // On ne fait plus de vérifications compliquées ici.
            // On se contente d'informer le BannerController de l'intention de l'utilisateur.
            // C'est lui le chef d'orchestre.
            if (debugClicks) Debug.Log($"[MouseManager] Appel de BannerController.PlaceBannerOnBuilding({info.building.name}).");

            if (BannerController.Exists)
            {
                // On délègue la décision et l'action au BannerController.
                // Si la bannière est placée, il notifiera les observateurs, et notre
                // méthode OnBannerPlaced s'occupera de l'affichage.
                BannerController.Instance.PlaceBannerOnBuilding(info.building);
            }
        }
        else
        {
            // CAS 2 : On a cliqué dans le vide.
            // On vérifie s'il y a une bannière active à retirer.
            if (BannerController.Exists && BannerController.Instance.HasActiveBanner)
            {
               if (debugClicks) Debug.Log("[MouseManager] Clic dans le vide, demande de suppression de la bannière.");

               // On demande au BannerController de nettoyer.
               // Sa méthode ClearBanner() appellera aussi les observateurs,
               // ce qui cachera la bannière visuelle via notre OnBannerPlaced.
               BannerController.Instance.ClearBanner();
            }
        }
    }
    private void OnDisable()
    {
        HideBanner(persistentPreviewBanner);
        HideBanner(persistentBanner);
        if (currentlyHoveredBuildingFeedback != null)
        {
            // Nettoyage final de l'outline au cas où
            if(currentlyHoveredBuildingFeedback.CurrentState == OutlineState.Hover)
            {
                currentlyHoveredBuildingFeedback.SetOutlineState(OutlineState.Default);
            }
            currentlyHoveredBuildingFeedback = null;
        }
        if (BannerController.Exists)
        {
            BannerController.Instance.RemoveObserver(this);
        }
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
    
    public void OnBannerPlaced(int column, int row)
    {
        // Au moment où cette notification est reçue, le BannerController
        // a déjà mis à jour son état. On peut donc lui demander le bâtiment actuel.
        Building currentBuilding = BannerController.Instance.CurrentBuilding;

        // On vérifie que le bâtiment existe (la notification peut aussi servir à effacer)
        if (currentBuilding != null)
        {
            // On s'assure que le bâtiment reçu correspond bien aux coordonnées
            // (c'est une sécurité, normalement toujours vrai)
            Tile tile = currentBuilding.GetOccupiedTile();
            if (tile != null && tile.column == column && tile.row == row)
            {
                // On a le bâtiment, on peut faire la mise à jour visuelle !
                Vector3 buildingPos = currentBuilding.transform.position;
                float buildingTopY = GetTopOfBuilding(currentBuilding);

                ShowBanner(persistentBanner, buildingPos, buildingTopY, false);
                currentBanner = persistentBanner;
            }
        }
        else
        {
            // Si currentBuilding est null, ça veut dire que la bannière a été retirée.
            HideBanner(persistentBanner);
            currentBanner = null;
        }
    }
    private void OnEnable()
    {
        // S'abonner aux événements du BannerController
        if (BannerController.Exists)
        {
            BannerController.Instance.AddObserver(this);
        }
    }

    
    private void OnDestroy()
    {
        if (persistentPreviewBanner != null) Destroy(persistentPreviewBanner);
        if (persistentBanner != null) Destroy(persistentBanner);
        if (debugPreviewSphere != null) Destroy(debugPreviewSphere);
        if (debugBannerSphere != null) Destroy(debugBannerSphere);
        if (Application.isPlaying) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}
