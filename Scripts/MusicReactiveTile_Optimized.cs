using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Game.Observers;
using ScriptableObjects;

/// <summary>
/// Version optimisée de MusicReactiveTile qui utilise le TileAnimationManager
/// au lieu de coroutines individuelles
/// </summary>
public class MusicReactiveTile_Optimized : Tile, IComboObserver
{
    #region Profile & State Reactivity
    [Title("Reaction Profile")]
    [SerializeField]
    [Required("A TileReactionProfile_SO must be assigned for this tile to react.")]
    private TileReactionProfile_SO reactionProfile;

    [Title("Rhythm Interaction")]
    [Tooltip("Si coché, cette tuile ne réagira PAS aux événements rythmiques.")]
    [SerializeField]
    public bool disableRhythmReactions = false;

    [Title("Music State Reactivity")]
    [SerializeField] private bool enableMusicStateReactions = true;
    [ShowIf("enableMusicStateReactions")]
    [SerializeField] private float explorationIntensityFactor = 1.0f;
    [ShowIf("enableMusicStateReactions")]
    [SerializeField] private float combatIntensityFactor = 1.2f;
    [ShowIf("enableMusicStateReactions")]
    [SerializeField] private float bossIntensityFactor = 1.4f;
    #endregion

    #region Instance Specific Settings
    [Title("Instance Specific Water Sequence")]
    [ShowIf("IsWaterTile")]
    [SerializeField, Tooltip("Sequence number for this specific water tile (0 to Total-1).")]
    private int waterSequenceNumber = 0;

    [ShowIf("IsWaterTile")]
    [SerializeField, Tooltip("Total number of unique steps in this water body's animation sequence.")]
    [MinValue(1)]
    private int waterSequenceTotal = 3;
    #endregion

    private string currentMusicStateKey = "Exploration";

    #region Private Variables
    // CHANGEMENT MAJEUR : Plus de Coroutine !
    private bool isAnimating = false;
    private float currentMovementDuration;
    private bool isReactiveStateInitialized = false;
    private List<int> activeWaveSequences = new List<int>();
    private int beatCounterForWaterWaves = 0;
    private TMPro.TextMeshPro sequenceNumberText;
    private float currentDynamicReactionProbability;
    private int lastComboThresholdReached = 0;
    private Vector3 basePositionForAnimation;
    
    // NOUVEAU : Flag pour éviter les animations multiples
    private bool animationQueued = false;
    #endregion

    #region Initialization Methods
    protected override void Start()
    {
        base.Start();
        
        basePositionForAnimation = transform.position;
        
        if (reactionProfile == null)
        {
            disableRhythmReactions = true;
        }

        ValidateProfileAssignment();
        if (reactionProfile != null)
        {
            currentDynamicReactionProbability = reactionProfile.reactionProbability;
            
            // NOUVEAU : Pré-cacher les courbes d'animation si le manager existe
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
            if(reactionProfile != null) beatCounterForWaterWaves = reactionProfile.waterBeatsBetweenWaves;
        }

        if (Application.isPlaying)
        {
            InitializeReactiveVisualState();
        }

        isReactiveStateInitialized = true;
    }
    #endregion

