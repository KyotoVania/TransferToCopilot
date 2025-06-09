// Fichier: Scripts2/MusicReactiveTile.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Game.Observers; // Assurez-vous que ce namespace existe si IComboObserver est dedans

public class MusicReactiveTile : Tile, IComboObserver
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
    private Coroutine currentAnimation;
    private bool isAnimating = false;
    private float currentMovementDuration;
    private bool isReactiveStateInitialized = false;
    private Material tileMaterial;
    private List<int> activeWaveSequences = new List<int>();
    private int beatCounterForWaterWaves = 0;
    private TMPro.TextMeshPro sequenceNumberText;
    private float currentDynamicReactionProbability;
    private int lastComboThresholdReached = 0;

    // NOUVEAU: Position de base pour les animations, capturée au Start en mode Play.
    private Vector3 basePositionForAnimation;
    #endregion

    #region Initialization Methods
    protected override void Start()
    {
        base.Start(); // Tile.Start() est maintenant plus simple

        // Capturer la position actuelle comme base pour les animations de CETTE session de jeu.
        // Cela se produit APRÈS que la tuile soit potentiellement attachée à un parent et positionnée.
        basePositionForAnimation = transform.position;
        // Debug.Log($"[{this.name}/MusicReactiveTile.Start] basePositionForAnimation capturée: {basePositionForAnimation}");

        if (reactionProfile == null)
        {
            disableRhythmReactions = true; // Sécurité
        }

        ValidateProfileAssignment();
        if (reactionProfile != null)
        {
             currentDynamicReactionProbability = reactionProfile.reactionProbability;
        }

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null) tileMaterial = renderer.material; // Utiliser .material pour obtenir une instance si des changements par tuile sont prévus

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

        // Appliquer l'état visuel initial (avec offset si Ground) UNIQUEMENT en mode Play.
        // En mode éditeur, InitializeReactiveVisualState ne modifiera plus la position.
        if (Application.isPlaying)
        {
            InitializeReactiveVisualState();
        }
        else
        {
            // En mode éditeur, on peut appeler d'autres logiques d'init visuelle qui ne touchent pas à la position.
            // Par exemple, mise à jour de matériel si nécessaire.
        }

        isReactiveStateInitialized = true;
    }

    private void ValidateProfileAssignment() // Inchangé
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

    // MODIFIÉ: Ne change la position qu'en mode Play
    public void InitializeReactiveVisualState()
    {
        if (Application.isPlaying) // N'appliquer la logique de positionnement initial qu'en mode Play
        {
            if (disableRhythmReactions || reactionProfile == null)
            {
                transform.position = basePositionForAnimation; // Utiliser la base capturée au Start
            }
            else if (tileType == TileType.Ground)
            {
                // Appliquer l'offset aléatoire UNIQUEMENT en mode Play
                float initialOffset = Random.Range(reactionProfile.downMin, reactionProfile.upMax);
                transform.position = basePositionForAnimation + Vector3.up * initialOffset;
            }
            else // Water, Mountain
            {
                transform.position = basePositionForAnimation; // Utiliser la base capturée au Start
            }
        }
        // Si !Application.isPlaying (appel potentiel depuis OnValidate), cette méthode
        // ne modifiera PLUS transform.position. La tuile conservera sa position de scène.

        // Arrêter toute animation en cours et réinitialiser l'état d'animation (ceci est sûr dans les deux modes)
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }
        isAnimating = false;
    }
    // ... (CreateSequenceNumberText reste inchangé) ...
    #endregion

    #region Combo Observer Implementation // Inchangé
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
    #endregion

    #region Beat Handling // Inchangé, mais les animations utiliseront basePositionForAnimation
    private void HandleBeat(float beatDuration)
    {
        if (disableRhythmReactions)
        {
            if (isAnimating) { if (currentAnimation != null) StopCoroutine(currentAnimation); transform.position = basePositionForAnimation; isAnimating = false; }
            return;
        }
        if (!isReactiveStateInitialized) InitializeReactiveVisualState();
        if (reactionProfile == null) return; // isBasePositionCaptured n'est plus pertinent ici

        switch (tileType)
        {
            case TileType.Water: HandleWaterTileBeat(beatDuration); break;
            case TileType.Ground: HandleGroundTileBeat(beatDuration); break;
            case TileType.Mountain: HandleMountainTileBeat(beatDuration); break;
        }
    }
    // ... (HandleWaterTileBeat, HandleGroundTileBeat, HandleMountainTileBeat restent structurellement les mêmes mais les animations internes changeront) ...
     private void HandleWaterTileBeat(float beatDuration) // Structure inchangée
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

    private void HandleGroundTileBeat(float beatDuration) // Structure inchangée
    {
        if (reactionProfile == null) return;
        if (!reactionProfile.alwaysReact && Random.value > currentDynamicReactionProbability) return;
        if (currentAnimation != null) { StopCoroutine(currentAnimation); isAnimating = false; }
        RandomizeMovementDuration(beatDuration, reactionProfile);
        float currentOffset = transform.position.y - basePositionForAnimation.y; // Changé pour utiliser basePositionForAnimation
        float intensity = GetCurrentIntensityFactor();
        float targetOffset = (currentOffset >= 0f) ?
            Random.Range(reactionProfile.downMin * intensity, reactionProfile.downMax * intensity) :
            Random.Range(reactionProfile.upMin * intensity, reactionProfile.upMax * intensity);
        currentAnimation = StartCoroutine(AnimateWithBounce(targetOffset, reactionProfile));
    }

    private void HandleMountainTileBeat(float beatDuration) // Structure inchangée
    {
        if (reactionProfile == null) return;
        if (!reactionProfile.alwaysReact && Random.value > currentDynamicReactionProbability) return;
        if (currentAnimation != null) { StopCoroutine(currentAnimation); isAnimating = false; }
        float shakeDurationMultiplier = reactionProfile.groundAnimBeatMultiplier * 0.7f; // Assurez-vous que groundAnimBeatMultiplier est pertinent ou utilisez une variable dédiée
        float shakeDuration = beatDuration * shakeDurationMultiplier;
        float currentMountainReactionStrength = reactionProfile.mountainReactionStrength * GetCurrentIntensityFactor();
        currentAnimation = StartCoroutine(ShakeMountain(currentMountainReactionStrength, shakeDuration));
    }
    #endregion

    #region Animations // Doivent maintenant utiliser basePositionForAnimation
    private IEnumerator AnimateWaterTile(float beatDuration, TileReactionProfile_SO profile)
    {
        if (profile == null) yield break;
        isAnimating = true;
        Vector3 currentActualPos = transform.position;
        Vector3 originalScale = transform.localScale;
        float intensityFactor = GetCurrentIntensityFactor();
        Vector3 maxScale = originalScale * profile.waterScaleFactor * intensityFactor;
        float currentWaterMoveHeight = profile.waterMoveHeight * intensityFactor;
        Vector3 upPosTarget = basePositionForAnimation + new Vector3(0, currentWaterMoveHeight, 0); // UTILISE basePositionForAnimation

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
            transform.position = Vector3.Lerp(currentActualPos, basePositionForAnimation, easedProgress); // RETOURNE à basePositionForAnimation
            transform.localScale = Vector3.Lerp(maxScale, originalScale, easedProgress);
            yield return null;
        }
        transform.position = basePositionForAnimation; // Assure le retour à basePositionForAnimation
        transform.localScale = originalScale;
        isAnimating = false;
    }

    private void RandomizeMovementDuration(float beatDuration, TileReactionProfile_SO profile) // Inchangé
    {
        if (profile == null) return;
        float baseAnimDuration = beatDuration * profile.groundAnimBeatMultiplier;
        currentMovementDuration = Mathf.Clamp(baseAnimDuration + Random.Range(-profile.durationVariation, profile.durationVariation), 0.1f, beatDuration * 0.95f);
    }

    private IEnumerator AnimateWithBounce(float targetOffsetY, TileReactionProfile_SO profile)
    {
        if (profile == null) yield break;
        isAnimating = true;
        Vector3 startPos = transform.position;
        Vector3 targetPos = basePositionForAnimation + Vector3.up * targetOffsetY; // UTILISE basePositionForAnimation
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
        // Le rebond se fait par rapport à targetPos, qui est déjà calculée par rapport à basePositionForAnimation
        Vector3 bounceUpTarget = targetPos + Vector3.up * (targetOffsetY > (startPos.y - basePositionForAnimation.y) ? -bounceAmplitude : bounceAmplitude);

        yield return StartCoroutine(AnimateBounceInternal(targetPos, bounceUpTarget, profile.bounceDuration, profile));
        isAnimating = false;
    }

    private IEnumerator AnimateBounceInternal(Vector3 fromPos, Vector3 bouncePeakPos, float duration, TileReactionProfile_SO profile) // Inchangé
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

    private IEnumerator ShakeMountain(float intensity, float duration)
    {
        isAnimating = true;
        Vector3 actualBasePos = basePositionForAnimation; // UTILISE basePositionForAnimation
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float xOffset = Random.Range(-1f, 1f) * 0.02f * intensity;
            float zOffset = Random.Range(-1f, 1f) * 0.02f * intensity;
            transform.position = actualBasePos + new Vector3(xOffset, 0, zOffset);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = actualBasePos; // Retourne à basePositionForAnimation
        isAnimating = false;
    }
    #endregion

    #region Utility and State Management // GetCurrentIntensityFactor et HandleMusicStateChange inchangés
    private void HandleMusicStateChange(string newStateKey)
    {
        if (disableRhythmReactions) return;
        if (enableMusicStateReactions) currentMusicStateKey = newStateKey;
    }

    private float GetCurrentIntensityFactor()
    {
        if (!enableMusicStateReactions) return 1.0f;
        switch (currentMusicStateKey.ToLower()) // Utiliser ToLower() pour la robustesse
        {
            case "exploration": return explorationIntensityFactor;
            case "combat": return combatIntensityFactor;
            case "boss": return bossIntensityFactor;
            default: return 1.0f;
        }
    }

    protected override void OnDestroy() // Inchangé
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

    public void ResetToDefaultState() // Doit maintenant utiliser basePositionForAnimation
    {
        if (currentAnimation != null) StopCoroutine(currentAnimation);
        if (reactionProfile != null) currentDynamicReactionProbability = reactionProfile.reactionProbability;
        lastComboThresholdReached = 0;

        isReactiveStateInitialized = false; // Permettre à Initialize de s'exécuter (si Start l'appelle conditionnellement)
        // Réinitialiser la position à celle capturée au début du jeu
        if (Application.isPlaying) // Ne le faire que si le jeu tourne, sinon OnValidate s'en occupe.
        {
            transform.position = basePositionForAnimation;
        }
        InitializeReactiveVisualState(); // Pour réinitialiser l'état d'animation, etc.
    }

    protected override void UpdateTileAppearance() // Inchangé
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

    #region Editor Specifics // OnValidate ne doit plus appeler InitializeReactiveVisualState pour la position
    #if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || this.gameObject == null || !this.gameObject.scene.IsValid()) return;

                // InitializeReactiveVisualState() ne modifiera plus transform.position en mode éditeur.
                // Elle peut être appelée si elle a d'autres logiques d'initialisation visuelle
                // qui sont sûres pour l'éditeur (ex: matériaux).
                // Si elle ne fait QUE gérer la position, cet appel peut être commenté/supprimé d'ici.
                // Pour l'instant, on la laisse, car elle reset isAnimating et currentAnimation.
                InitializeReactiveVisualState();

                // Si vous avez d'autres logiques dans OnValidate qui doivent s'exécuter, gardez-les.
                ValidateProfileAssignment();
                if (tileType == TileType.Water && GetComponentInChildren<TMPro.TextMeshPro>() != null) // Recréer si nécessaire
                {
                    // Mettre à jour le texte si le composant existe déjà
                    // Pourrait aussi être dans CreateSequenceNumberText, mais OnValidate est appelé plus souvent.
                    var tmp = GetComponentInChildren<TMPro.TextMeshPro>();
                    if (tmp) tmp.text = this.waterSequenceNumber.ToString();
                } else if (tileType == TileType.Water) {
                    CreateSequenceNumberText(); // S'il n'existe pas
                }


                // Forcer la mise à jour de la vue Scène peut toujours être utile
                if (UnityEditor.SceneView.lastActiveSceneView != null) {
                    UnityEditor.SceneView.lastActiveSceneView.Repaint();
                }
            };
        }
    #endif
    #endregion

    #region Helper Methods for Odin Inspector // Inchangé
    private bool IsWaterTile() => tileType == TileType.Water;
    private bool IsGroundTile() => tileType == TileType.Ground;
    private bool IsMountainTile() => tileType == TileType.Mountain;
    #endregion

    #region Editor Utilities // Inchangé
