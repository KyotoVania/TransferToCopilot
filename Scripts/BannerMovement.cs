using UnityEngine;
using System.Collections;

public class BannerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Hauteur finale de la bannière au-dessus du point de référence du bâtiment.")]
    [SerializeField] private float finalHeightOffset = 1f;

    [Header("Rhythmic Movement")]
    [Tooltip("Activer le balancement rythmique.")]
    [SerializeField] private bool enableRhythmicMovement = true;
    [Tooltip("Amplitude du balancement en position (unités du monde) si useRotationSway est faux.")]
    [SerializeField] private float swayAmount = 0.1f;
    [Tooltip("Vitesse à laquelle l'animation de balancement progresse entre les battements.")]
    [SerializeField] private float swayTransitionSpeed = 2.0f;

    [Header("Sway Type")]
    [Tooltip("Utiliser un balancement en rotation plutôt qu'en position.")]
    [SerializeField] private bool useRotationSway = true;
    [Tooltip("Angle maximal du balancement en degrés si useRotationSway est vrai.")]
    [SerializeField] private float rotationSwayAmount = 10f;
    [Tooltip("Hauteur de la bannière (pour le calcul du pivot si useRotationSway est vrai).")]
    [SerializeField] private float bannerHeight = 1.0f;
    [Tooltip("Décalage du pivot pour la rotation (0 = base, 0.5 = centre, 1 = sommet).")]
    [SerializeField] [Range(0f, 1f)] private float pivotOffsetRatio = 0f;

    [Header("Camera Facing")]
    [SerializeField] private bool shouldFaceCamera = true;

    // État interne
    private Vector3 baseWorldPosition;
    private Quaternion baseWorldRotation;

    private Camera mainCamera;

    private float currentSwayTargetOffset;
    private float currentSwayActualOffset;

    private float currentSwayTargetRotation;
    private float currentSwayActualRotation;

    private bool isInitialized = false;
    private int swayDirection = 1;

    public float FinalHeightOffset => finalHeightOffset;

    void Awake()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[BannerMovement] Caméra principale non trouvée !", this);
            enabled = false;
            return;
        }
    }

    void OnEnable()
    {
        // --- CORRECTION : Utilisation de l'instance pour s'abonner ---
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += OnBeat;
        }
        else
        {
            Debug.LogWarning("[BannerMovement] MusicManager.Instance non trouvé lors de OnEnable. Le balancement rythmique pourrait ne pas fonctionner.", this);
        }

        currentSwayTargetOffset = 0;
        currentSwayActualOffset = 0;
        currentSwayTargetRotation = 0;
        currentSwayActualRotation = 0;
        swayDirection = 1;

        if (!isInitialized && baseWorldPosition != Vector3.zero)
        {
            InitializeTransform();
        }
    }

    void OnDisable()
    {
        // --- CORRECTION : Utilisation de l'instance pour se désabonner ---
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= OnBeat;
        }
    }

    public void UpdatePosition(Vector3 newBaseWorldPosition)
    {
        baseWorldPosition = newBaseWorldPosition;
        if (isActiveAndEnabled)
        {
            InitializeTransform();
        }
    }

    private void InitializeTransform()
    {
        transform.position = baseWorldPosition;
        if (shouldFaceCamera)
        {
            FaceCamera();
        }
        baseWorldRotation = transform.rotation;
        isInitialized = true;
    }

    private void OnBeat(float beatDuration)
    {
        if (!enableRhythmicMovement || !isInitialized || !isActiveAndEnabled) return;

        swayDirection *= -1;
        if (useRotationSway)
        {
            currentSwayTargetRotation = rotationSwayAmount * swayDirection;
        }
        else
        {
            currentSwayTargetOffset = swayAmount * swayDirection;
        }
    }

    void Update()
    {
        if (!isInitialized || !enableRhythmicMovement || !isActiveAndEnabled)
        {
            if (baseWorldPosition != Vector3.zero)
            {
                transform.position = baseWorldPosition;
                 if (shouldFaceCamera)
                {
                    FaceCamera();
                }
            }
            return;
        }

        ApplySwaying();
    }

    private void ApplySwaying()
    {
        if (shouldFaceCamera)
        {
            FaceCamera();
            if (useRotationSway) {
                baseWorldRotation = transform.rotation;
            }
        }

        if (useRotationSway)
        {
            currentSwayActualRotation = Mathf.Lerp(currentSwayActualRotation, currentSwayTargetRotation, Time.deltaTime * swayTransitionSpeed);
            float pivotYOffset = bannerHeight * (pivotOffsetRatio - 0.5f);
            Vector3 pivotPoint = baseWorldPosition + transform.up * pivotYOffset;
            Quaternion sway = Quaternion.AngleAxis(currentSwayActualRotation, transform.right);
            transform.rotation = baseWorldRotation * sway;
            transform.position = baseWorldPosition;
            transform.RotateAround(pivotPoint, transform.right, currentSwayActualRotation);
        }
        else
        {
            currentSwayActualOffset = Mathf.Lerp(currentSwayActualOffset, currentSwayTargetOffset, Time.deltaTime * swayTransitionSpeed);
            Vector3 swayOffsetVector = transform.right * currentSwayActualOffset;
            transform.position = baseWorldPosition + swayOffsetVector;
        }
    }

    private void FaceCamera()
    {
        if (mainCamera == null) return;

        Vector3 lookDirection = mainCamera.transform.position - transform.position;
        if (!useRotationSway)
        {
            lookDirection.y = 0;
        }

        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !isInitialized) return;

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(baseWorldPosition, 0.1f);
        Gizmos.DrawLine(baseWorldPosition, transform.position);

        if (useRotationSway)
        {
            float pivotYOffset = bannerHeight * (pivotOffsetRatio - 0.5f);
            Vector3 pivotPointPreview = baseWorldPosition + transform.up * pivotYOffset;
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(pivotPointPreview, 0.05f);
            Gizmos.DrawLine(baseWorldPosition, pivotPointPreview);

            Quaternion leftSwayPreview = Quaternion.AngleAxis(-rotationSwayAmount, transform.right);
            Quaternion rightSwayPreview = Quaternion.AngleAxis(rotationSwayAmount, transform.right);
            Vector3 topOfBannerRelative = Vector3.up * bannerHeight * (1f - pivotOffsetRatio);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(pivotPointPreview, pivotPointPreview + (baseWorldRotation * leftSwayPreview * topOfBannerRelative));
            Gizmos.DrawLine(pivotPointPreview, pivotPointPreview + (baseWorldRotation * rightSwayPreview * topOfBannerRelative));
        }
        else
        {
            Gizmos.color = Color.green;
            Vector3 leftExtent = baseWorldPosition - (transform.right * swayAmount);
            Vector3 rightExtent = baseWorldPosition + (transform.right * swayAmount);
            Gizmos.DrawLine(leftExtent, rightExtent);
            Gizmos.DrawSphere(leftExtent, 0.05f);
            Gizmos.DrawSphere(rightExtent, 0.05f);
        }
    }
}