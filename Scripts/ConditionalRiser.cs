using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ConditionalRiser : MonoBehaviour
{
    [Header("Configuration des Prérequis")]
    [Tooltip("Liste des bâtiments qui doivent être détruits ou capturés par le joueur.")]
    public List<Building> prerequisiteBuildings = new List<Building>();

    [Header("Configuration de Position et Animation")]
    [Tooltip("De combien l'objet doit descendre initialement par rapport à sa position Y de départ.")]
    public float teleportDownAmount = 10f;
    [Tooltip("Hauteur supplémentaire au-dessus de la position Y d'origine pendant l'animation de montée.")]
    public float riseExtraHeight = 2f;

    [Tooltip("Nombre de battements musicaux pour l'animation de montée vers le pic.")]
    public int riseToPeakBeats = 4; // Exemple: 4 battements pour monter
    [Tooltip("Nombre de battements musicaux pour l'animation de descente vers la position d'origine.")]
    public int settleToOriginalBeats = 3; // Exemple: 3 battements pour descendre

    [Header("Configuration du Wobble (Oscillation après chaque pas)")]
    [Tooltip("Amplitude verticale du wobble.")]
    public float wobbleAmount = 0.1f;
    [Tooltip("Durée totale en secondes d'un cycle de wobble (doit être < durée d'un battement).")]
    public float wobbleDurationSeconds = 0.25f;
     [Tooltip("Courbe pour l'effet de wobble.")]
    public AnimationCurve wobbleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // Courbe pour la forme du wobble

    // Fallback durations (inchangé)
    [Tooltip("Durée de secours en secondes pour la montée si MusicManager n'est pas disponible.")]
    public float fallbackRiseDurationSeconds = 1.5f;
    [Tooltip("Durée de secours en secondes pour la descente si MusicManager n'est pas disponible.")]
    public float fallbackSettleDurationSeconds = 1.0f;


    private Vector3 _originalWorldPosition;
    private bool _conditionMet = false;
    private bool _isAnimating = false;
    private Coroutine _animationCoroutine;
    private List<Building> _buildingsToMonitor = new List<Building>();

    // Pour la synchronisation des pas avec les battements
    private bool _beatReceivedForStep;
    private System.Action<float> _onBeatAction; // Pour stocker la référence à la méthode et pouvoir se désabonner

    void Start()
    {
        _originalWorldPosition = transform.position;
        Vector3 lowerPosition = _originalWorldPosition - new Vector3(0, teleportDownAmount, 0);
        transform.position = lowerPosition;
        Debug.Log($"[{gameObject.name}] Initialized. Original Y: {_originalWorldPosition.y}. Teleported down to Y: {transform.position.y}");

        InitializeBuildingMonitoring();
        CheckAllConditionsMet();
        _onBeatAction = (_) => _beatReceivedForStep = true; // Initialisation de l'action
    }

    void InitializeBuildingMonitoring()
    {
        _buildingsToMonitor = prerequisiteBuildings.Where(b => b != null).ToList();
        if (_buildingsToMonitor.Count == 0 && prerequisiteBuildings.Count > 0)
        {
            Debug.LogWarning($"[{gameObject.name}] Tous les bâtiments dans prerequisiteBuildings sont null ou la liste est vide après filtrage.");
        }
        else if (prerequisiteBuildings.Count == 0)
        {
             Debug.LogWarning($"[{gameObject.name}] La liste prerequisiteBuildings est vide. La condition sera considérée comme remplie immédiatement.");
        }
        Building.OnBuildingDestroyed += HandleBuildingEvent;
        Building.OnBuildingTeamChangedGlobal += HandleBuildingTeamChangeEvent;
    }

    void OnDestroy()
    {
        UnsubscribeFromBuildingEvents();
        if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
        // S'assurer de se désabonner si l'objet est détruit pendant une phase d'attente de battement
        if (MusicManager.Instance != null && _beatReceivedForStep == false) // Si on attendait un beat
        {
            MusicManager.Instance.OnBeat -= _onBeatAction;
        }
    }

    void UnsubscribeFromBuildingEvents()
    {
        Building.OnBuildingDestroyed -= HandleBuildingEvent;
        Building.OnBuildingTeamChangedGlobal -= HandleBuildingTeamChangeEvent;
    }

    private void HandleBuildingEvent(Building building)
    {
        if (!_conditionMet && _buildingsToMonitor.Contains(building)) CheckAllConditionsMet();
    }

    private void HandleBuildingTeamChangeEvent(Building building, TeamType oldTeam, TeamType newTeam)
    {
        if (!_conditionMet && _buildingsToMonitor.Contains(building)) CheckAllConditionsMet();
    }

    void CheckAllConditionsMet()
    {
        if (_conditionMet || _isAnimating) return;
        if (_buildingsToMonitor.Count == 0)
        {
            TriggerRiseAnimation();
            return;
        }
        bool allSatisfied = _buildingsToMonitor.All(b => b == null || b.CurrentHealth <= 0 || (b is NeutralBuilding nb && nb.Team == TeamType.Player));
        if (allSatisfied) TriggerRiseAnimation();
    }

    private void TriggerRiseAnimation()
    {
        if (!_conditionMet && !_isAnimating)
        {
            _conditionMet = true;
            _isAnimating = true;
            UnsubscribeFromBuildingEvents();
            if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
            _animationCoroutine = StartCoroutine(AnimateRiseAndSettleByBeats()); // Nouvelle coroutine
        }
    }

    IEnumerator AnimateRiseAndSettleByBeats()
    {
        Debug.Log($"[{gameObject.name}] Starting BEAT-BASED rise and settle animation.");
        _isAnimating = true;

        Vector3 startRisePosition = transform.position; // Position abaissée actuelle
        Vector3 peakTargetPosition = new Vector3(_originalWorldPosition.x, _originalWorldPosition.y + riseExtraHeight, _originalWorldPosition.z);
        Vector3 finalSettlePosition = _originalWorldPosition;

        float musicBeatDuration = fallbackRiseDurationSeconds / Mathf.Max(1, riseToPeakBeats); // Durée par défaut si pas de MusicManager

        if (MusicManager.Instance != null)
        {
            float tempBeatDur = MusicManager.Instance.GetBeatDuration();
            if (tempBeatDur > 0.01f) musicBeatDuration = tempBeatDur;
            else Debug.LogWarning($"[{gameObject.name}] MusicManager returned invalid beat duration ({tempBeatDur}). Using fallback logic.");
        } else {
            Debug.LogWarning($"[{gameObject.name}] MusicManager.Instance not found. Using fallback timing logic.");
        }

        // --- Phase 1: Montée vers le pic, pas à pas sur les battements ---
        if (riseToPeakBeats > 0)
        {
            float totalRiseHeight = peakTargetPosition.y - startRisePosition.y;
            float risePerBeat = totalRiseHeight / riseToPeakBeats;
            Vector3 currentStepTargetPos = startRisePosition;

            Debug.Log($"[{gameObject.name}] Rise Phase: Target Y={peakTargetPosition.y}, Steps={riseToPeakBeats}, RisePerBeatY={risePerBeat}");
            if(MusicManager.Instance != null) MusicManager.Instance.OnBeat += _onBeatAction;

            for (int i = 0; i < riseToPeakBeats; i++)
            {
                _beatReceivedForStep = false;
                // Attendre le prochain battement (ou fallback si MusicManager est null)
                if (MusicManager.Instance != null) {
                    yield return new WaitUntil(() => _beatReceivedForStep);
                } else {
                    yield return new WaitForSeconds(musicBeatDuration); // Fallback timing
                }
                if (!_isAnimating) yield break; // Si l'animation a été stoppée

                currentStepTargetPos.y += risePerBeat;
                if (i == riseToPeakBeats - 1) currentStepTargetPos.y = peakTargetPosition.y; // Assurer la position exacte au dernier pas

                transform.position = currentStepTargetPos;
                Debug.Log($"[{gameObject.name}] Rise Step {i + 1}/{riseToPeakBeats}: Moved to Y={transform.position.y}");

                if (wobbleAmount > 0.001f && wobbleDurationSeconds > 0.01f)
                {
                    yield return StartCoroutine(PerformWobble(currentStepTargetPos));
                }
            }
            if(MusicManager.Instance != null) MusicManager.Instance.OnBeat -= _onBeatAction;
        }
        transform.position = peakTargetPosition; // Assurer la position de pic exacte
        Debug.Log($"[{gameObject.name}] Reached peak position Y: {transform.position.y}");

        // --- Phase 2: Descente vers la position finale, pas à pas ---
        if (settleToOriginalBeats > 0)
        {
            float totalSettleHeight = peakTargetPosition.y - finalSettlePosition.y;
            float settlePerBeat = totalSettleHeight / settleToOriginalBeats;
            Vector3 currentStepTargetPos = peakTargetPosition;

            Debug.Log($"[{gameObject.name}] Settle Phase: Target Y={finalSettlePosition.y}, Steps={settleToOriginalBeats}, SettlePerBeatY={settlePerBeat}");
            if(MusicManager.Instance != null) MusicManager.Instance.OnBeat += _onBeatAction;

            for (int i = 0; i < settleToOriginalBeats; i++)
            {
                 _beatReceivedForStep = false;
                if (MusicManager.Instance != null) {
                    yield return new WaitUntil(() => _beatReceivedForStep);
                } else {
                    yield return new WaitForSeconds(musicBeatDuration); // Fallback timing
                }
                if (!_isAnimating) yield break;

                currentStepTargetPos.y -= settlePerBeat;
                 if (i == settleToOriginalBeats - 1) currentStepTargetPos.y = finalSettlePosition.y;

                transform.position = currentStepTargetPos;
                Debug.Log($"[{gameObject.name}] Settle Step {i + 1}/{settleToOriginalBeats}: Moved to Y={transform.position.y}");

                if (wobbleAmount > 0.001f && wobbleDurationSeconds > 0.01f)
                {
                    yield return StartCoroutine(PerformWobble(currentStepTargetPos));
                }
            }
            if(MusicManager.Instance != null) MusicManager.Instance.OnBeat -= _onBeatAction;
        }
        transform.position = finalSettlePosition; // Assurer la position finale exacte
        Debug.Log($"[{gameObject.name}] Settled at original position Y: {transform.position.y}. Beat-based animation complete.");

        _isAnimating = false;
        _animationCoroutine = null;
    }

    private IEnumerator PerformWobble(Vector3 basePositionForWobble)
    {
        if (wobbleDurationSeconds <= 0.01f || wobbleAmount <= 0.001f) yield break;

        float elapsedTime = 0f;
        Vector3 startPos = basePositionForWobble; // Le wobble part de la position atteinte après le pas
        // Un wobble simple: monte, puis redescend à la position de base.
        // Le pic du wobble est à la moitié de sa durée.
        float peakTime = wobbleDurationSeconds / 2f;

        while (elapsedTime < wobbleDurationSeconds)
        {
            float t_overall = elapsedTime / wobbleDurationSeconds; // Progrès global du wobble 0..1

            // Calculer l'offset Y basé sur une courbe pour un effet de rebond/oscillation
            // Exemple : une sinusoïde qui fait un aller-retour (monte puis redescend)
            // float yOffset = Mathf.Sin(t_overall * Mathf.PI) * wobbleAmount;
            // Pour utiliser la wobbleCurve sur un cycle montée-descente:
            float yOffset;
            if (elapsedTime < peakTime) // Phase de montée du wobble
            {
                yOffset = wobbleCurve.Evaluate(elapsedTime / peakTime) * wobbleAmount;
            }
            else // Phase de descente du wobble
            {
                yOffset = wobbleCurve.Evaluate(1f - ((elapsedTime - peakTime) / (wobbleDurationSeconds - peakTime))) * wobbleAmount;
            }

            transform.position = new Vector3(basePositionForWobble.x, basePositionForWobble.y + yOffset, basePositionForWobble.z);

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = basePositionForWobble; // S'assurer qu'on termine exactement à la position de base du wobble
    }


    [ContextMenu("Debug: Force Trigger Rise Animation (Beat Based)")]
    public void Debug_ForceTriggerAnimation()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug_ForceTriggerAnimation ne peut être appelé qu'en mode Play.");
            return;
        }
        if (_isAnimating)
        {
            Debug.LogWarning($"[{gameObject.name}] Animation déjà en cours.");
            return;
        }
        Debug.Log($"[{gameObject.name}] DEBUG: Forçage de l'animation de montée (basée sur les battements) !");
        _originalWorldPosition = transform.position + new Vector3(0, teleportDownAmount, 0);
        _conditionMet = false;
        _isAnimating = false;
        TriggerRiseAnimation();
    }
}