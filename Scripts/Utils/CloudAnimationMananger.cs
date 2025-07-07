using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gestionnaire centralisé pour animer efficacement des centaines de nuages.
/// Utilise une seule boucle Update pour contrôler tous les nuages, maximisant les performances.
/// </summary>
public class CloudAnimationManager : MonoBehaviour
{
    public static CloudAnimationManager Instance { get; private set; }

    [Header("Animation Settings")]
    [Tooltip("Vitesse de rotation sur l'axe Y en degrés par seconde")]
    [SerializeField] private float rotationSpeed = 5f;
    
    [Tooltip("Vitesse du mouvement de flottement")]
    [SerializeField] private float bobbingSpeed = 1f;
    
    [Tooltip("Amplitude du mouvement de flottement")]
    [SerializeField] private float bobbingHeight = 0.2f;
    
    [Tooltip("Variation de phase entre les nuages (0 = synchronisé, 1 = complètement varié)")]
    [SerializeField, Range(0f, 1f)] private float phaseVariation = 0.8f;
    
    [Tooltip("Variation de vitesse entre les nuages")]
    [SerializeField, Range(0f, 1f)] private float speedVariation = 0.3f;

    [Header("Performance")]
    [Tooltip("Tag utilisé pour identifier les nuages")]
    [SerializeField] private string cloudTag = "Cloud";
    
    [Tooltip("Nombre maximum de nuages à traiter par frame (0 = tous)")]
    [SerializeField] private int cloudsPerFrame = 0;

    // Structures de données optimisées
    private Transform[] cloudTransforms;
    private Vector3[] initialPositions;
    private float[] phaseOffsets;
    private float[] speedMultipliers;
    private float[] rotationMultipliers;
    
    private int cloudCount = 0;
    private int currentCloudIndex = 0;
    
    // Pour le debug
    private float lastUpdateTime;
    private float updateDeltaTime;

    void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        InitializeClouds();
    }

    /// <summary>
    /// Trouve tous les nuages et pré-calcule les valeurs nécessaires
    /// </summary>
    private void InitializeClouds()
    {
        // Trouver tous les GameObjects avec le tag "Cloud"
        GameObject[] cloudObjects = GameObject.FindGameObjectsWithTag(cloudTag);
        cloudCount = cloudObjects.Length;
        
        if (cloudCount == 0)
        {
            Debug.LogWarning($"[CloudAnimationManager] Aucun GameObject avec le tag '{cloudTag}' trouvé!");
            enabled = false;
            return;
        }
        
        // Allouer les tableaux
        cloudTransforms = new Transform[cloudCount];
        initialPositions = new Vector3[cloudCount];
        phaseOffsets = new float[cloudCount];
        speedMultipliers = new float[cloudCount];
        rotationMultipliers = new float[cloudCount];
        
        // Initialiser les données
        for (int i = 0; i < cloudCount; i++)
        {
            cloudTransforms[i] = cloudObjects[i].transform;
            initialPositions[i] = cloudTransforms[i].position;
            
            // Générer des variations uniques pour chaque nuage
            // Utilisation de la position pour un offset pseudo-aléatoire déterministe
            float pseudoRandom = (initialPositions[i].x * 73.1f + initialPositions[i].z * 37.7f) % 1f;
            pseudoRandom = Mathf.Abs(pseudoRandom);
            
            // Phase offset pour le bobbing (évite que tous les nuages bougent en synchrone)
            phaseOffsets[i] = pseudoRandom * Mathf.PI * 2f * phaseVariation;
            
            // Multiplicateurs de vitesse pour varier légèrement les mouvements
            speedMultipliers[i] = 1f + (pseudoRandom - 0.5f) * speedVariation * 2f;
            
            // Variation de la vitesse de rotation
            float rotRandom = (initialPositions[i].y * 13.7f + i * 7.3f) % 1f;
            rotationMultipliers[i] = 1f + (Mathf.Abs(rotRandom) - 0.5f) * speedVariation * 2f;
        }
        
        Debug.Log($"[CloudAnimationManager] Initialisé avec {cloudCount} nuages");
    }

    void Update()
    {
        if (cloudCount == 0) return;
        
        // Mesure du temps pour le debug (peut être retiré en production)
        #if UNITY_EDITOR
        float startTime = Time.realtimeSinceStartup;
        #endif
        
        float currentTime = Time.time;
        float deltaTime = Time.deltaTime;
        
        // Déterminer combien de nuages traiter ce frame
        int cloudsToProcess = cloudsPerFrame > 0 ? Mathf.Min(cloudsPerFrame, cloudCount) : cloudCount;
        
        // Traitement par lots si configuré
        if (cloudsPerFrame > 0)
        {
            // Traiter un sous-ensemble de nuages chaque frame
            for (int i = 0; i < cloudsToProcess; i++)
            {
                int index = (currentCloudIndex + i) % cloudCount;
                AnimateCloud(index, currentTime, deltaTime);
            }
            currentCloudIndex = (currentCloudIndex + cloudsToProcess) % cloudCount;
        }
        else
        {
            // Traiter tous les nuages d'un coup
            for (int i = 0; i < cloudCount; i++)
            {
                AnimateCloud(i, currentTime, deltaTime);
            }
        }
        
        // Mesure du temps pour le debug
        #if UNITY_EDITOR
        updateDeltaTime = (Time.realtimeSinceStartup - startTime) * 1000f;
        lastUpdateTime = Time.time;
        #endif
    }

    /// <summary>
    /// Anime un nuage individuel (appelé depuis la boucle principale)
    /// </summary>
    private void AnimateCloud(int index, float currentTime, float deltaTime)
    {
        if (cloudTransforms[index] == null) return;
        
        Transform cloud = cloudTransforms[index];
        
        // Rotation continue sur Y
        float rotationDelta = rotationSpeed * rotationMultipliers[index] * deltaTime;
        cloud.Rotate(0, rotationDelta, 0, Space.World);
        
        // Mouvement de flottement vertical (bobbing)
        float bobbingPhase = currentTime * bobbingSpeed * speedMultipliers[index] + phaseOffsets[index];
        float yOffset = Mathf.Sin(bobbingPhase) * bobbingHeight;
        
        // Appliquer la nouvelle position
        cloud.position = new Vector3(
            initialPositions[index].x,
            initialPositions[index].y + yOffset,
            initialPositions[index].z
        );
    }

    /// <summary>
    /// Permet de mettre à jour dynamiquement les paramètres d'animation
    /// </summary>
    public void SetAnimationParameters(float newRotationSpeed, float newBobbingSpeed, float newBobbingHeight)
    {
        rotationSpeed = newRotationSpeed;
        bobbingSpeed = newBobbingSpeed;
        bobbingHeight = newBobbingHeight;
    }

    /// <summary>
    /// Réinitialise les nuages si nécessaire (après génération de nouveaux nuages par exemple)
    /// </summary>
    public void RefreshCloudList()
    {
        InitializeClouds();
    }

    #if UNITY_EDITOR
    void OnGUI()
    {
        // Affichage optionnel des stats de performance en mode debug
        if (Debug.isDebugBuild && cloudCount > 0)
        {
            GUI.Label(new Rect(10, 10, 300, 20), $"Clouds: {cloudCount} | Update: {updateDeltaTime:F2}ms");
        }
    }
    #endif

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}