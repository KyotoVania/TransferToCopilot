using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Game.Observers;
using ScriptableObjects;

/// <summary>
/// An optimized version of a tile that reacts to music beats and game events.
/// It uses a central TileAnimationManager to handle animations efficiently.
/// </summary>
public class MusicReactiveTile_Optimized : Tile, IComboObserver
{
    #region Profile & State Reactivity
    [Title("Reaction Profile")]
    [SerializeField]
    [Required("A TileReactionProfile_SO must be assigned for this tile to react.")]
    /// <summary>
    /// The scriptable object profile that defines how this tile reacts to different stimuli.
    /// </summary>
    private TileReactionProfile_SO reactionProfile;

    [Title("Rhythm Interaction")]
    [Tooltip("If checked, this tile will NOT react to rhythmic events.")]
    [SerializeField]
    /// <summary>
    /// If true, all rhythm-based reactions for this tile are disabled.
    /// </summary>
    public bool disableRhythmReactions = false;

    [Title("Music State Reactivity")]
    /// <summary>
    /// Enables or disables reactions based on the current music state (e.g., Exploration, Combat).
    /// </summary>
    [SerializeField] private bool enableMusicStateReactions = true;
    /// <summary>
    /// Multiplier for reaction intensity during the 'Exploration' music state.
    /// </summary>
    [ShowIf("enableMusicStateReactions")]
    [SerializeField] private float explorationIntensityFactor = 1.0f;
    /// <summary>
    /// Multiplier for reaction intensity during the 'Combat' music state.
    /// </summary>
    [ShowIf("enableMusicStateReactions")]
    [SerializeField] private float combatIntensityFactor = 1.2f;
    /// <summary>
    /// Multiplier for reaction intensity during the 'Boss' music state.
    /// </summary>
    [ShowIf("enableMusicStateReactions")]
    [SerializeField] private float bossIntensityFactor = 1.4f;
    #endregion

    #region Instance Specific Settings
    [Title("Instance Specific Water Sequence")]
    [ShowIf("IsWaterTile")]
    [SerializeField, Tooltip("Sequence number for this specific water tile (0 to Total-1).")]
    /// <summary>
    /// The unique sequence number for this water tile within a larger body of water.
    /// </summary>
    private int waterSequenceNumber = 0;

    [ShowIf("IsWaterTile")]
    [SerializeField, Tooltip("Total number of unique steps in this water body's animation sequence.")]
    [MinValue(1)]
    /// <summary>
    /// The total number of steps in the water animation sequence for this body of water.
    /// </summary>
    private int waterSequenceTotal = 3;
    #endregion

    /// <summary>
    /// The current music state key, used to determine the intensity factor.
    /// </summary>
    private string currentMusicStateKey = "Exploration";

    #region Private Variables
    /// <summary>
    /// Flag indicating if the tile is currently animating.
    /// </summary>
    private bool isAnimating = false;
    /// <summary>
    /// The duration of the current movement animation.
    /// </summary>
    private float currentMovementDuration;
    /// <summary>
    /// Flag to check if the reactive state has been initialized.
    /// </summary>
    private bool isReactiveStateInitialized = false;
    /// <summary>
    /// A list of active wave sequences for water tiles.
    /// </summary>
    private List<int> activeWaveSequences = new List<int>();
    /// <summary>
    /// Counter for beats to trigger water waves.
    /// </summary>
    private int beatCounterForWaterWaves = 0;
    /// <summary>
    /// Text mesh component to display the water sequence number for debugging.
    /// </summary>
    private TMPro.TextMeshPro sequenceNumberText;
    /// <summary>
    /// The dynamic probability of this tile reacting to a beat.
    /// </summary>
    private float currentDynamicReactionProbability;
    /// <summary>
    /// The last combo threshold reached, used for combo-based reactions.
    /// </summary>
    private int lastComboThresholdReached = 0;
    /// <summary>
    /// The base position of the tile before any animation.
    /// </summary>
    private Vector3 basePositionForAnimation;
    /// <summary>
    /// The base scale of the tile before any animation.
    /// </summary>
    private Vector3 baseScaleForAnimation;
    /// <summary>
    /// The pending target position for a deferred water animation.
    /// </summary>
    private Vector3 pendingWaterTargetPos;
    /// <summary>
    /// The pending target scale for a deferred water animation.
    /// </summary>
    private Vector3 pendingWaterTargetScale;
    /// <summary>
    /// The pending duration for phase 1 of a deferred water animation.
    /// </summary>
    private float pendingWaterPhase1Duration;
    /// <summary>
    /// The pending duration for phase 2 of a deferred water animation.
    /// </summary>
    private float pendingWaterPhase2Duration;
    #endregion

