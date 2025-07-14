using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Centralized manager for all tile animations.
/// This final, corrected version has a simplified interface, supports position and scale,
/// and includes necessary management methods.
/// </summary>
public class TileAnimationManager : MonoBehaviour
{
    /// <summary>
    /// Singleton instance of the TileAnimationManager.
    /// </summary>
    public static TileAnimationManager Instance { get; private set; }

    /// <summary>
    /// Represents the state of a single tile animation.
    /// </summary>
    public struct TileAnimationState
    {
        // References
        public Transform transform;
        public System.Action onCompleteCallback;

        // Animation parameters
        public float startTime;
        public float duration;

        // Position movement
        public Vector3 startPosition;
        public Vector3 targetPosition;
        public AnimationCurve movementCurve;

        // Scale movement
        public Vector3 startScale;
        public Vector3 targetScale;
        public AnimationCurve scaleCurve;

        // "Shake" type animation
        public bool isShakeAnimation;
        public float shakeIntensity;

        public bool isActive;
    }

    private List<TileAnimationState> activeAnimations = new List<TileAnimationState>(500);
    private Queue<int> freeIndices = new Queue<int>(500);
    private int currentActiveAnimations = 0;

    private Dictionary<string, AnimationCurve> curveCache = new Dictionary<string, AnimationCurve>();

    /// <summary>
    /// Unity's Awake method. Initializes the singleton instance and pre-allocates animation structures.
    /// </summary>
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        for (int i = 0; i < 500; i++)
        {
            activeAnimations.Add(new TileAnimationState());
            freeIndices.Enqueue(i);
        }
    }

    /// <summary>
    /// The single, flexible method to request all types of animations.
    /// </summary>
    /// <param name="tileTransform">The transform of the tile to animate.</param>
    /// <param name="duration">The duration of the animation.</param>
    /// <param name="onComplete">The callback to invoke when the animation is complete.</param>
    /// <param name="targetPosition">The target position for movement animations.</param>
    /// <param name="moveCurve">The animation curve for movement.</param>
    /// <param name="targetScale">The target scale for scaling animations.</param>
    /// <param name="scaleCurve">The animation curve for scaling.</param>
    /// <param name="isShake">Whether this is a shake animation.</param>
    /// <param name="shakeIntensity">The intensity of the shake.</param>
    /// <returns>True if the animation was successfully requested, false otherwise.</returns>
    public bool RequestAnimation(
        Transform tileTransform,
        float duration,
        System.Action onComplete,
        Vector3? targetPosition = null,
        AnimationCurve moveCurve = null,
        Vector3? targetScale = null,
        AnimationCurve scaleCurve = null,
        bool isShake = false,
        float shakeIntensity = 0f)
    {
        if (freeIndices.Count == 0)
        {
            Debug.LogWarning("[TileAnimationManager] Animation limit reached!");
            return false;
        }

        int index = freeIndices.Dequeue();

        var animState = new TileAnimationState
        {
            transform = tileTransform,
            onCompleteCallback = onComplete,
            startTime = Time.time,
            duration = duration,

            startPosition = tileTransform.position,
            targetPosition = targetPosition ?? tileTransform.position,
            movementCurve = moveCurve,

            startScale = tileTransform.localScale,
            targetScale = targetScale ?? tileTransform.localScale,
            scaleCurve = scaleCurve,

            isShakeAnimation = isShake,
            shakeIntensity = shakeIntensity,

            isActive = true
        };

        activeAnimations[index] = animState;
        currentActiveAnimations++;
        return true;
    }

    /// <summary>
    /// Unity's Update method. Processes all active animations.
    /// </summary>
    void Update()
    {
        if (currentActiveAnimations == 0) return;

        float currentTime = Time.time;
        for (int i = 0; i < activeAnimations.Count; i++)
        {
            if (!activeAnimations[i].isActive) continue;

            var anim = activeAnimations[i];
            float progress = Mathf.Clamp01((currentTime - anim.startTime) / anim.duration);

            if (anim.isShakeAnimation)
            {
                float currentIntensity = Mathf.Lerp(anim.shakeIntensity, 0f, progress);
                float shakeX = (Mathf.PerlinNoise(currentTime * 20f, 0f) * 2f - 1f) * currentIntensity;
                float shakeZ = (Mathf.PerlinNoise(0f, currentTime * 20f) * 2f - 1f) * currentIntensity;
                anim.transform.position = anim.startPosition + new Vector3(shakeX, 0, shakeZ);
            }
            else
            {
                if (anim.movementCurve != null)
                {
                    float curveValue = anim.movementCurve.Evaluate(progress);
                    anim.transform.position = Vector3.LerpUnclamped(anim.startPosition, anim.targetPosition, curveValue);
                }

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
                activeAnimations[i] = anim;
            }
        }
    }

    /// <summary>
    /// Completes an animation at a given index.
    /// </summary>
    /// <param name="index">The index of the animation to complete.</param>
    private void CompleteAnimation(int index)
    {
        var anim = activeAnimations[index];
        if (!anim.isActive) return;

        anim.transform.position = anim.isShakeAnimation ? anim.startPosition : anim.targetPosition;
        anim.transform.localScale = anim.targetScale;

        anim.onCompleteCallback?.Invoke();

        anim.isActive = false;
        activeAnimations[index] = anim;
        freeIndices.Enqueue(index);
        currentActiveAnimations--;
    }

    /// <summary>
    /// Stops all animations for a given tile transform.
    /// </summary>
    /// <param name="tileTransform">The transform of the tile.</param>
    public void StopTileAnimations(Transform tileTransform)
    {
        if (tileTransform == null) return;
        for (int i = 0; i < activeAnimations.Count; i++)
        {
            if (activeAnimations[i].isActive && activeAnimations[i].transform == tileTransform)
            {
                CompleteAnimation(i);
            }
        }
    }

    /// <summary>
    /// Caches an animation curve for future use.
    /// </summary>
    /// <param name="curveName">The name of the curve.</param>
    /// <param name="curve">The animation curve to cache.</param>
    public void CacheAnimationCurve(string curveName, AnimationCurve curve)
    {
        if (curve == null || string.IsNullOrEmpty(curveName)) return;
        curveCache[curveName] = curve;
    }

    /// <summary>
    /// Clears all active animations. Used during level transitions.
    /// </summary>
    public void ClearAllAnimations()
    {
        Debug.Log($"[TileAnimationManager] Clearing {currentActiveAnimations} active animations.");
        
        for (int i = 0; i < activeAnimations.Count; i++)
        {
            if (activeAnimations[i].isActive)
            {
                var anim = activeAnimations[i];
                
                if (anim.transform != null)
                {
                    anim.transform.position = anim.isShakeAnimation ? anim.startPosition : anim.targetPosition;
                    anim.transform.localScale = anim.targetScale;
                }
                
                try
                {
                    anim.onCompleteCallback?.Invoke();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[TileAnimationManager] Error during animation callback: {ex.Message}");
                }
                
                anim.isActive = false;
                activeAnimations[i] = anim;
                
                freeIndices.Enqueue(i);
            }
        }
        
        currentActiveAnimations = 0;
        
        curveCache.Clear();
        
        Debug.Log("[TileAnimationManager] All animations have been cleared.");
    }
}