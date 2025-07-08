using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Interface pour les environnements qui peuvent être animés par le gestionnaire centralisé
/// </summary>
public interface IAnimatableEnvironment
{
    Transform transform { get; }
    void OnAnimationComplete();
}

/// <summary>
/// Gestionnaire centralisé pour toutes les animations d'environnements.
/// Basé sur la même architecture que TileAnimationManager pour des performances optimales.
/// </summary>
public class EnvironmentAnimationManager : MonoBehaviour
{
    public static EnvironmentAnimationManager Instance { get; private set; }

    public enum AnimationType
    {
        Stretch,
        Bounce,
        TileReactiveBounce,
        Shake
    }

    public struct EnvironmentAnimationState
    {
        // Références
        public Transform transform;
        public IAnimatableEnvironment environment;
        public System.Action onCompleteCallback;

        // Type et état
        public AnimationType animationType;
        public bool isActive;
        public float startTime;
        public float duration;

        // Positions et échelles de base
        public Vector3 originalPosition;
        public Vector3 originalScale;
        public Quaternion originalRotation;

        // Paramètres communs
        public AnimationCurve animationCurve;
        public float intensity;

        // Stretch specific
        public Vector3 stretchAxis;
        public float stretchIntensity;
        public bool useRebound;
        public float reboundAmount;
        public bool isRebounding;
        public float reboundStartTime;

        // Bounce specific
        public float bounceHeight;
        public float maxRotation;
        public Vector3 rotationAxis;
        public AnimationCurve bounceCurve;
        public bool useSquash;
        public float squashFactor;
        public bool isSquashing;
        public float squashStartTime;

        // Tile Reactive specific
        public int bounceCounter;
        public float uniqueObjectID;
        public Vector3 uniqueRotationAxis;
        public float variationFactor;

        // Phase tracking
        public int currentPhase; // 0=rising, 1=peak/hold, 2=falling
        public float phaseStartTime;
        public float nextPhaseTime;
    }

    // Pool d'animations
    private List<EnvironmentAnimationState> activeAnimations = new List<EnvironmentAnimationState>(200);
    private Queue<int> freeIndices = new Queue<int>(200);
    private int currentActiveAnimations = 0;

    // Cache pour les performances
    private Dictionary<string, AnimationCurve> curveCache = new Dictionary<string, AnimationCurve>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Pré-allouer
        for (int i = 0; i < 200; i++)
        {
            activeAnimations.Add(new EnvironmentAnimationState());
            freeIndices.Enqueue(i);
        }

