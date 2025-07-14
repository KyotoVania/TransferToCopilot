using UnityEngine;
using System.Collections;

/// <summary>
/// Manages the movement and visual behavior of a banner.
/// The banner can be attached to a moving unit or placed at a fixed position in the world.
/// It features a rhythmic swaying motion synchronized with the MusicManager.
/// </summary>
public class BannerMovement : MonoBehaviour
{
    /// <summary>
    /// Reference to the unit this banner is attached to. Null if the banner is static.
    /// </summary>
    private Unit attachedUnit;

    [Header("Movement Settings")]
    /// <summary>
    /// Defines the final height of the banner above its reference point (the unit's base or its world position).
    /// Configurable from the Unity Inspector.
    /// </summary>
    [Tooltip("Final height of the banner above its reference point.")]
    [SerializeField] private float finalHeightOffset = 4f;

    [Header("Rhythmic Movement")]
    /// <summary>
    /// Enables or disables the rhythmic swaying motion.
    /// If true, the banner will sway in time with the beats detected by the MusicManager.
    /// </summary>
    [SerializeField] private bool enableRhythmicMovement = true;
    
    /// <summary>
    /// The amplitude of the swaying motion when `useRotationSway` is false.
    /// </summary>
    [SerializeField] private float swayAmount = 0.1f;

    /// <summary>
    /// The speed at which the banner reaches its sway target. A higher value results in a sharper movement.
    /// </summary>
    [SerializeField] private float swayTransitionSpeed = 2.0f;

    [Header("Sway Type")]
    /// <summary>
    /// Determines the type of sway. If true, the banner rotates. If false, it moves laterally.
    /// </summary>
    [SerializeField] private bool useRotationSway = true;

    /// <summary>
    /// The maximum angle (in degrees) of the rotational sway.
    /// </summary>
    [SerializeField] private float rotationSwayAmount = 10f;

    /// <summary>
    /// The height of the banner, used to calculate the pivot point for a natural rotation.
    /// </summary>
    [SerializeField] private float bannerHeight = 2.0f;

    /// <summary>
    /// Defines the pivot point for the rotation as a ratio of the banner's height (0 = base, 0.5 = center, 1 = top).
    /// </summary>
    [SerializeField] [Range(0f, 1f)] private float pivotOffsetRatio = 0f;

    [Header("Camera Facing")]
    /// <summary>
    /// If true, the banner will orient itself to face the main camera.
    /// </summary>
    [SerializeField] private bool shouldFaceCamera = true;

    // --- Internal State ---
    /// <summary>
    /// Cached reference to the main camera for orientation calculations.
    /// </summary>
    private Camera mainCamera;
    
    /// <summary>
    /// The base world position from which movement calculations (like swaying) are performed.
    /// </summary>
    private Vector3 baseWorldPosition;

    /// <summary>
    /// The base rotation of the banner, usually oriented towards the camera.
    /// </summary>
    private Quaternion baseWorldRotation;

    /// <summary>
    /// The target sway value (angle or position) for the current beat.
    /// </summary>
    private float currentSwayTarget;

    /// <summary>
    /// The current sway value, interpolated towards `currentSwayTarget` for smooth movement.
    /// </summary>
    private float currentSwayActual;

    /// <summary>
    /// The direction of the sway (1 for right/forward, -1 for left/backward).
    /// </summary>
    private int swayDirection = 1;

    /// <summary>
    /// A flag to ensure the banner has been properly initialized before applying movements.
    /// </summary>
    private bool isInitialized = false;

    /// <summary>
    /// Publicly exposes the `finalHeightOffset` value.
    /// </summary>
    public float FinalHeightOffset => finalHeightOffset;

    #region Public Setup Methods

