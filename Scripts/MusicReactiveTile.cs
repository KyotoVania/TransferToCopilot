using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Game.Observers;

public class MusicReactiveTile : Tile, IComboObserver //
{
    #region Profile & State Reactivity
    [Title("Reaction Profile")]
    [SerializeField]
    [Required("A TileReactionProfile_SO must be assigned for this tile to react.")]
    private TileReactionProfile_SO reactionProfile;

    [Title("Music State Reactivity")]
    [SerializeField] private bool enableMusicStateReactions = true;
    [ShowIf("enableMusicStateReactions")]
    [SerializeField] private float explorationIntensityFactor = 1.0f;
    [ShowIf("enableMusicStateReactions")]
    [SerializeField] private float combatIntensityFactor = 1.2f; // Valeur ajustée
    [ShowIf("enableMusicStateReactions")]
    [SerializeField] private float bossIntensityFactor = 1.4f; // Valeur ajustée
    #endregion

    #region Instance Specific Settings
    [Title("Instance Specific Water Sequence")] // Section pour les paramètres d'instance
    [ShowIf("IsWaterTile")] // Ne s'affiche que si tileType est Water
    [SerializeField, Tooltip("Sequence number for this specific water tile (0 to Total-1). Example: 0, 1, 2...")]
    private int waterSequenceNumber = 0;

    [ShowIf("IsWaterTile")]
    [SerializeField, Tooltip("Total number of unique steps in this water body's animation sequence. Example: 3 for a 0,1,2 sequence.")]
    [MinValue(1)] // Une séquence a au moins 1 étape
    private int waterSequenceTotal = 3;
    #endregion

    private string currentMusicStateKey = "Exploration";

    #region Private Variables
    private Coroutine currentAnimation;
    private bool isAnimating = false;
    private float currentMovementDuration;
    private bool isInitialized = false;
    private Material tileMaterial; // Peut être utilisé pour des effets de shader à l'avenir
    // currentWaterSequence n'est plus nécessaire car activeWaveSequences gère la progression
    private List<int> activeWaveSequences = new List<int>();
    private int beatCounterForWaterWaves = 0; // Renommé pour clarté
    private TMPro.TextMeshPro sequenceNumberText;

    private float currentDynamicReactionProbability;
    private int lastComboThresholdReached = 0;
    #endregion