        // Créer des courbes standard
        InitializeStandardCurves();
    }

    void InitializeStandardCurves()
    {
        // Courbe sinusoïdale d'entrée
        var sineIn = AnimationCurve.EaseInOut(0, 0, 1, 1);
        sineIn.keys = new Keyframe[] {
            new Keyframe(0f, 0f, 0f, 1.8f),
            new Keyframe(0.5f, 0.5f, 1.2f, 1.2f),
            new Keyframe(1f, 1f, 0f, 0f)
        };
        curveCache["SineIn"] = sineIn;

        // Courbe sinusoïdale de sortie
        var sineOut = AnimationCurve.EaseInOut(0, 0, 1, 1);
        sineOut.keys = new Keyframe[] {
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.5f, 0.5f, 1.2f, 1.2f),
            new Keyframe(1f, 1f, 1.8f, 0f)
        };
        curveCache["SineOut"] = sineOut;
    }

    public bool RequestStretchAnimation(
        IAnimatableEnvironment environment,
        float duration,
        Vector3 stretchAxis,
        float stretchIntensity,
        bool useRebound,
        float reboundAmount,
        AnimationCurve curve = null,
        float randomness = 0f)
    {
        if (freeIndices.Count == 0) return false;

        int index = freeIndices.Dequeue();
        var transform = environment.transform;

        var state = new EnvironmentAnimationState
        {
            transform = transform,
            environment = environment,
            animationType = AnimationType.Stretch,
            isActive = true,
            startTime = Time.time,
            duration = duration,
            originalPosition = transform.localPosition,
            originalScale = transform.localScale,
            originalRotation = transform.localRotation,
            stretchAxis = stretchAxis.normalized,
            stretchIntensity = stretchIntensity * (1f + Random.Range(-randomness, randomness)),
            useRebound = useRebound,
            reboundAmount = reboundAmount,
            animationCurve = curve ?? AnimationCurve.EaseInOut(0, 0, 1, 1),
            currentPhase = 0
        };

        activeAnimations[index] = state;
        currentActiveAnimations++;
        return true;
    }

    public bool RequestBounceAnimation(
        IAnimatableEnvironment environment,
        float duration,
        float bounceHeight,
        float maxRotation,
        Vector3 rotationAxis,
        bool useSquash,
        float squashFactor,
        AnimationCurve curve = null,
        float randomness = 0f)
    {
        if (freeIndices.Count == 0) return false;

        int index = freeIndices.Dequeue();
        var transform = environment.transform;

        var state = new EnvironmentAnimationState
        {
            transform = transform,
            environment = environment,
            animationType = AnimationType.Bounce,
            isActive = true,
            startTime = Time.time,
            duration = duration,
            originalPosition = transform.localPosition,
            originalScale = transform.localScale,
            originalRotation = transform.localRotation,
            bounceHeight = bounceHeight * (1f + Random.Range(-randomness, randomness)),
            maxRotation = maxRotation * Random.Range(0.7f, 1.3f),
            rotationAxis = rotationAxis.normalized,
            bounceCurve = curve ?? curveCache["SineIn"],
            useSquash = useSquash,
            squashFactor = squashFactor,
            currentPhase = 0
        };

        activeAnimations[index] = state;
        currentActiveAnimations++;
        return true;
    }

    public bool RequestTileReactiveBounceAnimation(
        IAnimatableEnvironment environment,
        float duration,
        float bounceHeight,
        float maxRotation,
        int bounceCounter,
        float uniqueObjectID,
        Vector3 baseRotationAxis,
        float variationFactor,
        float pathWobble,
        bool useSquash,
        float squashFactor,
        AnimationCurve curve = null)
    {
        if (freeIndices.Count == 0) return false;

        int index = freeIndices.Dequeue();
        var transform = environment.transform;

        // Génération des variations organiques
        float bounceVariation = (bounceCounter * 7919 + uniqueObjectID * 104729) % 1000 / 1000.0f;
        Vector3 uniqueRotAxis = new Vector3(
            baseRotationAxis.x + (bounceVariation - 0.5f) * 0.5f,
            baseRotationAxis.y + ((bounceCounter * 13) % 100 / 100.0f - 0.5f) * 0.5f,
            baseRotationAxis.z + ((Time.time * 7) % 1 - 0.5f) * 0.5f
        ).normalized;

        var state = new EnvironmentAnimationState
        {
            transform = transform,
            environment = environment,
            animationType = AnimationType.TileReactiveBounce,
            isActive = true,
            startTime = Time.time,
            duration = duration,
            originalPosition = transform.localPosition,
            originalScale = transform.localScale,
            originalRotation = transform.localRotation,
            bounceHeight = bounceHeight * variationFactor,
            maxRotation = maxRotation,
            uniqueRotationAxis = uniqueRotAxis,
            bounceCounter = bounceCounter,
            uniqueObjectID = uniqueObjectID,
            variationFactor = variationFactor,
            bounceCurve = curve ?? curveCache["SineIn"],
            useSquash = useSquash,
            squashFactor = squashFactor,
            currentPhase = 0,
            intensity = pathWobble
        };

        activeAnimations[index] = state;
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

            var anim = activeAnimations[i];
            
            switch (anim.animationType)
            {
                case AnimationType.Stretch:
                    UpdateStretchAnimation(ref anim, currentTime);
                    break;
                case AnimationType.Bounce:
                    UpdateBounceAnimation(ref anim, currentTime);
                    break;
                case AnimationType.TileReactiveBounce:
                    UpdateTileReactiveBounceAnimation(ref anim, currentTime);
                    break;
            }

            if (!anim.isActive)
            {
                CompleteAnimation(i);
            }
            else
            {
                activeAnimations[i] = anim;
            }
        }
    }

    void UpdateStretchAnimation(ref EnvironmentAnimationState anim, float currentTime)
    {
        float elapsed = currentTime - anim.startTime;
        float progress = Mathf.Clamp01(elapsed / anim.duration);

        if (anim.isRebounding)
        {
            // Phase de rebond
            float reboundElapsed = currentTime - anim.reboundStartTime;
            float reboundDuration = anim.duration * 0.15f;
            float reboundProgress = Mathf.Clamp01(reboundElapsed / reboundDuration);
            float reboundEase = Mathf.Sin(reboundProgress * Mathf.PI);

            Vector3 stretchedScale = CalculateStretchedScale(anim.originalScale, anim.stretchAxis, anim.stretchIntensity);
            Vector3 reboundScale = CalculateReboundScale(anim.originalScale, anim.stretchAxis, anim.reboundAmount);
            
            anim.transform.localScale = Vector3.Lerp(stretchedScale, reboundScale, reboundEase);

            if (reboundProgress >= 1f)
            {
                anim.transform.localScale = anim.originalScale;
                anim.isActive = false;
            }
        }
        else
        {
            // Animation principale
            float easedProgress = anim.animationCurve.Evaluate(progress);
            
            // Déterminer si on s'étire ou on revient
            bool isStretching = anim.currentPhase == 0;
            Vector3 fromScale = isStretching ? anim.originalScale : CalculateStretchedScale(anim.originalScale, anim.stretchAxis, anim.stretchIntensity);
            Vector3 toScale = isStretching ? CalculateStretchedScale(anim.originalScale, anim.stretchAxis, anim.stretchIntensity) : anim.originalScale;
            
            anim.transform.localScale = Vector3.Lerp(fromScale, toScale, easedProgress);

            if (progress >= 1f)
            {
                if (anim.useRebound && anim.currentPhase == 0)
                {
                    // Commencer le rebond
                    anim.isRebounding = true;
                    anim.reboundStartTime = currentTime;
                }
                else
                {
                    anim.transform.localScale = anim.originalScale;
                    anim.isActive = false;
                }
            }
        }
    }

    void UpdateBounceAnimation(ref EnvironmentAnimationState anim, float currentTime)
    {
        float elapsed = currentTime - anim.startTime;
        float totalDuration = anim.duration;
        
        // Phases: 0=montée (40%), 1=descente (40%), 2=squash (20%)
        float riseTime = totalDuration * 0.4f;
        float fallTime = totalDuration * 0.4f;
        float squashTime = totalDuration * 0.2f;

        if (elapsed < riseTime)
        {
            // Phase montée
            float t = elapsed / riseTime;
            float easedT = anim.bounceCurve.Evaluate(t);
            
            Vector3 peakPos = anim.originalPosition + Vector3.up * anim.bounceHeight;
            anim.transform.localPosition = Vector3.Lerp(anim.originalPosition, peakPos, easedT);
            
            Quaternion peakRot = Quaternion.AngleAxis(anim.maxRotation, anim.rotationAxis) * anim.originalRotation;
            anim.transform.localRotation = Quaternion.Slerp(anim.originalRotation, peakRot, easedT);
        }
        else if (elapsed < riseTime + fallTime)
        {
            // Phase descente
            float t = (elapsed - riseTime) / fallTime;
            float easedT = anim.bounceCurve.Evaluate(1f - t);
            
            Vector3 peakPos = anim.originalPosition + Vector3.up * anim.bounceHeight;
            anim.transform.localPosition = Vector3.Lerp(anim.originalPosition, peakPos, easedT);
            
            Quaternion peakRot = Quaternion.AngleAxis(anim.maxRotation, anim.rotationAxis) * anim.originalRotation;
            anim.transform.localRotation = Quaternion.Slerp(anim.originalRotation, peakRot, easedT);
        }
        else if (anim.useSquash && elapsed < totalDuration)
        {
            // Phase squash
            float t = (elapsed - riseTime - fallTime) / squashTime;
            float squashT = t < 0.3f ? t / 0.3f : 1f - ((t - 0.3f) / 0.7f);
            
            Vector3 squashedScale = new Vector3(
                anim.originalScale.x * (1f + (1f - anim.squashFactor) * 0.5f),
                anim.originalScale.y * anim.squashFactor,
                anim.originalScale.z * (1f + (1f - anim.squashFactor) * 0.5f)
            );
            
            anim.transform.localScale = Vector3.Lerp(anim.originalScale, squashedScale, squashT);
        }
        else
        {
            // Animation terminée
            anim.transform.localPosition = anim.originalPosition;
            anim.transform.localRotation = anim.originalRotation;
            anim.transform.localScale = anim.originalScale;
            anim.isActive = false;
        }
    }

    void UpdateTileReactiveBounceAnimation(ref EnvironmentAnimationState anim, float currentTime)
    {
        float elapsed = currentTime - anim.startTime;
        float totalDuration = anim.duration;
        
        // Variations organiques basées sur le compteur de rebond
        float bounceVariation = (anim.bounceCounter * 7919 + anim.uniqueObjectID * 104729) % 1000 / 1000.0f;
        float timingMod = 1.0f + (bounceVariation - 0.5f) * 0.3f;
        
        // Phases avec timing variable
        float riseTime = totalDuration * (0.35f + bounceVariation * 0.1f) * timingMod;
        float holdTime = totalDuration * (0.15f + bounceVariation * 0.1f);
        float fallTime = totalDuration * (0.25f + bounceVariation * 0.1f) * timingMod;
        float squashTime = totalDuration * (0.08f + bounceVariation * 0.04f);

        // Calcul des positions avec wobble
        float wobbleX = Mathf.Sin(elapsed * Mathf.PI * 2f + Time.time) * anim.intensity * 0.02f;
        float wobbleZ = Mathf.Cos(elapsed * Mathf.PI * 3f + Time.time) * anim.intensity * 0.02f;
        
        Vector3 currentPos = anim.originalPosition;
        Vector3 peakPos = anim.originalPosition + Vector3.up * anim.bounceHeight;
        peakPos.x += wobbleX * 0.5f;
        peakPos.z += wobbleZ * 0.5f;

        if (elapsed < riseTime)
        {
            // Phase montée avec courbe variable selon le rebond
            float t = elapsed / riseTime;
            float easedT;
            
            if (anim.bounceCounter % 3 == 0)
                easedT = Mathf.Sin(t * Mathf.PI * 0.5f);
            else if (anim.bounceCounter % 3 == 1)
                easedT = anim.bounceCurve.Evaluate(t);
            else
                easedT = t * t * (3f - 2f * t);

            currentPos = Vector3.Lerp(anim.originalPosition, peakPos, easedT);
            currentPos.x += wobbleX;
            currentPos.z += wobbleZ;
            
            anim.transform.localPosition = currentPos;
            
            Quaternion peakRot = Quaternion.AngleAxis(anim.maxRotation, anim.uniqueRotationAxis) * anim.originalRotation;
            anim.transform.localRotation = Quaternion.Slerp(anim.originalRotation, peakRot, easedT);
        }
        else if (elapsed < riseTime + holdTime)
        {
            // Phase de maintien avec hover
            float holdProgress = (elapsed - riseTime) / holdTime;
            float hoverOffset = Mathf.Sin(holdProgress * Mathf.PI * 2) * 0.03f * anim.bounceHeight;
            
            currentPos = peakPos;
            currentPos.y += hoverOffset;
            currentPos.x += wobbleX * 0.3f;
            currentPos.z += wobbleZ * 0.3f;
            
            anim.transform.localPosition = currentPos;
        }
        else if (elapsed < riseTime + holdTime + fallTime)
        {
            // Phase descente avec courbe variable
            float t = (elapsed - riseTime - holdTime) / fallTime;
            float easedT;
            
            if (anim.bounceCounter % 4 == 0)
                easedT = 1f - Mathf.Pow(1f - t, 2f);
            else if (anim.bounceCounter % 4 == 1)
                easedT = anim.bounceCurve.Evaluate(1f - t);
            else if (anim.bounceCounter % 4 == 2)
                easedT = 1f - t;
            else
                easedT = 1f - (t * t);

            currentPos = Vector3.Lerp(anim.originalPosition, peakPos, easedT);
            currentPos.x += wobbleX * 0.7f;
            currentPos.z += wobbleZ * 0.7f;
            
            anim.transform.localPosition = currentPos;
            
            Quaternion peakRot = Quaternion.AngleAxis(anim.maxRotation, anim.uniqueRotationAxis) * anim.originalRotation;
            anim.transform.localRotation = Quaternion.Slerp(anim.originalRotation, peakRot, easedT);
        }
        else if (anim.useSquash && elapsed < totalDuration)
        {
            // Phase squash avec variation
            float t = (elapsed - riseTime - holdTime - fallTime) / squashTime;
            float squashT;
            
            if (anim.bounceCounter % 2 == 0)
                squashT = t < 0.3f ? t / 0.3f : 1f - ((t - 0.3f) / 0.7f);
            else
                squashT = t < 0.4f ? Mathf.Sin(t / 0.4f * Mathf.PI * 0.5f) : Mathf.Cos((t - 0.4f) / 0.6f * Mathf.PI * 0.5f);

            float squashVariation = 0.9f + (bounceVariation * 0.2f);
            float effectiveSquashFactor = Mathf.Clamp(anim.squashFactor * squashVariation, 0.5f, 0.95f);
            
            Vector3 squashedScale = new Vector3(
                anim.originalScale.x * (1f + (1f - effectiveSquashFactor) * 0.5f),
                anim.originalScale.y * effectiveSquashFactor,
                anim.originalScale.z * (1f + (1f - effectiveSquashFactor) * 0.5f)
            );
            
            anim.transform.localScale = Vector3.Lerp(anim.originalScale, squashedScale, squashT);
        }
        else
        {
            // Animation terminée
            anim.transform.localPosition = anim.originalPosition;
            anim.transform.localRotation = anim.originalRotation;
            anim.transform.localScale = anim.originalScale;
            anim.isActive = false;
        }
    }

    Vector3 CalculateStretchedScale(Vector3 originalScale, Vector3 stretchAxis, float intensity)
    {
        return new Vector3(
            originalScale.x * (1 + (stretchAxis.x * (intensity - 1))),
            originalScale.y * (1 + (stretchAxis.y * (intensity - 1))),
            originalScale.z * (1 + (stretchAxis.z * (intensity - 1)))
        );
    }

    Vector3 CalculateReboundScale(Vector3 originalScale, Vector3 stretchAxis, float reboundAmount)
    {
        float reboundFactor = 1f - (reboundAmount * Random.Range(0.7f, 1.3f));
        return new Vector3(
            originalScale.x * (1 + (stretchAxis.x * (1f - reboundFactor))),
            originalScale.y * (1 + (stretchAxis.y * (1f - reboundFactor))),
            originalScale.z * (1 + (stretchAxis.z * (1f - reboundFactor)))
        );
    }

    void CompleteAnimation(int index)
    {
        var anim = activeAnimations[index];
        
        // Callback
        anim.environment?.OnAnimationComplete();
        
        // Recycler
        anim.isActive = false;
        activeAnimations[index] = anim;
        freeIndices.Enqueue(index);
        currentActiveAnimations--;
    }

    public void StopEnvironmentAnimations(Transform environmentTransform)
    {
        for (int i = 0; i < activeAnimations.Count; i++)
        {
            if (activeAnimations[i].isActive && activeAnimations[i].transform == environmentTransform)
            {
                var anim = activeAnimations[i];
                // Restaurer l'état original
                anim.transform.localPosition = anim.originalPosition;
                anim.transform.localRotation = anim.originalRotation;
                anim.transform.localScale = anim.originalScale;
                
                CompleteAnimation(i);
            }
        }
    }

    public void CacheAnimationCurve(string curveName, AnimationCurve curve)
    {
        if (curve != null && !string.IsNullOrEmpty(curveName))
        {
            curveCache[curveName] = curve;
        }
    }
}