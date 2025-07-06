// Fichier : Assets/Scripts/TileAnimationManager.cs (COMPLET ET CORRIGÉ)
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gestionnaire centralisé pour toutes les animations de tuiles.
/// Version finale et corrigée : interface simplifiée, supporte position et scale,
/// et inclut les méthodes de gestion nécessaires.
/// </summary>
public class TileAnimationManager : MonoBehaviour
{
    public static TileAnimationManager Instance { get; private set; }

    public struct TileAnimationState
    {
        // Références
        public Transform transform;
        public System.Action onCompleteCallback;

        // Paramètres de l'animation
        public float startTime;
        public float duration;

        // Mouvement de position
        public Vector3 startPosition;
        public Vector3 targetPosition;
        public AnimationCurve movementCurve;

        // Mouvement de scale
        public Vector3 startScale;
        public Vector3 targetScale;
        public AnimationCurve scaleCurve;

        // Animation de type "Shake"
        public bool isShakeAnimation;
        public float shakeIntensity;

        public bool isActive;
    }

    private List<TileAnimationState> activeAnimations = new List<TileAnimationState>(500);
    private Queue<int> freeIndices = new Queue<int>(500);
    private int currentActiveAnimations = 0;

    // Le cache de courbe que votre code utilise
    private Dictionary<string, AnimationCurve> curveCache = new Dictionary<string, AnimationCurve>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Pré-allouer la capacité de la liste et de la file
        for (int i = 0; i < 500; i++)
        {
            activeAnimations.Add(new TileAnimationState());
            freeIndices.Enqueue(i);
        }
    }

    /// <summary>
    /// La méthode UNIQUE et flexible pour demander TOUS types d'animations.
    /// </summary>
    public bool RequestAnimation(
        Transform tileTransform,
        float duration,
        System.Action onComplete,
        // Paramètres pour le mouvement
        Vector3? targetPosition = null,
        AnimationCurve moveCurve = null,
        // Paramètres pour le scale
        Vector3? targetScale = null,
        AnimationCurve scaleCurve = null,
        // Paramètres pour le shake
        bool isShake = false,
        float shakeIntensity = 0f)
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
            onCompleteCallback = onComplete,
            startTime = Time.time,
            duration = duration,

            // Position (utilise la position actuelle si la cible est nulle)
            startPosition = tileTransform.position,
            targetPosition = targetPosition ?? tileTransform.position,
            movementCurve = moveCurve,

            // Scale (utilise le scale actuel si la cible est nulle)
            startScale = tileTransform.localScale,
            targetScale = targetScale ?? tileTransform.localScale,
            scaleCurve = scaleCurve,

            // Shake
            isShakeAnimation = isShake,
            shakeIntensity = shakeIntensity,

            isActive = true
        };

        activeAnimations[index] = animState;
        currentActiveAnimations++;
        return true;
    }

    void Update()
    {
        if (currentActiveAnimations == 0) return;

        float currentTime = Time.time;
        for (int i = 0; i < activeAnimations.Count; i++)
        {
            if (!activeAnimations[i].isActive) continue;

            // Il est plus sûr de travailler sur une copie
            var anim = activeAnimations[i];
            float progress = Mathf.Clamp01((currentTime - anim.startTime) / anim.duration);

            if (anim.isShakeAnimation)
            {
                // Logique de Shake
                float currentIntensity = Mathf.Lerp(anim.shakeIntensity, 0f, progress);
                float shakeX = (Mathf.PerlinNoise(currentTime * 20f, 0f) * 2f - 1f) * currentIntensity;
                float shakeZ = (Mathf.PerlinNoise(0f, currentTime * 20f) * 2f - 1f) * currentIntensity;
                anim.transform.position = anim.startPosition + new Vector3(shakeX, 0, shakeZ);
            }
            else
            {
                // Logique de Position
                if (anim.movementCurve != null)
                {
                    float curveValue = anim.movementCurve.Evaluate(progress);
                    anim.transform.position = Vector3.LerpUnclamped(anim.startPosition, anim.targetPosition, curveValue);
                }

                // Logique de Scale
                if (anim.scaleCurve != null && anim.targetScale != anim.startScale)
                {
                    float scaleValue = anim.scaleCurve.Evaluate(progress);
                    anim.transform.localScale = Vector3.LerpUnclamped(anim.startScale, anim.targetScale, scaleValue);
                }
            }

            if (progress >= 1f)
            {
                CompleteAnimation(i);
            }
            else
            {
                // Remettre la copie modifiée dans la liste (important car c'est une struct)
                activeAnimations[i] = anim;
            }
        }
    }

    private void CompleteAnimation(int index)
    {
        var anim = activeAnimations[index];
        if (!anim.isActive) return; // Sécurité pour éviter double complétion

        // Assurer l'état final
        anim.transform.position = anim.isShakeAnimation ? anim.startPosition : anim.targetPosition;
        anim.transform.localScale = anim.targetScale;

        // Le callback fiable
        anim.onCompleteCallback?.Invoke();

        // Recycler l'animation
        anim.isActive = false;
        activeAnimations[index] = anim; // Mettre à jour la structure dans la liste
        freeIndices.Enqueue(index);
        currentActiveAnimations--;
    }

    /// <summary>
    /// Arrête proprement toutes les animations pour une tuile donnée.
    /// Méthode requise pour corriger l'erreur de compilation.
    /// </summary>
    public void StopTileAnimations(Transform tileTransform)
    {
        if (tileTransform == null) return;
        for (int i = 0; i < activeAnimations.Count; i++)
        {
            if (activeAnimations[i].isActive && activeAnimations[i].transform == tileTransform)
            {
                // On appelle CompleteAnimation qui s'occupe de tout le nettoyage.
                CompleteAnimation(i);
            }
        }
    }

    /// <summary>
    /// Met en cache une courbe pour une utilisation future (si nécessaire).
    /// Méthode requise pour corriger l'erreur de compilation.
    /// </summary>
    public void CacheAnimationCurve(string curveName, AnimationCurve curve)
    {
        if (curve == null || string.IsNullOrEmpty(curveName)) return;
        curveCache[curveName] = curve;
    }
}