    /// <summary>
    /// Attaches the banner to a specific unit, making it follow its movements.
    /// </summary>
    /// <param name="unit">The unit to which the banner should be attached.</param>
    public void AttachToUnit(Unit unit)
    {
        if (unit == null)
        {
            Destroy(gameObject);
            return;
        }

        // If already attached, unsubscribe from the old unit's death event.
        if (attachedUnit != null)
        {
            attachedUnit.OnUnitDestroyed -= HandleAttachedUnitDeath;
        }

        this.attachedUnit = unit;
        transform.SetParent(unit.transform, false); // 'false' to keep the local position/rotation at zero.

        // Subscribe to the new unit's death event to be destroyed cleanly.
        unit.OnUnitDestroyed += HandleAttachedUnitDeath;

        InitializeTransform(unit.transform.position + new Vector3(0, finalHeightOffset, 0));
    }

    /// <summary>
    /// Places the banner at a static position in the world.
    /// If the banner was previously attached to a unit, it is detached.
    /// </summary>
    /// <param name="worldPosition">The world position where the banner should be placed.</param>
    public void PlaceAtWorldPosition(Vector3 worldPosition)
    {
        if (attachedUnit != null)
        {
            attachedUnit.OnUnitDestroyed -= HandleAttachedUnitDeath;
            attachedUnit = null;
            transform.SetParent(null);
        }

        InitializeTransform(worldPosition);
    }

    /// <summary>
    /// Alias for `PlaceAtWorldPosition` to ensure compatibility with other scripts.
    /// </summary>
    /// <param name="newBaseWorldPosition">The new base position for the banner.</param>
    public void UpdatePosition(Vector3 newBaseWorldPosition)
    {
        PlaceAtWorldPosition(newBaseWorldPosition);
    }

    #endregion

    #region Unity Lifecycle & Callbacks

