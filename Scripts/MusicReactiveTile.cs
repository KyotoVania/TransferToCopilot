using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Game.Observers; 
using ScriptableObjects;

/// <summary>
/// Represents a tile in the game world that can react to music beats.
/// This is the original, non-optimized version which manages its own coroutines for animations.
/// </summary>
public class MusicReactiveTile : Tile, IComboObserver
{
    #region Profile & State Reactivity
    [Title("Reaction Profile")]
    [SerializeField]
    [Required("A TileReactionProfile_SO must be assigned for this tile to react.")]
    /// <summary>
    /// The profile defining how this tile reacts to various stimuli.
    /// </summary>
    private TileReactionProfile_SO reactionProfile;

    [Title("Rhythm Interaction")]
    [Tooltip("If checked, this tile will NOT react to rhythmic events.")]
    [SerializeField]
    /// <summary>
    /// Disables all rhythm-based reactions for this tile if set to true.
    /// </summary>
    public bool disableRhythmReactions = false;

    [Title("Music State Reactivity")]
    /// <summary>
    /// Determines if the tile's reaction intensity should change based on the music state (Exploration, Combat, etc.).
    /// </summary>
    [SerializeField] private bool enableMusicStateReactions = true;
    /// <summary>
    /// The intensity multiplier for reactions when the music state is 'Exploration'.
    /// </summary>
    [ShowIf("enableMusicStateReactions")]
    [SerializeField] private float explorationIntensityFactor = 1.0f;
    /// <summary>
    /// The intensity multiplier for reactions when the music state is 'Combat'.
    /// </summary>
    [ShowIf("enableMusicStateReactions")]
    [SerializeField] private float combatIntensityFactor = 1.2f;
    /// <summary>
    /// The intensity multiplier for reactions when the music state is 'Boss'.
    /// </summary>
    [ShowIf("enableMusicStateReactions")]
    [SerializeField] private float bossIntensityFactor = 1.4f;
    #endregion

    #region Instance Specific Settings
    [Title("Instance Specific Water Sequence")]
    [ShowIf("IsWaterTile")]
    [SerializeField, Tooltip("Sequence number for this specific water tile (0 to Total-1).")]
    /// <summary>
    /// For water tiles, this defines its order in a wave sequence.
    /// </summary>
    private int waterSequenceNumber = 0;

    [ShowIf("IsWaterTile")]
    [SerializeField, Tooltip("Total number of unique steps in this water body's animation sequence.")]
    [MinValue(1)]
    /// <summary>
    /// The total number of tiles in the wave sequence.
    /// </summary>
    private int waterSequenceTotal = 3;
    #endregion

    /// <summary>
    /// The current music state key, determining the reaction intensity.
    /// </summary>
    private string currentMusicStateKey = "Exploration";

    #region Private Variables
    /// <summary>
    /// Reference to the currently running animation coroutine.
    /// </summary>
    private Coroutine currentAnimation;
    /// <summary>
    /// Flag indicating if the tile is currently animating.
    /// </summary>
    private bool isAnimating = false;
    /// <summary>
    /// The calculated duration for the current movement animation.
    /// </summary>
    private float currentMovementDuration;
    /// <summary>
    /// Flag to ensure the tile's reactive state is initialized before it reacts.
    /// </summary>
    private bool isReactiveStateInitialized = false;
    /// <summary>
    /// List of active wave sequences for water tiles.
    /// </summary>
    private List<int> activeWaveSequences = new List<int>();
    /// <summary>
    /// A counter to trigger new water waves every few beats.
    /// </summary>
    private int beatCounterForWaterWaves = 0;
    /// <summary>
    /// The TextMeshPro component used to display the sequence number for debugging.
    /// </summary>
    private TMPro.TextMeshPro sequenceNumberText;
    /// <summary>
    /// The current probability of reacting to a beat, which can be modified by combos.
    /// </summary>
    private float currentDynamicReactionProbability;
    /// <summary>
    /// The last combo milestone reached, to avoid boosting probability multiple times for the same milestone.
    /// </summary>
    private int lastComboThresholdReached = 0;
    /// <summary>
    /// The base position of the tile, captured at Start, to ensure animations are relative to a stable point.
    /// </summary>
    private Vector3 basePositionForAnimation;
    #endregion