    #region Beat Handling - Version Optimisée
    private void HandleBeat(float beatDuration)
    {
        if (disableRhythmReactions || !isReactiveStateInitialized || reactionProfile == null)
        {
            return;
        }

        // NOUVEAU : Vérifier si une animation est déjà en cours via le manager
        if (animationQueued || isAnimating)
        {
            return; // Éviter les animations multiples
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
    /// Version optimisée de HandleGroundTileBeat qui utilise TileAnimationManager
    /// </summary>
    private void HandleGroundTileBeat_Optimized(float beatDuration)
    {
        if (reactionProfile == null || TileAnimationManager.Instance == null) return;
        
        // Vérifier la probabilité
        if (!reactionProfile.alwaysReact && Random.value > currentDynamicReactionProbability) return;
        
        // Si une animation est déjà en cours, arrêter
        if (isAnimating)
        {
            TileAnimationManager.Instance.StopTileAnimations(transform);
            isAnimating = false;
        }
        
        // Calculer la durée du mouvement
        RandomizeMovementDuration(beatDuration, reactionProfile);
        
        // Calculer la position cible
        float currentOffset = transform.position.y - basePositionForAnimation.y;
        float intensity = GetCurrentIntensityFactor();
        float targetOffset = (currentOffset >= 0f) ?
            Random.Range(reactionProfile.downMin * intensity, reactionProfile.downMax * intensity) :
            Random.Range(reactionProfile.upMin * intensity, reactionProfile.upMax * intensity);
        
        Vector3 targetPosition = basePositionForAnimation + Vector3.up * targetOffset;
        
        // NOUVEAU : Calculer la hauteur de rebond
        float traveledDistance = Mathf.Abs(targetPosition.y - transform.position.y);
        float bounceHeight = reactionProfile.bouncePercentage * traveledDistance;
        
        // NOUVEAU : Ajouter l'animation au manager centralisé
        // On passe le transform au lieu de 'this' pour éviter les problèmes de typage
        bool added = TileAnimationManager.Instance.AddGroundTileAnimation(
            transform,
            targetPosition,
            currentMovementDuration + reactionProfile.bounceDuration,
            bounceHeight
        );
        
        if (added)
        {
            isAnimating = true;
            animationQueued = true;
            
            // Planifier la fin de l'animation
            Invoke(nameof(OnAnimationComplete), currentMovementDuration + reactionProfile.bounceDuration);
        }
    }

    /// <summary>
    /// Version optimisée pour les tuiles Water
    /// </summary>
    private void HandleWaterTileBeat_Optimized(float beatDuration)
    {
        if (reactionProfile == null || TileAnimationManager.Instance == null) return;
        
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
                
                // Arrêter l'animation en cours si elle existe
                if (isAnimating)
                {
                    TileAnimationManager.Instance.StopTileAnimations(transform);
                }
                
                // Préparer les paramètres d'animation
                float intensityFactor = GetCurrentIntensityFactor();
                float currentWaterMoveHeight = reactionProfile.waterMoveHeight * intensityFactor;
                Vector3 targetPos = basePositionForAnimation + Vector3.up * currentWaterMoveHeight;
                
                // NOUVEAU : Utiliser le type d'animation Water
                bool added = TileAnimationManager.Instance.AddTileAnimation(
                    transform,
                    targetPos,
                    beatDuration * reactionProfile.waterAnimationDurationMultiplier,
                    TileAnimationManager.AnimationType.WaterWave,
                    reactionProfile.movementCurve
                );
                
                if (added)
                {
                    isAnimating = true;
                    animationQueued = true;
                    
                    // TODO: Gérer le scale séparément ou l'inclure dans TileAnimationManager
                    float duration = beatDuration * reactionProfile.waterAnimationDurationMultiplier;
                    Invoke(nameof(OnAnimationComplete), duration);
                }
                
                break;
            }
        }
    }

    /// <summary>
    /// Version optimisée pour les montagnes
    /// </summary>
    private void HandleMountainTileBeat_Optimized(float beatDuration)
    {
        if (reactionProfile == null || TileAnimationManager.Instance == null) return;
        
        if (!reactionProfile.alwaysReact && Random.value > currentDynamicReactionProbability) return;
        
        if (isAnimating)
        {
            TileAnimationManager.Instance.StopTileAnimations(transform);
        }
        
        float shakeDurationMultiplier = reactionProfile.groundAnimBeatMultiplier * 0.7f;
        float shakeDuration = beatDuration * shakeDurationMultiplier;
        
        // NOUVEAU : Ajouter comme animation de type Mountain Shake
        bool added = TileAnimationManager.Instance.AddTileAnimation(
            transform,
            transform.position, // La position cible est la même (le shake est géré différemment)
            shakeDuration,
            TileAnimationManager.AnimationType.MountainShake,
            null
        );
        
        if (added)
        {
            isAnimating = true;
            animationQueued = true;
            Invoke(nameof(OnAnimationComplete), shakeDuration);
        }
    }