    #region Initialization Methods
    /// <summary>
    /// Unity's Start method. Initializes the tile.
    /// </summary>
    protected override void Start()
    {
        base.Start();

        basePositionForAnimation = transform.position;
        baseScaleForAnimation = transform.localScale;

        if (reactionProfile == null)
        {
            disableRhythmReactions = true;
        }

        ValidateProfileAssignment();
        if (reactionProfile != null)
        {
            currentDynamicReactionProbability = reactionProfile.reactionProbability;

            if (TileAnimationManager.Instance != null && reactionProfile.movementCurve != null)
            {
                TileAnimationManager.Instance.CacheAnimationCurve(
                    $"TileProfile_{reactionProfile.name}",
                    reactionProfile.movementCurve
                );
            }
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
            if (reactionProfile != null) beatCounterForWaterWaves = reactionProfile.waterBeatsBetweenWaves;
        }

        if (Application.isPlaying)
        {
            InitializeReactiveVisualState();
        }

        isReactiveStateInitialized = true;
    }
    #endregion

    #region Beat Handling
    /// <summary>
    /// Handles the music beat event.
    /// </summary>
    /// <param name="beatDuration">The duration of the beat.</param>
    private void HandleBeat(float beatDuration)
    {
        if (disableRhythmReactions || !isReactiveStateInitialized || reactionProfile == null || isAnimating)
        {
            return;
        }

        switch (tileType)
        {
            case TileType.Water:
                HandleWaterTileBeat_Optimized(beatDuration);
                break;
            case TileType.Ground:
                HandleGroundTileBeat_Optimized(beatDuration);
                break;
            case TileType.Mountain:
                HandleMountainTileBeat_Optimized(beatDuration);
                break;
        }
    }

    /// <summary>
    /// Handles the beat event for a ground tile.
    /// </summary>
    /// <param name="beatDuration">The duration of the beat.</param>
    private void HandleGroundTileBeat_Optimized(float beatDuration)
    {
        if (!reactionProfile.alwaysReact && Random.value > currentDynamicReactionProbability) return;

        RandomizeMovementDuration(beatDuration, reactionProfile);
        float intensity = GetCurrentIntensityFactor();
        float targetOffset = (transform.position.y >= basePositionForAnimation.y) ?
            Random.Range(reactionProfile.downMin * intensity, reactionProfile.downMax * intensity) :
            Random.Range(reactionProfile.upMin * intensity, reactionProfile.upMax * intensity);

        Vector3 targetPosition = basePositionForAnimation + Vector3.up * targetOffset;
        float totalDuration = currentMovementDuration + reactionProfile.bounceDuration;

        isAnimating = true;
        TileAnimationManager.Instance.RequestAnimation(
            this.transform,
            totalDuration,
            OnAnimationComplete,
            targetPosition: targetPosition,
            moveCurve: reactionProfile.movementCurve
        );
    }

    /// <summary>
    /// Handles the beat event for a water tile.
    /// </summary>
    /// <param name="beatDuration">The duration of the beat.</param>
    private void HandleWaterTileBeat_Optimized(float beatDuration)
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
            if (activeWaveSequences[i] > this.waterSequenceTotal)
            {
                activeWaveSequences.RemoveAt(i);
                continue;
            }

