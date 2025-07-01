using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Gère la détection des cibles via la souris ou la manette.
/// Lance des événements pour notifier les autres systèmes (comme le BannerController)
/// des intentions du joueur (survol, sélection) sans gérer lui-même la logique de jeu.
/// Ce script remplace la partie "détection" de l'ancien MouseManager.
/// </summary>
public class InputTargetingManager : MonoBehaviour
{
    public static InputTargetingManager Instance { get; private set; }

    // --- Événements Publics ---
    // Notifié en continu quand un bâtiment est ciblé (souris ou manette)
    public static event System.Action<Building> OnBuildingHovered;
    // Notifié quand plus rien n'est survolé/ciblé
    public static event System.Action OnHoverEnded;
    // Notifié quand le joueur valide la sélection (clic ou bouton manette)
    public static event System.Action<Building> OnBuildingSelected;

    [Header("Raycast Settings")]
    [SerializeField] private LayerMask tileLayerMask;
    [SerializeField] private LayerMask buildingLayerMask;
    [SerializeField] private float raycastDistance = 100f;

    [Header("Mouse Settings")]
    [SerializeField] private float clickCooldown = 0.2f;
    [SerializeField] private Texture2D defaultCursorTexture;
    [SerializeField] private Texture2D hoverCursorTexture;
    [SerializeField] private Vector2 cursorHotspot = Vector2.zero;

    [Header("Gamepad Targeting")]
    [SerializeField] private bool isTargetingMode = false;
    private List<Building> targetableBuildings = new List<Building>();
    private int currentTargetIndex = 0;
    
    [Header("Debugging")]
    [SerializeField] private bool debugLogs = true;

    private float lastClickTime;
    private Building currentlyHoveredBuilding;
    private BuildingSelectionFeedback currentFeedback;
    
    void Awake()
    {
        // --- Singleton Initialisation ---
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        lastClickTime = -clickCooldown;
        SetCursor(defaultCursorTexture);
    }

