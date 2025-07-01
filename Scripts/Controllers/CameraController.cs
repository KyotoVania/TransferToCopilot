using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections;
using UnityEngine.InputSystem; // Added for Input System

/// <summary>
/// Camera controller for a rhythm game that supports right-click drag panning in 3D space,
/// arrow key movement, mouse wheel zooming, and gamepad targeting with lock mode.
/// </summary>
public class RhythmGameCameraController : MonoBehaviour
{
    [TitleGroup("Movement Settings")]
    [Tooltip("Speed of camera panning with arrow keys")]
    [SerializeField] private float keyboardMoveSpeed = 10f;

    [Tooltip("Speed of camera panning with right-click drag")]
    [SerializeField] private float mousePanSpeed = 1.5f;

    [Tooltip("Whether to invert mouse panning direction")]
    [SerializeField] private bool invertMousePan = false;

    [Tooltip("Layer mask for the ground/surface for raycasting")]
    [SerializeField] private LayerMask groundLayerMask = -1; // Default to everything

    [TitleGroup("Zoom Settings")]
    [Tooltip("Speed of camera zooming with mouse wheel")]
    [SerializeField] private float zoomSpeed = 5f;

    [Tooltip("Minimum allowed zoom level (smaller value = closer zoom for Orthographic, closer Y for Perspective)")]
    [SerializeField] private float minZoom = 2f;

    [Tooltip("Maximum allowed zoom level (larger value = further zoom for Orthographic, further Y for Perspective)")]
    [SerializeField] private float maxZoom = 20f;

    [Tooltip("Whether to invert mouse wheel zoom direction")]
    [SerializeField] private bool invertZoom = false;

    [TitleGroup("Bounds Settings")]
    [Tooltip("Whether to restrict camera movement within bounds")]
    [SerializeField] private bool useBounds = false;

    [ShowIf("useBounds")]
    [Tooltip("Minimum X position the camera can move to")]
    [SerializeField] private float minX = -50f;

    [ShowIf("useBounds")]
    [Tooltip("Maximum X position the camera can move to")]
    [SerializeField] private float maxX = 50f;

    [ShowIf("useBounds")]
    [Tooltip("Minimum Z position the camera can move to")]
    [SerializeField] private float minZ = -50f;

    [ShowIf("useBounds")]
    [Tooltip("Maximum Z position the camera can move to")]
    [SerializeField] private float maxZ = 50f;

    [ShowIf("useBounds")]
    [Tooltip("Minimum Y position the camera can move to (used for perspective zoom limit)")]
    [SerializeField] private float minY = 2f;

    [ShowIf("useBounds")]
    [Tooltip("Maximum Y position the camera can move to (used for perspective zoom limit)")]
    [SerializeField] private float maxY = 50f;

    [TitleGroup("Debug")]
    [ReadOnly]
    [SerializeField] private bool isDragging = false;

    [TitleGroup("Lock Mode Settings")]
    [Tooltip("Speed at which camera moves to follow target when locked")]
    [SerializeField] private float lockFollowSpeed = 5f;

    [Tooltip("Distance to maintain from target when locked")]
    [SerializeField] private float lockDistance = 10f;

    [Tooltip("Height offset when following target")]
    [SerializeField] private float lockHeightOffset = 5f;
    [SerializeField] private float unlockAnimationDuration = 0.4f;
    [SerializeField] private float lockMoveSmoothTime = 0.2f;
    private Vector3 _cameraVelocity = Vector3.zero;


    [TitleGroup("Locking Mechanism")]
    [ReadOnly]
    [SerializeField] public bool controlsLocked = false;
    [ReadOnly]
    [SerializeField] private bool zoomLocked = false;
    [ReadOnly]
    [SerializeField] private bool isCameraLocked = false;

    // NEW: Variables for smooth unlock transition
    [TitleGroup("Unlock Transition")]
    [Tooltip("Duration of the smooth transition when unlocking camera")]
    [SerializeField] private float unlockTransitionDuration = 0.5f;
    
    [Tooltip("If true, camera returns to initial scene position when unlocking. If false, returns to pre-lock position.")]
    [SerializeField] private bool returnToInitialPositionOnUnlock = false;
    
    private Vector3 preLockPosition;
    private Quaternion preLockRotation;
    private Coroutine unlockTransitionCoroutine;

    private Vector3 initialPosition;
    private Quaternion initialRotation; // NEW: Save initial rotation from scene
    private float initialZoomValue;

    private Camera cameraComponent;
    private bool isOrthographic;

    private Coroutine _zoomCoroutine;

    // Lock mode variables
    private Transform currentTarget;
    private Vector3 targetPosition;