    #region Initialization Methods
    /// <summary>
    /// Unity's Start method, called on the frame when a script is enabled just before any of the Update methods are called the first time.
    /// </summary>
    protected override void Start()
    {
        base.Start(); 

        // Capture the current position as the base for all animations during this play session.
        basePositionForAnimation = transform.position;

        if (reactionProfile == null)
        {
            disableRhythmReactions = true; // Failsafe
        }

        ValidateProfileAssignment();
        if (reactionProfile != null)
        {
             currentDynamicReactionProbability = reactionProfile.reactionProbability;
        }

        if (!disableRhythmReactions && reactionProfile != null)
        {
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.OnBeat += HandleBeat;
                MusicManager.Instance.OnMusicStateChanged += HandleMusicStateChange;
            }
            if (tileType == TileType.Ground && reactionProfile.reactToCombo && ComboController.Instance != null)
            {
                ComboController.Instance.AddObserver(this);
            }
        }

        if (tileType == TileType.Water)
        {
            waterSequenceNumber = Mathf.Clamp(waterSequenceNumber, 0, Mathf.Max(0, waterSequenceTotal - 1));
            CreateSequenceNumberText();
            if(reactionProfile != null) beatCounterForWaterWaves = reactionProfile.waterBeatsBetweenWaves;
        }

        if (Application.isPlaying)
        {
            InitializeReactiveVisualState();
        }

