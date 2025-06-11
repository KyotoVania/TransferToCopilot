using UnityEngine;
using System.Collections;

public class ConditionalRiser : MonoBehaviour, IScenarioTriggerable
{
    [Header("Configuration de Position et Animation")]
    [Tooltip("De combien l'objet doit descendre initialement par rapport à sa position Y de départ.")]
    public float teleportDownAmount = 10f;
    [Tooltip("Hauteur supplémentaire au-dessus de la position Y d'origine pendant l'animation de montée.")]
    public float riseExtraHeight = 2f;

    [Tooltip("Nombre de battements musicaux pour l'animation de montée vers le pic.")]
    public int riseToPeakBeats = 4;
    [Tooltip("Nombre de battements musicaux pour l'animation de descente vers la position d'origine.")]
    public int settleToOriginalBeats = 3;

    [Header("Configuration du Wobble (Oscillation après chaque pas)")]
    [Tooltip("Amplitude verticale du wobble.")]
    public float wobbleAmount = 0.1f;
    [Tooltip("Durée totale en secondes d'un cycle de wobble (doit être < durée d'un battement).")]
    public float wobbleDurationSeconds = 0.25f;
    [Tooltip("Courbe pour l'effet de wobble.")]
    public AnimationCurve wobbleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Durées de Secours")]
    [Tooltip("Durée de secours en secondes pour la montée si MusicManager n'est pas disponible.")]
    public float fallbackRiseDurationSeconds = 1.5f;
    [Tooltip("Durée de secours en secondes pour la descente si MusicManager n'est pas disponible.")]
    public float fallbackSettleDurationSeconds = 1.0f;

    private Vector3 _originalWorldPosition;
    private bool _isAnimating = false;
    private Coroutine _animationCoroutine;
    private bool _beatReceivedForStep;
    private System.Action<float> _onBeatAction;

    void Awake()
    {
        // On sauvegarde la position finale désirée et on se place en position basse.
        _originalWorldPosition = transform.position;
        Vector3 lowerPosition = _originalWorldPosition - new Vector3(0, teleportDownAmount, 0);
        transform.position = lowerPosition;
        Debug.Log($"[{gameObject.name}] Initialized. Original Y: {_originalWorldPosition.y}. Teleported down to Y: {transform.position.y}");

        _onBeatAction = (_) => _beatReceivedForStep = true;
    }

    /// <summary>
    /// C'est la méthode que le LevelScenarioManager va appeler.
    /// </summary>
    public void TriggerAction()
    {
        if (!_isAnimating)
        {
            _isAnimating = true;
            if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
            _animationCoroutine = StartCoroutine(AnimateRiseAndSettleByBeats());
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] A déjà une animation en cours, appel de TriggerAction ignoré.");
        }
    }

    // La coroutine d'animation reste la même que dans votre script original.
    // Je la replace ici pour que le script soit complet.
    IEnumerator AnimateRiseAndSettleByBeats()
    {
        Debug.Log($"[{gameObject.name}] Starting BEAT-BASED rise and settle animation.");
        _isAnimating = true;

        Vector3 startRisePosition = transform.position;
        Vector3 peakTargetPosition = new Vector3(_originalWorldPosition.x, _originalWorldPosition.y + riseExtraHeight, _originalWorldPosition.z);
        Vector3 finalSettlePosition = _originalWorldPosition;

        float musicBeatDuration = fallbackRiseDurationSeconds / Mathf.Max(1, riseToPeakBeats);

        if (MusicManager.Instance != null)
        {
            float tempBeatDur = MusicManager.Instance.GetBeatDuration();
            if (tempBeatDur > 0.01f) musicBeatDuration = tempBeatDur;
            else Debug.LogWarning($"[{gameObject.name}] MusicManager returned invalid beat duration ({tempBeatDur}). Using fallback logic.");
        } else {
            Debug.LogWarning($"[{gameObject.name}] MusicManager.Instance not found. Using fallback timing logic.");
        }

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
                if (MusicManager.Instance != null) {
                    yield return new WaitUntil(() => _beatReceivedForStep);
                } else {
                    yield return new WaitForSeconds(musicBeatDuration);
                }
                if (!_isAnimating) yield break;

                currentStepTargetPos.y += risePerBeat;
                if (i == riseToPeakBeats - 1) currentStepTargetPos.y = peakTargetPosition.y;

                transform.position = currentStepTargetPos;
                Debug.Log($"[{gameObject.name}] Rise Step {i + 1}/{riseToPeakBeats}: Moved to Y={transform.position.y}");

                if (wobbleAmount > 0.001f && wobbleDurationSeconds > 0.01f)
                {
                    yield return StartCoroutine(PerformWobble(currentStepTargetPos));
                }
            }
            if(MusicManager.Instance != null) MusicManager.Instance.OnBeat -= _onBeatAction;
        }
        transform.position = peakTargetPosition;
        Debug.Log($"[{gameObject.name}] Reached peak position Y: {transform.position.y}");

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
                    yield return new WaitForSeconds(musicBeatDuration);
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
        transform.position = finalSettlePosition;
        Debug.Log($"[{gameObject.name}] Settled at original position Y: {transform.position.y}. Beat-based animation complete.");

        _isAnimating = false;
        _animationCoroutine = null;
    }

    private IEnumerator PerformWobble(Vector3 basePositionForWobble)
    {
        if (wobbleDurationSeconds <= 0.01f || wobbleAmount <= 0.001f) yield break;

        float elapsedTime = 0f;
        float peakTime = wobbleDurationSeconds / 2f;

        while (elapsedTime < wobbleDurationSeconds)
        {
            float yOffset;
            if (elapsedTime < peakTime)
            {
                yOffset = wobbleCurve.Evaluate(elapsedTime / peakTime) * wobbleAmount;
            }
            else
            {
                yOffset = wobbleCurve.Evaluate(1f - ((elapsedTime - peakTime) / (wobbleDurationSeconds - peakTime))) * wobbleAmount;
            }

            transform.position = new Vector3(basePositionForWobble.x, basePositionForWobble.y + yOffset, basePositionForWobble.z);

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = basePositionForWobble;
    }

    void OnDestroy()
    {
        if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
        if (MusicManager.Instance != null && _onBeatAction != null)
        {
            MusicManager.Instance.OnBeat -= _onBeatAction;
        }
    }
}