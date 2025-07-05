using UnityEngine;
using System.Collections;
using Sirenix.OdinInspector;

public enum EnvironmentAnimationType
{
    Stretch,
    Bounce,
    BounceTileReactive
}

public class MusicReactiveEnvironment : Environment
{
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
    protected Vector3 rotationAxis = new Vector3(1, 0, 0); // Default to x-axis rotation

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

    // Private variables
    protected Coroutine currentAnimation;
    protected bool isAnimating = false;
    protected Vector3 originalScale;
    protected Vector3 originalPosition;
    protected Quaternion originalRotation;
    protected bool isInitialized = false;
    protected Vector3 lastTilePosition;
    protected float objectVariationFactor; // Unique variation factor per object for more natural forest look
    protected Vector3 originalWorldPosition; // Original world position (needed for reference)

    // Organic variation variables
    protected Vector3 uniqueRotationAxis;
    protected float uniqueObjectID;
    protected float lastBounceTime;
    protected int bounceCounter = 0;

    // Tile reactive variables
    protected bool isWaitingForTileDown = false;
    protected float lastSignificantTileMovement = 0f;
    protected bool hasTileMovedUp = false;

    // Frame counters for debugging
    protected int framesWithoutMovement = 0;

    protected override IEnumerator Start()
    {
        // Call the base Environment Start method which handles attachment to a tile
        yield return StartCoroutine(base.Start());

        // Store the original world position
        if (occupiedTile != null)
        {
            originalWorldPosition = occupiedTile.transform.position;
        }
        else
        {
            originalWorldPosition = transform.position;
        }

        // Initialize after we've attached to a tile
        InitializeReactiveState();

        // Subscribe to beat events
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += HandleBeat;
            MusicManager.Instance.OnMusicStateChanged += HandleMusicStateChange;
        }
        else
        {
            Debug.LogWarning($"[REACTIVE ENVIRONMENT] {gameObject.name} couldn't find MusicManager!");
        }

