using UnityEngine;
using System.Collections.Generic; // Nécessaire pour LayerMaskToString si vous le gardez

public class MouseManager : MonoBehaviour
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

    private BuildingOutlineFeedback currentlyHoveredBuildingOutline;

    // Structure to store building information
    private struct BuildingInfo
    {
        public Building building;
        public Tile tile;
        public Vector3 position;
        public float height;
        public BuildingOutlineFeedback outlineFeedback; // --- AJOUT POUR L'OUTLINE ---
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
        info.outlineFeedback = null;

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
            info.outlineFeedback = directlyHitBuilding.GetComponent<BuildingOutlineFeedback>();
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
                info.outlineFeedback = tileComponent.currentBuilding.GetComponent<BuildingOutlineFeedback>();
                // Log AJOUTÉ pour le débogage
                if (debugClicks) Debug.Log($"[MouseManager/GetBuildingInfoFromRay] RETOUR (Tile Hit): Building='{info.building?.name}', Tile='{info.tile?.name}', Pos='{info.position}'");
                return info;
            }
        }

        if (debugClicks) Debug.LogWarning("[MouseManager/GetBuildingInfoFromRay] RETOUR (Rien trouvé pour la bannière)");
        return info;
    }

    // Dans MouseManager.cs
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

        if (info.building != null) // Si on survole un bâtiment
        {
            // Gestion de l'outline
            if (info.outlineFeedback != null)
            {
                if (currentlyHoveredBuildingOutline != info.outlineFeedback)
                {
                    currentlyHoveredBuildingOutline?.HideOutline(); // Cacher l'ancien
                    currentlyHoveredBuildingOutline = info.outlineFeedback;
                    currentlyHoveredBuildingOutline.ShowOutline();
                    if (debugClicks) Debug.Log($"[MouseManager] Hover IN: {info.building.name}");
                }
            }
            else // Pas de composant d'outline sur ce bâtiment, mais on en survolait peut-être un avant
            {
                if (currentlyHoveredBuildingOutline != null)
                {
                     if (debugClicks) Debug.Log($"[MouseManager] Hover OUT (vers bâtiment sans outline): {currentlyHoveredBuildingOutline.gameObject.name}");
                    currentlyHoveredBuildingOutline.HideOutline();
                    currentlyHoveredBuildingOutline = null;
                }
            }

            // Gestion du preview de la bannière (existante)
            if (debugClicks && !isHoveringValidTarget) Debug.Log($"[MouseManager] Preview Banner pour {info.building.name}");
            ShowBanner(persistentPreviewBanner, info.position, info.height, true); // info.position est maintenant celle du bâtiment

            if (!isHoveringValidTarget) SetHoverCursor();
            isHoveringValidTarget = true;
        }
        else // Si on ne survole aucun bâtiment valide
        {
            // Gestion de l'outline
            if (currentlyHoveredBuildingOutline != null)
            {
                 if (debugClicks) Debug.Log($"[MouseManager] Hover OUT (vers rien): {currentlyHoveredBuildingOutline.gameObject.name}");
                currentlyHoveredBuildingOutline.HideOutline();
                currentlyHoveredBuildingOutline = null;
            }

            // Gestion du preview de la bannière (existante)
            if (isHoveringValidTarget)
            {
                if (debugClicks) Debug.Log("[MouseManager] Hiding Preview Banner (no valid target).");
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
        // 1. Cacher la bannière de prévisualisation si elle est active
        HideBanner(persistentPreviewBanner);

        // 2. Lancer un rayon depuis la position de la souris
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (debugClicks) Debug.DrawRay(ray.origin, ray.direction * raycastDistance, Color.magenta, 3f); // Magenta pour le clic

        // 3. Obtenir les informations sur le bâtiment/la tuile sous la souris
        BuildingInfo info = GetBuildingInfoFromRay(ray);

        // Log pour déboguer ce que GetBuildingInfoFromRay retourne lors d'un clic
        if (debugClicks)
        {
            if (info.building != null)
            {
                Debug.Log($"[MouseManager/HandleBannerPlacement] GetBuildingInfoFromRay AU CLIC a retourné -> Bâtiment: {info.building.name}, Tuile: {(info.tile != null ? info.tile.name : "PAS DE TUILE")}, Position du bâtiment: {info.position}");
            }
            else
            {
                Debug.LogWarning("[MouseManager/HandleBannerPlacement] GetBuildingInfoFromRay AU CLIC n'a retourné AUCUN bâtiment (info.building est null).");
            }
        }

        // 4. Vérifier si un bâtiment valide a été trouvé
        if (info.building != null)
        {
            if (debugClicks) Debug.Log($"[MouseManager/HandleBannerPlacement] Clic détecté sur/près du bâtiment '{info.building.name}'. Tentative de placement de la bannière.");

            // Si une bannière existe déjà sur un AUTRE bâtiment, ou si aucune bannière n'existe, on procède.
            // Si la bannière est déjà sur CE bâtiment, BannerController.PlaceBannerOnBuilding devrait gérer la logique (peut-être ne rien faire ou rafraîchir).
            bool shouldPlace = true;
            if (BannerController.Exists && BannerController.Instance.HasActiveBanner)
            {
                if (BannerController.Instance.CurrentBuilding == info.building)
                {
                     if (debugClicks) Debug.Log($"[MouseManager/HandleBannerPlacement] Clic sur le même bâtiment '{info.building.name}' où la bannière est déjà. Le BannerController décidera.");
                    // On laisse BannerController.PlaceBannerOnBuilding décider s'il faut replacer ou non.
                }
                else // Bannière existante, mais sur un bâtiment différent
                {
                     if (debugClicks) Debug.Log($"[MouseManager/HandleBannerPlacement] Nettoyage de l'ancienne bannière sur '{BannerController.Instance.CurrentBuilding?.name}' pour la placer sur '{info.building.name}'.");
                     BannerController.Instance.ClearBanner(); // Nettoie la logique du BannerController
                     // HideBanner(persistentBanner) sera appelé ci-dessous de toute façon.
                }
            }

            // Cacher le GameObjet de la bannière actuelle avant de potentiellement le replacer/réactiver.
            HideBanner(persistentBanner);
            currentBanner = null; // Réinitialiser la référence au GameObject de la bannière active.

            bool registeredInController = false;
            if (BannerController.Exists)
            {
                if (debugClicks) Debug.Log($"[MouseManager/HandleBannerPlacement] Appel de BannerController.PlaceBannerOnBuilding({info.building.name}).");
                registeredInController = BannerController.Instance.PlaceBannerOnBuilding(info.building);
                if (debugClicks) Debug.Log($"[MouseManager/HandleBannerPlacement] BannerController.PlaceBannerOnBuilding a retourné : {registeredInController}");
            }
            else
            {
                if (debugClicks) Debug.LogWarning("[MouseManager/HandleBannerPlacement] BannerController n'existe pas. Placement visuel uniquement.");
                registeredInController = true; // Simuler le succès si pas de BannerController pour le test visuel
            }

            if (registeredInController)
            {
                if (debugClicks) Debug.Log($"[MouseManager/HandleBannerPlacement] Enregistrement dans BannerController réussi (ou BannerController absent). Affichage de la bannière sur {info.building.name} à la position de base {info.position}, hauteur du top {info.height}.");

                // info.position est la position de base du bâtiment.
                // info.height est le Y du sommet du bâtiment.
                ShowBanner(persistentBanner, info.position, info.height, false);
                currentBanner = persistentBanner; // Mettre à jour la référence au GameObject de la bannière.

                if (BannerController.Exists)
                {
                    if (debugClicks) Debug.Log("[MouseManager/HandleBannerPlacement] Forçage de la notification des observateurs du BannerController.");
                    BannerController.Instance.ForceNotifyObservers();
                }
            }
            else
            {
                if (debugClicks) Debug.LogWarning($"[MouseManager/HandleBannerPlacement] Échec de l'enregistrement de la bannière avec BannerController pour le bâtiment '{info.building.name}'.");
            }
        }
        else // info.building était null après GetBuildingInfoFromRay
        {
            if (debugClicks) Debug.LogWarning("[MouseManager/HandleBannerPlacement] Aucun bâtiment valide trouvé au point de clic. La bannière n'est pas placée.");

            // Optionnel : Si l'on veut que cliquer dans le vide retire la bannière existante
            if (BannerController.Exists && BannerController.Instance.HasActiveBanner)
            {
               if (debugClicks) Debug.Log("[MouseManager/HandleBannerPlacement] Clic dans le vide, suppression de la bannière existante.");
               BannerController.Instance.ClearBanner();
               HideBanner(persistentBanner); // Cacher aussi le GameObjet visuel
               currentBanner = null;
            }
        }
    }
    private void OnDisable()
    {
        HideBanner(persistentPreviewBanner);
        HideBanner(persistentBanner);
        if (currentlyHoveredBuildingOutline != null) // Cacher l'outline si le manager est désactivé
        {
            currentlyHoveredBuildingOutline.HideOutline();
            currentlyHoveredBuildingOutline = null;
        }
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void OnDestroy()
    {
        if (persistentPreviewBanner != null) Destroy(persistentPreviewBanner);
        if (persistentBanner != null) Destroy(persistentBanner);
        if (debugPreviewSphere != null) Destroy(debugPreviewSphere);
        if (debugBannerSphere != null) Destroy(debugBannerSphere);
        // Pas besoin de gérer currentlyHoveredBuildingOutline.HideOutline() ici,
        // car si le MouseManager est détruit, l'objet survolé ne devrait plus être surligné.
        // Le OnDisable de BuildingOutlineFeedback s'en chargera si l'objet lui-même est désactivé/détruit.
        if (Application.isPlaying) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}