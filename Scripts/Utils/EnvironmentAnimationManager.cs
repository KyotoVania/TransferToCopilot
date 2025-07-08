using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Interface pour les objets animables par le manager.
/// </summary>
public interface IAnimatableEnvironment
{
    Transform transform { get; }
    void OnAnimationComplete();
}

/// <summary>
/// Le manager d'animation pour les objets d'environnement.
/// ARCHITECTURE CLONÉE SUR CELLE DE TILEANIMATIONMANAGER POUR LA ROBUSTESSE ET LA PERFORMANCE.
/// </summary>
public class EnvironmentAnimationManager : MonoBehaviour
{
    public static EnvironmentAnimationManager Instance { get; private set; }

    private List<AnimationState> activeAnimations;
    private Queue<int> freeIndices;
    private int animationPoolSize = 200; // Ajustable si nécessaire

    private struct AnimationState
    {
        public bool isActive;
        public IAnimatableEnvironment environment;
        public Transform transform;

        // Timings
        public float startTime;
        public float duration;
        public AnimationCurve curve;

        // Valeurs initiales
        public Vector3 startPosition;
        public Quaternion startRotation;
        public Vector3 startScale;

        // Paramètres de l'animation
        public AnimationType type;
        public float bounceHeight;
        public float bounceRotation;
        public Vector3 rotationAxis;
        public float stretchIntensity;
        public Vector3 stretchAxis;
        public float squashFactor;
    }

    public enum AnimationType
    {
        Bounce,
        Stretch
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialisation du pool, comme dans TileAnimationManager
        activeAnimations = new List<AnimationState>(animationPoolSize);
        freeIndices = new Queue<int>(animationPoolSize);
        for (int i = 0; i < animationPoolSize; i++)
        {
            activeAnimations.Add(new AnimationState());
            freeIndices.Enqueue(i);
        }
    }

    public void RequestAnimation(IAnimatableEnvironment env, AnimationType type, float duration, AnimationCurve curve,
                                 float bounceHeight = 0, float bounceRotation = 0, Vector3 rotationAxis = default,
                                 float stretchIntensity = 0, Vector3 stretchAxis = default, float squashFactor = 0)
    {
        if (freeIndices.Count == 0)
        {
            Debug.LogWarning("EnvironmentAnimationManager pool is full. No animation will be played.");
            return;
        }

        int index = freeIndices.Dequeue();
        var t = env.transform;

        activeAnimations[index] = new AnimationState
        {
            isActive = true,
            environment = env,
            transform = t,
            startTime = Time.time,
            duration = duration,
            curve = curve,
            startPosition = t.localPosition,
            startRotation = t.localRotation,
            startScale = t.localScale,
            type = type,
            bounceHeight = bounceHeight,
            bounceRotation = bounceRotation,
            rotationAxis = rotationAxis,
            stretchIntensity = stretchIntensity,
            stretchAxis = stretchAxis,
            squashFactor = squashFactor
        };
    }

    void Update()
    {
        if (activeAnimations == null || activeAnimations.Count == 0) return;

        float currentTime = Time.time;

        for (int i = 0; i < animationPoolSize; i++)
        {
            if (!activeAnimations[i].isActive) continue;

            var anim = activeAnimations[i];
            float elapsed = currentTime - anim.startTime;
            float progress = Mathf.Clamp01(elapsed / anim.duration);
            float easedProgress = anim.curve.Evaluate(progress);

            switch (anim.type)
            {
                case AnimationType.Bounce:
                    // Logique de rebond avec montée/descente et squash
                    float upDownProgress = Mathf.Sin(easedProgress * Mathf.PI); // 0 -> 1 -> 0
                    anim.transform.localPosition = Vector3.LerpUnclamped(anim.startPosition, anim.startPosition + Vector3.up * anim.bounceHeight, upDownProgress);
                    anim.transform.localRotation = Quaternion.SlerpUnclamped(anim.startRotation, anim.startRotation * Quaternion.AngleAxis(anim.bounceRotation, anim.rotationAxis), upDownProgress);

                    // Le squash se produit à la fin
                    if (progress > 0.8f)
                    {
                        float squashProgress = Mathf.Sin(((progress - 0.8f) / 0.2f) * Mathf.PI); // 0 -> 1 -> 0
                        float currentSquash = 1.0f - (1.0f - anim.squashFactor) * squashProgress;
                        float currentStretch = 1.0f + (1.0f - currentSquash) * 0.5f;
                        anim.transform.localScale = new Vector3(anim.startScale.x * currentStretch, anim.startScale.y * currentSquash, anim.startScale.z * currentStretch);
                    }
                    break;

                case AnimationType.Stretch:
                     // Logique de stretch avec un arc complet
                    float stretchProgress = Mathf.Sin(easedProgress * Mathf.PI); // 0 -> 1 -> 0
                    anim.transform.localScale = anim.startScale + anim.stretchAxis * (anim.stretchIntensity - 1) * stretchProgress;
                    break;
            }

            if (progress >= 1f)
            {
                // Fin de l'animation, on restaure l'état et on libère l'objet
                anim.transform.localPosition = anim.startPosition;
                anim.transform.localRotation = anim.startRotation;
                anim.transform.localScale = anim.startScale;

                anim.environment.OnAnimationComplete();
                
                // On remet dans le pool
                activeAnimations[i] = new AnimationState { isActive = false };
                freeIndices.Enqueue(i);
            }
        }
    }

    public void StopAllAnimationsFor(IAnimatableEnvironment env)
    {
        if (env == null || activeAnimations == null) return;
        for (int i = 0; i < animationPoolSize; i++)
        {
            if (activeAnimations[i].isActive && activeAnimations[i].environment == env)
            {
                var anim = activeAnimations[i];
                anim.transform.localPosition = anim.startPosition;
                anim.transform.localRotation = anim.startRotation;
                anim.transform.localScale = anim.startScale;
                
                anim.environment.OnAnimationComplete();

                activeAnimations[i] = new AnimationState { isActive = false };
                freeIndices.Enqueue(i);
            }
        }
    }
}