    /// <summary>
    /// Unity lifecycle method. Called before the first frame.
    /// Initializes the reference to the main camera.
    /// </summary>
    void Awake()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[BannerMovement] Main camera not found!", this);
            enabled = false;
        }
    }

    /// <summary>
    /// Unity lifecycle method. Called when the object becomes active.
    /// Subscribes to the OnBeat event of the MusicManager.
    /// </summary>
    void OnEnable()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += OnBeat;
        }

        // Reset the sway state each time it's enabled.
        currentSwayTarget = 0;
        currentSwayActual = 0;
        swayDirection = 1;
    }

    /// <summary>
    /// Unity lifecycle method. Called when the object becomes inactive.
    /// Unsubscribes from events to prevent memory leaks.
    /// </summary>
    void OnDisable()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= OnBeat;
        }
        
        if (attachedUnit != null)
        {
            attachedUnit.OnUnitDestroyed -= HandleAttachedUnitDeath;
        }
    }

    /// <summary>
    /// Unity lifecycle method. Called every frame, after all Update methods.
    /// Ideal for camera and tracking movements to avoid jitter.
    /// </summary>
    void LateUpdate()
    {
        // If attached to a unit, continuously update the base position.
        if (attachedUnit != null)
        {
            baseWorldPosition = attachedUnit.transform.position + new Vector3(0, finalHeightOffset, 0);
        }

        if (!isInitialized) return;

        if (enableRhythmicMovement)
        {
            ApplySwaying();
        }
    }

    /// <summary>
    /// Method called by the MusicManager on each music beat.
    /// </summary>
    /// <param name="beatDuration">The duration of the beat (not used here, but required by the event signature).</param>
    private void OnBeat(float beatDuration)
    {
        if (!enableRhythmicMovement || !isInitialized) return;

        // Invert the sway direction on each beat.
        swayDirection *= -1; 
        currentSwayTarget = useRotationSway ? (rotationSwayAmount * swayDirection) : (swayAmount * swayDirection);
    }

    /// <summary>
    /// Handles the destruction of the attached unit.
    /// </summary>
    private void HandleAttachedUnitDeath()
    {
        if (this == null) return;
        transform.SetParent(null); // Detach from the parent unit.
        Destroy(gameObject, 0.2f); // Destroy the banner after a short delay.
    }

    #endregion

    #region Private Logic

    /// <summary>
    /// Initializes the position, rotation, and state of the banner.
    /// </summary>
    /// <param name="initialPosition">The initial world position for the banner.</param>
    private void InitializeTransform(Vector3 initialPosition)
    {
        baseWorldPosition = initialPosition;
        transform.position = initialPosition;

        if (shouldFaceCamera)
        {
            FaceCamera();
        }
        baseWorldRotation = transform.rotation;
        isInitialized = true;
    }

    /// <summary>
    /// Applies the swaying motion (rotation or position) to the banner.
    /// </summary>
    private void ApplySwaying()
    {
        // Interpolate the current sway value towards the target for smooth movement.
        currentSwayActual = Mathf.Lerp(currentSwayActual, currentSwayTarget, Time.deltaTime * swayTransitionSpeed);

        // For static banners, refresh the orientation towards the camera if needed.
        if (shouldFaceCamera && attachedUnit == null) 
        {
            FaceCamera();
            baseWorldRotation = transform.rotation;
        }

        if (useRotationSway)
        {
            // Calculate the pivot point for a natural-looking rotation.
            float pivotYWorldOffset = bannerHeight * (pivotOffsetRatio - 0.5f);
            Vector3 pivotPoint = baseWorldPosition + transform.up * pivotYWorldOffset;

            // Apply the base rotation (face camera) and then the sway rotation.
            transform.rotation = baseWorldRotation * Quaternion.AngleAxis(currentSwayActual, transform.right);

            // Adjust the position to simulate the rotation around the pivot point.
            // This is crucial so that the base of the banner doesn't move if the pivot is at the base.
            transform.position = baseWorldPosition;
            transform.RotateAround(pivotPoint, transform.right, currentSwayActual);
        }
        else
        {
            // Apply a simple lateral position sway.
            Vector3 swayOffsetVector = transform.right * currentSwayActual;
            transform.position = baseWorldPosition + swayOffsetVector;
        }
    }

    /// <summary>
    /// Orients the banner to face the main camera.
    /// </summary>
    private void FaceCamera()
    {
        if (mainCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
        }
    }

    #endregion

    #region Editor Gizmos

    /// <summary>
    /// Draws visual aids in the Unity editor to facilitate configuration.
    /// Displays the base point, pivot, and movement range.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying && !isInitialized) return;

        Vector3 positionToDrawFrom = Application.isPlaying ? baseWorldPosition : transform.position;
        Quaternion rotationToDrawFrom = Application.isPlaying ? baseWorldRotation : transform.rotation;

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(positionToDrawFrom, 0.1f);
        Gizmos.DrawLine(positionToDrawFrom, transform.position);

        if (useRotationSway)
        {
            // Draw the pivot and the rotation arc.
            float pivotYOffset = bannerHeight * (pivotOffsetRatio - 0.5f);
            Vector3 pivotPointPreview = positionToDrawFrom + transform.up * pivotYOffset;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(pivotPointPreview, 0.1f);
            Gizmos.DrawLine(positionToDrawFrom, pivotPointPreview);

            Vector3 topOfBannerRelative = Vector3.up * bannerHeight * (1f - pivotOffsetRatio);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(pivotPointPreview, pivotPointPreview + (rotationToDrawFrom * Quaternion.AngleAxis(-rotationSwayAmount, Vector3.right) * topOfBannerRelative));
            Gizmos.DrawLine(pivotPointPreview, pivotPointPreview + (rotationToDrawFrom * Quaternion.AngleAxis(rotationSwayAmount, Vector3.right) * topOfBannerRelative));
        }
        else
        {
            // Draw the line for the lateral sway movement.
            Gizmos.color = Color.green;
            Vector3 leftExtent = positionToDrawFrom - (transform.right * swayAmount);
            Vector3 rightExtent = positionToDrawFrom + (transform.right * swayAmount);
            Gizmos.DrawLine(leftExtent, rightExtent);
        }
    }

    #endregion
}