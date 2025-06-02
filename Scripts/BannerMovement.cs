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
    [SerializeField] private float swayTransitionSpeed = 2.0f; // Vitesse de transition vers la nouvelle cible de balancement

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
    private Vector3 baseWorldPosition;        // La position "stable" autour de laquelle la bannière se balance, mise à jour par UpdatePosition.
    private Quaternion baseWorldRotation;     // La rotation "stable" (généralement face à la caméra) autour de laquelle la bannière se balance.

    private Camera mainCamera;
    private RhythmManager rhythmManager;

    private float currentSwayTargetOffset; // Pour le balancement en position : -swayAmount, 0, ou swayAmount
    private float currentSwayActualOffset; // L'offset de position actuel, lissé vers currentSwayTargetOffset

    private float currentSwayTargetRotation; // Pour le balancement en rotation : -rotationSwayAmount, 0, ou rotationSwayAmount
    private float currentSwayActualRotation; // L'angle de rotation actuel, lissé vers currentSwayTargetRotation

    private bool isInitialized = false;
    private int swayDirection = 1; // 1 pour droite/horaire, -1 pour gauche/anti-horaire

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
        // S'abonner à l'événement de rythme si ce n'est pas déjà fait
        if (RhythmManager.Instance != null)
        {
            RhythmManager.OnBeat += OnBeat;
        }
        else
        {
            Debug.LogWarning("[BannerMovement] RhythmManager.Instance non trouvé lors de OnEnable. Le balancement rythmique pourrait ne pas fonctionner.", this);
        }

        // Initialiser les valeurs de balancement
        // On commence avec un offset cible pour aller vers la première direction au premier battement.
        // Si on veut que ça commence déjà balancé, on peut initialiser currentSwayActual... différemment.
        currentSwayTargetOffset = 0;
        currentSwayActualOffset = 0;
        currentSwayTargetRotation = 0;
        currentSwayActualRotation = 0;
        swayDirection = 1; // Commencer par aller vers la "droite" (ou angle positif)

        // Si la position a déjà été définie par UpdatePosition avant OnEnable (peu probable mais possible)
        // et que isInitialized n'est pas encore vrai, on initialise.
        if (!isInitialized && baseWorldPosition != Vector3.zero) // baseWorldPosition est mis à jour dans UpdatePosition
        {
            InitializeTransform();
        }
    }

    void OnDisable()
    {
        if (RhythmManager.Instance != null)
        {
            RhythmManager.OnBeat -= OnBeat;
        }
    }

    /// <summary>
    /// Méthode publique appelée par MouseManager pour définir la position de base de la bannière.
    /// C'est autour de cette position que le balancement s'effectuera.
    /// </summary>
    public void UpdatePosition(Vector3 newBaseWorldPosition)
    {
        baseWorldPosition = newBaseWorldPosition;
        // Debug.Log($"[BannerMovement] UpdatePosition appelée avec: {newBaseWorldPosition}", this);

        // Si le script est déjà actif et initialisé, on met à jour directement.
        // Sinon, l'initialisation se fera dans OnEnable ou Start.
        if (isActiveAndEnabled)
        {
            InitializeTransform();
        }
    }

    /// <summary>
    /// Initialise ou réinitialise la transformation de la bannière
    /// à sa position et rotation de base.
    /// </summary>
    private void InitializeTransform()
    {
        transform.position = baseWorldPosition;
        if (shouldFaceCamera)
        {
            FaceCamera();
        }
        baseWorldRotation = transform.rotation; // Stocker la rotation après avoir fait face à la caméra
        isInitialized = true;
        // Debug.Log($"[BannerMovement] Transform initialisé. baseWorldPosition: {baseWorldPosition}, baseWorldRotation: {baseWorldRotation.eulerAngles}", this);
    }

    private void OnBeat()
    {
        if (!enableRhythmicMovement || !isInitialized || !isActiveAndEnabled) return;

        // Inverse la direction du balancement et définit la nouvelle cible d'offset/rotation
        swayDirection *= -1;
        if (useRotationSway)
        {
            currentSwayTargetRotation = rotationSwayAmount * swayDirection;
            // Debug.Log($"[BannerMovement] OnBeat! Nouvelle cible de rotation: {currentSwayTargetRotation} deg (direction: {swayDirection})", this);
        }
        else
        {
            currentSwayTargetOffset = swayAmount * swayDirection;
            // Debug.Log($"[BannerMovement] OnBeat! Nouvelle cible d'offset: {currentSwayTargetOffset} (direction: {swayDirection})", this);
        }
    }

    void Update()
    {
        if (!isInitialized || !enableRhythmicMovement || !isActiveAndEnabled)
        {
            // Si le mouvement rythmique n'est pas activé ou si on n'est pas initialisé,
            // s'assurer au moins que la bannière est à sa position de base et face à la caméra.
            if (baseWorldPosition != Vector3.zero) // S'assurer que UpdatePosition a été appelée au moins une fois
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
        // Orienter vers la caméra d'abord (seulement l'axe Y pour ne pas écraser le balancement en rotation)
        if (shouldFaceCamera)
        {
            FaceCamera(); // Met à jour transform.rotation
            if (useRotationSway) {
                baseWorldRotation = transform.rotation; // Réassigner baseWorldRotation si on veut que le "forward" du balancement soit toujours relatif à la caméra
            }
        }


        if (useRotationSway)
        {
            // Lisser la rotation actuelle vers la rotation cible
            currentSwayActualRotation = Mathf.Lerp(currentSwayActualRotation, currentSwayTargetRotation, Time.deltaTime * swayTransitionSpeed);

            // Calculer le point de pivot
            // pivotOffsetRatio: 0 pour la base, 0.5 pour le centre, 1 pour le haut de la bannière.
            float pivotYOffset = bannerHeight * (pivotOffsetRatio - 0.5f); // Négatif si pivot en bas, positif si en haut
            Vector3 pivotPoint = baseWorldPosition + transform.up * pivotYOffset; // Utiliser transform.up pour le décalage local du pivot

            // Appliquer la rotation de balancement
            // On combine la rotation de base (face caméra) avec la rotation de balancement.
            // Le balancement se fait sur l'axe "right" de la bannière après qu'elle se soit orientée vers la caméra.
            Quaternion sway = Quaternion.AngleAxis(currentSwayActualRotation, transform.right);
            transform.rotation = baseWorldRotation * sway;

            // Ajuster la position pour que la bannière pivote correctement autour du pivotPoint désiré
            transform.position = baseWorldPosition; // D'abord, réinitialiser à la position de base
            transform.RotateAround(pivotPoint, transform.right, currentSwayActualRotation); // Puis pivoter
        }
        else
        {
            // Balancement en position
            currentSwayActualOffset = Mathf.Lerp(currentSwayActualOffset, currentSwayTargetOffset, Time.deltaTime * swayTransitionSpeed);

            // Le balancement se fait sur l'axe "right" de la bannière (perpendiculaire à sa direction et à l'axe Y mondial)
            Vector3 swayOffsetVector = transform.right * currentSwayActualOffset;
            transform.position = baseWorldPosition + swayOffsetVector;
        }
    }

    private void FaceCamera()
    {
        if (mainCamera == null) return;

        Vector3 lookDirection = mainCamera.transform.position - transform.position;
        if (!useRotationSway) // Si balancement en position, on ne veut pas que le "regard" soit affecté par l'offset Y du sway.
        {
            lookDirection.y = 0; // Aplatir la direction pour que la bannière reste verticale
        }


        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            // Optionnel: Lisser la rotation vers la caméra pour éviter les changements brusques
            // transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            transform.rotation = targetRotation;
        }
    }

    // Méthode OnDrawGizmosSelected pour le débogage (optionnelle)
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !isInitialized) return;

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(baseWorldPosition, 0.1f);
        Gizmos.DrawLine(baseWorldPosition, transform.position);

        if (useRotationSway)
        {
            float pivotYOffset = bannerHeight * (pivotOffsetRatio - 0.5f);
            Vector3 pivotPointPreview = baseWorldPosition + transform.up * pivotYOffset; // Utiliser transform.up pour la direction locale
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(pivotPointPreview, 0.05f);
            Gizmos.DrawLine(baseWorldPosition, pivotPointPreview);

            // Visualiser l'amplitude de la rotation
            Quaternion leftSwayPreview = Quaternion.AngleAxis(-rotationSwayAmount, transform.right);
            Quaternion rightSwayPreview = Quaternion.AngleAxis(rotationSwayAmount, transform.right);
            Vector3 topOfBannerRelative = Vector3.up * bannerHeight * (1f - pivotOffsetRatio); // Position du haut par rapport au pivot

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