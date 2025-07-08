using UnityEngine;
using System.Collections;
using Sirenix.OdinInspector;

/// <summary>
/// Version optimisée de MusicReactiveEnvironment utilisant le gestionnaire centralisé.
/// Toutes les coroutines ont été remplacées par des appels au EnvironmentAnimationManager.
/// </summary>
public class MusicReactiveEnvironment_Optimized : Environment, IAnimatableEnvironment
{
    #region Configuration Fields (Identiques à l'original)
    [Title("Music Reaction Settings")]
    [SerializeField, Range(0f, 1f)]
    protected float reactionProbability = 0.65f;

    [SerializeField]
    protected bool reactToBeat = true;

    [SerializeField]
    protected EnvironmentAnimationType animationType = EnvironmentAnimationType.Stretch;

    [Title("Stretch Animation Settings")]
    [ShowIf("IsStretchAnimation")]
    [SerializeField]
    protected bool useStretchAnimation = true;

    [ShowIf("@this.IsStretchAnimation() && this.useStretchAnimation")]
    [SerializeField, LabelText("Stretch Axis")]
    protected Vector3 stretchAxis = new Vector3(1, 1, 1);

    [ShowIf("@this.IsStretchAnimation() && this.useStretchAnimation")]
    [SerializeField, Range(0.5f, 2f), LabelText("Stretch Intensity")]
    protected float stretchIntensity = 1.2f;

    [ShowIf("@this.IsStretchAnimation() && this.useStretchAnimation")]
    [SerializeField, LabelText("Start Stretched")]
    protected bool startStretched = false;

    [ShowIf("@this.IsStretchAnimation() && this.useStretchAnimation")]
    [SerializeField, LabelText("Two Beat Cycle")]
    protected bool twoBeatCycle = false;

    [ShowIf("@this.IsStretchAnimation() && this.useStretchAnimation")]
    [SerializeField, LabelText("Use Natural Rebound")]
    protected bool useNaturalRebound = true;

    [ShowIf("@this.IsStretchAnimation() && this.useStretchAnimation && this.useNaturalRebound")]
    [SerializeField, Range(0.05f, 0.5f), LabelText("Rebound Amount")]
    protected float reboundAmount = 0.2f;

    [Title("Bounce Animation Settings")]
    [ShowIf("@this.IsBounceAnimation() || this.IsBounceTileReactiveAnimation()")]
    [SerializeField, Range(0.1f, 3f), LabelText("Bounce Height")]
    protected float bounceHeight = 0.5f;

    [ShowIf("@this.IsBounceAnimation() || this.IsBounceTileReactiveAnimation()")]
    [SerializeField, Range(0f, 45f), LabelText("Max Bounce Rotation")]
    protected float maxBounceRotation = 15f;

    [ShowIf("@this.IsBounceAnimation() || this.IsBounceTileReactiveAnimation()")]
    [SerializeField, LabelText("Rotation Axis")]
    protected Vector3 rotationAxis = new Vector3(1, 0, 0);

    [ShowIf("IsBounceAnimation")]
    [SerializeField, LabelText("Two Beat Cycle")]
    protected bool twoBeatBounce = true;

    [ShowIf("@this.IsBounceAnimation() || this.IsBounceTileReactiveAnimation()")]
    [SerializeField, LabelText("Bounce Curve")]
    protected AnimationCurve bounceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [ShowIf("@this.IsBounceAnimation() || this.IsBounceTileReactiveAnimation()")]
    [SerializeField, LabelText("Squash On Land")]
    protected bool squashOnLand = true;

    [ShowIf("@(this.IsBounceAnimation() || this.IsBounceTileReactiveAnimation()) && this.squashOnLand")]
    [SerializeField, Range(0.5f, 1f), LabelText("Squash Factor")]
    protected float squashFactor = 0.8f;

    [Title("Tile Reactive Settings")]
    [ShowIf("IsBounceTileReactiveAnimation")]
    [SerializeField, Range(0.0001f, 0.5f), LabelText("Minimum Tile Movement Threshold")]
    protected float tileMovementThreshold = 0.0001f;

