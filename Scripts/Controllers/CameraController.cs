using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections;
using UnityEngine.InputSystem;

/// <summary>
/// Camera controller for a rhythm game that supports right-click drag panning in 3D space,
/// arrow key movement, mouse wheel zooming, and gamepad targeting with lock mode.
/// Handles both Orthographic and Perspective camera projections.
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

    [TitleGroup("Zoom Settings")]
    [Tooltip("Speed of camera zooming with mouse wheel or gamepad")]
    [SerializeField] private float zoomSpeed = 5f;

    [Tooltip("Minimum allowed zoom level (Orthographic Size or Perspective Y Position)")]
    [SerializeField] private float minZoom = 2f;

    [Tooltip("Maximum allowed zoom level (Orthographic Size or Perspective Y Position)")]
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
    
    // MODIFIÉ : Renommage de minY/maxY pour plus de clarté, car ils sont maintenant utilisés pour le zoom en perspective.
    [ShowIf("useBounds")]
    [Tooltip("Minimum Y position (zoom) for perspective camera")]
    [SerializeField] private float perspectiveMinY = 2f;

    [ShowIf("useBounds")]
    [Tooltip("Maximum Y position (zoom) for perspective camera")]
    [SerializeField] private float perspectiveMaxY = 50f;

    [TitleGroup("Lock Mode Settings")]
    [SerializeField] private float lockFollowSpeed = 5f;
    [SerializeField] private float lockDistance = 10f;
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

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float initialZoomValue;

    private Camera cameraComponent;
    private bool isOrthographic;

    private Coroutine _zoomCoroutine;
    private Transform currentTarget;
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
        
        initialPosition = transform.position;
        initialRotation = transform.rotation; 

        if (isOrthographic)
        {
            initialZoomValue = cameraComponent.orthographicSize;
        }
        else
        {
            // En perspective, le "zoom" initial est sa position Y.
            initialZoomValue = transform.position.y; 
        }
    }

    private void OnEnable()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.GameplayActions.ToggleCameraLock.performed += OnToggleCameraLockPressed;
        }
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.GameplayActions.ToggleCameraLock.performed -= OnToggleCameraLockPressed;
        }
    }

    private void Update()
    {
        if (controlsLocked) return;

        if (isCameraLocked)
        {
            HandleLockedCameraMovement();
        }
        else
        {
            HandleMouseInput();
            HandleKeyboardInput();
        }
        
        HandleZoomInput();
    }

    private void OnToggleCameraLockPressed(InputAction.CallbackContext context)
    {
        OnToggleCameraLockRequested?.Invoke();
    }

    private void HandleMouseInput()
    {
        Vector2 cameraPanInput = InputManager.Instance.GameplayActions.CameraPan.ReadValue<Vector2>();
        bool isCurrentlyDragging = cameraPanInput.sqrMagnitude > 0.1f;
        
        if (isCurrentlyDragging)
        {

            float horizontalMovement = -cameraPanInput.x * mousePanSpeed * 0.01f;
            float verticalMovement = -cameraPanInput.y * mousePanSpeed * 0.01f;

            if (invertMousePan)
            {
                horizontalMovement = -horizontalMovement;
                verticalMovement = -verticalMovement;
            }

            Vector3 right = transform.right;
            Vector3 forward = Vector3.Cross(transform.right, Vector3.up);

            // MODIFIÉ : On calcule la nouvelle position et on la clamp AVANT de l'assigner pour éviter le "glissement".
            Vector3 newPosition = transform.position + (right * horizontalMovement + forward * verticalMovement);
            transform.position = ClampPositionToBounds(newPosition);
        }
        else
        {
        }
    }
    
    private void HandleKeyboardInput()
    {
        Vector2 moveInput = InputManager.Instance.GameplayActions.CameraMove.ReadValue<Vector2>();

        if (moveInput.sqrMagnitude > 0.1f)
        {
            float moveSpeed = keyboardMoveSpeed * Time.deltaTime;
            Vector3 movement = new Vector3(moveInput.x, 0, moveInput.y).normalized * moveSpeed;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = transform.up;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 moveDirection = right * movement.x + forward * movement.z;

            // MODIFIÉ : On calcule la nouvelle position et on la clamp AVANT de l'assigner.
            Vector3 newPosition = transform.position + moveDirection;
            transform.position = ClampPositionToBounds(newPosition);
        }
    }

    private void HandleZoomInput()
    {
        if (zoomLocked || controlsLocked) return;

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
                // MODIFIÉ : En perspective, le zoom change la hauteur (Y) de la caméra.
                Vector3 pos = transform.position;
                float newY = pos.y - (zoomInput * (zoomSpeed / 2f)); // On divise pour une sensation moins rapide
                pos.y = Mathf.Clamp(newY, perspectiveMinY, perspectiveMaxY);
                transform.position = pos;
            }
        }
    }
    
    // MODIFIÉ : Anciennement EnforceBounds. Cette fonction s'assure que la position reste dans les limites.
    private Vector3 ClampPositionToBounds(Vector3 position)
    {
        if (!useBounds) return position;

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.z = Mathf.Clamp(position.z, minZ, maxZ);
        // La hauteur (Y) est déjà gérée par le zoom, on s'assure juste qu'elle ne sorte pas des clous.
        if (!isOrthographic)
        {
            position.y = Mathf.Clamp(position.y, perspectiveMinY, perspectiveMaxY);
        }
        
        return position;
    }

    public void ZoomOutToMaxAndLockZoomOnly(bool animate = true, float animationDuration = 1.0f)
    {
        zoomLocked = true;
        Debug.Log("[RhythmGameCameraController] Zoom locked. Zooming out to maximum.");

        if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);

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
            Vector3 targetPos = transform.position;
            if (useBounds)
            {
                targetPos.x = (minX + maxX) / 2f;
                targetPos.z = (minZ + maxZ) / 2f;
                targetPos.y = perspectiveMaxY;
            }
            else
            {
                // Si pas de limites, on se contente de monter
                targetPos.y = perspectiveMaxY;
            }
            transform.position = targetPos;
        }
    }

    private IEnumerator AnimateZoomOutCoroutine(float duration)
    {
        float elapsedTime = 0f;
        Vector3 startPosition = transform.position;
        
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
            // MODIFIÉ : On calcule une position cible centrée pour une belle vue d'ensemble.
            Vector3 targetPosition = startPosition;
            if (useBounds)
            {
                targetPosition.x = (minX + maxX) / 2f;
                targetPosition.z = (minZ + maxZ) / 2f;
                targetPosition.y = perspectiveMaxY;
            }
            else
            {
                Debug.LogWarning("Animated max zoom for perspective camera without 'useBounds' is not recommended.");
                targetPosition.y = perspectiveMaxY;
            }

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);
                transform.position = Vector3.Lerp(startPosition, targetPosition, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            transform.position = targetPosition;
        }
        _zoomCoroutine = null;
    }

    // --- Le reste du script reste globalement identique, le voici pour la complétude ---

    public void ResetCameraToInitialState()
    {
        if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
        if (_cameraAnimationCoroutine != null) StopCoroutine(_cameraAnimationCoroutine);
        
        transform.position = initialPosition;
        transform.rotation = initialRotation; 

        if (isOrthographic)
        {
            cameraComponent.orthographicSize = initialZoomValue;
        }
        // Pas besoin de 'else', la position Y est déjà dans initialPosition
        
        Debug.Log("[RhythmGameCameraController] Camera reset to initial state.");
    }
    
    public void UnlockZoomOnly()
    {
        zoomLocked = false;
        Debug.Log("[RhythmGameCameraController] Zoom controls unlocked.");
    }

    public void LockOnTarget(Transform newTarget)
    {
        if (newTarget == null) return;
        if (!isCameraLocked)
        {
            _preLockPosition = transform.position;
            _preLockRotation = transform.rotation;
        }
        
        currentTarget = newTarget;
        isCameraLocked = true;
    }

    public void UnlockCamera()
    {
        if (!isCameraLocked) return;

        isCameraLocked = false;
        currentTarget = null;
        
        if (_cameraAnimationCoroutine != null) StopCoroutine(_cameraAnimationCoroutine);
        _cameraAnimationCoroutine = StartCoroutine(AnimateUnlockZoomCoroutine());
    }

    /// <summary>
    /// NOUVEAU : Anime un dézoom fluide lors de l'unlock au lieu de retourner à la position pré-lock
    /// ET restaure la rotation pré-lock pour éviter la désorientation du joueur
    /// </summary>
    private IEnumerator AnimateUnlockZoomCoroutine()
    {
        controlsLocked = true;
        
        float duration = unlockAnimationDuration * 0.5f; // Dézoom plus rapide que l'animation complète
        float elapsedTime = 0f;
        
        // Sauvegarder la rotation actuelle pour l'interpolation
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = _preLockRotation; // Restaurer la rotation pré-lock
        
        if (isOrthographic)
        {
            float startSize = cameraComponent.orthographicSize;
            float targetSize = Mathf.Clamp(startSize + 3f, minZoom, maxZoom); // Dézoome de 3 unités
            
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsedTime / duration);
                cameraComponent.orthographicSize = Mathf.Lerp(startSize, targetSize, t);
                // Restaurer progressivement la rotation
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }
            
            cameraComponent.orthographicSize = targetSize;
        }
        else
        {
            // Pour la caméra perspective, on recule (augmente Y) pour dézoomer
            Vector3 startPosition = transform.position;
            Vector3 targetPosition = startPosition;
            targetPosition.y = Mathf.Clamp(startPosition.y + 5f, perspectiveMinY, perspectiveMaxY); // Recule de 5 unités
            
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsedTime / duration);
                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                // Restaurer progressivement la rotation
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }
            
            transform.position = targetPosition;
        }
        
        // S'assurer que la rotation finale est exactement celle pré-lock
        transform.rotation = targetRotation;
        
        controlsLocked = false;
        _cameraAnimationCoroutine = null;
    }

    private void HandleLockedCameraMovement()
    {
        if (currentTarget == null) return;

        Vector3 desiredPosition = currentTarget.position - (transform.forward * lockDistance);
        desiredPosition.y = currentTarget.position.y + lockHeightOffset;

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _cameraVelocity, lockMoveSmoothTime);

        Quaternion targetRotation = Quaternion.LookRotation(currentTarget.position - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lockFollowSpeed * Time.deltaTime);
    }
    
    private IEnumerator AnimateToStateCoroutine(Vector3 targetPosition, Quaternion targetRotation, float duration)
    {
        controlsLocked = true; 
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

        transform.position = targetPosition;
        transform.rotation = targetRotation;
        controlsLocked = false;
        _cameraAnimationCoroutine = null;
    }

    public bool IsLocked => isCameraLocked;
}
