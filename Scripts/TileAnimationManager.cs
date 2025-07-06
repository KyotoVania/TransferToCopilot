using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Jobs;

/// <summary>
/// Gestionnaire centralisé pour toutes les animations de tuiles
/// Remplace les coroutines individuelles par un système batch performant
/// </summary>
public class TileAnimationManager : MonoBehaviour
{
    // Singleton pour accès global
    public static TileAnimationManager Instance { get; private set; }
    
    // Structure légère pour stocker l'état d'animation d'une tuile
    [System.Serializable]
    public struct TileAnimationState
    {
        public Transform transform;
        public Vector3 startPosition;
        public Vector3 targetPosition;
        public Vector3 startScale;
        public Vector3 targetScale;
        public float startTime;
        public float duration;
        public AnimationType animationType;
        public float curveProgress; // Pré-calculé pour éviter Evaluate() répété
        public bool isActive;
        
        // Paramètres spécifiques selon le type d'animation
        public float bounceHeight;
        public float phase; // Pour les animations multi-phases
    }
    
    public enum AnimationType
    {
        GroundBounce,
        WaterWave,
        MountainShake,
        EnvironmentBounce
    }
    
    // Pool d'animations actives
    private List<TileAnimationState> activeAnimations = new List<TileAnimationState>(300);
    private Queue<int> freeIndices = new Queue<int>(300);
    
    // Cache pour AnimationCurve.Evaluate()
    private Dictionary<string, float[]> curveCache = new Dictionary<string, float[]>();
    private const int CURVE_CACHE_RESOLUTION = 100; // Points d'échantillonnage
    
    // Paramètres de performance
    [Header("Performance Settings")]
    [SerializeField] private int maxConcurrentAnimations = 500;
    [SerializeField] private bool useJobSystem = true;
    [SerializeField] private bool enableBatching = true;
    