        isReactiveStateInitialized = true;
    }

    /// <summary>
    /// Validates that the assigned reaction profile is suitable for this tile's type.
    /// </summary>
    private void ValidateProfileAssignment()
    {
        if (reactionProfile == null) return;
        if (reactionProfile.applicableTileType == TileReactionProfile_SO.ProfileApplicability.Generic) return;
        bool mismatch = false;
        switch (this.tileType)
        {
            case TileType.Ground: if (reactionProfile.applicableTileType != TileReactionProfile_SO.ProfileApplicability.Ground) mismatch = true; break;
            case TileType.Water: if (reactionProfile.applicableTileType != TileReactionProfile_SO.ProfileApplicability.Water) mismatch = true; break;
            case TileType.Mountain: if (reactionProfile.applicableTileType != TileReactionProfile_SO.ProfileApplicability.Mountain) mismatch = true; break;
        }
        if (mismatch) Debug.LogWarning($"[{this.name}] Mismatch: TileType '{this.tileType}', Profile for '{reactionProfile.applicableTileType}'.", this);
    }

    /// <summary>
    /// Initializes the visual state of the tile, applying an initial offset if necessary. Only applies position changes in Play mode.
    /// </summary>
    public void InitializeReactiveVisualState()
    {
        if (Application.isPlaying) // Only apply initial positioning logic in Play mode
        {
            if (disableRhythmReactions || reactionProfile == null)
            {
                transform.position = basePositionForAnimation; // Use the position captured at Start
            }
            else if (tileType == TileType.Ground)
            {
                // Apply random offset ONLY in Play mode
                float initialOffset = Random.Range(reactionProfile.downMin, reactionProfile.upMax);
                transform.position = basePositionForAnimation + Vector3.up * initialOffset;
            }
            else // Water, Mountain
            {
                transform.position = basePositionForAnimation; // Use the position captured at Start
            }
        }

        // Stop any running animation and reset the animation state (safe in both modes)
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }
        isAnimating = false;
    }
    #endregion

    #region Combo Observer Implementation
    /// <summary>
    /// Called by the ComboController when the combo count updates.
    /// </summary>
    /// <param name="newCombo">The new combo count.</param>
    public void OnComboUpdated(int newCombo)
    {
        if (disableRhythmReactions || reactionProfile == null || tileType != TileType.Ground || !reactionProfile.reactToCombo) return;
        int thresholdsReached = reactionProfile.comboThreshold > 0 ? newCombo / reactionProfile.comboThreshold : 0;
        if (thresholdsReached > lastComboThresholdReached)
        {
            lastComboThresholdReached = thresholdsReached;
            float boostPercentage = Mathf.Min(reactionProfile.comboReactionBoostPercentage * thresholdsReached, reactionProfile.maxReactionBoostPercentage);
            currentDynamicReactionProbability = Mathf.Clamp01(reactionProfile.reactionProbability * (1f + (boostPercentage / 100f)));
        }
    }

    /// <summary>
    /// Called by the ComboController when the combo is reset.
    /// </summary>
    public void OnComboReset()
    {
        if (disableRhythmReactions || reactionProfile == null || tileType == TileType.Ground || !reactionProfile.reactToCombo) return;
        currentDynamicReactionProbability = reactionProfile.reactionProbability;
        lastComboThresholdReached = 0;
    }
    #endregion

    #region Beat Handling
    /// <summary>
    /// The main handler for the OnBeat event from the MusicManager.
    /// </summary>
    /// <param name="beatDuration">The duration of the current beat.</param>
    private void HandleBeat(float beatDuration)
    {
        if (disableRhythmReactions)
        {
            if (isAnimating) { if (currentAnimation != null) StopCoroutine(currentAnimation); transform.position = basePositionForAnimation; isAnimating = false; }
            return;
        }
        if (!isReactiveStateInitialized) InitializeReactiveVisualState();
        if (reactionProfile == null) return;

        switch (tileType)
        {
            case TileType.Water: HandleWaterTileBeat(beatDuration); break;
            case TileType.Ground: HandleGroundTileBeat(beatDuration); break;
            case TileType.Mountain: HandleMountainTileBeat(beatDuration); break;
        }
    }

    /// <summary>
    /// Handles beat events specifically for Water tiles, managing wave sequences.
    /// </summary>
    /// <param name="beatDuration">The duration of the current beat.</param>
     private void HandleWaterTileBeat(float beatDuration)
    {
        if (reactionProfile == null) return;
        beatCounterForWaterWaves++;
        if (beatCounterForWaterWaves >= reactionProfile.waterBeatsBetweenWaves)
        {
            beatCounterForWaterWaves = 0;
            activeWaveSequences.Add(0);
        }
        for (int i = activeWaveSequences.Count - 1; i >= 0; i--)
        {
            activeWaveSequences[i]++;
            if (activeWaveSequences[i] > this.waterSequenceTotal) { activeWaveSequences.RemoveAt(i); continue; }
            if (activeWaveSequences[i] - 1 == this.waterSequenceNumber)
            {
                if (!reactionProfile.alwaysReact && Random.value > currentDynamicReactionProbability) continue;
                if (currentAnimation != null) StopCoroutine(currentAnimation);
                currentAnimation = StartCoroutine(AnimateWaterTile(beatDuration, reactionProfile));
                break;
            }
        }
    }

    /// <summary>
    /// Handles beat events specifically for Ground tiles.
    /// </summary>
    /// <param name="beatDuration">The duration of the current beat.</param>
    private void HandleGroundTileBeat(float beatDuration)
    {
        if (reactionProfile == null) return;
        if (!reactionProfile.alwaysReact && Random.value > currentDynamicReactionProbability) return;
        if (currentAnimation != null) { StopCoroutine(currentAnimation); isAnimating = false; }
        RandomizeMovementDuration(beatDuration, reactionProfile);
        float currentOffset = transform.position.y - basePositionForAnimation.y;
        float intensity = GetCurrentIntensityFactor();
        float targetOffset = (currentOffset >= 0f) ?
            Random.Range(reactionProfile.downMin * intensity, reactionProfile.downMax * intensity) :
            Random.Range(reactionProfile.upMin * intensity, reactionProfile.upMax * intensity);
        currentAnimation = StartCoroutine(AnimateWithBounce(targetOffset, reactionProfile));
    }

    /// <summary>
    /// Handles beat events specifically for Mountain tiles.
    /// </summary>
    /// <param name="beatDuration">The duration of the current beat.</param>
    private void HandleMountainTileBeat(float beatDuration)
    {
        if (reactionProfile == null) return;
        if (!reactionProfile.alwaysReact && Random.value > currentDynamicReactionProbability) return;
        if (currentAnimation != null) { StopCoroutine(currentAnimation); isAnimating = false; }
        float shakeDurationMultiplier = reactionProfile.groundAnimBeatMultiplier * 0.7f;
        float shakeDuration = beatDuration * shakeDurationMultiplier;
        float currentMountainReactionStrength = reactionProfile.mountainReactionStrength * GetCurrentIntensityFactor();
        currentAnimation = StartCoroutine(ShakeMountain(currentMountainReactionStrength, shakeDuration));
    }
    #endregion

    #region Animations
    /// <summary>
    /// Coroutine to animate a water tile, creating a wave effect synchronized to the beat.
    /// </summary>
    /// <param name="beatDuration">The duration of the beat to sync with.</param>
    /// <param name="profile">The reaction profile defining the animation parameters.</param>
    /// <returns>An IEnumerator for the coroutine.</returns>
    private IEnumerator AnimateWaterTile(float beatDuration, TileReactionProfile_SO profile)
    {
        if (profile == null) yield break;
        isAnimating = true;
        Vector3 currentActualPos = transform.position;
        Vector3 originalScale = transform.localScale;
        float intensityFactor = GetCurrentIntensityFactor();
        Vector3 maxScale = originalScale * profile.waterScaleFactor * intensityFactor;
        float currentWaterMoveHeight = profile.waterMoveHeight * intensityFactor;
        Vector3 upPosTarget = basePositionForAnimation + new Vector3(0, currentWaterMoveHeight, 0);

        float nextBeatTime = Time.time + beatDuration;
        if(MusicManager.Instance != null) nextBeatTime = MusicManager.Instance.GetNextBeatTime();

        float timeUntilNextBeatOnStart = nextBeatTime - Time.time;
        float totalAnimationDuration = beatDuration * profile.waterAnimationDurationMultiplier;
        float preBeatDuration = totalAnimationDuration * profile.preBeatFraction;
        float postBeatDuration = totalAnimationDuration - preBeatDuration;

        if (timeUntilNextBeatOnStart < preBeatDuration * 0.8f && MusicManager.Instance != null) nextBeatTime += beatDuration;

        float animationStartTime = nextBeatTime - preBeatDuration;
        float waitTime = animationStartTime - Time.time;
        if (waitTime > 0) yield return new WaitForSeconds(waitTime);

        float startTimePhase1 = Time.time;
        float endTimePhase1 = nextBeatTime;
        while (Time.time < endTimePhase1)
        {
            float progress = Mathf.InverseLerp(startTimePhase1, endTimePhase1, Time.time);
            float easedProgress = Mathf.Sin(progress * Mathf.PI * 0.5f);
            transform.position = Vector3.Lerp(currentActualPos, upPosTarget, easedProgress);
            transform.localScale = Vector3.Lerp(originalScale, maxScale, easedProgress);
            yield return null;
        }
        transform.position = upPosTarget;
        transform.localScale = maxScale;

        float startTimePhase2 = Time.time;
        float endTimePhase2 = startTimePhase2 + postBeatDuration;
        currentActualPos = transform.position;
        while (Time.time < endTimePhase2)
        {
            float progress = Mathf.InverseLerp(startTimePhase2, endTimePhase2, Time.time);
            float easedProgress = 1f - Mathf.Sin((1f - progress) * Mathf.PI * 0.5f);
            transform.position = Vector3.Lerp(currentActualPos, basePositionForAnimation, easedProgress);
            transform.localScale = Vector3.Lerp(maxScale, originalScale, easedProgress);
            yield return null;
        }
        transform.position = basePositionForAnimation;
        transform.localScale = originalScale;
        isAnimating = false;
    }

    /// <summary>
    /// Randomizes the movement duration based on the beat and profile settings.
    /// </summary>
    /// <param name="beatDuration">The current beat duration.</param>
    /// <param name="profile">The reaction profile.</param>
    private void RandomizeMovementDuration(float beatDuration, TileReactionProfile_SO profile)
    {
        if (profile == null) return;
        float baseAnimDuration = beatDuration * profile.groundAnimBeatMultiplier;
        currentMovementDuration = Mathf.Clamp(baseAnimDuration + Random.Range(-profile.durationVariation, profile.durationVariation), 0.1f, beatDuration * 0.95f);
    }

    /// <summary>
    /// Coroutine to animate a tile's vertical position with a bounce effect at the end.
    /// </summary>
    /// <param name="targetOffsetY">The target vertical offset from the base position.</param>
    /// <param name="profile">The reaction profile defining the animation parameters.</param>
    /// <returns>An IEnumerator for the coroutine.</returns>
    private IEnumerator AnimateWithBounce(float targetOffsetY, TileReactionProfile_SO profile)
    {
        if (profile == null) yield break;
        isAnimating = true;
        Vector3 startPos = transform.position;
        Vector3 targetPos = basePositionForAnimation + Vector3.up * targetOffsetY;
        float elapsedTime = 0f;
        while (elapsedTime < currentMovementDuration)
        {
            float t = profile.movementCurve.Evaluate(elapsedTime / currentMovementDuration);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;

        float traveledDistance = Mathf.Abs(targetPos.y - startPos.y);
        float bounceAmplitude = profile.bouncePercentage * traveledDistance;
        Vector3 bounceUpTarget = targetPos + Vector3.up * (targetOffsetY > (startPos.y - basePositionForAnimation.y) ? -bounceAmplitude : bounceAmplitude);

        yield return StartCoroutine(AnimateBounceInternal(targetPos, bounceUpTarget, profile.bounceDuration, profile));
        isAnimating = false;
    }

    /// <summary>
    /// The internal part of the bounce animation.
    /// </summary>
    /// <param name="fromPos">The starting position of the bounce.</param>
    /// <param name="bouncePeakPos">The peak position of the bounce.</param>
    /// <param name="duration">The total duration of the bounce.</param>
    /// <param name="profile">The reaction profile.</param>
    /// <returns>An IEnumerator for the coroutine.</returns>
    private IEnumerator AnimateBounceInternal(Vector3 fromPos, Vector3 bouncePeakPos, float duration, TileReactionProfile_SO profile)
    {
        if (profile == null) yield break;
        float halfDuration = duration / 2f;
        float elapsedTime = 0f;
        while (elapsedTime < halfDuration)
        {
            transform.position = Vector3.Lerp(fromPos, bouncePeakPos, profile.movementCurve.Evaluate(elapsedTime / halfDuration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        elapsedTime = 0f;
        while (elapsedTime < halfDuration)
        {
            transform.position = Vector3.Lerp(bouncePeakPos, fromPos, profile.movementCurve.Evaluate(elapsedTime / halfDuration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = fromPos;
    }

    /// <summary>
    /// Coroutine to apply a shaking effect to a mountain tile.
    /// </summary>
    /// <param name="intensity">The intensity of the shake.</param>
    /// <param name="duration">The duration of the shake.</param>
    /// <returns>An IEnumerator for the coroutine.</returns>
    private IEnumerator ShakeMountain(float intensity, float duration)
    {
        isAnimating = true;
        Vector3 actualBasePos = basePositionForAnimation;
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float xOffset = Random.Range(-1f, 1f) * 0.02f * intensity;
            float zOffset = Random.Range(-1f, 1f) * 0.02f * intensity;
            transform.position = actualBasePos + new Vector3(xOffset, 0, zOffset);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = actualBasePos;
        isAnimating = false;
    }
    #endregion

    #region Utility and State Management
    /// <summary>
    /// Handles changes in the music state, updating the current intensity key.
    /// </summary>
    /// <param name="newStateKey">The new music state key (e.g., "Exploration", "Combat").</param>
    private void HandleMusicStateChange(string newStateKey)
    {
        if (disableRhythmReactions) return;
        if (enableMusicStateReactions) currentMusicStateKey = newStateKey;
    }

    /// <summary>
    /// Gets the intensity factor based on the current music state.
    /// </summary>
    /// <returns>The intensity multiplier.</returns>
    private float GetCurrentIntensityFactor()
    {
        if (!enableMusicStateReactions) return 1.0f;
        switch (currentMusicStateKey.ToLower()) // Use ToLower() for robustness
        {
            case "exploration": return explorationIntensityFactor;
            case "combat": return combatIntensityFactor;
            case "boss": return bossIntensityFactor;
            default: return 1.0f;
        }
    }

    /// <summary>
    /// Unity's OnDestroy method, used to unsubscribe from events to prevent memory leaks.
    /// </summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (!disableRhythmReactions)
        {
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.OnBeat -= HandleBeat;
                MusicManager.Instance.OnMusicStateChanged -= HandleMusicStateChange;
            }
            if (reactionProfile != null && ComboController.Instance != null && tileType == TileType.Ground && reactionProfile.reactToCombo)
            {
                ComboController.Instance.RemoveObserver(this);
            }
        }
    }

    /// <summary>
    /// Resets the tile to its default visual and logical state.
    /// </summary>
    public void ResetToDefaultState()
    {
        if (currentAnimation != null) StopCoroutine(currentAnimation);
        if (reactionProfile != null) currentDynamicReactionProbability = reactionProfile.reactionProbability;
        lastComboThresholdReached = 0;

        isReactiveStateInitialized = false;
        if (Application.isPlaying)
        {
            transform.position = basePositionForAnimation;
        }
        InitializeReactiveVisualState();
    }

    /// <summary>
    /// Updates the tile's appearance based on its type.
    /// </summary>
    protected override void UpdateTileAppearance()
    {
        base.UpdateTileAppearance();
        if (currentAnimation != null) StopCoroutine(currentAnimation);
        if (tileType == TileType.Water)
        {
            if (sequenceNumberText == null) CreateSequenceNumberText();
            else { sequenceNumberText.text = waterSequenceNumber.ToString(); sequenceNumberText.gameObject.SetActive(true); }
        }
        else if (sequenceNumberText != null) sequenceNumberText.gameObject.SetActive(false);
    }
    #endregion

    #region Editor Specifics
    #if UNITY_EDITOR
        /// <summary>
        /// Unity's OnValidate method, called in the editor when the script is loaded or a value is changed in the Inspector.
        /// </summary>
        void OnValidate()
        {
            if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || this.gameObject == null || !this.gameObject.scene.IsValid()) return;
                
                InitializeReactiveVisualState();
                ValidateProfileAssignment();
                if (tileType == TileType.Water && GetComponentInChildren<TMPro.TextMeshPro>() != null)
                {
                    var tmp = GetComponentInChildren<TMPro.TextMeshPro>();
                    if (tmp) tmp.text = this.waterSequenceNumber.ToString();
                } else if (tileType == TileType.Water) {
                    CreateSequenceNumberText();
                }

                if (UnityEditor.SceneView.lastActiveSceneView != null) {
                    UnityEditor.SceneView.lastActiveSceneView.Repaint();
                }
            };
        }
    #endif
    #endregion

    #region Helper Methods for Odin Inspector
    /// <summary>
    /// Helper method for Odin Inspector to check if the tile type is Water.
    /// </summary>
    /// <returns>True if the tile type is Water.</returns>
    private bool IsWaterTile() => tileType == TileType.Water;
    /// <summary>
    /// Helper method for Odin Inspector to check if the tile type is Ground.
    /// </summary>
    /// <returns>True if the tile type is Ground.</returns>
    private bool IsGroundTile() => tileType == TileType.Ground;
    /// <summary>
    /// Helper method for Odin Inspector to check if the tile type is Mountain.
    /// </summary>
    /// <returns>True if the tile type is Mountain.</returns>
    private bool IsMountainTile() => tileType == TileType.Mountain;
    #endregion

    #region Editor Utilities
#if UNITY_EDITOR
    /// <summary>
    /// Context menu item to increment the water sequence number.
    /// </summary>
    [ContextMenu("Increment Sequence Number")]
    private void IncrementSequenceNumber()
    {
        if (waterSequenceTotal <= 0) waterSequenceTotal = 1;
        waterSequenceNumber = (waterSequenceNumber + 1) % waterSequenceTotal;
        if (sequenceNumberText != null) sequenceNumberText.text = waterSequenceNumber.ToString();
        else CreateSequenceNumberText();
    }
    /// <summary>
    /// Context menu item to decrement the water sequence number.
    /// </summary>
    [ContextMenu("Decrement Sequence Number")]
    private void DecrementSequenceNumber()
    {
        if (waterSequenceTotal <= 0) waterSequenceTotal = 1;
        waterSequenceNumber = (waterSequenceNumber - 1 + waterSequenceTotal) % waterSequenceTotal;
        if (sequenceNumberText != null) sequenceNumberText.text = waterSequenceNumber.ToString();
        else CreateSequenceNumberText();
    }
    /// <summary>
    /// Context menu item to test the combo reaction boost.
    /// </summary>
    [ContextMenu("Test Combo Increase (Add Threshold)")]
    private void TestComboIncrease()
    {
        if (reactionProfile == null) { Debug.LogWarning($"Cannot test combo: ReactionProfile is null."); return; }
        if (!reactionProfile.reactToCombo) { Debug.LogWarning($"Cannot test combo: reactToCombo is false in profile."); return; }
        if (tileType != TileType.Ground) { Debug.LogWarning($"Cannot test combo: TileType is not Ground."); return; }

        int currentComboForTest = (lastComboThresholdReached + 1) * (reactionProfile.comboThreshold > 0 ? reactionProfile.comboThreshold : 5);
        OnComboUpdated(currentComboForTest);
        Debug.Log($"Tested combo increase to {currentComboForTest}. New dynamic probability: {currentDynamicReactionProbability}");
    }
    /// <summary>
    /// Context menu item to reset the combo reaction boost.
    /// </summary>
    [ContextMenu("Reset Combo Reaction")]
    private void TestComboReset() { OnComboReset(); Debug.Log($"Tested combo reset. Dynamic probability: {currentDynamicReactionProbability}"); }
#endif
    #endregion

    /// <summary>
    /// Creates the TextMeshPro object to display the water sequence number.
    /// </summary>
    private void CreateSequenceNumberText()
    {
        sequenceNumberText = GetComponentInChildren<TMPro.TextMeshPro>();
        if (sequenceNumberText == null)
        {
            GameObject textObject = new GameObject("SequenceNumberText");
            textObject.transform.SetParent(transform);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.localPosition = new Vector3(0, 0.05f, 0);
            rect.localRotation = Quaternion.Euler(90, 0, 0);
            rect.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            rect.sizeDelta = new Vector2(100, 20);

            sequenceNumberText = textObject.AddComponent<TMPro.TextMeshPro>();
            sequenceNumberText.alignment = TMPro.TextAlignmentOptions.Center;
            sequenceNumberText.fontSize = 10;
            sequenceNumberText.color = Color.white;
            sequenceNumberText.enableWordWrapping = false;
        }
        sequenceNumberText.text = this.waterSequenceNumber.ToString();
        sequenceNumberText.gameObject.SetActive(true);
    }
}