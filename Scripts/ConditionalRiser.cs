using UnityEngine;
using System.Collections;

/// <summary>
/// Manages the animation of an object that "rises" from a hidden position to its final position.
/// The animation is synchronized with the beats of the MusicManager and can include "wobble" effects.
/// This script implements IScenarioTriggerable to be activated by the LevelScenarioManager.
/// </summary>
public class ConditionalRiser : MonoBehaviour, IScenarioTriggerable
{
    [Header("Position and Animation Configuration")]
    /// <summary>
    /// The vertical distance (in Unity units) the object is moved down on initialization.
    /// The object will start its animation from this lowered position.
    /// </summary>
    [Tooltip("How much the object should initially move down from its starting Y position.")]
    public float teleportDownAmount = 10f;

    /// <summary>
    /// An additional height the object reaches at the peak of its rise animation before settling back to its final position.
    /// Creates an "overshoot" effect for a more dynamic animation.
    /// </summary>
    [Tooltip("Additional height above the original Y position during the rise animation.")]
    public float riseExtraHeight = 2f;

    /// <summary>
    /// The number of music beats required to complete the rise phase of the animation (from the low position to the peak).
    /// </summary>
    [Tooltip("Number of musical beats for the animation to rise to the peak.")]
    public int riseToPeakBeats = 4;

    /// <summary>
    /// The number of music beats required for the object to settle from its peak height to its final position.
    /// </summary>
    [Tooltip("Number of musical beats for the animation to settle to the original position.")]
    public int settleToOriginalBeats = 3;

    [Header("Wobble Configuration (Oscillation after each step)")]
    /// <summary>
    /// The vertical amplitude of the wobble effect that occurs after each step of the animation.
    /// </summary>
    [Tooltip("Vertical amplitude of the wobble.")]
    public float wobbleAmount = 0.1f;

    /// <summary>
    /// The total duration in seconds of a complete wobble cycle. Should be less than the duration of a beat for an optimal effect.
    /// </summary>
    [Tooltip("Total duration in seconds of a wobble cycle (should be < beat duration).")]
    public float wobbleDurationSeconds = 0.25f;

    /// <summary>
    /// The animation curve used for the wobble effect, defining its acceleration and deceleration.
    /// </summary>
    [Tooltip("Curve for the wobble effect.")]
    public AnimationCurve wobbleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Fallback Durations")]
    /// <summary>
    /// Duration of the rise animation in seconds, used if the MusicManager is not available.
    /// </summary>
    [Tooltip("Fallback duration in seconds for the rise if MusicManager is not available.")]
    public float fallbackRiseDurationSeconds = 1.5f;

    /// <summary>
    /// Duration of the settle animation in seconds, used if the MusicManager is not available.
    /// </summary>
    [Tooltip("Fallback duration in seconds for the settle if MusicManager is not available.")]
    public float fallbackSettleDurationSeconds = 1.0f;

    /// <summary>
    /// The original world position of the object, saved before the initial downward displacement.
    /// </summary>
    private Vector3 _originalWorldPosition;

    /// <summary>
    /// Indicates if an animation is currently in progress.
    /// </summary>
    private bool _isAnimating = false;

    /// <summary>
    /// Reference to the current animation coroutine, to be able to stop it if necessary.
    /// </summary>
    private Coroutine _animationCoroutine;

    /// <summary>
    /// A flag that becomes true when a music beat is received.
    /// </summary>
    private bool _beatReceivedForStep;

    /// <summary>
    /// Cached action to subscribe and unsubscribe to the OnBeat event of the MusicManager.
    /// </summary>
    private System.Action<float> _onBeatAction;

    /// <summary>
    /// Unity lifecycle method. Called on initialization.
    /// Saves the original position and moves the object to its starting (hidden) position.
    /// </summary>
    void Awake()
    {
        _originalWorldPosition = transform.position;
        Vector3 lowerPosition = _originalWorldPosition - new Vector3(0, teleportDownAmount, 0);
        transform.position = lowerPosition;
        _onBeatAction = (_) => _beatReceivedForStep = true;
    }

    /// <summary>
    /// Triggers the rise animation. This is the method called by the LevelScenarioManager.
    /// </summary>
    public void TriggerAction()
    {
        if (!_isAnimating)
        {
            _isAnimating = true;
            if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
            _animationCoroutine = StartCoroutine(AnimateRiseAndSettleByBeats());
        }
    }

    /// <summary>
    /// Main coroutine that manages the rise and settle animation, synchronized to the beats.
    /// </summary>
    /// <returns>IEnumerator for the coroutine.</returns>
    IEnumerator AnimateRiseAndSettleByBeats()
    {
        _isAnimating = true;

        Vector3 startRisePosition = transform.position;
        Vector3 peakTargetPosition = new Vector3(_originalWorldPosition.x, _originalWorldPosition.y + riseExtraHeight, _originalWorldPosition.z);
        Vector3 finalSettlePosition = _originalWorldPosition;

        float musicBeatDuration = fallbackRiseDurationSeconds / Mathf.Max(1, riseToPeakBeats);

        if (MusicManager.Instance != null)
        {
            float tempBeatDur = MusicManager.Instance.GetBeatDuration();
            if (tempBeatDur > 0.01f) musicBeatDuration = tempBeatDur;
        }

        // Rise phase
        if (riseToPeakBeats > 0)
        {
            float totalRiseHeight = peakTargetPosition.y - startRisePosition.y;
            float risePerBeat = totalRiseHeight / riseToPeakBeats;
            Vector3 currentStepTargetPos = startRisePosition;

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

                if (wobbleAmount > 0.001f && wobbleDurationSeconds > 0.01f)
                {
                    yield return StartCoroutine(PerformWobble(currentStepTargetPos));
                }
            }
            if(MusicManager.Instance != null) MusicManager.Instance.OnBeat -= _onBeatAction;
        }
        transform.position = peakTargetPosition;

        // Settle phase
        if (settleToOriginalBeats > 0)
        {
            float totalSettleHeight = peakTargetPosition.y - finalSettlePosition.y;
            float settlePerBeat = totalSettleHeight / settleToOriginalBeats;
            Vector3 currentStepTargetPos = peakTargetPosition;

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

                if (wobbleAmount > 0.001f && wobbleDurationSeconds > 0.01f)
                {
                    yield return StartCoroutine(PerformWobble(currentStepTargetPos));
                }
            }
            if(MusicManager.Instance != null) MusicManager.Instance.OnBeat -= _onBeatAction;
        }
        transform.position = finalSettlePosition;

        _isAnimating = false;
        _animationCoroutine = null;
    }

    /// <summary>
    /// Coroutine that executes a small wobble effect on the Y axis.
    /// </summary>
    /// <param name="basePositionForWobble">The base position around which the wobble should occur.</param>
    /// <returns>IEnumerator for the coroutine.</returns>
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

    /// <summary>
    /// Unity lifecycle method. Called on object destruction.
    /// Ensures that the coroutine is stopped and that the subscription to the OnBeat event is cancelled.
    /// </summary>
    void OnDestroy()
    {
        if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
        if (MusicManager.Instance != null && _onBeatAction != null)
        {
            MusicManager.Instance.OnBeat -= _onBeatAction;
        }
    }
}