    [ShowIf("IsBounceTileReactiveAnimation")]
    [SerializeField, LabelText("React To Tile Upward Movement")]
    protected bool reactToTileUpMovement = true;

    [ShowIf("IsBounceTileReactiveAnimation")]
    [SerializeField, LabelText("React To Tile Downward Movement")]
    protected bool reactToTileDownMovement = false;

    [ShowIf("IsBounceTileReactiveAnimation")]
    [SerializeField, Range(0.2f, 2f), LabelText("Animation Duration")]
    protected float tileReactiveAnimationDuration = 0.4f;

    [ShowIf("IsBounceTileReactiveAnimation")]
    [SerializeField, LabelText("Always Debug Tile Movement")]
    protected bool alwaysDebugTileMovement = false;

    [Title("Organic Movement Settings")]
    [ShowIf("@this.IsBounceTileReactiveAnimation()")]
    [SerializeField, Range(0f, 1f), LabelText("Axis Variation")]
    protected float axisVariation = 0.5f;

    [ShowIf("@this.IsBounceTileReactiveAnimation()")]
    [SerializeField, Range(0f, 1f), LabelText("Height Variation Per Bounce")]
    protected float heightVariationPerBounce = 0.2f;

    [ShowIf("@this.IsBounceTileReactiveAnimation()")]
    [SerializeField, Range(0f, 1f), LabelText("Rotation Variation")]
    protected float rotationVariation = 0.3f;

    [ShowIf("@this.IsBounceTileReactiveAnimation()")]
    [SerializeField, Range(0f, 1f), LabelText("Timing Variation")]
    protected float timingVariation = 0.15f;

    [ShowIf("@this.IsBounceTileReactiveAnimation()")]
    [SerializeField, Range(0.1f, 1f), LabelText("Path Wobble")]
    protected float pathWobble = 0.2f;

    [SerializeField, Range(0.1f, 2f), LabelText("Animation Duration")]
    protected float animationDuration = 0.5f;

    [SerializeField, LabelText("Animation Curve")]
    protected AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [SerializeField, Range(0.1f, 0.9f), LabelText("Pre-Beat Fraction")]
    protected float preBeatFraction = 0.3f;

    [SerializeField, Range(0f, 0.5f), LabelText("Randomness")]
    protected float animationRandomness = 0.1f;

    [SerializeField, LabelText("Varied Animation Per Object")]
    protected bool varyAnimationPerObject = true;

    [Title("Debug Settings")]
    [SerializeField]
    protected bool debugReactions = false;
    #endregion

    #region Private Variables
    protected bool isAnimating = false;
    protected Vector3 originalScale;
    protected Vector3 originalPosition;
    protected Quaternion originalRotation;
    protected bool isInitialized = false;
    protected Vector3 lastTilePosition;
    protected float objectVariationFactor;
    protected Vector3 originalWorldPosition;

    // Organic variation variables
    protected Vector3 uniqueRotationAxis;
    protected float uniqueObjectID;
    protected float lastBounceTime;
    protected int bounceCounter = 0;

    // Tile reactive variables
    protected bool isWaitingForTileDown = false;
    protected float lastSignificantTileMovement = 0f;
    protected bool hasTileMovedUp = false;
    protected int framesWithoutMovement = 0;

    // Pour gérer les animations en deux temps (stretch two-beat)
    private bool isInTwoBeatCycle = false;
    private float twoBeatPhaseStartTime = 0f;
    #endregion

    protected override IEnumerator Start()
    {
        yield return StartCoroutine(base.Start());

        originalWorldPosition = occupiedTile != null ? occupiedTile.transform.position : transform.position;
        InitializeReactiveState();

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += HandleBeat;
            MusicManager.Instance.OnMusicStateChanged += HandleMusicStateChange;
        }

