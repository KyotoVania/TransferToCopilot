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
    // MODIFIED: Events now pass a generic GameObject, which can be a building or a unit.
    public static event System.Action<GameObject> OnTargetHovered;
    public static event System.Action OnHoverEnded;
    public static event System.Action<GameObject> OnTargetSelected;

    [Header("Raycast Settings")]
    [SerializeField] private LayerMask tileLayerMask;
    [SerializeField] private LayerMask buildingLayerMask;
    [SerializeField] private LayerMask unitLayerMask; // NEW: Layer mask for your targetable units.
    [SerializeField] private float raycastDistance = 100f;

    [Header("Mouse Settings")]
    [SerializeField] private float clickCooldown = 0.2f;
    [SerializeField] private Texture2D defaultCursorTexture;
    [SerializeField] private Texture2D hoverCursorTexture;
    [SerializeField] private Vector2 cursorHotspot = Vector2.zero;

    [Header("Gamepad Targeting")]
    [SerializeField] private bool isTargetingMode = false;
    // MODIFIED: The list now holds GameObjects. Note: Gamepad cycling will only find Buildings by default.
    private List<GameObject> targetableObjects = new List<GameObject>();
    private int currentTargetIndex = 0;

    [Header("Debugging")]
    [SerializeField] private bool debugLogs = true;

    private float lastClickTime;
    // MODIFIED: The currently hovered object is now a GameObject.
    private GameObject currentlyHoveredObject;
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

        UpdateHoveredTarget(null);
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
        if (isTargetingMode) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        // MODIFIED: Method now finds any targetable GameObject.
        GameObject foundTarget = GetTargetableFromRay(ray);
        UpdateHoveredTarget(foundTarget);

        // Gestion du clic pour la sélection
        if (Input.GetMouseButtonDown(0) && Time.time - lastClickTime > clickCooldown)
        {
            lastClickTime = Time.time;
            if (currentlyHoveredObject != null)
            {
                 if(debugLogs) Debug.Log($"[InputTargetingManager] Mouse click selection: {currentlyHoveredObject.name}");
                 OnTargetSelected?.Invoke(currentlyHoveredObject); // MODIFIED: Pass the GameObject
            }
            else
            {
                 // Clic dans le vide, on notifie aussi pour que les systèmes puissent réagir (ex: enlever la bannière)
                 if(debugLogs) Debug.Log($"[InputTargetingManager] Mouse click on empty space.");
                 OnTargetSelected?.Invoke(null); // MODIFIED: Pass null
            }
        }
    }

    /// <summary>
    /// Met à jour le bâtiment actuellement survolé et déclenche les événements et la surbrillance.
    /// </summary>
    private void UpdateHoveredTarget(GameObject newTarget) // MODIFIED: Parameter is a GameObject
    {
        if (newTarget != currentlyHoveredObject)
        {
            // 1. Nettoyer l'ancien bâtiment
            if (currentlyHoveredObject != null)
            {
                if(debugLogs) Debug.Log($"[InputTargetingManager] Hover ended on {currentlyHoveredObject.name}");
                OnHoverEnded?.Invoke();

                if (currentFeedback != null && currentFeedback.CurrentState == OutlineState.Hover)
                {
                    currentFeedback.SetOutlineState(OutlineState.Default);
                }
            }

            // 2. Mettre à jour avec le nouveau bâtiment
            currentlyHoveredObject = newTarget;

            if (currentlyHoveredObject != null)
            {
                if(debugLogs) Debug.Log($"[InputTargetingManager] Hover started on {currentlyHoveredObject.name}");
                OnTargetHovered?.Invoke(currentlyHoveredObject); // MODIFIED: Pass the GameObject

                // Gérer l'outline (will only work if the hovered object has this component)
                currentFeedback = currentlyHoveredObject.GetComponent<BuildingSelectionFeedback>();
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
        if (debugLogs) Debug.Log("[InputTargetingManager] Entering targeting mode WITH camera lock.");
        ScanForTargetableObjects();
        if (targetableObjects.Count == 0) return;

        isTargetingMode = true;
        SelectClosestTargetAsDefault();
        UpdateGamepadTarget();

        var cameraController = FindFirstObjectByType<RhythmGameCameraController>();
        if (cameraController != null && targetableObjects.Count > 0)
        {
            cameraController.LockOnTarget(targetableObjects[currentTargetIndex].transform);
        }
    }

    private void ExitTargetingMode()
    {
        if (debugLogs) Debug.Log("[InputTargetingManager] Exiting targeting mode.");
        isTargetingMode = false;
        UpdateHoveredTarget(null); // Clear hover state

        var cameraController = FindFirstObjectByType<RhythmGameCameraController>();
        if (cameraController != null) cameraController.UnlockCamera();

        targetableObjects.Clear();
    }

    private void OnCycleTargetPressed(InputAction.CallbackContext context)
    {
        if (!isTargetingMode)
        {
            InitializeTargetingWithoutCameraLock();
            return;
        }

        if (targetableObjects.Count <= 1) return;

        float axisValue = context.ReadValue<float>();
        if (Mathf.Abs(axisValue) < 0.5f) return;

        if (axisValue > 0) currentTargetIndex = (currentTargetIndex + 1) % targetableObjects.Count;
        else currentTargetIndex = (currentTargetIndex - 1 + targetableObjects.Count) % targetableObjects.Count;

        UpdateGamepadTarget();
    }

    private void InitializeTargetingWithoutCameraLock()
    {
        if (debugLogs) Debug.Log("[InputTargetingManager] Initializing targeting mode without camera lock.");
        ScanForTargetableObjects();
        if (targetableObjects.Count == 0) return;

        isTargetingMode = true;
        SelectClosestTargetAsDefault();

        if (targetableObjects.Count > 0)
        {
            GameObject targetObject = targetableObjects[currentTargetIndex];
            UpdateHoveredTarget(targetObject);
        }
    }

    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        if (!isTargetingMode || currentlyHoveredObject == null) return;

        if (debugLogs) Debug.Log($"[InputTargetingManager] Gamepad selection: {currentlyHoveredObject.name}");
        OnTargetSelected?.Invoke(currentlyHoveredObject);
    }

    private void UpdateGamepadTarget()
    {
        CleanupDestroyedTargets();

        if (targetableObjects.Count == 0)
        {
            ExitTargetingMode();
            return;
        }

        if (currentTargetIndex >= targetableObjects.Count)
        {
            currentTargetIndex = 0;
        }

        GameObject targetObject = targetableObjects[currentTargetIndex];
        UpdateHoveredTarget(targetObject);

        var cameraController = FindFirstObjectByType<RhythmGameCameraController>();
        if (cameraController != null && cameraController.IsLocked)
        {
            cameraController.LockOnTarget(targetObject.transform);
        }
    }

    private void CleanupDestroyedTargets()
    {
        int originalCount = targetableObjects.Count;
        targetableObjects.RemoveAll(obj => obj == null);

        if (originalCount != targetableObjects.Count && debugLogs)
        {
            Debug.Log($"[InputTargetingManager] Cleaned up {originalCount - targetableObjects.Count} destroyed objects. Remaining: {targetableObjects.Count}");
        }
    }

    private void ScanForTargetableObjects()
    {
        // NOTE: This scan only finds 'Building' objects for gamepad cycling.
        // To include units, you would need to find them and add their GameObjects to the list.
        // For example:
        // var units = FindObjectsOfType<YourUnitScript>().Select(u => u.gameObject);
        // targetableObjects.AddRange(units);
        targetableObjects.Clear();
        var buildings = FindObjectsOfType<Building>()
            .Where(b => b != null && b.IsTargetable)
            .Select(b => b.gameObject); // Select the GameObject

        targetableObjects.AddRange(buildings);

        targetableObjects = targetableObjects
            .OrderBy(b => b.transform.position.x)
            .ThenBy(b => b.transform.position.z)
            .ToList();

        if(debugLogs) Debug.Log($"[InputTargetingManager] Found {targetableObjects.Count} targetable objects for gamepad cycling.");
    }

    private void SelectClosestTargetAsDefault()
    {
        if (targetableObjects.Count == 0) return;
        var cameraPos = Camera.main.transform.position;
        float closestDist = float.MaxValue;

        for (int i = 0; i < targetableObjects.Count; i++)
        {
            float dist = Vector3.Distance(cameraPos, targetableObjects[i].transform.position);
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
    /// Lance un rayon et retourne le GameObject trouvé, que ce soit un bâtiment ou une unité.
    /// </summary>
    private GameObject GetTargetableFromRay(Ray ray)
    {
        // MODIFIED: Combine building and unit layers for the raycast.
        LayerMask combinedMask = buildingLayerMask | unitLayerMask;

        // Priority 1: Toucher directement un collider sur le bâtiment ou une unité.
        if (Physics.Raycast(ray, out RaycastHit hitInfo, raycastDistance, combinedMask))
        {
            // Check if the hit object is a Building first
            Building building = hitInfo.collider.GetComponentInParent<Building>();
            if (building != null)
            {
                return building.gameObject;
            }

            // If not a building, return the GameObject directly. This will be your unit.
            return hitInfo.collider.gameObject;
        }

        // Priority 2: Toucher une tuile qui contient un bâtiment
        if (Physics.Raycast(ray, out hitInfo, raycastDistance, tileLayerMask))
        {
            Tile tile = hitInfo.collider.GetComponent<Tile>();
            if (tile != null && tile.currentBuilding != null)
            {
                return tile.currentBuilding.gameObject;
            }
        }

        return null; // Rien n'a été trouvé
    }

    #endregion
}