        // Log initial state if debugging is enabled
        if (debugReactions && animationType == EnvironmentAnimationType.BounceTileReactive)
        {
            Debug.Log($"[REACTIVE ENVIRONMENT] {gameObject.name} initialized as BounceTileReactive. " +
                       $"Threshold: {tileMovementThreshold}, " +
                       $"React to up: {reactToTileUpMovement}, " +
                       $"Tile position: {lastTilePosition}");
        }
    }

    protected void InitializeReactiveState()
    {
        if (!isInitialized)
        {
            // Store original transform values (before any animation)
            originalScale = transform.localScale;
            originalPosition = transform.localPosition;
            originalRotation = transform.localRotation;

            // Generate a unique variation factor for this object if enabled
            if (varyAnimationPerObject)
            {
                // This creates a value between 0.8 and 1.2 that's unique to this object
                objectVariationFactor = Random.Range(0.8f, 1.2f);

                // Create a unique object ID based on position (for consistent randomization)
                uniqueObjectID = transform.position.x * 1000 + transform.position.y * 100 + transform.position.z * 10;

                // Generate a unique rotation axis with variation from the main axis
                float axisRandomizer = uniqueObjectID * 0.1f % 1.0f;

                // Create a unique but stable rotation axis that's different for each object
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

            // Initialize bounce counter
            bounceCounter = 0;
            lastBounceTime = Time.time;

            // Store the initial tile position if we're attached to a tile
            if (occupiedTile != null)
            {
                lastTilePosition = occupiedTile.transform.position;
            }

            // Apply stretched state if configured to start stretched and using stretch animation
            if (animationType == EnvironmentAnimationType.Stretch && startStretched && useStretchAnimation)
            {
                // Normalize stretch axis to use as a multiplier
                Vector3 normalizedStretchAxis = stretchAxis.normalized;

                // Create the stretched scale by applying the stretch intensity along the selected axis
                Vector3 stretchedScale = new Vector3(
                    originalScale.x * (1 + (normalizedStretchAxis.x * (stretchIntensity - 1))),
                    originalScale.y * (1 + (normalizedStretchAxis.y * (stretchIntensity - 1))),
                    originalScale.z * (1 + (normalizedStretchAxis.z * (stretchIntensity - 1)))
                );

                // Apply the stretched scale
                transform.localScale = stretchedScale;

                if (debugReactions)
                {
                    Debug.Log($"[REACTIVE ENVIRONMENT] {gameObject.name} initialized in stretched state");
                }
            }

            // Initialize tile reactive variables
            isWaitingForTileDown = false;
            hasTileMovedUp = false;
            framesWithoutMovement = 0;

            isInitialized = true;

            if (debugReactions)
            {
                Debug.Log($"[REACTIVE ENVIRONMENT] {gameObject.name} initialized with variation factor: {objectVariationFactor}");
            }
        }
    }

    protected virtual void Update()
    {
        // If we're attached to a tile, follow its vertical movement
        if (occupiedTile != null && isInitialized)
        {
            Vector3 currentTilePosition = occupiedTile.transform.position;

            // Check if the tile has moved vertically
            if (currentTilePosition.y != lastTilePosition.y)
            {
                framesWithoutMovement = 0;

                // Calculate the change in height
                float deltaY = currentTilePosition.y - lastTilePosition.y;

                // Debug logging for tile movement
                if (debugReactions && animationType == EnvironmentAnimationType.BounceTileReactive &&
                    (Mathf.Abs(deltaY) > 0.001f || alwaysDebugTileMovement))
                {
                    Debug.Log($"[REACTIVE ENVIRONMENT] {gameObject.name} detected tile movement: deltaY={deltaY}, " +
                             $"threshold={tileMovementThreshold}, isAnimating={isAnimating}, " +
                             $"waitingForDown={isWaitingForTileDown}");
                }

                // For BounceTileReactive animation type, check if we should trigger animations
                if (animationType == EnvironmentAnimationType.BounceTileReactive && !isAnimating)
                {
                    // Detect significant upward movement to trigger bounce
                    if (reactToTileUpMovement && deltaY > tileMovementThreshold)
                    {
                        if (debugReactions)
                        {
                            Debug.Log($"[REACTIVE ENVIRONMENT] {gameObject.name} detected SIGNIFICANT upward tile movement: {deltaY}, triggering animation!");
                        }

                        // Only react if we're not waiting for the tile to go down
                        if (!isWaitingForTileDown)
                        {
                            // Increment bounce counter for variation
                            bounceCounter++;
                            lastBounceTime = Time.time;

                            // Trigger the bounce animation
                            if (currentAnimation != null)
                            {
                                StopCoroutine(currentAnimation);
                            }

                            currentAnimation = StartCoroutine(AnimateTileReactiveBounce(tileReactiveAnimationDuration, deltaY));
                            hasTileMovedUp = true;
                            isWaitingForTileDown = true;
                        }
                    }
                    // Detect when the tile has moved back down to reset
                    else if (reactToTileDownMovement && deltaY < -tileMovementThreshold)
                    {
                        if (debugReactions)
                        {
                            Debug.Log($"[REACTIVE ENVIRONMENT] {gameObject.name} detected significant downward tile movement: {deltaY}");
                        }

                        // Only trigger if the tile has moved down after moving up
                        if (isWaitingForTileDown && hasTileMovedUp)
                        {
                            // The tile has returned to its down position, reset the flag
                            isWaitingForTileDown = false;
                            hasTileMovedUp = false;
                        }
                    }

                    // If we detect the tile has returned to a stable position near original
                    // and we were waiting for it, reset the waiting flag
                    if (isWaitingForTileDown && Mathf.Abs(currentTilePosition.y - originalWorldPosition.y) < tileMovementThreshold * 0.5f)
                    {
                        isWaitingForTileDown = false;
                        hasTileMovedUp = false;

                        if (debugReactions)
                        {
                            Debug.Log($"[REACTIVE ENVIRONMENT] {gameObject.name} tile has returned to near-original position, resetting flags");
                        }
                    }
                }

                // IMPORTANT: For non-tile reactive animations OR when not actively animating,
                // update position to maintain relative height
                if (animationType != EnvironmentAnimationType.BounceTileReactive || !isAnimating)
                {
                    // Update our local position to maintain the same relative height
                    transform.localPosition = new Vector3(
                        originalPosition.x,
                        originalPosition.y,
                        originalPosition.z
                    );
                }

                // Update the stored tile position
                lastTilePosition = currentTilePosition;
            }
            else
            {
                // No movement detected
                framesWithoutMovement++;

                // If we've seen no movement for a while and we're in a waiting state, reset the flags
                if (framesWithoutMovement > 60 && isWaitingForTileDown &&
                    animationType == EnvironmentAnimationType.BounceTileReactive)
                {
                    isWaitingForTileDown = false;
                    hasTileMovedUp = false;

                    if (debugReactions)
                    {
                        Debug.Log($"[REACTIVE ENVIRONMENT] {gameObject.name} no movement for 60 frames, resetting flags");
                    }
                }
            }
        }
    }

    protected virtual void HandleBeat(float beatDuration)
    {
        if (!isInitialized || !reactToBeat)
        {
            return;
        }

        // Skip direct beat reaction for BounceTileReactive type
        if (animationType == EnvironmentAnimationType.BounceTileReactive)
        {
            return;
        }

        // Apply probability check
        if (Random.value > reactionProbability)
        {
            return;
        }

        // For bounce animations, only interrupt if we're not in a critical phase
        if (currentAnimation != null && isAnimating)
        {
            if (animationType == EnvironmentAnimationType.Bounce)
            {
                // For bounce animations, check if we should allow completion
                // Don't interrupt if we're in a two-beat cycle and still in the first beat
                if (twoBeatBounce)
                {
                    // Let two-beat bounces complete their cycle
                    if (debugReactions)
                    {
                        Debug.Log($"[REACTIVE ENVIRONMENT] {gameObject.name} skipping beat - two-beat bounce in progress");
                    }
                    return;
                }
                else
                {
                    // For one-beat bounces, only interrupt if the animation has been running for a reasonable time
                    // This prevents rapid fire interruptions
                    float minAnimationTime = beatDuration * 0.3f; // Allow at least 30% of beat duration
                    if (Time.time - lastBounceTime < minAnimationTime)
                    {
                        if (debugReactions)
                        {
                            Debug.Log($"[REACTIVE ENVIRONMENT] {gameObject.name} skipping beat - recent bounce still in progress");
                        }
                        return;
                    }
                }
            }
            
            // Safe to stop the current animation
            StopCoroutine(currentAnimation);
            isAnimating = false;
            currentAnimation = null;
        }

        // Choose animation based on selected type
        switch (animationType)
        {
            case EnvironmentAnimationType.Stretch:
                if (useStretchAnimation)
                {
                    currentAnimation = StartCoroutine(AnimateStretch(beatDuration));
                }
                break;

            case EnvironmentAnimationType.Bounce:
                // Update the last bounce time when starting a new bounce
                lastBounceTime = Time.time;
                currentAnimation = StartCoroutine(AnimateBounce(beatDuration));
                break;
        }
    }

    protected virtual IEnumerator AnimateStretch(float beatDuration)
    {
        isAnimating = true;

        // Get current scale as starting point (in case we're already stretched)
        Vector3 currentScale = transform.localScale;

        // Normalize stretch axis to use as a multiplier
        Vector3 normalizedStretchAxis = stretchAxis.normalized;

        // Add slight randomness to the stretch intensity for organic feel
        // Also apply the object's unique variation factor
        float effectiveIntensity = stretchIntensity * objectVariationFactor;
        float randomizedIntensity = effectiveIntensity * Random.Range(1f - animationRandomness, 1f + animationRandomness);

        // Create the target scale by applying the stretch intensity along the selected axis
        Vector3 stretchedScale = new Vector3(
            originalScale.x * (1 + (normalizedStretchAxis.x * (randomizedIntensity - 1))),
            originalScale.y * (1 + (normalizedStretchAxis.y * (randomizedIntensity - 1))),
            originalScale.z * (1 + (normalizedStretchAxis.z * (randomizedIntensity - 1)))
        );

        // If using natural rebound, prepare an "overshoot" scale
        Vector3 reboundScale = originalScale;
        if (useNaturalRebound)
        {
            // For rebound, we need to go slightly past the target in the opposite direction
            // i.e., if we're stretching up, we'll slightly compress after
            float reboundFactor = 1f - (reboundAmount * Random.Range(0.7f, 1.3f));
            reboundScale = new Vector3(
                originalScale.x * (1 + (normalizedStretchAxis.x * (1f - reboundFactor))),
                originalScale.y * (1 + (normalizedStretchAxis.y * (1f - reboundFactor))),
                originalScale.z * (1 + (normalizedStretchAxis.z * (1f - reboundFactor)))
            );
        }

        // Determine if we're going from stretched to normal or vice versa
        bool isCurrentlyStretched = Vector3.Distance(currentScale, stretchedScale) < Vector3.Distance(currentScale, originalScale);

        // Set the source and target scales based on current state
        Vector3 sourceScale = isCurrentlyStretched ? stretchedScale : originalScale;
        Vector3 targetScale = isCurrentlyStretched ? originalScale : stretchedScale;

        // Add small random offset to the animation timing for less mechanical feel
        float randomTimeOffset = beatDuration * Random.Range(-animationRandomness * 0.3f, animationRandomness * 0.3f);

        // Get next beat time for animation timing
        float nextBeatTime = MusicManager.Instance.GetNextBeatTime() + randomTimeOffset;

        // Calculate animation durations with slight randomness
        float durationRandomFactor = Random.Range(1f - animationRandomness * 0.5f, 1f + animationRandomness * 0.5f);
        float totalAnimDuration;
        float preBeatDuration;
        float postBeatDuration;

        if (twoBeatCycle)
        {
            // In two-beat mode, we stretch on first beat, then return on second beat
            totalAnimDuration = beatDuration * durationRandomFactor; // Full beat duration with randomness
            preBeatDuration = totalAnimDuration * preBeatFraction;
            postBeatDuration = totalAnimDuration - preBeatDuration;
        }
        else
        {
            // In one-beat mode, we do the full animation in one beat
            totalAnimDuration = beatDuration * animationDuration * durationRandomFactor;
            preBeatDuration = totalAnimDuration * preBeatFraction;
            postBeatDuration = totalAnimDuration - preBeatDuration;
        }

        // Calculate when to start the animation to hit the peak exactly on the beat
        float animationStartTime = nextBeatTime - preBeatDuration;

        // Wait until it's time to start the animation
        float waitTime = animationStartTime - Time.time;
        if (waitTime > 0)
        {
            yield return new WaitForSeconds(waitTime);
        }

        // Animation Phase 1: First transition
        float startTimePhase1 = Time.time;
        float endTimePhase1 = nextBeatTime;

        while (Time.time < endTimePhase1)
        {
            float progress = Mathf.InverseLerp(startTimePhase1, endTimePhase1, Time.time);

            // Apply a slightly modified easing for more organic movement
            // This combines the animation curve with a sine wave for subtle variation
            float easedProgress = animationCurve.Evaluate(progress);
            if (animationRandomness > 0)
            {
                // Add subtle sine wave overlay for more natural motion
                float sineWave = Mathf.Sin(progress * Mathf.PI * 2) * animationRandomness * 0.15f;
                easedProgress = Mathf.Clamp01(easedProgress + sineWave);
            }

            // Apply scale transformation
            transform.localScale = Vector3.Lerp(sourceScale, targetScale, easedProgress);

            yield return null;
        }

        // Ensure we reach the exact target scale at the beat
        transform.localScale = targetScale;

        // If using natural rebound, add a small rebound animation
        if (useNaturalRebound && !twoBeatCycle) // Only in one-beat mode
        {
            // Add a short rebound phase
            float reboundDuration = beatDuration * 0.15f; // Brief rebound
            float startRebound = Time.time;
            float endRebound = startRebound + reboundDuration;

            // Animate to the rebound scale
            while (Time.time < endRebound)
            {
                float progress = Mathf.InverseLerp(startRebound, endRebound, Time.time);
                // Use a sin curve for the rebound motion
                float reboundEase = Mathf.Sin(progress * Mathf.PI);

                transform.localScale = Vector3.Lerp(targetScale, reboundScale, reboundEase);

                yield return null;
            }
        }

        // If using two-beat cycle, wait for next beat before returning
        if (twoBeatCycle)
        {
            // Wait for the next beat to occur
            float timeToNextBeat = MusicManager.Instance.GetNextBeatTime() - Time.time;

            if (timeToNextBeat > 0)
            {
                yield return new WaitForSeconds(timeToNextBeat);
            }

            // Swap source and target for the return journey
            Vector3 tempScale = sourceScale;
            sourceScale = targetScale;
            targetScale = tempScale;

            // Animation Phase 2: Second transition
            float startTimePhase2 = Time.time;
            float endTimePhase2 = startTimePhase2 + beatDuration;

            while (Time.time < endTimePhase2)
            {
                float progress = Mathf.InverseLerp(startTimePhase2, endTimePhase2, Time.time);

                // Apply slightly modified easing
                float easedProgress = animationCurve.Evaluate(progress);
                if (animationRandomness > 0)
                {
                    // Add subtle sine wave overlay
                    float sineWave = Mathf.Sin(progress * Mathf.PI * 2) * animationRandomness * 0.15f;
                    easedProgress = Mathf.Clamp01(easedProgress + sineWave);
                }

                // Apply scale transformation
                transform.localScale = Vector3.Lerp(sourceScale, targetScale, easedProgress);

                yield return null;
            }

            // Add rebound at the end of two-beat cycle if enabled
            if (useNaturalRebound)
            {
                // Add a short rebound phase
                float reboundDuration = beatDuration * 0.15f; // Brief rebound
                float startRebound = Time.time;
                float endRebound = startRebound + reboundDuration;

                // Animate to the rebound scale
                while (Time.time < endRebound)
                {
                    float progress = Mathf.InverseLerp(startRebound, endRebound, Time.time);
                    // Use a sin curve for the rebound motion
                    float reboundEase = Mathf.Sin(progress * Mathf.PI);

                    transform.localScale = Vector3.Lerp(targetScale, reboundScale, reboundEase);

                    yield return null;
                }
            }
        }
        else // One-beat cycle
        {
            // Animation Phase 2: Return journey
            float startTimePhase2 = Time.time;
            float endTimePhase2 = startTimePhase2 + postBeatDuration;

            while (Time.time < endTimePhase2)
            {
                float progress = Mathf.InverseLerp(startTimePhase2, endTimePhase2, Time.time);

                // Apply slightly modified easing for more organic movement
                float easedProgress = animationCurve.Evaluate(progress);
                if (animationRandomness > 0)
                {
                    // Add subtle sine wave overlay
                    float sineWave = Mathf.Sin(progress * Mathf.PI) * animationRandomness * 0.15f;
                    easedProgress = Mathf.Clamp01(easedProgress + sineWave);
                }

                // If we started stretched, we go normal then back to stretched
                // If we started normal, we go stretched then back to normal
                transform.localScale = Vector3.Lerp(targetScale, sourceScale, easedProgress);

                yield return null;
            }
        }

        // Ensure we end at the right scale (same as what we started with)
        transform.localScale = sourceScale;

        isAnimating = false;
    }

    protected virtual IEnumerator AnimateBounce(float beatDuration)
    {
        isAnimating = true;

        // Store original position and rotation
        Vector3 startPos = transform.localPosition;
        Quaternion startRot = transform.localRotation;
        Vector3 startScale = transform.localScale;

        // Apply the object's unique variation factor to bounce height and rotation
        float effectiveHeight = bounceHeight * objectVariationFactor;
        float effectiveRotation = maxBounceRotation * objectVariationFactor;

        // Add slight randomness for organic feel
        float randomizedHeight = effectiveHeight * Random.Range(1f - animationRandomness, 1f + animationRandomness);
        float randomizedRotation = effectiveRotation * Random.Range(0.7f, 1.3f);

        // Calculate target positions and rotations
        Vector3 peakPos = new Vector3(
            originalPosition.x,
            originalPosition.y + randomizedHeight,
            originalPosition.z
        );

        // Normalize rotation axis
        Vector3 normalizedRotAxis = rotationAxis.normalized;

        // Create target rotation (tilted during bounce)
        Quaternion peakRot = Quaternion.AngleAxis(randomizedRotation, normalizedRotAxis) * originalRotation;

        // Calculate squashed scale for landing
        Vector3 squashedScale = originalScale;
        if (squashOnLand)
        {
            squashedScale = new Vector3(
                originalScale.x * (1f + (1f - squashFactor) * 0.5f),
                originalScale.y * squashFactor,
                originalScale.z * (1f + (1f - squashFactor) * 0.5f)
            );
        }

        float randomTimeOffset = beatDuration * Random.Range(-animationRandomness * 0.2f, animationRandomness * 0.2f); // Reduced randomness
        
        // Get next beat time for animation timing - with better null check
        float nextBeatTime;
        if (MusicManager.Instance != null)
        {
            nextBeatTime = MusicManager.Instance.GetNextBeatTime() + randomTimeOffset;
        }
        else
        {
            nextBeatTime = Time.time + beatDuration + randomTimeOffset;
        }

        if (twoBeatBounce)
        {
            // === TWO BEAT CYCLE ===
            
            float durationRandomFactor = Random.Range(1f - animationRandomness * 0.3f, 1f + animationRandomness * 0.3f); // Reduced range
            float riseAnimDuration = beatDuration * durationRandomFactor;
            float preBeatRiseDuration = riseAnimDuration * preBeatFraction;

            float animationStartTime = nextBeatTime - preBeatRiseDuration;
            float waitTime = animationStartTime - Time.time;
            
            if (waitTime > 0 && waitTime < beatDuration * 2f) // Don't wait longer than 2 beats
            {
                yield return new WaitForSeconds(waitTime);
            }

            if (!isAnimating)
            {
                yield break;
            }

            // Animation Phase 1: Rise up to the first beat (ground to peak)
            float startTimeRise = Time.time;
            float endTimeRise = nextBeatTime;
            float maxRiseTime = beatDuration * 1.5f; // Safety limit

            while (Time.time < endTimeRise && Time.time - startTimeRise < maxRiseTime && isAnimating)
            {
                float progress = Mathf.InverseLerp(startTimeRise, endTimeRise, Time.time);
                float easedProgress = bounceCurve.Evaluate(progress);

                transform.localPosition = Vector3.Lerp(originalPosition, peakPos, easedProgress);
                transform.localRotation = Quaternion.Slerp(originalRotation, peakRot, easedProgress);

                yield return null;
            }

            if (!isAnimating)
            {
                yield break;
            }

            // Ensure we reach the exact peak position at the beat
            transform.localPosition = peakPos;
            transform.localRotation = peakRot;

            // Hold near the peak until the next beat
            float secondBeatTime;
            if (MusicManager.Instance != null)
            {
                secondBeatTime = MusicManager.Instance.GetNextBeatTime();
            }
            else
            {
                secondBeatTime = nextBeatTime + beatDuration;
            }
            
            float timeToNextBeat = secondBeatTime - Time.time;

            if (timeToNextBeat > 0 && timeToNextBeat < beatDuration * 1.5f && isAnimating) // Safety limits
            {
                float holdStartTime = Time.time;
                float holdEndTime = secondBeatTime - (beatDuration * 0.1f);

                while (Time.time < holdEndTime && isAnimating)
                {
                    float holdProgress = Mathf.InverseLerp(holdStartTime, holdEndTime, Time.time);
                    float hoverOffset = Mathf.Sin(holdProgress * Mathf.PI * 2) * 0.05f * randomizedHeight;

                    transform.localPosition = new Vector3(
                        peakPos.x,
                        peakPos.y + hoverOffset,
                        peakPos.z
                    );

                    yield return null;
                }

                if (!isAnimating)
                {
                    yield break;
                }

                // Fall phase to the second beat
                float fallStartTime = Time.time;
                float fallEndTime = secondBeatTime;
                float maxFallTime = beatDuration; // Safety limit

                while (Time.time < fallEndTime && Time.time - fallStartTime < maxFallTime && isAnimating)
                {
                    float fallProgress = Mathf.InverseLerp(fallStartTime, fallEndTime, Time.time);
                    float easedFallProgress = bounceCurve.Evaluate(fallProgress);

                    transform.localPosition = Vector3.Lerp(peakPos, originalPosition, easedFallProgress);
                    transform.localRotation = Quaternion.Slerp(peakRot, originalRotation, easedFallProgress);

                    yield return null;
                }
            }

            if (!isAnimating)
            {
                yield break;
            }

            // Ensure we're at ground level
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;

            if (squashOnLand && isAnimating)
            {
                float squashDuration = beatDuration * 0.2f;
                float startSquash = Time.time;
                float endSquash = startSquash + squashDuration;

                while (Time.time < endSquash && isAnimating)
                {
                    float progress = Mathf.InverseLerp(startSquash, endSquash, Time.time);
                    
                    float squashProgress;
                    if (progress < 0.3f)
                    {
                        squashProgress = progress / 0.3f;
                    }
                    else
                    {
                        squashProgress = 1f - ((progress - 0.3f) / 0.7f);
                    }

                    transform.localScale = Vector3.Lerp(originalScale, squashedScale, squashProgress);
                    yield return null;
                }

                // Ensure we end at the original scale
                if (isAnimating)
                {
                    transform.localScale = originalScale;
                }
            }
        }
        else
        {
            // === ONE BEAT CYCLE ===
            
            float durationRandomFactor = Random.Range(1f - animationRandomness * 0.3f, 1f + animationRandomness * 0.3f);
            float totalAnimDuration = beatDuration * animationDuration * durationRandomFactor;
            float preBeatDuration = totalAnimDuration * preBeatFraction;
            float postBeatDuration = totalAnimDuration - preBeatDuration;

            float animationStartTime = nextBeatTime - preBeatDuration;
            float waitTime = animationStartTime - Time.time;
            
            if (waitTime > 0 && waitTime < beatDuration) // Don't wait longer than 1 beat
            {
                yield return new WaitForSeconds(waitTime);
            }

            if (!isAnimating)
            {
                yield break;
            }

            // Animation Phase 1: Rise up to the beat (ground to peak)
            float startTimePhase1 = Time.time;
            float endTimePhase1 = nextBeatTime;
            float maxPhase1Time = beatDuration; // Safety limit

            while (Time.time < endTimePhase1 && Time.time - startTimePhase1 < maxPhase1Time && isAnimating)
            {
                float progress = Mathf.InverseLerp(startTimePhase1, endTimePhase1, Time.time);
                float easedProgress = bounceCurve.Evaluate(progress);

                transform.localPosition = Vector3.Lerp(originalPosition, peakPos, easedProgress);
                transform.localRotation = Quaternion.Slerp(originalRotation, peakRot, easedProgress);

                yield return null;
            }

            if (!isAnimating)
            {
                yield break;
            }

            // Ensure we reach the exact peak position and rotation at the beat
            transform.localPosition = peakPos;
            transform.localRotation = peakRot;

            // Animation Phase 2: Fall back down (peak to ground)
            float startTimePhase2 = Time.time;
            float endTimePhase2 = startTimePhase2 + postBeatDuration * 0.8f;

            while (Time.time < endTimePhase2 && isAnimating)
            {
                float progress = Mathf.InverseLerp(startTimePhase2, endTimePhase2, Time.time);
                float easedProgress = bounceCurve.Evaluate(1f - progress); // Inverse for the way down

                transform.localPosition = Vector3.Lerp(peakPos, originalPosition, easedProgress);
                transform.localRotation = Quaternion.Slerp(peakRot, originalRotation, easedProgress);

                yield return null;
            }

            if (!isAnimating)
            {
                yield break;
            }

            // Ensure we reach the ground
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;

            if (squashOnLand && isAnimating)
            {
                float squashDuration = postBeatDuration * 0.2f;
                float startSquash = Time.time;
                float endSquash = startSquash + squashDuration;

                while (Time.time < endSquash && isAnimating)
                {
                    float progress = Mathf.InverseLerp(startSquash, endSquash, Time.time);
                    
                    float squashProgress;
                    if (progress < 0.3f)
                    {
                        squashProgress = progress / 0.3f;
                    }
                    else
                    {
                        squashProgress = 1f - ((progress - 0.3f) / 0.7f);
                    }

                    transform.localScale = Vector3.Lerp(originalScale, squashedScale, squashProgress);
                    yield return null;
                }

                // Ensure we end at the original scale
                if (isAnimating)
                {
                    transform.localScale = originalScale;
                }
            }
        }

        isAnimating = false;
        currentAnimation = null;
    }

    protected virtual IEnumerator AnimateTileReactiveBounce(float duration, float tileMovementAmount)
    {
        isAnimating = true;

        if (debugReactions)
        {
            Debug.Log($"[REACTIVE ENVIRONMENT] {gameObject.name} starting tile reactive bounce animation. Movement amount: {tileMovementAmount}");
        }

        // Store original position and rotation
        Vector3 startPos = transform.localPosition;
        Quaternion startRot = transform.localRotation;
        Vector3 startScale = transform.localScale;

        // Apply the object's unique variation factor to bounce height and rotation
        float effectiveHeight = bounceHeight * objectVariationFactor;
        float effectiveRotation = maxBounceRotation * objectVariationFactor;

        // Create bounce-specific variations based on the bounce counter
        // This ensures each bounce has a different feel

        // Generate pseudo-random but deterministic values based on object ID, bounce counter and time
        float bounceVariation = (bounceCounter * 7919 + uniqueObjectID * 104729) % 1000 / 1000.0f;
        float timeOffset = Mathf.Sin(Time.time * 0.1f + uniqueObjectID) * 0.5f + 0.5f;

        // Create unique values for this specific bounce
        float variationHeight = 1.0f + (bounceVariation - 0.5f) * heightVariationPerBounce * 2.0f;
        float timingMod = 1.0f + (timeOffset - 0.5f) * timingVariation * 2.0f;
        float rotationMod = 1.0f + (bounceVariation - 0.5f) * rotationVariation * 2.0f;

        // Create a unique rotation axis for this bounce that's different from the default
        Vector3 thisBounceRotationAxis = new Vector3(
            uniqueRotationAxis.x + (bounceVariation - 0.5f) * axisVariation,
            uniqueRotationAxis.y + ((bounceCounter * 13) % 100 / 100.0f - 0.5f) * axisVariation,
            uniqueRotationAxis.z + ((Time.time * 7) % 1 - 0.5f) * axisVariation
        ).normalized;

        // Scale bounce height based on how much the tile moved
        float heightScale = Mathf.Clamp01(tileMovementAmount / (tileMovementThreshold * 3));
        float adjustedHeight = effectiveHeight * variationHeight * (0.7f + heightScale * 0.6f);

        // Add slight randomness for organic feel
        float randomizedHeight = adjustedHeight * Random.Range(1f - animationRandomness * 0.5f, 1f + animationRandomness * 0.5f);
        float randomizedRotation = effectiveRotation * rotationMod * Random.Range(0.7f, 1.3f) * heightScale;

        // Create lateral offsets to add wobble to the path
        float lateralOffsetX = Mathf.Sin(bounceCounter * 0.7f) * pathWobble * 0.05f;
        float lateralOffsetZ = Mathf.Cos(bounceCounter * 1.3f) * pathWobble * 0.05f;

        // Calculate target positions and rotations with wobble
        Vector3 peakPos = new Vector3(
            originalPosition.x + lateralOffsetX,
            originalPosition.y + randomizedHeight,
            originalPosition.z + lateralOffsetZ
        );

        // Create target rotation for this specific bounce
        Quaternion peakRot = Quaternion.AngleAxis(randomizedRotation, thisBounceRotationAxis) * originalRotation;

        // Adjust squash effect for this bounce
        float squashVariation = 0.9f + (bounceVariation * 0.2f);

        // Calculate squashed scale for landing with variation
        Vector3 squashedScale = originalScale;
        if (squashOnLand)
        {
            // Make squash vary by bounce
            float effectiveSquashFactor = squashFactor * squashVariation;
            effectiveSquashFactor = Mathf.Clamp(effectiveSquashFactor, 0.5f, 0.95f);

            // Each bounce squashes differently
            squashedScale = new Vector3(
                originalScale.x * (1f + (1f - effectiveSquashFactor) * 0.5f),
                originalScale.y * effectiveSquashFactor,
                originalScale.z * (1f + (1f - effectiveSquashFactor) * 0.5f)
            );
        }

        // Apply timing variation for this bounce
        float modifiedDuration = duration * timingMod;

        // Rise up phase - with varied timing
        float riseTime = modifiedDuration * (0.35f + bounceVariation * 0.1f); // Rise time varies between bounces
        float elapsedTime = 0f;

        while (elapsedTime < riseTime)
        {
            float t = elapsedTime / riseTime;

            // Create a unique easing for this bounce
            float easedT;
            if (bounceCounter % 3 == 0) {
                // Sine-based easing
                easedT = Mathf.Sin(t * Mathf.PI * 0.5f);
            } else if (bounceCounter % 3 == 1) {
                // Bounce curve easing
                easedT = bounceCurve.Evaluate(t);
            } else {
                // Cubic easing
                easedT = t * t * (3f - 2f * t);
            }

            // Apply position and rotation changes - add some wobble to the path
            float wobbleX = Mathf.Sin(t * Mathf.PI * 2f + Time.time) * pathWobble * 0.02f;
            float wobbleZ = Mathf.Cos(t * Mathf.PI * 3f + Time.time) * pathWobble * 0.02f;

            Vector3 currentPos = Vector3.Lerp(startPos, peakPos, easedT);
            currentPos.x += wobbleX;
            currentPos.z += wobbleZ;

            transform.localPosition = currentPos;
            transform.localRotation = Quaternion.Slerp(startRot, peakRot, easedT);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure we reach the peak
        transform.localPosition = peakPos;
        transform.localRotation = peakRot;

        // Hold at peak for a moment - varies per bounce
        float holdTime = modifiedDuration * (0.15f + bounceVariation * 0.1f);

        // Add a little hovering effect during the hold
        float holdStartTime = Time.time;
        float holdEndTime = holdStartTime + holdTime;

        while (Time.time < holdEndTime)
        {
            float holdProgress = Mathf.InverseLerp(holdStartTime, holdEndTime, Time.time);

            // Create a gentle floating effect
            float hoverOffset = Mathf.Sin(holdProgress * Mathf.PI * 2) * 0.03f * randomizedHeight;
            float wobbleX = Mathf.Sin(holdProgress * Mathf.PI * 3f) * pathWobble * 0.01f;
            float wobbleZ = Mathf.Cos(holdProgress * Mathf.PI * 2f) * pathWobble * 0.01f;

            // Apply subtle hovering motion
            transform.localPosition = new Vector3(
                peakPos.x + wobbleX,
                peakPos.y + hoverOffset,
                peakPos.z + wobbleZ
            );

            yield return null;
        }

        // Fall down phase - slightly faster for more cartoon-like effect, varies by bounce
        float fallTime = modifiedDuration * (0.25f + bounceVariation * 0.1f);
        elapsedTime = 0f;

        while (elapsedTime < fallTime)
        {
            float t = elapsedTime / fallTime;

            // Create a unique easing for the fall of this bounce
            float easedT;
            if (bounceCounter % 4 == 0) {
                // Quick start, slow end
                easedT = 1f - Mathf.Pow(1f - t, 2f);
            } else if (bounceCounter % 4 == 1) {
                // Bounce curve inversion for fall
                easedT = bounceCurve.Evaluate(1f - t);
            } else if (bounceCounter % 4 == 2) {
                // Linear fall
                easedT = 1f - t;
            } else {
                // Accelerating fall
                easedT = 1f - (t * t);
            }

            // Apply position and rotation changes with subtle path variation
            float wobbleX = Mathf.Sin(t * Mathf.PI * 2f + Time.time * 0.7f) * pathWobble * 0.015f;
            float wobbleZ = Mathf.Cos(t * Mathf.PI * 1.5f + Time.time * 0.9f) * pathWobble * 0.015f;

            Vector3 currentPos = Vector3.Lerp(originalPosition, peakPos, easedT);
            currentPos.x += wobbleX;
            currentPos.z += wobbleZ;

            transform.localPosition = currentPos;
            transform.localRotation = Quaternion.Slerp(originalRotation, peakRot, easedT);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure we reach the ground
        transform.localPosition = originalPosition;
        transform.localRotation = originalRotation;

        // Add squash effect if enabled - with variations
        if (squashOnLand)
        {
            float squashTime = modifiedDuration * (0.08f + bounceVariation * 0.04f);
            elapsedTime = 0f;

            while (elapsedTime < squashTime)
            {
                float t = elapsedTime / squashTime;

                // Squash and then unsquash - faster in, slower out - varies by bounce
                float squashT;
                if (bounceCounter % 2 == 0) {
                    // Standard squash easing
                    if (t < 0.3f) {
                        squashT = t / 0.3f; // Fast ease in
                    } else {
                        squashT = 1f - ((t - 0.3f) / 0.7f); // Slower ease out
                    }
                } else {
                    // Alternate squash easing
                    if (t < 0.4f) {
                        squashT = Mathf.Sin(t / 0.4f * Mathf.PI * 0.5f);
                    } else {
                        squashT = Mathf.Cos((t - 0.4f) / 0.6f * Mathf.PI * 0.5f);
                    }
                }

                transform.localScale = Vector3.Lerp(originalScale, squashedScale, squashT);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Ensure we end at the original scale
            transform.localScale = originalScale;
        }

        isAnimating = false;

        if (debugReactions)
        {
            Debug.Log($"[REACTIVE ENVIRONMENT] {gameObject.name} finished tile reactive bounce animation");
        }
    }

    protected virtual void HandleMusicStateChange(string newState)
    {
        // Reset animation if music state changes
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            isAnimating = false;

            // Reset to original state
            transform.localScale = originalScale;
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;

            // Reset tile reactive state
            isWaitingForTileDown = false;
            hasTileMovedUp = false;
        }
    }

    public virtual void ResetToOriginalState()
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }

        transform.localScale = originalScale;
        transform.localPosition = originalPosition;
        transform.localRotation = originalRotation;

        isAnimating = false;
        isWaitingForTileDown = false;
        hasTileMovedUp = false;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleBeat;
            MusicManager.Instance.OnMusicStateChanged -= HandleMusicStateChange;
        }

        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }
    }

    #region Helper Methods for Odin Inspector
    // Used for showing/hiding stretch animation properties
    protected bool IsStretchAnimation()
    {
        return animationType == EnvironmentAnimationType.Stretch;
    }

    // Used for showing/hiding bounce animation properties
    protected bool IsBounceAnimation()
    {
        return animationType == EnvironmentAnimationType.Bounce;
    }

    // Used for showing/hiding tile reactive animation properties
    protected bool IsBounceTileReactiveAnimation()
    {
        return animationType == EnvironmentAnimationType.BounceTileReactive;
    }
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Test Beat Reaction")]
    protected virtual void TestBeatReaction()
    {
        if (!isInitialized)
        {
            InitializeReactiveState();
        }

        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }

        switch (animationType)
        {
            case EnvironmentAnimationType.Stretch:
                if (useStretchAnimation)
                {
                    currentAnimation = StartCoroutine(AnimateStretch(0.5f));
                }
                break;
            case EnvironmentAnimationType.Bounce:
                currentAnimation = StartCoroutine(AnimateBounce(0.5f));
                break;
            case EnvironmentAnimationType.BounceTileReactive:
                bounceCounter++; // Increment bounce counter for testing
                currentAnimation = StartCoroutine(AnimateTileReactiveBounce(tileReactiveAnimationDuration, 0.2f));
                break;
        }
    }

    [ContextMenu("Reset To Original State")]
    protected virtual void EditorResetState()
    {
        ResetToOriginalState();
    }

    [ContextMenu("Reset Tile Reactive Flags")]
    protected virtual void ResetTileReactiveFlags()
    {
        isWaitingForTileDown = false;
        hasTileMovedUp = false;
        isAnimating = false;

        Debug.Log($"[REACTIVE ENVIRONMENT] {gameObject.name} tile reactive flags reset via editor menu");
    }

    [ContextMenu("Test Organic Bounce Variations")]
    protected virtual void TestOrganicBounceVariations()
    {
        if (!isInitialized)
        {
            InitializeReactiveState();
        }

        if (animationType != EnvironmentAnimationType.BounceTileReactive)
        {
            Debug.LogWarning("This test is for BounceTileReactive animation type only!");
            return;
        }

        StartCoroutine(TestMultipleBounces());
    }

    private IEnumerator TestMultipleBounces()
    {
        for (int i = 0; i < 5; i++)
        {
            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
            }

            bounceCounter++;
            Debug.Log($"Testing bounce #{bounceCounter}");
            currentAnimation = StartCoroutine(AnimateTileReactiveBounce(tileReactiveAnimationDuration, 0.2f));

            // Wait for animation to complete plus a small delay
            yield return new WaitForSeconds(tileReactiveAnimationDuration + 0.3f);
        }
    }
#endif
}