    private void OnEnable()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.GameplayActions.PlaceBanner.performed += OnSelectPerformed;
            InputManager.Instance.GameplayActions.CycleTarget.performed += OnCycleTargetPressed;
        }
        RhythmGameCameraController.OnToggleCameraLockRequested += HandleToggleCameraLockRequest;
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.GameplayActions.PlaceBanner.performed -= OnSelectPerformed;
            InputManager.Instance.GameplayActions.CycleTarget.performed -= OnCycleTargetPressed;
        }
        RhythmGameCameraController.OnToggleCameraLockRequested -= HandleToggleCameraLockRequest;
        
        // Nettoyage au cas où un objet serait encore en surbrillance
        UpdateHoveredBuilding(null);
        SetCursor(defaultCursorTexture);
    }

    void Update()
    {
        // La détection à la souris a la priorité si la souris bouge.
        if (Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0 || isTargetingMode == false)
        {
            HandleMouseTargeting();
        }
    }
    
    /// <summary>
    /// Gère la sélection via clic de souris. La sélection via manette est gérée par OnSelectPerformed.
    /// </summary>
    private void HandleMouseTargeting()
    {
        // Si on est en mode ciblage manette, on ne fait rien avec la souris.
        if (isTargetingMode) return;
        
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Building foundBuilding = GetBuildingFromRay(ray);
        UpdateHoveredBuilding(foundBuilding);

        // Gestion du clic pour la sélection
        if (Input.GetMouseButtonDown(0) && Time.time - lastClickTime > clickCooldown)
        {
            lastClickTime = Time.time;
            if (currentlyHoveredBuilding != null)
            {
                 if(debugLogs) Debug.Log($"[InputTargetingManager] Mouse click selection: {currentlyHoveredBuilding.name}");
                 OnBuildingSelected?.Invoke(currentlyHoveredBuilding);
            }
            else
            {
                 // Clic dans le vide, on notifie aussi pour que les systèmes puissent réagir (ex: enlever la bannière)
                 if(debugLogs) Debug.Log($"[InputTargetingManager] Mouse click on empty space.");
                 OnBuildingSelected?.Invoke(null);
            }
        }
    }

    /// <summary>
    /// Met à jour le bâtiment actuellement survolé et déclenche les événements et la surbrillance.
    /// </summary>
    private void UpdateHoveredBuilding(Building newBuilding)
    {
        if (newBuilding != currentlyHoveredBuilding)
        {
            // 1. Nettoyer l'ancien bâtiment
            if (currentlyHoveredBuilding != null)
            {
                if(debugLogs) Debug.Log($"[InputTargetingManager] Hover ended on {currentlyHoveredBuilding.name}");
                OnHoverEnded?.Invoke();

                if (currentFeedback != null && currentFeedback.CurrentState == OutlineState.Hover)
                {
                    currentFeedback.SetOutlineState(OutlineState.Default);
                }
            }

            // 2. Mettre à jour avec le nouveau bâtiment
            currentlyHoveredBuilding = newBuilding;
            
            if (currentlyHoveredBuilding != null)
            {
                if(debugLogs) Debug.Log($"[InputTargetingManager] Hover started on {currentlyHoveredBuilding.name}");
                OnBuildingHovered?.Invoke(currentlyHoveredBuilding);

                // Gérer l'outline
                currentFeedback = currentlyHoveredBuilding.GetComponent<BuildingSelectionFeedback>();
                if (currentFeedback != null && currentFeedback.CurrentState == OutlineState.Default)
                {
                    currentFeedback.SetOutlineState(OutlineState.Hover);
                }
                SetCursor(hoverCursorTexture);
            }
            else
            {
                // Pas de nouveau bâtiment
                currentFeedback = null;
                SetCursor(defaultCursorTexture);
            }
        }
    }
    
    private void SetCursor(Texture2D cursorTexture)
    {
        if (cursorTexture != null)
        {
            Cursor.SetCursor(cursorTexture, cursorHotspot, CursorMode.Auto);
        }
    }

    #region Gamepad Targeting
    
    private void HandleToggleCameraLockRequest()
    {
        if (isTargetingMode) ExitTargetingMode();
        else EnterTargetingMode();
    }
    
    private void EnterTargetingMode()
    {
        if (debugLogs) Debug.Log("[InputTargetingManager] Entering targeting mode.");
        ScanForTargetableBuildings();
        if (targetableBuildings.Count == 0) return;

        isTargetingMode = true;
        SelectClosestBuildingAsDefault();
        UpdateGamepadTarget();
    }

    private void ExitTargetingMode()
    {
        if (debugLogs) Debug.Log("[InputTargetingManager] Exiting targeting mode.");
        isTargetingMode = false;
        UpdateHoveredBuilding(null); // Clear hover state

        var cameraController = FindFirstObjectByType<RhythmGameCameraController>();
        if (cameraController != null) cameraController.UnlockCamera();
        
        targetableBuildings.Clear();
    }

    private void OnCycleTargetPressed(InputAction.CallbackContext context)
    {
        if (!isTargetingMode) EnterTargetingMode();
        if (targetableBuildings.Count <= 1) return;

        float axisValue = context.ReadValue<float>();
        if (Mathf.Abs(axisValue) < 0.5f) return;

        if (axisValue > 0) currentTargetIndex = (currentTargetIndex + 1) % targetableBuildings.Count;
        else currentTargetIndex = (currentTargetIndex - 1 + targetableBuildings.Count) % targetableBuildings.Count;
        
        UpdateGamepadTarget();
    }

    /// <summary>
    /// Gère la sélection via le bouton de la manette (partagé avec le clic souris via le même handler)
    /// </summary>
    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        // On ne gère la manette que si elle est le dernier périphérique utilisé et qu'on est en mode ciblage
        if (!isTargetingMode || currentlyHoveredBuilding == null) return;
        
        if (debugLogs) Debug.Log($"[InputTargetingManager] Gamepad selection: {currentlyHoveredBuilding.name}");
        OnBuildingSelected?.Invoke(currentlyHoveredBuilding);
    }
    
    private void UpdateGamepadTarget()
    {
        if (targetableBuildings.Count == 0) return;
        
        Building targetBuilding = targetableBuildings[currentTargetIndex];
        UpdateHoveredBuilding(targetBuilding);

        var cameraController = FindFirstObjectByType<RhythmGameCameraController>();
        if (cameraController != null)
        {
            cameraController.LockOnTarget(targetBuilding.transform);
        }
    }

    private void ScanForTargetableBuildings()
    {
        targetableBuildings = FindObjectsOfType<Building>()
            .Where(b => b.IsTargetable)
            .OrderBy(b => b.transform.position.x)
            .ThenBy(b => b.transform.position.z)
            .ToList();
        if(debugLogs) Debug.Log($"[InputTargetingManager] Found {targetableBuildings.Count} targetable buildings.");
    }
    
    private void SelectClosestBuildingAsDefault()
    {
        if (targetableBuildings.Count == 0) return;
        var cameraPos = Camera.main.transform.position;
        float closestDist = float.MaxValue;
        
        for (int i = 0; i < targetableBuildings.Count; i++)
        {
            float dist = Vector3.Distance(cameraPos, targetableBuildings[i].transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                currentTargetIndex = i;
            }
        }
    }

    #endregion

    #region Utility
    
    /// <summary>
    /// Lance un rayon et retourne le bâtiment trouvé, que ce soit directement ou via sa tuile.
    /// C'est une fusion de la logique de l'ancien MouseManager.
    /// </summary>
    private Building GetBuildingFromRay(Ray ray)
    {
        // Priorité 1: Toucher directement un collider sur le bâtiment
        if (Physics.Raycast(ray, out RaycastHit hitInfo, raycastDistance, buildingLayerMask))
        {
            Building building = hitInfo.collider.GetComponentInParent<Building>();
            if (building != null) return building;
        }

        // Priorité 2: Toucher une tuile qui contient un bâtiment
        if (Physics.Raycast(ray, out hitInfo, raycastDistance, tileLayerMask))
        {
            Tile tile = hitInfo.collider.GetComponent<Tile>();
            if (tile != null && tile.currentBuilding != null)
            {
                return tile.currentBuilding;
            }
        }

        return null; // Rien n'a été trouvé
    }

    #endregion
}