    #region Initialization Methods
    protected override void Start()
    {
        base.Start();
        originalWorldPosition = transform.position;

        if (reactionProfile == null)
        {
            Debug.LogError($"[{this.name}] No TileReactionProfile_SO assigned! Tile reactions disabled.", this);
            enabled = false;
            return;
        }

        // Validation Automatique (Option B)
        ValidateProfileAssignment();

        currentDynamicReactionProbability = reactionProfile.reactionProbability;

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            tileMaterial = renderer.material;
        }

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += HandleBeat;
            MusicManager.Instance.OnMusicStateChanged += HandleMusicStateChange;
            if (enableMusicStateReactions)
            {
                // Tentative d'initialisation de l'état musical actuel
                // currentMusicStateKey = MusicManager.Instance.GetCurrentMusicStateKey(); // Nécessite une méthode sur MusicManager
            }
        }
        else
        {
            Debug.LogWarning($"[{this.name}] MusicManager.Instance not found. Music state reactions and beat sync might not work.", this);
        }

        if (tileType == TileType.Ground && reactionProfile.reactToCombo && ComboController.Instance != null)
        {
            ComboController.Instance.AddObserver(this);
        }

        if (tileType == TileType.Water)
        {
            waterSequenceNumber = Mathf.Clamp(waterSequenceNumber, 0, Mathf.Max(0, waterSequenceTotal - 1));
            CreateSequenceNumberText();
            beatCounterForWaterWaves = reactionProfile.waterBeatsBetweenWaves;
        }
        
        // S'assurer que InitializeReactiveState est appelé APRÈS que le profil soit validé et les valeurs initiales settées.
        // Si Tile.cs n'appelle pas InitializeReactiveState, il faut l'appeler ici.
        // Si MusicReactiveTile surcharge Start et que Tile.Start() fait des choses importantes avant
        // que MusicReactiveTile ait besoin de son profil, l'ordre actuel est bon.
        // Assumons que InitializeReactiveState() est appelé ici ou par une logique externe au bon moment.
        // Si vous avez une méthode Setup() ou Initialize() qui est appelée après Start, c'est aussi un bon endroit.
        // Pour l'instant, on va supposer qu'elle est appelée dans OnEnable ou la première fois que HandleBeat est appelé.
    }

    private void ValidateProfileAssignment()
    {
        if (reactionProfile.applicableTileType == TileReactionProfile_SO.ProfileApplicability.Generic)
            return; // Un profil générique est acceptable pour n'importe quel type de tuile.

        bool mismatch = false;
        switch (this.tileType)
        {
            case TileType.Ground:
                if (reactionProfile.applicableTileType != TileReactionProfile_SO.ProfileApplicability.Ground) mismatch = true;
                break;
            case TileType.Water:
                if (reactionProfile.applicableTileType != TileReactionProfile_SO.ProfileApplicability.Water) mismatch = true;
                break;
            case TileType.Mountain:
                if (reactionProfile.applicableTileType != TileReactionProfile_SO.ProfileApplicability.Mountain) mismatch = true;
                break;
        }
        if (mismatch)
        {
            Debug.LogWarning($"[{this.name}] Mismatch: TileType is '{this.tileType}' but assigned ReactionProfile ('{reactionProfile.name}') is marked for '{reactionProfile.applicableTileType}'. Tile reactions might be unintended.", this);
        }
    }
    
    public void InitializeReactiveState() // Doit être appelée après que `reactionProfile` soit défini
    {
        if (isInitialized || reactionProfile == null) return;

        switch (tileType)
        {
            case TileType.Ground:
                float initialOffset = Random.Range(reactionProfile.downMin, reactionProfile.upMax);
                transform.position = originalWorldPosition + Vector3.up * initialOffset;
                break;
            case TileType.Water:
            case TileType.Mountain:
                transform.position = originalWorldPosition;
                break;
        }
        isInitialized = true;
    }

    private void CreateSequenceNumberText()
    {
        // ... (code existant, s'assurer qu'il utilise this.waterSequenceNumber)
        sequenceNumberText = GetComponentInChildren<TMPro.TextMeshPro>();
        if (sequenceNumberText == null)
        {
            GameObject textObject = new GameObject("SequenceNumber");
            textObject.transform.SetParent(transform);
            textObject.transform.localPosition = new Vector3(0, 0.05f, 0); 
            textObject.transform.localRotation = Quaternion.Euler(90, 0, 0); 
            textObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); 
            sequenceNumberText = textObject.AddComponent<TMPro.TextMeshPro>();
            sequenceNumberText.alignment = TMPro.TextAlignmentOptions.Center;
            sequenceNumberText.fontSize = 5;
            sequenceNumberText.color = Color.white;
        }
        sequenceNumberText.text = this.waterSequenceNumber.ToString(); // Utilise le champ de l'instance
    }
    #endregion

    #region Combo Observer Implementation
    public void OnComboUpdated(int newCombo)
    {
        if (reactionProfile == null || tileType != TileType.Ground || !reactionProfile.reactToCombo) return;

        int thresholdsReached = reactionProfile.comboThreshold > 0 ? newCombo / reactionProfile.comboThreshold : 0;
        if (thresholdsReached > lastComboThresholdReached)
        {
            lastComboThresholdReached = thresholdsReached;
            float boostPercentage = Mathf.Min(reactionProfile.comboReactionBoostPercentage * thresholdsReached, reactionProfile.maxReactionBoostPercentage);
            float boostMultiplier = 1f + (boostPercentage / 100f);
            currentDynamicReactionProbability = Mathf.Clamp01(reactionProfile.reactionProbability * boostMultiplier);
        }
    }

    public void OnComboReset()
    {
        if (reactionProfile == null || tileType != TileType.Ground || !reactionProfile.reactToCombo) return;
        currentDynamicReactionProbability = reactionProfile.reactionProbability;
        lastComboThresholdReached = 0;
    }
    #endregion

    #region Beat Handling
    private void HandleBeat(float beatDuration)
    {
        if (!isInitialized) { // S'assurer que l'initialisation a eu lieu
            InitializeReactiveState();
            if(!isInitialized) return; // Si toujours pas initialisé (ex: pas de profil), sortir
        }
        if (reactionProfile == null) return;

        switch (tileType)
        {
            case TileType.Water:
                HandleWaterTileBeat(beatDuration);
                break;
            case TileType.Ground:
                HandleGroundTileBeat(beatDuration);
                break;
            case TileType.Mountain:
                HandleMountainTileBeat(beatDuration);
                break;
        }
    }

    private void HandleWaterTileBeat(float beatDuration)
    {
        if (reactionProfile == null) return;

        beatCounterForWaterWaves++;
        if (beatCounterForWaterWaves >= reactionProfile.waterBeatsBetweenWaves)
        {
            beatCounterForWaterWaves = 0;
            activeWaveSequences.Add(0); // Commence une nouvelle vague à la séquence 0
        }

        for (int i = activeWaveSequences.Count - 1; i >= 0; i--)
        {
            activeWaveSequences[i]++; // Fait avancer cette vague dans sa séquence
            // Utilise this.waterSequenceTotal (de l'instance)
            if (activeWaveSequences[i] > this.waterSequenceTotal) // > car une vague de total 3 va de 1 à 3
            {
                activeWaveSequences.RemoveAt(i);
                continue;
            }
            // Utilise this.waterSequenceNumber (de l'instance)
            if (activeWaveSequences[i] -1 == this.waterSequenceNumber) // Si c'est le tour de cette tuile dans cette vague
            {
                if (!reactionProfile.alwaysReact && Random.value > currentDynamicReactionProbability) continue;
                if (currentAnimation != null) StopCoroutine(currentAnimation);
                currentAnimation = StartCoroutine(AnimateWaterTile(beatDuration, reactionProfile));
                break; 
            }
        }
    }

    // ... (HandleGroundTileBeat, HandleMountainTileBeat, et les coroutines d'animation
    //      doivent continuer à utiliser reactionProfile pour leurs paramètres, comme précédemment)
    private void HandleGroundTileBeat(float beatDuration)
    {
        if (reactionProfile == null) return;
        if (!reactionProfile.alwaysReact && Random.value > currentDynamicReactionProbability) return;
        if (currentAnimation != null) { StopCoroutine(currentAnimation); isAnimating = false; }

        RandomizeMovementDuration(beatDuration, reactionProfile);
        float currentOffset = transform.position.y - originalWorldPosition.y;
        float intensity = GetCurrentIntensityFactor();
        float targetOffset = (currentOffset >= 0f) ?
            Random.Range(reactionProfile.downMin * intensity, reactionProfile.downMax * intensity) :
            Random.Range(reactionProfile.upMin * intensity, reactionProfile.upMax * intensity);
        currentAnimation = StartCoroutine(AnimateWithBounce(targetOffset, reactionProfile));
    }
    
    private void HandleMountainTileBeat(float beatDuration)
    {
        if (reactionProfile == null) return;
        if (!reactionProfile.alwaysReact && Random.value > currentDynamicReactionProbability) return;
        if (currentAnimation != null) { StopCoroutine(currentAnimation); isAnimating = false; }

        // Utiliser un multiplicateur du profil pour la durée du shake, par exemple
        float shakeDurationMultiplier = reactionProfile.groundAnimBeatMultiplier * 0.7f; // Ou un nouveau champ mountainShakeDurationMultiplier
        float shakeDuration = beatDuration * shakeDurationMultiplier;

        float currentMountainReactionStrength = reactionProfile.mountainReactionStrength * GetCurrentIntensityFactor();
        currentAnimation = StartCoroutine(ShakeMountain(currentMountainReactionStrength, shakeDuration));
    }
    #endregion

    #region Animations (doivent utiliser reactionProfile)

    private IEnumerator AnimateWaterTile(float beatDuration, TileReactionProfile_SO profile)
    {
        if (profile == null) yield break;
        isAnimating = true;
        Vector3 startPos = transform.position;
        Vector3 originalScale = transform.localScale;
        
        float intensityFactor = GetCurrentIntensityFactor(); // Utilise les facteurs de MusicReactiveTile
        Vector3 maxScale = originalScale * profile.waterScaleFactor * intensityFactor;
        float currentWaterMoveHeight = profile.waterMoveHeight * intensityFactor;
        // Utiliser originalWorldPosition comme base pour le mouvement vertical pour éviter la dérive si la tuile est déjà en mouvement
        Vector3 upPos = originalWorldPosition + new Vector3(0, currentWaterMoveHeight, 0);


        float nextBeatTime = MusicManager.Instance.GetNextBeatTime();
        // Recalculer timeUntilNextBeat ici car la coroutine peut avoir attendu
        float timeUntilNextBeatOnStart = nextBeatTime - Time.time;

        float totalAnimationDuration = beatDuration * profile.waterAnimationDurationMultiplier;
        float preBeatDuration = totalAnimationDuration * profile.preBeatFraction;
        float postBeatDuration = totalAnimationDuration - preBeatDuration;

        if (timeUntilNextBeatOnStart < preBeatDuration * 0.8f) // Si pas assez de temps avant le prochain beat visé
        {
            nextBeatTime += beatDuration; // Viser le beat d'après
        }
        
        // Le reste du timing doit être relatif à nextBeatTime qui est maintenant correctement ciblé
        float animationStartTime = nextBeatTime - preBeatDuration;
        float waitTime = animationStartTime - Time.time;
        if (waitTime > 0) yield return new WaitForSeconds(waitTime);

        // Phase 1: Montée
        float startTimePhase1 = Time.time; // Démarrage réel de l'animation de montée
        float endTimePhase1 = nextBeatTime; // Pic de l'animation sur le beat ciblé

        while (Time.time < endTimePhase1)
        {
            float progress = Mathf.InverseLerp(startTimePhase1, endTimePhase1, Time.time);
            float easedProgress = Mathf.Sin(progress * Mathf.PI * 0.5f); 
            transform.position = Vector3.Lerp(startPos, upPos, easedProgress);
            transform.localScale = Vector3.Lerp(originalScale, maxScale, easedProgress);
            yield return null;
        }
        transform.position = upPos;
        transform.localScale = maxScale;

        // Phase 2: Descente
        float startTimePhase2 = Time.time; // Démarrage réel de la descente (juste après le beat)
        float endTimePhase2 = startTimePhase2 + postBeatDuration;

        while (Time.time < endTimePhase2)
        {
            float progress = Mathf.InverseLerp(startTimePhase2, endTimePhase2, Time.time);
            float easedProgress = 1f - Mathf.Sin((1f - progress) * Mathf.PI * 0.5f); 
            transform.position = Vector3.Lerp(upPos, startPos, easedProgress);
            transform.localScale = Vector3.Lerp(maxScale, originalScale, easedProgress);
            yield return null;
        }
        transform.position = startPos;
        transform.localScale = originalScale;
        isAnimating = false;
    }
    
    private IEnumerator ResetWaterAmplitude(float duration) // Cette méthode semble incorrecte dans le contexte des SO
    {
        yield return new WaitForSeconds(duration * 0.5f);
        // On ne peut pas modifier reactionProfile.waterWaveAmplitude directement car c'est un asset.
        // Si un effet temporaire est désiré, il faut une variable d'instance dans MusicReactiveTile.
        Debug.LogWarning("ResetWaterAmplitude: This method needs review. Cannot modify ScriptableObject profile directly at runtime for temporary effects.");
    }

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

    private IEnumerator AnimateWithBounce(float targetOffset, TileReactionProfile_SO profile)
    {
        if (profile == null) yield break;
        isAnimating = true;
        Vector3 startPos = transform.position;
        Vector3 targetPos = originalWorldPosition + Vector3.up * targetOffset;
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
        float bounceOffsetValue = profile.bouncePercentage * traveledDistance; // Renommé pour éviter conflit
        Vector3 bounceTarget = targetPos + Vector3.up * (targetPos.y > startPos.y ? bounceOffsetValue : -bounceOffsetValue);

        yield return StartCoroutine(AnimateBounceInternal(targetPos, bounceTarget, profile.bounceDuration, profile));
        isAnimating = false;
    }

    private IEnumerator AnimateBounceInternal(Vector3 from, Vector3 bounceTarget, float duration, TileReactionProfile_SO profile)
    {
        if (profile == null) yield break;
        float halfDuration = duration / 2f;
        float elapsedTime = 0f;

        while (elapsedTime < halfDuration)
        {
            float t = elapsedTime / halfDuration;
            transform.position = Vector3.Lerp(from, bounceTarget, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        elapsedTime = 0f;
        while (elapsedTime < halfDuration)
        {
            float t = elapsedTime / halfDuration;
            transform.position = Vector3.Lerp(bounceTarget, from, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = from;
    }

    // Supprimer la version surchargée de AnimateBounce qui ne prend pas de profil
    // private IEnumerator AnimateBounce(Vector3 from, Vector3 bounceTarget, float duration) { ... }

    private IEnumerator ShakeMountain(float intensity, float duration)
    {
        isAnimating = true;
        Vector3 startPos = transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float xOffset = Random.Range(-1f, 1f) * 0.02f * intensity;
            float zOffset = Random.Range(-1f, 1f) * 0.02f * intensity;
            transform.position = startPos + new Vector3(xOffset, 0, zOffset);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = startPos;
        isAnimating = false;
    }
    #endregion

    #region Utility and State Management
    private void HandleMusicStateChange(string newStateKey)
    {
        if (enableMusicStateReactions)
        {
            currentMusicStateKey = newStateKey;
        }
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

    protected override void OnDestroy()
    {
        base.OnDestroy();
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

    public void ResetToDefaultState()
    {
        if (currentAnimation != null) StopCoroutine(currentAnimation);
        if (reactionProfile != null)
        {
             currentDynamicReactionProbability = reactionProfile.reactionProbability;
        }
        lastComboThresholdReached = 0;
        transform.position = originalWorldPosition;
        isInitialized = false;
        InitializeReactiveState();
    }
    
    protected override void UpdateTileAppearance()
    {
        base.UpdateTileAppearance();
        if (currentAnimation != null) StopCoroutine(currentAnimation);

        if (tileType == TileType.Water)
        {
            if (sequenceNumberText == null) CreateSequenceNumberText();
            else
            {
                sequenceNumberText.text = waterSequenceNumber.ToString();
                sequenceNumberText.gameObject.SetActive(true);
            }
        }
        else if (sequenceNumberText != null)
        {
            sequenceNumberText.gameObject.SetActive(false);
        }
    }
    #endregion

    #region Helper Methods for Odin Inspector
    private bool IsWaterTile() => tileType == TileType.Water; //
    private bool IsGroundTile() => tileType == TileType.Ground; //
    private bool IsMountainTile() => tileType == TileType.Mountain; //
    #endregion

    #region Editor Utilities
#if UNITY_EDITOR
    [ContextMenu("Increment Sequence Number")]
    private void IncrementSequenceNumber()
    {
        waterSequenceNumber = (waterSequenceNumber + 1) % waterSequenceTotal;
        if (sequenceNumberText != null)
        {
            sequenceNumberText.text = waterSequenceNumber.ToString();
        }
    }

    [ContextMenu("Decrement Sequence Number")]
    private void DecrementSequenceNumber()
    {
        waterSequenceNumber = (waterSequenceNumber - 1 + waterSequenceTotal) % waterSequenceTotal;
        if (sequenceNumberText != null)
        {
            sequenceNumberText.text = waterSequenceNumber.ToString();
        }
    }

    [ContextMenu("Test Combo Increase (Add 10)")]
    private void TestComboIncrease()
    {
        if (reactionProfile != null && ComboController.Instance != null && tileType == TileType.Ground && reactionProfile.reactToCombo)
        {
            OnComboUpdated((lastComboThresholdReached + 1) * reactionProfile.comboThreshold);
        }
         else if (reactionProfile == null)
        {
            Debug.LogWarning($"[{this.name}] TestComboIncrease: ReactionProfile is null. Cannot test combo reaction.");
        }
        else if (!reactionProfile.reactToCombo) //
        {
            Debug.LogWarning($"[{this.name}] TestComboIncrease: reactToCombo is false in the profile. Combo reaction is disabled for this profile.");
        }
    }

    [ContextMenu("Reset Combo Reaction")]
    private void TestComboReset()
    {
        OnComboReset();
    }
#endif
    #endregion
}