            if (activeWaveSequences[i] - 1 == this.waterSequenceNumber)
            {
                if (!reactionProfile.alwaysReact && Random.value > currentDynamicReactionProbability) continue;

                float intensityFactor = GetCurrentIntensityFactor();
                float currentWaterMoveHeight = reactionProfile.waterMoveHeight * intensityFactor;
                Vector3 upPosition = basePositionForAnimation + Vector3.up * currentWaterMoveHeight;
                Vector3 maxScale = baseScaleForAnimation * reactionProfile.waterScaleFactor * intensityFactor;
                
                float totalAnimationDuration = beatDuration * reactionProfile.waterAnimationDurationMultiplier;
                float preBeatDuration = totalAnimationDuration * reactionProfile.preBeatFraction;
                float postBeatDuration = totalAnimationDuration - preBeatDuration;

                float nextBeatTime = Time.time + beatDuration;
                if (MusicManager.Instance != null) 
                {
                    nextBeatTime = MusicManager.Instance.GetNextBeatTime();
                }
                
                float timeUntilNextBeat = nextBeatTime - Time.time;
                
                if (timeUntilNextBeat < preBeatDuration * 0.8f && MusicManager.Instance != null) 
                {
                    nextBeatTime += beatDuration;
                }
                
                float animationStartTime = nextBeatTime - preBeatDuration;
                float delayBeforeStart = Mathf.Max(0, animationStartTime - Time.time);

                isAnimating = true;
                
                if (delayBeforeStart > 0)
                {
                    Invoke(nameof(StartWaterAnimationSequence), delayBeforeStart);
                    StoreWaterAnimationParams(upPosition, maxScale, preBeatDuration, postBeatDuration);
                }
                else
                {
                    StartWaterAnimationPhase1(upPosition, maxScale, preBeatDuration, postBeatDuration);
                }
                
                break;
            }
        }
    }

    /// <summary>
    /// Stores the parameters for a deferred water animation.
    /// </summary>
    /// <param name="targetPos">The target position.</param>
    /// <param name="targetScale">The target scale.</param>
    /// <param name="phase1Duration">The duration of phase 1.</param>
    /// <param name="phase2Duration">The duration of phase 2.</param>
    private void StoreWaterAnimationParams(Vector3 targetPos, Vector3 targetScale, float phase1Duration, float phase2Duration)
    {
        pendingWaterTargetPos = targetPos;
        pendingWaterTargetScale = targetScale;
        pendingWaterPhase1Duration = phase1Duration;
        pendingWaterPhase2Duration = phase2Duration;
    }

    /// <summary>
    /// Starts the water animation sequence using the stored parameters.
    /// </summary>
    private void StartWaterAnimationSequence()
    {
        StartWaterAnimationPhase1(pendingWaterTargetPos, pendingWaterTargetScale, pendingWaterPhase1Duration, pendingWaterPhase2Duration);
    }

    /// <summary>
    /// Starts phase 1 of the water animation (rise and scale up).
    /// </summary>
    /// <param name="upPosition">The target position.</param>
    /// <param name="maxScale">The target scale.</param>
    /// <param name="phase1Duration">The duration of this phase.</param>
    /// <param name="phase2Duration">The duration of the next phase.</param>
    private void StartWaterAnimationPhase1(Vector3 upPosition, Vector3 maxScale, float phase1Duration, float phase2Duration)
    {
        AnimationCurve phase1Curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        phase1Curve.keys = new Keyframe[] {
            new Keyframe(0f, 0f, 0f, 1.8f),
            new Keyframe(0.5f, 0.5f, 1.2f, 1.2f),
            new Keyframe(1f, 1f, 0f, 0f)
        };
        
        TileAnimationManager.Instance.RequestAnimation(
            this.transform,
            phase1Duration,
            () => OnWaterPhase1Complete(upPosition, maxScale, phase2Duration),
            targetPosition: upPosition,
            moveCurve: phase1Curve,
            targetScale: maxScale,
            scaleCurve: phase1Curve
        );
    }

    /// <summary>
    /// Called when phase 1 of the water animation is complete. Starts phase 2 (fall and scale down).
    /// </summary>
    /// <param name="currentUpPosition">The current position.</param>
    /// <param name="currentMaxScale">The current scale.</param>
    /// <param name="phase2Duration">The duration of this phase.</param>
    private void OnWaterPhase1Complete(Vector3 currentUpPosition, Vector3 currentMaxScale, float phase2Duration)
    {
        AnimationCurve phase2Curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        phase2Curve.keys = new Keyframe[] {
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.5f, 0.5f, 1.2f, 1.2f),
            new Keyframe(1f, 1f, 1.8f, 0f)
        };
        
        TileAnimationManager.Instance.RequestAnimation(
            this.transform,
            phase2Duration,
            OnAnimationComplete,
            targetPosition: basePositionForAnimation,
            moveCurve: phase2Curve,
            targetScale: baseScaleForAnimation,
            scaleCurve: phase2Curve
        );
    }

    /// <summary>
    /// Handles the beat event for a mountain tile.
    /// </summary>
    /// <param name="beatDuration">The duration of the beat.</param>
    private void HandleMountainTileBeat_Optimized(float beatDuration)
    {
        if (!reactionProfile.alwaysReact && Random.value > currentDynamicReactionProbability) return;

        float shakeDuration = beatDuration * (reactionProfile.groundAnimBeatMultiplier * 0.7f);
        float shakeIntensity = reactionProfile.mountainReactionStrength * GetCurrentIntensityFactor() * 0.02f;

        isAnimating = true;
        TileAnimationManager.Instance.RequestAnimation(
            this.transform,
            shakeDuration,
            OnAnimationComplete,
            isShake: true,
            shakeIntensity: shakeIntensity
        );
    }

    /// <summary>
    /// Reliable callback called by the TileAnimationManager when an animation is complete.
    /// </summary>
    public void OnAnimationComplete()
    {
        isAnimating = false;
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// Randomizes the duration of the movement animation.
    /// </summary>
    /// <param name="beatDuration">The duration of the beat.</param>
    /// <param name="profile">The tile reaction profile.</param>
    private void RandomizeMovementDuration(float beatDuration, TileReactionProfile_SO profile)
    {
        float baseAnimDuration = beatDuration * profile.groundAnimBeatMultiplier;
        currentMovementDuration = Mathf.Clamp(
            baseAnimDuration + Random.Range(-profile.durationVariation, profile.durationVariation),
            0.1f,
            beatDuration * 0.95f
        );
    }

    /// <summary>
    /// Gets the current intensity factor based on the music state.
    /// </summary>
    /// <returns>The intensity factor.</returns>
    private float GetCurrentIntensityFactor()
    {
        if (!enableMusicStateReactions) return 1.0f;
        switch (currentMusicStateKey.ToLower())
        {
            case "exploration": return explorationIntensityFactor;
            case "combat": return combatIntensityFactor;
            case "boss": return bossIntensityFactor;
            default: return 1.0f;
        }
    }
    
    /// <summary>
    /// Initializes the visual state of the tile.
    /// </summary>
    public void InitializeReactiveVisualState()
    {
        CancelInvoke(nameof(StartWaterAnimationSequence));
    
        if (Application.isPlaying)
        {
            if (isAnimating && TileAnimationManager.Instance != null)
            {
                TileAnimationManager.Instance.StopTileAnimations(transform);
            }
            isAnimating = false;

            if (disableRhythmReactions || reactionProfile == null)
            {
                transform.position = basePositionForAnimation;
            }
            else if (tileType == TileType.Ground)
            {
                float initialOffset = Random.Range(reactionProfile.downMin, reactionProfile.upMax);
                transform.position = basePositionForAnimation + Vector3.up * initialOffset;
            }
            else
            {
                transform.position = basePositionForAnimation;
            }
            transform.localScale = baseScaleForAnimation;
        }
    }

    /// <summary>
    /// Unity's OnDestroy method. Cleans up resources and subscriptions.
    /// </summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (isAnimating && TileAnimationManager.Instance != null)
        {
            TileAnimationManager.Instance.StopTileAnimations(transform);
        }

        CancelInvoke();

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleBeat;
            MusicManager.Instance.OnMusicStateChanged -= HandleMusicStateChange;
        }
        if (ComboController.Instance != null && reactionProfile != null && reactionProfile.reactToCombo)
        {
            ComboController.Instance.RemoveObserver(this);
        }
    }
    #endregion
    
    /// <summary>
    /// Creates the text mesh for the water sequence number.
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
            sequenceNumberText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        }
        sequenceNumberText.text = this.waterSequenceNumber.ToString();
        sequenceNumberText.gameObject.SetActive(true);
    }
    
    #region Unchanged Methods
    /// <summary>
    /// Validates that the assigned reaction profile is appropriate for this tile type.
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
    /// Called by the ComboController when the combo is updated.
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

    /// <summary>
    /// Handles the music state change event.
    /// </summary>
    /// <param name="newStateKey">The new music state key.</param>
    private void HandleMusicStateChange(string newStateKey)
    {
        if (disableRhythmReactions) return;
        if (enableMusicStateReactions) currentMusicStateKey = newStateKey;
    }

    /// <summary>
    /// Updates the tile's appearance based on its current state.
    /// </summary>
    protected override void UpdateTileAppearance()
    {
        base.UpdateTileAppearance();
        if (isAnimating && TileAnimationManager.Instance != null)
        {
            TileAnimationManager.Instance.StopTileAnimations(transform);
        }
        if (tileType == TileType.Water)
        {
            if (sequenceNumberText == null) CreateSequenceNumberText();
            else { sequenceNumberText.text = waterSequenceNumber.ToString(); sequenceNumberText.gameObject.SetActive(true); }
        }
        else if (sequenceNumberText != null) sequenceNumberText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Checks if the tile is a water tile.
    /// </summary>
    /// <returns>True if the tile is a water tile, false otherwise.</returns>
    private bool IsWaterTile() => tileType == TileType.Water;
    /// <summary>
    /// Checks if the tile is a ground tile.
    /// </summary>
    /// <returns>True if the tile is a ground tile, false otherwise.</returns>
    private bool IsGroundTile() => tileType == TileType.Ground;
    /// <summary>
    /// Checks if the tile is a mountain tile.
    /// </summary>
    /// <returns>True if the tile is a mountain tile, false otherwise.</returns>
    private bool IsMountainTile() => tileType == TileType.Mountain;
    #endregion

#if UNITY_EDITOR
    [Title("Migration Tools")]
    [Button("Copy Values from Original MusicReactiveTile", ButtonSizes.Large)]
    /// <summary>
    /// Copies values from the original MusicReactiveTile script to this one.
    /// </summary>
    private void CopyValuesFromOriginalTile()
    {
        MusicReactiveTile originalTile = GetComponent<MusicReactiveTile>();
        
        if (originalTile == null)
        {
            Debug.LogError("No original MusicReactiveTile found on this GameObject!");
            return;
        }
        
        this.column = originalTile.column;
        this.row = originalTile.row;
        this.state = originalTile.state;
        this.tileType = originalTile.tileType;
        
        var originalType = originalTile.GetType();
        var optimizedType = this.GetType();
        
        string[] fieldsToCopy = {
            "reactionProfile",
            "disableRhythmReactions",
            "enableMusicStateReactions",
            "explorationIntensityFactor",
            "combatIntensityFactor",
            "bossIntensityFactor",
            "waterSequenceNumber",
            "waterSequenceTotal"
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
                object value = originalField.GetValue(originalTile);
                optimizedField.SetValue(this, value);
            }
        }
        
        Debug.Log($"Successfully copied values from original tile at ({column}, {row})");
        
        UnityEditor.EditorUtility.SetDirty(this);
    }
    
    [Button("Remove Original Script After Migration")]
    [ShowIf("HasOriginalScript")]
    /// <summary>
    /// Removes the original MusicReactiveTile script after migration.
    /// </summary>
    private void RemoveOriginalScript()
    {
        MusicReactiveTile originalTile = GetComponent<MusicReactiveTile>();
        if (originalTile != null)
        {
            DestroyImmediate(originalTile);
            Debug.Log("Original MusicReactiveTile script removed.");
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
    }
    
    /// <summary>
    /// Checks if the original MusicReactiveTile script is present on this GameObject.
    /// </summary>
    /// <returns>True if the original script is present, false otherwise.</returns>
    private bool HasOriginalScript()
    {
        return GetComponent<MusicReactiveTile>() != null;
    }
#endif
}