    /// <summary>
    /// Callback quand une animation est terminée
    /// </summary>
    private void OnAnimationComplete()
    {
        isAnimating = false;
        animationQueued = false;
        
        // S'assurer que la tuile est revenue à sa position de base pour certains types
        if (tileType == TileType.Water || tileType == TileType.Mountain)
        {
            transform.position = basePositionForAnimation;
        }
    }
    #endregion

    #region Utility Methods (Inchangés ou légèrement modifiés)
    private void RandomizeMovementDuration(float beatDuration, TileReactionProfile_SO profile)
    {
        if (profile == null) return;
        float baseAnimDuration = beatDuration * profile.groundAnimBeatMultiplier;
        currentMovementDuration = Mathf.Clamp(
            baseAnimDuration + Random.Range(-profile.durationVariation, profile.durationVariation), 
            0.1f, 
            beatDuration * 0.95f
        );
    }

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

    public void InitializeReactiveVisualState()
    {
        if (Application.isPlaying)
        {
            // Arrêter toute animation en cours
            if (isAnimating && TileAnimationManager.Instance != null)
            {
                TileAnimationManager.Instance.StopTileAnimations(transform);
            }
            
            isAnimating = false;
            animationQueued = false;
            
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
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        
        // NOUVEAU : S'assurer d'arrêter toute animation en cours
        if (isAnimating && TileAnimationManager.Instance != null)
        {
            TileAnimationManager.Instance.StopTileAnimations(transform);
        }
        
        // Nettoyer les invokes
        CancelInvoke();
        
        if (!disableRhythmReactions)
        {
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.OnBeat -= HandleBeat;
                MusicManager.Instance.OnMusicStateChanged -= HandleMusicStateChange;
            }
            if (reactionProfile != null && ComboController.Instance != null && 
                tileType == TileType.Ground && reactionProfile.reactToCombo)
            {
                ComboController.Instance.RemoveObserver(this);
            }
        }
    }
    #endregion

    // Les autres méthodes restent identiques...
    #region Unchanged Methods
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

    public void OnComboReset()
    {
        if (disableRhythmReactions || reactionProfile == null || tileType == TileType.Ground || !reactionProfile.reactToCombo) return;
        currentDynamicReactionProbability = reactionProfile.reactionProbability;
        lastComboThresholdReached = 0;
    }

    private void HandleMusicStateChange(string newStateKey)
    {
        if (disableRhythmReactions) return;
        if (enableMusicStateReactions) currentMusicStateKey = newStateKey;
    }

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

    private bool IsWaterTile() => tileType == TileType.Water;
    private bool IsGroundTile() => tileType == TileType.Ground;
    private bool IsMountainTile() => tileType == TileType.Mountain;
    #endregion

#if UNITY_EDITOR
    [Title("Migration Tools")]
    [Button("Copy Values from Original MusicReactiveTile", ButtonSizes.Large)]
    private void CopyValuesFromOriginalTile()
    {
        // Chercher l'ancien script sur le même GameObject
        MusicReactiveTile originalTile = GetComponent<MusicReactiveTile>();
        
        if (originalTile == null)
        {
            Debug.LogError("No original MusicReactiveTile found on this GameObject!");
            return;
        }
        
        // Copier toutes les valeurs publiques et serialized
        // D'abord les valeurs de base de Tile
        this.column = originalTile.column;
        this.row = originalTile.row;
        this.state = originalTile.state;
        this.tileType = originalTile.tileType;
        
        // Ensuite les valeurs spécifiques à MusicReactiveTile via reflection
        var originalType = originalTile.GetType();
        var optimizedType = this.GetType();
        
        // Liste des champs à copier
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
        
        // Marquer l'objet comme modifié pour sauvegarder les changements
        UnityEditor.EditorUtility.SetDirty(this);
    }
    
    [Button("Remove Original Script After Migration")]
    [ShowIf("HasOriginalScript")]
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
    
    private bool HasOriginalScript()
    {
        return GetComponent<MusicReactiveTile>() != null;
    }
#endif
}