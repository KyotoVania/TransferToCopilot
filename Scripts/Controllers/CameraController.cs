using UnityEngine;
using Sirenix.OdinInspector;

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

    [Tooltip("Minimum allowed zoom level (smaller value = closer zoom)")]
    [SerializeField] private float minZoom = 2f;

    [Tooltip("Maximum allowed zoom level (larger value = further zoom)")]
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
    [Tooltip("Minimum Y position the camera can move to")]
    [SerializeField] private float minY = 2f;

    [ShowIf("useBounds")]
    [Tooltip("Maximum Y position the camera can move to")]
    [SerializeField] private float maxY = 50f;

    [TitleGroup("Debug")]
    [ReadOnly]
    [SerializeField] private bool isRightMouseDown = false;

    [ReadOnly]
    [SerializeField] private Vector3 lastMousePosition;

    [ReadOnly]
    [SerializeField] private bool isDragging = false;

    // The initial camera position when starting
    private Vector3 initialPosition;

    // The initial orthographic size (for orthographic cameras) or field of view (for perspective cameras)
    private float initialZoom;

    // Reference to the camera component
    private Camera cameraComponent;

    // Is this an orthographic camera?
    private bool isOrthographic;

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
            initialZoom = cameraComponent.orthographicSize;
        }
        else
        {
            initialZoom = cameraComponent.fieldOfView;
        }
    }

    private void Update()
    {
        HandleMouseInput();
        HandleKeyboardInput();
        HandleZoomInput();
    }

    private void HandleMouseInput()
    {
        // Detect right mouse button down
        if (Input.GetMouseButtonDown(1))
        {
            isRightMouseDown = true;
            lastMousePosition = Input.mousePosition;
            isDragging = true;
        }

        // Detect right mouse button up
        if (Input.GetMouseButtonUp(1))
        {
            isRightMouseDown = false;
            isDragging = false;
        }

        // Handle right-click drag for panning in 3D space
        if (isRightMouseDown && isDragging)
        {
            Vector3 currentMousePosition = Input.mousePosition;
            Vector3 mouseDelta = currentMousePosition - lastMousePosition;

            // Only process if there's actual mouse movement
            if (mouseDelta.sqrMagnitude > 0.1f)
            {
                // Calculate movement in camera's local space
                float horizontalMovement = -mouseDelta.x * mousePanSpeed * 0.01f;
                float verticalMovement = -mouseDelta.y * mousePanSpeed * 0.01f;

                // Adjust direction if inverted
                if (invertMousePan)
                {
                    horizontalMovement = -horizontalMovement;
                    verticalMovement = -verticalMovement;
                }

                // Apply movement based on camera's orientation
                Vector3 right = transform.right;
                Vector3 forward = Vector3.Cross(transform.right, Vector3.up);

                // Move camera parallel to the ground plane
                transform.position += right * horizontalMovement + forward * verticalMovement;

                // Store current position for next frame
                lastMousePosition = currentMousePosition;

                // Enforce bounds if enabled
                if (useBounds)
                {
                    EnforceBounds();
                }
            }
        }
    }

    private void HandleKeyboardInput()
    {
        // Get input from arrow keys (or WASD)
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        if (horizontalInput != 0 || verticalInput != 0)
        {
            // Scale movement by time and speed
            float moveSpeed = keyboardMoveSpeed * Time.deltaTime;

            // Create movement vector (x and z axes for horizontal movement)
            Vector3 movement = new Vector3(horizontalInput, 0, verticalInput).normalized * moveSpeed;

            // Transform the movement direction to be relative to the camera's orientation
            // This assumes the camera is looking down at an angle (not directly top-down)
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) // If camera is looking straight up/down
            {
                forward = transform.up;
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            // Apply movement using the camera's local orientation
            Vector3 moveDirection = right * movement.x + forward * movement.z;
            transform.position += moveDirection;

            // Enforce bounds if enabled
            if (useBounds)
            {
                EnforceBounds();
            }
        }
    }

    private void HandleZoomInput()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        if (scrollInput != 0)
        {
            // Adjust zoom direction if inverted
            if (invertZoom)
            {
                scrollInput = -scrollInput;
            }

            // Apply zoom based on camera type
            if (isOrthographic)
            {
                // For orthographic cameras, adjust orthographic size
                cameraComponent.orthographicSize = Mathf.Clamp(
                    cameraComponent.orthographicSize - scrollInput * zoomSpeed,
                    minZoom,
                    maxZoom
                );
            }
            else
            {
                // For perspective cameras, we'll zoom by moving the camera forward/backward
                Vector3 zoomDirection = transform.forward * scrollInput * zoomSpeed;
                transform.position += zoomDirection;

                // Enforce bounds after zooming
                if (useBounds)
                {
                    EnforceBounds();
                }
            }
        }
    }

    private void EnforceBounds()
    {
        // Get current position
        Vector3 pos = transform.position;

        // Clamp position to bounds on all three axes
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);

        // Apply clamped position
        transform.position = pos;
    }

    [Button("Reset Camera")]
    public void ResetCamera()
    {
        // Reset position to initial
        transform.position = initialPosition;

        // Reset zoom based on camera type
        if (isOrthographic)
        {
            cameraComponent.orthographicSize = initialZoom;
        }
        else
        {
            cameraComponent.fieldOfView = initialZoom;
        }
    }
}