        if (debugReactions && animationType == EnvironmentAnimationType.BounceTileReactive)
        {
            Debug.Log($"[REACTIVE ENV OPTIMIZED] {gameObject.name} initialized. Threshold: {tileMovementThreshold}");
        }
    }

    protected void InitializeReactiveState()
    {
        if (!isInitialized)
        {
            originalScale = transform.localScale;
            originalPosition = transform.localPosition;
            originalRotation = transform.localRotation;

            if (varyAnimationPerObject)
            {
                objectVariationFactor = Random.Range(0.8f, 1.2f);
                uniqueObjectID = transform.position.x * 1000 + transform.position.y * 100 + transform.position.z * 10;
                uniqueRotationAxis = new Vector3(
                    rotationAxis.x + Random.Range(-0.3f, 0.3f) * axisVariation,
                    rotationAxis.y + Random.Range(-0.3f, 0.3f) * axisVariation,
                    rotationAxis.z + Random.Range(-0.3f, 0.3f) * axisVariation
                ).normalized;
            }
            else
            {
                objectVariationFactor = 1.0f;
                uniqueObjectID = 0;
                uniqueRotationAxis = rotationAxis.normalized;
            }

            bounceCounter = 0;
            lastBounceTime = Time.time;

            if (occupiedTile != null)
                lastTilePosition = occupiedTile.transform.position;

            // Appliquer l'état initial stretched si configuré
            if (animationType == EnvironmentAnimationType.Stretch && startStretched && useStretchAnimation)
            {
                Vector3 normalizedStretchAxis = stretchAxis.normalized;
                Vector3 stretchedScale = new Vector3(
                    originalScale.x * (1 + (normalizedStretchAxis.x * (stretchIntensity - 1))),
                    originalScale.y * (1 + (normalizedStretchAxis.y * (stretchIntensity - 1))),
                    originalScale.z * (1 + (normalizedStretchAxis.z * (stretchIntensity - 1)))
                );
                transform.localScale = stretchedScale;
            }

            isWaitingForTileDown = false;
            hasTileMovedUp = false;
            framesWithoutMovement = 0;
            isInitialized = true;
        }
    }

    protected virtual void Update()
    {
        if (occupiedTile != null && isInitialized)
        {
            Vector3 currentTilePosition = occupiedTile.transform.position;

            if (currentTilePosition.y != lastTilePosition.y)
            {
                framesWithoutMovement = 0;
                float deltaY = currentTilePosition.y - lastTilePosition.y;

                if (animationType == EnvironmentAnimationType.BounceTileReactive && !isAnimating)
                {
                    if (reactToTileUpMovement && deltaY > tileMovementThreshold && !isWaitingForTileDown)
                    {
                        bounceCounter++;
                        lastBounceTime = Time.time;
                        RequestTileReactiveBounce(deltaY);
                        hasTileMovedUp = true;
                        isWaitingForTileDown = true;
                    }
                    else if (reactToTileDownMovement && deltaY < -tileMovementThreshold && !isAnimating)
                    {
                        if (isWaitingForTileDown && hasTileMovedUp)
                        {
                            isWaitingForTileDown = false;
                            hasTileMovedUp = false;
                        }
                    }

                    if (isWaitingForTileDown && !isAnimating && 
                        Mathf.Abs(currentTilePosition.y - originalWorldPosition.y) < tileMovementThreshold * 0.5f)
                    {
                        isWaitingForTileDown = false;
                        hasTileMovedUp = false;
                    }
                }

                if (animationType != EnvironmentAnimationType.BounceTileReactive || !isAnimating)
                {
                    transform.localPosition = originalPosition;
                }

                lastTilePosition = currentTilePosition;
            }
            else
            {   
                framesWithoutMovement++;
                if (framesWithoutMovement > 120 && isWaitingForTileDown && !isAnimating &&
                    animationType == EnvironmentAnimationType.BounceTileReactive)
                {
                    isWaitingForTileDown = false;
                    hasTileMovedUp = false;
                }
            }
        }
    }

    protected virtual void HandleBeat(float beatDuration)
    {
        if (!isInitialized || !reactToBeat || animationType == EnvironmentAnimationType.BounceTileReactive)
            return;

        if (Random.value > reactionProbability)
            return;

        // Pour les animations two-beat, gérer la continuité
        if (isInTwoBeatCycle && (animationType == EnvironmentAnimationType.Stretch && twoBeatCycle))
        {
            // On est dans la deuxième partie du cycle, on laisse continuer
            return;
        }

        if (isAnimating)
        {
            // Si on est en two-beat bounce, laisser terminer
            if (animationType == EnvironmentAnimationType.Bounce && twoBeatBounce)
                return;
            
            // Sinon, arrêter l'animation en cours
            if (EnvironmentAnimationManager.Instance != null)
            {
                EnvironmentAnimationManager.Instance.StopEnvironmentAnimations(transform);
            }
            isAnimating = false;
        }

        switch (animationType)
        {
            case EnvironmentAnimationType.Stretch:
                if (useStretchAnimation)
                    RequestStretchAnimation(beatDuration);
                break;

            case EnvironmentAnimationType.Bounce:
                lastBounceTime = Time.time;
                RequestBounceAnimation(beatDuration);
                break;
        }
    }

    void RequestStretchAnimation(float beatDuration)
    {
        if (EnvironmentAnimationManager.Instance == null) return;

        float effectiveIntensity = stretchIntensity * objectVariationFactor;
        float randomizedIntensity = effectiveIntensity * Random.Range(1f - animationRandomness, 1f + animationRandomness);

        float randomTimeOffset = beatDuration * Random.Range(-animationRandomness * 0.3f, animationRandomness * 0.3f);
        float nextBeatTime = MusicManager.Instance.GetNextBeatTime() + randomTimeOffset;

        float durationRandomFactor = Random.Range(1f - animationRandomness * 0.5f, 1f + animationRandomness * 0.5f);
        float totalAnimDuration = twoBeatCycle ? beatDuration * durationRandomFactor : beatDuration * animationDuration * durationRandomFactor;

        isAnimating = true;
        if (twoBeatCycle)
        {
            isInTwoBeatCycle = true;
            twoBeatPhaseStartTime = Time.time;
        }

        bool success = EnvironmentAnimationManager.Instance.RequestStretchAnimation(
            this,
            totalAnimDuration,
            stretchAxis,
            randomizedIntensity,
            useNaturalRebound && !twoBeatCycle,
            reboundAmount,
            animationCurve,
            animationRandomness
        );

        if (!success)
        {
            isAnimating = false;
            isInTwoBeatCycle = false;
        }
    }

    void RequestBounceAnimation(float beatDuration)
    {
        if (EnvironmentAnimationManager.Instance == null) return;

        float effectiveHeight = bounceHeight * objectVariationFactor;
        float effectiveRotation = maxBounceRotation * objectVariationFactor;
        
        float randomizedHeight = effectiveHeight * Random.Range(1f - animationRandomness, 1f + animationRandomness);
        float randomizedRotation = effectiveRotation * Random.Range(0.7f, 1.3f);

        float totalDuration = twoBeatBounce ? beatDuration * 2f : beatDuration * animationDuration;

        isAnimating = true;
        bool success = EnvironmentAnimationManager.Instance.RequestBounceAnimation(
            this,
            totalDuration,
            randomizedHeight,
            randomizedRotation,
            rotationAxis,
            squashOnLand,
            squashFactor,
            bounceCurve,
            animationRandomness
        );

        if (!success)
        {
            isAnimating = false;
        }
    }

    void RequestTileReactiveBounce(float tileMovementAmount)
    {
        if (EnvironmentAnimationManager.Instance == null) return;

        float heightScale = Mathf.Clamp01(tileMovementAmount / (tileMovementThreshold * 3));
        float adjustedHeight = bounceHeight * (0.7f + heightScale * 0.6f);

        isAnimating = true;
        bool success = EnvironmentAnimationManager.Instance.RequestTileReactiveBounceAnimation(
            this,
            tileReactiveAnimationDuration,
            adjustedHeight,
            maxBounceRotation,
            bounceCounter,
            uniqueObjectID,
            rotationAxis,
            objectVariationFactor,
            pathWobble,
            squashOnLand,
            squashFactor,
            bounceCurve
        );

        if (!success)
        {
            isAnimating = false;
        }

        if (debugReactions)
        {
            Debug.Log($"[REACTIVE ENV OPTIMIZED] {gameObject.name} requested tile reactive bounce #{bounceCounter}");
        }
    }

    public void OnAnimationComplete()
    {
        isAnimating = false;
        
        // Gérer la fin du cycle two-beat pour stretch
        if (isInTwoBeatCycle && animationType == EnvironmentAnimationType.Stretch)
        {
            isInTwoBeatCycle = false;
        }
    }

    protected virtual void HandleMusicStateChange(string newState)
    {
        if (isAnimating && EnvironmentAnimationManager.Instance != null)
        {
            EnvironmentAnimationManager.Instance.StopEnvironmentAnimations(transform);
            isAnimating = false;
            isInTwoBeatCycle = false;
        }
    }

    public virtual void ResetToOriginalState()
    {
        if (EnvironmentAnimationManager.Instance != null)
        {
            EnvironmentAnimationManager.Instance.StopEnvironmentAnimations(transform);
        }

        transform.localScale = originalScale;
        transform.localPosition = originalPosition;
        transform.localRotation = originalRotation;

        isAnimating = false;
        isWaitingForTileDown = false;
        hasTileMovedUp = false;
        isInTwoBeatCycle = false;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleBeat;
            MusicManager.Instance.OnMusicStateChanged -= HandleMusicStateChange;
        }

        if (EnvironmentAnimationManager.Instance != null)
        {
            EnvironmentAnimationManager.Instance.StopEnvironmentAnimations(transform);
        }
    }

    #region Helper Methods for Odin Inspector
    protected bool IsStretchAnimation() => animationType == EnvironmentAnimationType.Stretch;
    protected bool IsBounceAnimation() => animationType == EnvironmentAnimationType.Bounce;
    protected bool IsBounceTileReactiveAnimation() => animationType == EnvironmentAnimationType.BounceTileReactive;
    #endregion

