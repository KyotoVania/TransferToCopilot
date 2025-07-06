using UnityEngine;
using System.Collections;

public class BannerMovement : MonoBehaviour
{
    // --- FEATURE FUSIONNÉE: Garde la référence à l'unité attachée ---
    private Unit attachedUnit;

    [Header("Movement Settings")]
    [Tooltip("Hauteur finale de la bannière au-dessus de son point de référence.")]
    [SerializeField] private float finalHeightOffset = 4f;

    [Header("Rhythmic Movement")]
    [SerializeField] private bool enableRhythmicMovement = true;
    [SerializeField] private float swayAmount = 0.1f;
    [SerializeField] private float swayTransitionSpeed = 2.0f;

    [Header("Sway Type")]
    [SerializeField] private bool useRotationSway = true;
    [SerializeField] private float rotationSwayAmount = 10f;
    [SerializeField] private float bannerHeight = 2.0f;
    [SerializeField] [Range(0f, 1f)] private float pivotOffsetRatio = 0f;

    [Header("Camera Facing")]
    [SerializeField] private bool shouldFaceCamera = true;

    // --- État Interne (fusionné) ---
    private Camera mainCamera;
    private Vector3 baseWorldPosition;
    private Quaternion baseWorldRotation;
    private float currentSwayTarget;
    private float currentSwayActual;
    private int swayDirection = 1;
    private bool isInitialized = false;

    // --- Propriété publique pour l'accès externe ---
    public float FinalHeightOffset => finalHeightOffset;

    #region Public Setup Methods

    /// <summary>
    /// FEATURE DU FICHIER 1: Attache la bannière à une unité en mouvement.
    /// </summary>
    public void AttachToUnit(Unit unit)
    {
        if (unit == null)
        {
            Destroy(gameObject);
            return;
        }

        // Si on était attaché à une autre unité, on se désabonne d'abord
        if (attachedUnit != null)
        {
            attachedUnit.OnUnitDestroyed -= HandleAttachedUnitDeath;
        }

        this.attachedUnit = unit;
        transform.SetParent(unit.transform, false); // 'false' pour réinitialiser la position/rotation locale

        // S'abonner à la mort de la nouvelle unité
        unit.OnUnitDestroyed += HandleAttachedUnitDeath;

        // Initialiser la position et la rotation
        InitializeTransform(unit.transform.position + new Vector3(0, finalHeightOffset, 0));
    }

    /// <summary>
    /// FEATURE DU FICHIER 2: Place la bannière à une position fixe dans le monde.
    /// </summary>
    public void PlaceAtWorldPosition(Vector3 worldPosition)
    {
        // Si on était attaché à une unité, on se détache et on se désabonne
        if (attachedUnit != null)
        {
            attachedUnit.OnUnitDestroyed -= HandleAttachedUnitDeath;
            attachedUnit = null;
            transform.SetParent(null);
        }

        InitializeTransform(worldPosition);
    }

    // Alias pour la compatibilité avec le BannerController existant
    public void UpdatePosition(Vector3 newBaseWorldPosition)
    {
        PlaceAtWorldPosition(newBaseWorldPosition);
    }

    #endregion

    #region Unity Lifecycle & Callbacks

    void Awake()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[BannerMovement] Caméra principale non trouvée !", this);
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += OnBeat;
        }

        // Réinitialiser l'état du balancement
        currentSwayTarget = 0;
        currentSwayActual = 0;
        swayDirection = 1;
    }

    void OnDisable()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= OnBeat;
        }
        // Sécurité pour se désabonner si l'objet est désactivé
        if (attachedUnit != null)
        {
            attachedUnit.OnUnitDestroyed -= HandleAttachedUnitDeath;
        }
    }

    // LateUpdate est meilleur pour les suivis de position pour éviter les saccades
    void LateUpdate()
    {
        // FEATURE FUSIONNÉE: Met à jour la position de base si attaché à une unité
        if (attachedUnit != null)
        {
            // La position de base pour le balancement est mise à jour en permanence
            baseWorldPosition = attachedUnit.transform.position + new Vector3(0, finalHeightOffset, 0);
        }

        if (!isInitialized) return;

        if (enableRhythmicMovement)
        {
            ApplySwaying();
        }
    }

    private void OnBeat(float beatDuration)
    {
        if (!enableRhythmicMovement || !isInitialized) return;

        swayDirection *= -1; // Inverser la direction du balancement
        currentSwayTarget = useRotationSway ? (rotationSwayAmount * swayDirection) : (swayAmount * swayDirection);
    }

    private void HandleAttachedUnitDeath()
    {
        if (this == null) return;
        transform.SetParent(null);
        // On peut ajouter un petit effet avant de détruire
        Destroy(gameObject, 0.2f);
    }

    #endregion

    #region Private Logic

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
    /// FEATURE DU FICHIER 2: Applique le balancement avancé basé sur un pivot.
    /// </summary>
    private void ApplySwaying()
    {
        currentSwayActual = Mathf.Lerp(currentSwayActual, currentSwayTarget, Time.deltaTime * swayTransitionSpeed);

        if (shouldFaceCamera && attachedUnit == null) // Pour les bannières statiques, on rafraîchit l'orientation
        {
            FaceCamera();
            baseWorldRotation = transform.rotation;
        }

        if (useRotationSway)
        {
            // Calcul du pivot pour une rotation naturelle
            float pivotYWorldOffset = bannerHeight * (pivotOffsetRatio - 0.5f);
            Vector3 pivotPoint = baseWorldPosition + transform.up * pivotYWorldOffset;

            // Appliquer la rotation de base (vers la caméra) et la rotation de balancement
            transform.rotation = baseWorldRotation * Quaternion.AngleAxis(currentSwayActual, transform.right);

            // Ajuster la position pour simuler la rotation autour du pivot
            transform.position = baseWorldPosition;
            transform.RotateAround(pivotPoint, transform.right, currentSwayActual);
        }
        else
        {
            // Balancement simple en position
            Vector3 swayOffsetVector = transform.right * currentSwayActual;
            transform.position = baseWorldPosition + swayOffsetVector;
        }
    }

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
    /// FEATURE DU FICHIER 2: Dessine des aides visuelles dans l'éditeur.
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
            Gizmos.color = Color.green;
            Vector3 leftExtent = positionToDrawFrom - (transform.right * swayAmount);
            Vector3 rightExtent = positionToDrawFrom + (transform.right * swayAmount);
            Gizmos.DrawLine(leftExtent, rightExtent);
        }
    }

    #endregion
}