    // Events for BannerController communication
    public static event System.Action OnToggleCameraLockRequested;
    private Vector3 _preLockPosition;
    private Quaternion _preLockRotation;
    private Coroutine _cameraAnimationCoroutine;
    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        if (cameraComponent == null)
        {
            Debug.LogError("No Camera component found on this GameObject!");
            enabled = false;
            return;
        }

        isOrthographic = cameraComponent.orthographic;
        
        // NEW: Save BOTH initial position AND rotation from scene setup
        initialPosition = transform.position;
        initialRotation = transform.rotation; // This preserves your scene setup angle!

        if (isOrthographic)
        {
            initialZoomValue = cameraComponent.orthographicSize;
        }
        else
        {
            initialZoomValue = cameraComponent.fieldOfView;
        }
        
        Debug.Log($"[RhythmGameCameraController] Initial state saved - Position: {initialPosition}, Rotation: {initialRotation.eulerAngles}");
    }

    private void OnEnable()
    {
        // Subscribe to ToggleCameraLock input
        if (InputManager.Instance != null)
        {
            InputManager.Instance.GameplayActions.ToggleCameraLock.performed += OnToggleCameraLockPressed;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from input
        if (InputManager.Instance != null)
        {
            InputManager.Instance.GameplayActions.ToggleCameraLock.performed -= OnToggleCameraLockPressed;
        }
    }

    private void Update()
    {
        if (controlsLocked)
            return;

        if (isCameraLocked)
        {
            HandleLockedCameraMovement();
        }
        else
        {
            HandleMouseInput();
            HandleKeyboardInput();
        }
        
        HandleZoomInput(); // Zoom works in both modes unless specifically locked
    }

    private void OnToggleCameraLockPressed(InputAction.CallbackContext context)
    {
        // Inform BannerController that toggle was requested
        OnToggleCameraLockRequested?.Invoke();
    }

    private void HandleLockedCameraMovement()
    {
        if (currentTarget == null) return;

        // Le calcul de la position désirée ne change pas
        Vector3 desiredPosition = currentTarget.position - (transform.forward * lockDistance);
        desiredPosition.y = currentTarget.position.y + lockHeightOffset;

        // SmoothDamp va lisser le mouvement de manière plus naturelle et éviter les à-coups.
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _cameraVelocity, lockMoveSmoothTime);

        // La rotation va maintenant suivre un mouvement de position beaucoup plus fluide,
        // ce qui éliminera les rotations étranges.
        Quaternion targetRotation = Quaternion.LookRotation(currentTarget.position - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lockFollowSpeed * Time.deltaTime);
    }
    private void HandleFreeMovement()
    {
        // On utilise les actions de l'InputManager
        Vector2 moveInput = InputManager.Instance.GameplayActions.CameraMove.ReadValue<Vector2>(); // Stick + ZQSD/WASD
        Vector2 panInput = InputManager.Instance.GameplayActions.CameraPan.ReadValue<Vector2>();   // Clic droit souris

        // Mouvement clavier/stick
        if (moveInput.sqrMagnitude > 0.1f)
        {
            float moveSpeed = keyboardMoveSpeed * Time.deltaTime;
            Vector3 movement = new Vector3(moveInput.x, 0, moveInput.y).normalized * moveSpeed;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 moveDirection = right * movement.x + forward * movement.z;
            transform.position += moveDirection;
        }
        
        // Mouvement "drag" souris
        if (panInput.sqrMagnitude > 0.1f)
        {
            float horizontalMovement = -panInput.x * mousePanSpeed * 0.01f;
            float verticalMovement = -panInput.y * mousePanSpeed * 0.01f;

            if (invertMousePan)
            {
                horizontalMovement = -horizontalMovement;
                verticalMovement = -verticalMovement;
            }

            Vector3 right = transform.right;
            Vector3 forward = Vector3.Cross(transform.right, Vector3.up);

            transform.position += right * horizontalMovement + forward * verticalMovement;
        }

        if (useBounds) EnforceBounds();
    }
    private void HandleMouseInput()
    {
        // NEW: Use Input System for mouse panning
        Vector2 cameraPanInput = InputManager.Instance.GameplayActions.CameraPan.ReadValue<Vector2>();
        
        // Check if right mouse button is being held (CameraPan composite action handles this)
        bool isCurrentlyDragging = cameraPanInput.sqrMagnitude > 0.1f;
        
        if (isCurrentlyDragging)
        {
            if (!isDragging)
            {
                isDragging = true;
            }

            float horizontalMovement = -cameraPanInput.x * mousePanSpeed * 0.01f;
            float verticalMovement = -cameraPanInput.y * mousePanSpeed * 0.01f;

            if (invertMousePan)
            {
                horizontalMovement = -horizontalMovement;
                verticalMovement = -verticalMovement;
            }

            Vector3 right = transform.right;
            Vector3 forward = Vector3.Cross(transform.right, Vector3.up);

            transform.position += right * horizontalMovement + forward * verticalMovement;
            if (useBounds) EnforceBounds();
        }
        else
        {
            isDragging = false;
        }
    }
    private IEnumerator AnimateToStateCoroutine(Vector3 targetPosition, Quaternion targetRotation, float duration)
    {
        controlsLocked = true; // On bloque les inputs pendant l'animation
        float elapsedTime = 0f;
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsedTime / duration);
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        // S'assurer qu'on est bien à la position finale
        transform.position = targetPosition;
        transform.rotation = targetRotation;
        controlsLocked = false; // On libère les inputs
        _cameraAnimationCoroutine = null;
    }
    private void HandleKeyboardInput()
    {
        // NEW: Use Input System for keyboard/gamepad movement
        Vector2 moveInput = InputManager.Instance.GameplayActions.CameraMove.ReadValue<Vector2>();

        if (moveInput.sqrMagnitude > 0.1f)
        {
            float moveSpeed = keyboardMoveSpeed * Time.deltaTime;
            Vector3 movement = new Vector3(moveInput.x, 0, moveInput.y).normalized * moveSpeed;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = transform.up;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 moveDirection = right * movement.x + forward * movement.z;
            transform.position += moveDirection;
            if (useBounds) EnforceBounds();
        }
    }

    private void HandleZoomInput()
    {
        if (zoomLocked || controlsLocked)
        {
            return;
        }

        // NEW: Use Input System for zoom (supports both mouse wheel and gamepad triggers)
        float zoomInput = InputManager.Instance.GameplayActions.CameraZoom.ReadValue<float>();

        if (Mathf.Abs(zoomInput) > 0.01f)
        {
            if (invertZoom)
            {
                zoomInput = -zoomInput;
            }

            if (isOrthographic)
            {
                cameraComponent.orthographicSize = Mathf.Clamp(
                    cameraComponent.orthographicSize - zoomInput * zoomSpeed,
                    minZoom,
                    maxZoom
                );
            }
            else
            {
                Vector3 zoomDirection = transform.forward * zoomInput * zoomSpeed;
                transform.position += zoomDirection;
                if (useBounds)
                {
                    EnforceBounds();
                }
            }
        }
    }

    private void EnforceBounds()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
        transform.position = pos;
    }

    [Button("Reset Camera")]
    public void ResetCameraToInitialState()
    {
        if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
        if (unlockTransitionCoroutine != null) StopCoroutine(unlockTransitionCoroutine);
        
        transform.position = initialPosition;
        transform.rotation = initialRotation; // NEW: Restore scene setup rotation instead of identity

        if (isOrthographic)
        {
            cameraComponent.orthographicSize = initialZoomValue;
        }
        else
        {
            cameraComponent.fieldOfView = initialZoomValue;
        }
        Debug.Log("[RhythmGameCameraController] Camera reset to initial state.");
    }

    public void ZoomOutToMaxAndLockControls(bool animate = true, float animationDuration = 1.0f)
    {
        controlsLocked = true;
        ZoomOutToMaxAndLockZoomOnly(animate, animationDuration);
        Debug.Log("[RhythmGameCameraController] All controls locked and zooming to max.");
    }

    public void ZoomOutToMaxAndLockZoomOnly(bool animate = true, float animationDuration = 1.0f)
    {
        zoomLocked = true;
        Debug.Log("[RhythmGameCameraController] Zoom locked. Zooming out to maximum.");

        if (_zoomCoroutine != null)
        {
            StopCoroutine(_zoomCoroutine);
        }

        if (animate && animationDuration > 0)
        {
            _zoomCoroutine = StartCoroutine(AnimateZoomOutCoroutine(animationDuration));
        }
        else
        {
            ApplyMaxZoomInstantly();
        }
    }

    private void ApplyMaxZoomInstantly()
    {
        if (isOrthographic)
        {
            cameraComponent.orthographicSize = maxZoom;
        }
        else
        {
            if (useBounds)
            {
                Vector3 targetPos = transform.position;
                targetPos.y = maxY;
                transform.position = targetPos;
                EnforceBounds();
            }
            else
            {
                Debug.LogWarning("[RhythmGameCameraController] Max zoom for perspective camera without 'useBounds' enabled is not performing a position change. Consider adjusting Field of View or enabling bounds with maxY.");
            }
        }
    }

    private IEnumerator AnimateZoomOutCoroutine(float duration)
    {
        float elapsedTime = 0f;

        if (isOrthographic)
        {
            float startSize = cameraComponent.orthographicSize;
            float targetSize = maxZoom;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);
                cameraComponent.orthographicSize = Mathf.Lerp(startSize, targetSize, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            cameraComponent.orthographicSize = targetSize;
        }
        else
        {
            Vector3 startPosition = transform.position;
            Vector3 targetPosition = startPosition;

            if (useBounds)
            {
                targetPosition.y = maxY;
            }
            else
            {
                Debug.LogWarning("[RhythmGameCameraController] Animated max zoom for perspective camera without 'useBounds' is not changing position. Consider Field of View animation or enabling bounds.");
                controlsLocked = true;
                _zoomCoroutine = null;
                yield break;
            }

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);
                transform.position = Vector3.Lerp(startPosition, targetPosition, Mathf.SmoothStep(0f, 1f, t));
                if (useBounds) EnforceBounds();
                yield return null;
            }
            transform.position = targetPosition;
            if (useBounds) EnforceBounds();
        }
        _zoomCoroutine = null;
        Debug.Log("[RhythmGameCameraController] Animated zoom out complete.");
    }

    public void UnlockZoomOnly()
    {
        zoomLocked = false;
        Debug.Log("[RhythmGameCameraController] Zoom controls unlocked.");
    }

    public void UnlockControlsAndReset()
    {
        if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
        if (unlockTransitionCoroutine != null) StopCoroutine(unlockTransitionCoroutine);
        controlsLocked = false;
        isCameraLocked = false; // NEW: Make sure to unlock camera state
        currentTarget = null;   // NEW: Clear target
        ResetCameraToInitialState(); // This now correctly restores initial rotation
        Debug.Log("[RhythmGameCameraController] Controls unlocked and camera reset to scene setup.");
    }

    /// <summary>
    /// Activates lock mode and sets the target to follow
    /// </summary>
    public void LockOnTarget(Transform newTarget)
    {
        if (newTarget == null) return;

        // Si on n'était pas déjà locké, on sauvegarde la position/rotation
        if (!isCameraLocked)
        {
            _preLockPosition = transform.position;
            _preLockRotation = transform.rotation;
        }

        Building buildingComponent = newTarget.GetComponent<Building>();
        // On vérifie si on a un bâtiment et s'il a une tuile
        if (buildingComponent != null && buildingComponent.GetOccupiedTile() != null)
        {
            // La cible devient la TUILE, pas le bâtiment
            currentTarget = buildingComponent.GetOccupiedTile().transform;
        }
        else
        {
            // Si ce n'est pas un bâtiment ou s'il n'a pas de tuile, on cible l'objet lui-même
            currentTarget = newTarget;
        }
        
        isCameraLocked = true;
    }

    /// <summary>
    /// Deactivates lock mode and returns to free camera movement with smooth transition
    /// </summary>
    public void UnlockCamera()
    {
        if (!isCameraLocked) return;

        isCameraLocked = false;
        currentTarget = null;
        
        // On lance la coroutine pour un retour en douceur
        if (_cameraAnimationCoroutine != null) StopCoroutine(_cameraAnimationCoroutine);
        _cameraAnimationCoroutine = StartCoroutine(AnimateToStateCoroutine(_preLockPosition, _preLockRotation, unlockAnimationDuration));
    }

    /// <summary>
    /// NEW: Smooth transition coroutine for unlocking camera
    /// FIXED: Returns to initial scene rotation, position configurable
    /// </summary>
    private IEnumerator SmoothUnlockTransition()
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        
        // NEW: Choose target position based on setting
        Vector3 targetPosition = returnToInitialPositionOnUnlock ? initialPosition : preLockPosition;
        
        float elapsedTime = 0f;

        while (elapsedTime < unlockTransitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / unlockTransitionDuration);
            
            // Use SmoothStep for a more natural transition
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            
            transform.position = Vector3.Lerp(startPosition, targetPosition, smoothT);
            transform.rotation = Quaternion.Slerp(startRotation, initialRotation, smoothT); // Always return to initial scene rotation
            
            yield return null;
        }

        // Ensure final state is exact
        transform.position = targetPosition;
        transform.rotation = initialRotation; // Always restore the scene setup angle
        
        unlockTransitionCoroutine = null;
        
        string positionType = returnToInitialPositionOnUnlock ? "initial scene" : "pre-lock";
        Debug.Log($"[RhythmGameCameraController] Unlock transition completed - returned to {positionType} position and initial scene angle");
    }

    /// <summary>
    /// Returns whether the camera is currently in lock mode
    /// </summary>
    public bool IsLocked => isCameraLocked;
}