#if UNITY_EDITOR
    [ContextMenu("Increment Sequence Number")]
    private void IncrementSequenceNumber()
    {
        if (waterSequenceTotal <= 0) waterSequenceTotal = 1;
        waterSequenceNumber = (waterSequenceNumber + 1) % waterSequenceTotal;
        if (sequenceNumberText != null) sequenceNumberText.text = waterSequenceNumber.ToString();
        else CreateSequenceNumberText();
    }
    [ContextMenu("Decrement Sequence Number")]
    private void DecrementSequenceNumber()
    {
        if (waterSequenceTotal <= 0) waterSequenceTotal = 1;
        waterSequenceNumber = (waterSequenceNumber - 1 + waterSequenceTotal) % waterSequenceTotal;
        if (sequenceNumberText != null) sequenceNumberText.text = waterSequenceNumber.ToString();
        else CreateSequenceNumberText();
    }
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
    [ContextMenu("Reset Combo Reaction")]
    private void TestComboReset() { OnComboReset(); Debug.Log($"Tested combo reset. Dynamic probability: {currentDynamicReactionProbability}"); }
#endif
    #endregion

    // Ajout de CreateSequenceNumberText() comme dans la version précédente si elle avait été omise.
    private void CreateSequenceNumberText()
    {
        sequenceNumberText = GetComponentInChildren<TMPro.TextMeshPro>();
        if (sequenceNumberText == null)
        {
            GameObject textObject = new GameObject("SequenceNumberText");
            textObject.transform.SetParent(transform);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.localPosition = new Vector3(0, 0.05f, 0); // Ajuster si nécessaire
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