    // Stats de débogage
    private int currentActiveAnimations = 0;
    private float lastUpdateTime = 0f;
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Pré-allouer la capacité
        for (int i = 0; i < maxConcurrentAnimations; i++)
        {
            activeAnimations.Add(new TileAnimationState());
            freeIndices.Enqueue(i);
        }
    }
    
    void Update()
    {
        if (currentActiveAnimations == 0) return;
        
        float currentTime = Time.time;
        float deltaTime = Time.deltaTime;
        
        // Mise à jour batch de toutes les animations
        if (useJobSystem && currentActiveAnimations > 50)
        {
            UpdateAnimationsWithJobs(currentTime);
        }
        else
        {
            UpdateAnimationsSimple(currentTime);
        }
        
        lastUpdateTime = currentTime;
    }
    
    /// <summary>
    /// Ajoute une nouvelle animation de tuile au système
    /// </summary>
    public bool AddTileAnimation(Transform tileTransform, Vector3 targetPos, float duration, 
                                AnimationType type, AnimationCurve curve = null)
    {
        if (freeIndices.Count == 0) 
        {
            Debug.LogWarning("[TileAnimationManager] Limite d'animations atteinte!");
            return false;
        }
        
        int index = freeIndices.Dequeue();
        
        var animState = new TileAnimationState
        {
            transform = tileTransform,
            startPosition = tileTransform.position,
            targetPosition = targetPos,
            startScale = tileTransform.localScale,
            targetScale = tileTransform.localScale, // Par défaut, pas de changement
            startTime = Time.time,
            duration = duration,
            animationType = type,
            isActive = true,
            phase = 0
        };
        
        activeAnimations[index] = animState;
        currentActiveAnimations++;
        
        return true;
    }
    
    /// <summary>
    /// Version optimisée pour les animations de type Ground avec bounce
    /// Accepte n'importe quel Transform au lieu d'un type spécifique de tuile
    /// </summary>
    public bool AddGroundTileAnimation(Transform tileTransform, Vector3 targetPos, 
                                      float duration, float bounceHeight)
    {
        if (freeIndices.Count == 0) return false;
        
        int index = freeIndices.Dequeue();
        
        var animState = new TileAnimationState
        {
            transform = tileTransform,
            startPosition = tileTransform.position,
            targetPosition = targetPos,
            startScale = tileTransform.localScale,
            targetScale = tileTransform.localScale,
            startTime = Time.time,
            duration = duration,
            animationType = AnimationType.GroundBounce,
            bounceHeight = bounceHeight,
            isActive = true,
            phase = 0 // 0: montée, 1: descente, 2: bounce
        };
        
        activeAnimations[index] = animState;
        currentActiveAnimations++;
        
        return true;
    }
    
    /// <summary>
    /// Mise à jour simple sans Job System (pour peu d'animations)
    /// </summary>
    private void UpdateAnimationsSimple(float currentTime)
    {
        for (int i = 0; i < activeAnimations.Count; i++)
        {
            if (!activeAnimations[i].isActive) continue;
            
            var anim = activeAnimations[i];
            float elapsed = currentTime - anim.startTime;
            float progress = Mathf.Clamp01(elapsed / anim.duration);
            
            // Appliquer l'animation selon le type
            switch (anim.animationType)
            {
                case AnimationType.GroundBounce:
                    UpdateGroundBounceAnimation(ref anim, progress);
                    break;
                    
                case AnimationType.WaterWave:
                    UpdateWaterWaveAnimation(ref anim, progress);
                    break;
                    
                case AnimationType.MountainShake:
                    UpdateMountainShakeAnimation(ref anim, progress);
                    break;
            }
            
            // Vérifier si l'animation est terminée
            if (progress >= 1f)
            {
                CompleteAnimation(i);
            }
            else
            {
                activeAnimations[i] = anim; // Remettre à jour la structure
            }
        }
    }
    
    /// <summary>
    /// Animation optimisée pour les tuiles Ground avec bounce
    /// </summary>
    private void UpdateGroundBounceAnimation(ref TileAnimationState anim, float progress)
    {
        // Phase 0-0.8 : Movement principal
        // Phase 0.8-1.0 : Bounce
        
        if (progress < 0.8f)
        {
            // Movement principal avec courbe d'ease
            float moveProgress = progress / 0.8f;
            float easedProgress = EaseInOutCubic(moveProgress);
            
            anim.transform.position = Vector3.Lerp(
                anim.startPosition, 
                anim.targetPosition, 
                easedProgress
            );
        }
        else
        {
            // Phase de bounce
            float bounceProgress = (progress - 0.8f) / 0.2f;
            float bounceEase = Mathf.Sin(bounceProgress * Mathf.PI);
            
            Vector3 bounceOffset = Vector3.up * (anim.bounceHeight * bounceEase);
            anim.transform.position = anim.targetPosition + bounceOffset;
        }
    }
    
    /// <summary>
    /// Animation optimisée pour les tuiles Water
    /// </summary>
    private void UpdateWaterWaveAnimation(ref TileAnimationState anim, float progress)
    {
        // Utiliser une courbe sinusoïdale pour un mouvement fluide
        float sineProgress = Mathf.Sin(progress * Mathf.PI * 0.5f);
        
        anim.transform.position = Vector3.Lerp(
            anim.startPosition,
            anim.targetPosition,
            sineProgress
        );
        
        // Scale animation simultanée
        if (anim.targetScale != anim.startScale)
        {
            anim.transform.localScale = Vector3.Lerp(
                anim.startScale,
                anim.targetScale,
                sineProgress
            );
        }
    }
    
    /// <summary>
    /// Animation de shake pour les montagnes
    /// </summary>
    private void UpdateMountainShakeAnimation(ref TileAnimationState anim, float progress)
    {
        // Shake aléatoire qui diminue avec le temps
        float shakeIntensity = (1f - progress) * 0.02f;
        
        Vector3 shakeOffset = new Vector3(
            Mathf.PerlinNoise(Time.time * 10f, 0f) * shakeIntensity,
            0f,
            Mathf.PerlinNoise(0f, Time.time * 10f) * shakeIntensity
        );
        
        anim.transform.position = anim.startPosition + shakeOffset;
    }
    
    /// <summary>
    /// Termine une animation et libère son slot
    /// </summary>
    private void CompleteAnimation(int index)
    {
        var anim = activeAnimations[index];
        
        // S'assurer que la position finale est exacte
        anim.transform.position = anim.targetPosition;
        if (anim.targetScale != anim.startScale)
        {
            anim.transform.localScale = anim.targetScale;
        }
        
        // Marquer comme inactive et recycler l'index
        anim.isActive = false;
        activeAnimations[index] = anim;
        freeIndices.Enqueue(index);
        currentActiveAnimations--;
    }
    
    /// <summary>
    /// Fonction d'easing optimisée (remplace AnimationCurve.Evaluate)
    /// </summary>
    private float EaseInOutCubic(float t)
    {
        // Formule mathématique directe, beaucoup plus rapide qu'AnimationCurve
        if (t < 0.5f)
            return 4f * t * t * t;
        else
            return 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }
    
    /// <summary>
    /// Pré-calcule les valeurs d'une AnimationCurve pour éviter Evaluate()
    /// </summary>
    public void CacheAnimationCurve(string curveName, AnimationCurve curve)
    {
        if (curveCache.ContainsKey(curveName)) return;
        
        float[] values = new float[CURVE_CACHE_RESOLUTION];
        for (int i = 0; i < CURVE_CACHE_RESOLUTION; i++)
        {
            float t = i / (float)(CURVE_CACHE_RESOLUTION - 1);
            values[i] = curve.Evaluate(t);
        }
        
        curveCache[curveName] = values;
    }
    
    /// <summary>
    /// Récupère une valeur depuis le cache de courbe
    /// </summary>
    private float GetCachedCurveValue(string curveName, float t)
    {
        if (!curveCache.ContainsKey(curveName))
            return EaseInOutCubic(t); // Fallback
        
        float[] values = curveCache[curveName];
        int index = Mathf.FloorToInt(t * (CURVE_CACHE_RESOLUTION - 1));
        
        if (index >= CURVE_CACHE_RESOLUTION - 1)
            return values[CURVE_CACHE_RESOLUTION - 1];
            
        // Interpolation linéaire entre deux points
        float remainder = (t * (CURVE_CACHE_RESOLUTION - 1)) - index;
        return Mathf.Lerp(values[index], values[index + 1], remainder);
    }
    
    // TODO: Implémenter UpdateAnimationsWithJobs pour utiliser le Job System
    // (Nécessite plus de refactoring pour être thread-safe)
    private void UpdateAnimationsWithJobs(float currentTime)
    {
        // Pour l'instant, utiliser la version simple
        UpdateAnimationsSimple(currentTime);
    }
    
    /// <summary>
    /// Arrête toutes les animations d'une tuile spécifique
    /// </summary>
    public void StopTileAnimations(Transform tileTransform)
    {
        for (int i = 0; i < activeAnimations.Count; i++)
        {
            if (activeAnimations[i].isActive && 
                activeAnimations[i].transform == tileTransform)
            {
                CompleteAnimation(i);
            }
        }
    }
    
    /// <summary>
    /// Obtient le nombre d'animations actives (pour debug)
    /// </summary>
    public int GetActiveAnimationCount()
    {
        return currentActiveAnimations;
    }
    
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}