using UnityEngine;
using Sirenix.OdinInspector; // Si tu utilises Odin Inspector
using System.Collections; // Pour les coroutines si tu veux un dezoom animé

/// <summary>
/// Camera controller for a rhythm game that supports right-click drag panning in 3D space,
/// arrow key movement, and mouse wheel zooming.
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
    [SerializeField] private float minY = 2f; // Renommé pour clarté

    [ShowIf("useBounds")]
    [Tooltip("Maximum Y position the camera can move to (used for perspective zoom limit)")]
    [SerializeField] private float maxY = 50f; // Renommé pour clarté

    [TitleGroup("Debug")]
    [ReadOnly]
    [SerializeField] private bool isRightMouseDown = false;

    [ReadOnly]
    [SerializeField] private Vector3 lastMousePosition;

    [ReadOnly]
    [SerializeField] private bool isDragging = false;

    [TitleGroup("Locking Mechanism")]
    [ReadOnly]
    [SerializeField] public bool controlsLocked = false; // Pour verrouiller tous les contrôles
    [ReadOnly] // NOUVEAU : Pour voir l'état du verrouillage du zoom
    [SerializeField] private bool zoomLocked = false;    // Pour verrouiller uniquement le zoom

    private Vector3 initialPosition;
    private float initialZoomValue;

    private Camera cameraComponent;
    private bool isOrthographic;

    private Coroutine _zoomCoroutine;

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

        if (isOrthographic)
        {
            initialZoomValue = cameraComponent.orthographicSize;
        }
        else
        {
            initialZoomValue = cameraComponent.fieldOfView;
        }
    }

    private void Update()
    {
        if (controlsLocked) // Si tous les contrôles sont verrouillés, ne rien faire
            return;
        // Les autres inputs sont traités même si seul le zoom est verrouillé
        HandleMouseInput();
        HandleKeyboardInput();
        HandleZoomInput(); // Cette méthode vérifiera zoomLocked en interne
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(1))
        {
            isRightMouseDown = true;
            lastMousePosition = Input.mousePosition;
            isDragging = true;
        }

        if (Input.GetMouseButtonUp(1))
        {
            isRightMouseDown = false;
            isDragging = false;
        }

        if (isRightMouseDown && isDragging)
        {
            Vector3 currentMousePosition = Input.mousePosition;
            Vector3 mouseDelta = currentMousePosition - lastMousePosition;

            if (mouseDelta.sqrMagnitude > 0.1f)
            {
                float horizontalMovement = -mouseDelta.x * mousePanSpeed * 0.01f;
                float verticalMovement = -mouseDelta.y * mousePanSpeed * 0.01f;

                if (invertMousePan)
                {
                    horizontalMovement = -horizontalMovement;
                    verticalMovement = -verticalMovement;
                }

                Vector3 right = transform.right;
                Vector3 forward = Vector3.Cross(transform.right, Vector3.up);

                transform.position += right * horizontalMovement + forward * verticalMovement;
                lastMousePosition = currentMousePosition;
                if (useBounds) EnforceBounds();
            }
        }
    }

    private void HandleKeyboardInput()
    {
        // ... (code existant inchangé)
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        if (horizontalInput != 0 || verticalInput != 0)
        {
            float moveSpeed = keyboardMoveSpeed * Time.deltaTime;
            Vector3 movement = new Vector3(horizontalInput, 0, verticalInput).normalized * moveSpeed;
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
        // --- MODIFIÉ ---
        if (zoomLocked || controlsLocked) // Vérifie si le zoom spécifique ou tous les contrôles sont verrouillés
        {
            return;
        }
        // ---------------

        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        if (scrollInput != 0)
        {
            // ... (code existant pour le zoom ortho et perspective) ...
            if (invertZoom)
            {
                scrollInput = -scrollInput;
            }

            if (isOrthographic)
            {
                cameraComponent.orthographicSize = Mathf.Clamp(
                    cameraComponent.orthographicSize - scrollInput * zoomSpeed,
                    minZoom,
                    maxZoom
                );
            }
            else
            {
                Vector3 zoomDirection = transform.forward * scrollInput * zoomSpeed;
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
        pos.y = Mathf.Clamp(pos.y, minY, maxY); // minY/maxY pour la perspective
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
        transform.position = pos;
    }

    [Button("Reset Camera")]
    public void ResetCameraToInitialState() // Renommé pour plus de clarté
    {
        if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
        transform.position = initialPosition;

        if (isOrthographic)
        {
            cameraComponent.orthographicSize = initialZoomValue;
        }
        else
        {
            cameraComponent.fieldOfView = initialZoomValue; // Si tu utilisais FoV
            // Si le zoom perspective est basé sur Y, initialPosition.y est déjà pris en compte.
        }
        Debug.Log("[RhythmGameCameraController] Camera reset to initial state.");
    }

    public void ZoomOutToMaxAndLockControls(bool animate = true, float animationDuration = 1.0f)
    {
        // Cette méthode verrouille TOUS les contrôles
        controlsLocked = true; // Verrouille aussi le mouvement/pan
        ZoomOutToMaxAndLockZoomOnly(animate, animationDuration); // Appelle la nouvelle méthode qui ne verrouille que le zoom
        Debug.Log("[RhythmGameCameraController] All controls locked and zooming to max.");
    }

    public void ZoomOutToMaxAndLockZoomOnly(bool animate = true, float animationDuration = 1.0f)
    {
        zoomLocked = true; // Verrouille uniquement le zoom
        // Ne pas mettre controlsLocked = true ici
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
            cameraComponent.orthographicSize = maxZoom; // maxZoom est la taille ortho max
        }
        else // Perspective
        {
            if (useBounds)
            {
                // Pour la perspective, "max zoom out" signifie atteindre la position Y maximale.
                // Nous devons préserver X et Z autant que possible tout en atteignant maxY.
                // Une approche simple est de définir Y directement si c'est la contrainte principale.
                // Attention, cela pourrait changer l'angle de vue si la caméra n'est pas censée bouger uniquement sur Y.
                // Si le zoom est un "dolly" (avant/arrière), il faudrait reculer jusqu'à atteindre maxY.

                // Approche simple : fixer Y à maxY si les bornes sont utilisées.
                Vector3 targetPos = transform.position;
                targetPos.y = maxY;
                transform.position = targetPos;
                EnforceBounds(); // S'assurer que X et Z sont toujours dans les bornes
            }
            else
            {
                // Si pas de bornes, le concept de "maxZoom" pour une caméra perspective
                // est moins bien défini par les paramètres actuels (qui affectent la position).
                // Tu pourrais augmenter le Field of View ici si tu veux.
                // cameraComponent.fieldOfView = unValeurMaxFoV; (ex: 90)
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
            float targetSize = maxZoom; // maxZoom est la taille ortho max
            while (elapsedTime < duration)
            {
                // Utiliser Time.unscaledDeltaTime car cette animation peut se jouer quand Time.timeScale = 0
                elapsedTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);
                cameraComponent.orthographicSize = Mathf.Lerp(startSize, targetSize, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            cameraComponent.orthographicSize = targetSize;
        }
        else // Perspective
        {
            Vector3 startPosition = transform.position;
            Vector3 targetPosition = startPosition; // Commence avec la position actuelle

            if (useBounds)
            {
                targetPosition.y = maxY; // Cible la hauteur maximale
                // Si tu veux préserver X et Z, c'est déjà fait car on part de startPosition.
                // EnforceBounds sera appelé à la fin pour s'assurer.
            }
            else
            {
                // Si pas de bornes, on pourrait reculer la caméra d'une certaine distance comme "max zoom"
                // targetPosition -= transform.forward * uneCertaineDistancePourMaxZoomPerspective;
                // Pour l'instant, on ne fait rien de spécial pour la position si pas de bornes,
                // car `maxZoom` n'est pas défini pour la position perspective sans bornes.
                Debug.LogWarning("[RhythmGameCameraController] Animated max zoom for perspective camera without 'useBounds' is not changing position. Consider Field of View animation or enabling bounds.");
                controlsLocked = true; // Assurer que les contrôles sont bien bloqués
                _zoomCoroutine = null;
                yield break; // Sortir si pas de bornes pour l'animation perspective
            }

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);
                transform.position = Vector3.Lerp(startPosition, targetPosition, Mathf.SmoothStep(0f, 1f, t));
                if (useBounds) EnforceBounds(); // Appliquer pendant l'animation pour éviter de sortir des bornes X/Z
                yield return null;
            }
            transform.position = targetPosition;
            if (useBounds) EnforceBounds(); // Une dernière fois pour la précision
        }
        _zoomCoroutine = null;
        Debug.Log("[RhythmGameCameraController] Animated zoom out complete.");
    }

    public void UnlockZoomOnly()
    {
        zoomLocked = false;
        // Optionnel: Réinitialiser le zoom à une valeur par défaut ou le laisser tel quel
        // if (isOrthographic) cameraComponent.orthographicSize = initialZoomValue;
        // else { /* ajuster position Y ou FoV si besoin */ }
        Debug.Log("[RhythmGameCameraController] Zoom controls unlocked.");
    }

    public void UnlockControlsAndReset() // Optionnel
    {
        if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
        controlsLocked = false;
        ResetCameraToInitialState(); // Retour à l'état initial
        Debug.Log("[RhythmGameCameraController] Controls unlocked and camera reset.");
    }
    // -------------------------
}