#if UNITY_EDITOR
    [Title("Migration Tools")]
    [Button("Copy Values from Original MusicReactiveEnvironment", ButtonSizes.Large)]
    private void CopyValuesFromOriginal()
    {
        MusicReactiveEnvironment original = GetComponent<MusicReactiveEnvironment>();
        if (original == null)
        {
            Debug.LogError("No original MusicReactiveEnvironment found!");
            return;
        }

        // Utiliser la réflexion pour copier les valeurs
        var originalType = original.GetType();
        var optimizedType = this.GetType();

        string[] fieldsToCopy = {
            "reactionProbability", "reactToBeat", "animationType",
            "useStretchAnimation", "stretchAxis", "stretchIntensity", "startStretched",
            "twoBeatCycle", "useNaturalRebound", "reboundAmount",
            "bounceHeight", "maxBounceRotation", "rotationAxis", "twoBeatBounce",
            "bounceCurve", "squashOnLand", "squashFactor",
            "tileMovementThreshold", "reactToTileUpMovement", "reactToTileDownMovement",
            "tileReactiveAnimationDuration", "alwaysDebugTileMovement",
            "axisVariation", "heightVariationPerBounce", "rotationVariation",
            "timingVariation", "pathWobble", "animationDuration", "animationCurve",
            "preBeatFraction", "animationRandomness", "varyAnimationPerObject", "debugReactions"
        };

        foreach (string fieldName in fieldsToCopy)
        {
            var originalField = originalType.GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            var optimizedField = optimizedType.GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);

            if (originalField != null && optimizedField != null)
            {
                object value = originalField.GetValue(original);
                optimizedField.SetValue(this, value);
            }
        }

        Debug.Log($"Successfully copied values from original MusicReactiveEnvironment");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [Button("Remove Original Script After Migration")]
    [ShowIf("HasOriginalScript")]
    private void RemoveOriginalScript()
    {
        MusicReactiveEnvironment original = GetComponent<MusicReactiveEnvironment>();
        if (original != null)
        {
            DestroyImmediate(original);
            Debug.Log("Original script removed.");
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
    }

    private bool HasOriginalScript() => GetComponent<MusicReactiveEnvironment>() != null;

    [ContextMenu("Test Beat Reaction")]
    protected virtual void TestBeatReaction()
    {
        if (!isInitialized)
            InitializeReactiveState();

        HandleBeat(0.5f);
    }

    [ContextMenu("Reset To Original State")]
    protected virtual void EditorResetState()
    {
        ResetToOriginalState();
    }
#endif
}

