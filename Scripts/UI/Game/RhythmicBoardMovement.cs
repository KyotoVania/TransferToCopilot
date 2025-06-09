using UnityEngine;
using System.Collections;

public class RhythmicBoardMovement : MonoBehaviour
{
    [Header("Paramètres du Mouvement Rythmique")]
    [Tooltip("Amplitude du mouvement vers le haut.")]
    [SerializeField] private float upAmount = 0.05f;
    [Tooltip("Amplitude du mouvement latéral (gauche/droite).")]
    [SerializeField] private float sideAmount = 0.08f;
    [Tooltip("Amplitude maximale de la variation sur l'axe Z (profondeur).")]
    [SerializeField] private float zVariationAmount = 0.02f;
    [Tooltip("Vitesse de lissage du mouvement vers la position cible (plus petit = plus rapide).")]
    [SerializeField] private float movementSmoothTime = 0.15f;
    [Tooltip("Délai en secondes (temps réel) après l'activation de cet objet avant que l'animation rythmique ne commence. Doit être supérieur à la durée de l'animation d'entrée du board.")]
    [SerializeField] private float startRhythmicAnimationDelay = 1.5f;

    [Tooltip("Décalage en nombre de battements avant que ce board ne commence son cycle d'animation. Mettre à 0 pour un démarrage normal, 1 pour démarrer un battement plus tard, etc. Pour un cycle de 4 phases, un décalage de 2 le mettra en opposition.")]
    [SerializeField] private int startBeatOffset = 0;

    private Vector3 _initialLocalPosition;
    private Vector3 _currentTargetLocalPosition;
    private Vector3 _smoothDampVelocity;

    private int _currentAnimationPhase = 0;
    private bool _canAnimate = false;
    private Coroutine _initializationCoroutine;

    // ----- NOUVEAU CHAMP INTERNE -----
    private int _beatOffsetCounter = 0; // Compteur pour le décalage de démarrage
    private bool _offsetWaitComplete = false; // Indique si le décalage initial est terminé
    // ---------------------------------

    void Awake()
    {
        _currentTargetLocalPosition = transform.localPosition;
    }

    void OnEnable()
    {
        _canAnimate = false;
        _currentAnimationPhase = 0; // Le cycle commencera toujours par la phase 0 après le décalage
        _currentTargetLocalPosition = transform.localPosition;
        _smoothDampVelocity = Vector3.zero;

        // ----- Initialisation pour le décalage -----
        _beatOffsetCounter = 0;
        _offsetWaitComplete = (startBeatOffset <= 0); // Si pas de décalage, c'est complété d'office
        // -----------------------------------------

        if (_initializationCoroutine != null)
        {
            StopCoroutine(_initializationCoroutine);
        }
        _initializationCoroutine = StartCoroutine(DelayedInitializationAndSubscription());
    }

    IEnumerator DelayedInitializationAndSubscription()
    {
        yield return new WaitForSecondsRealtime(startRhythmicAnimationDelay);

        _initialLocalPosition = transform.localPosition;
        _currentTargetLocalPosition = _initialLocalPosition;
        _canAnimate = true; // Prêt à potentiellement animer (après le décalage de battement)

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += HandleMusicManagerBeat;
            Debug.Log($"[{gameObject.name} DelayedInit] Abonné à MusicManager.OnBeat. Offset à attendre: {startBeatOffset} battements.", this);
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] MusicManager.Instance non trouvé. RhythmicBoardMovement ne fonctionnera pas.", this);
            enabled = false;
        }
        _initializationCoroutine = null;
    }

    void OnDisable()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleMusicManagerBeat;
        }
        _canAnimate = false;
        if (_initializationCoroutine != null)
        {
            StopCoroutine(_initializationCoroutine);
            _initializationCoroutine = null;
        }
    }

    void Update()
    {
        if (!_canAnimate || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (Vector3.Distance(transform.localPosition, _currentTargetLocalPosition) > 0.001f)
        {
            transform.localPosition = Vector3.SmoothDamp(
                transform.localPosition,
                _currentTargetLocalPosition,
                ref _smoothDampVelocity,
                movementSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );
        }
    }

    private void HandleMusicManagerBeat(float beatDurationFromMusicManager)
    {
        if (!_canAnimate || !this.enabled || !this.gameObject.activeInHierarchy) return;

        // ----- GESTION DU DÉCALAGE DE DÉMARRAGE -----
        if (!_offsetWaitComplete)
        {
            _beatOffsetCounter++;
            // Debug.Log($"[{gameObject.name} HandleMusicManagerBeat] Offset en attente. Battements attendus: {_beatOffsetCounter}/{startBeatOffset}", this);
            if (_beatOffsetCounter >= startBeatOffset)
            {
                _offsetWaitComplete = true;
                _currentAnimationPhase = -1; // Pour que le premier vrai battement traité mette la phase à 0 puis action
                Debug.Log($"[{gameObject.name} HandleMusicManagerBeat] Décalage de {startBeatOffset} battements terminé. L'animation rythmique va commencer au prochain battement traité.", this);
            }
            return; // Ne pas traiter l'animation de phase tant que le décalage n'est pas terminé
        }
        // ---------------------------------------------

        // La logique d'animation de phase commence ici, une fois le décalage passé
        _currentAnimationPhase = (_currentAnimationPhase + 1) % 4;
        float randomZ = Random.Range(-zVariationAmount, zVariationAmount);

        Vector3 previousTarget = _currentTargetLocalPosition;

        switch (_currentAnimationPhase)
        {
            case 0: // Repos (après Haut-Droite ou après décalage initial)
                _currentTargetLocalPosition = _initialLocalPosition + new Vector3(0, 0, randomZ);
                break;
            case 1: // Haut-Gauche
                _currentTargetLocalPosition = _initialLocalPosition + new Vector3(-sideAmount, upAmount, randomZ);
                break;
            case 2: // Repos (après Haut-Gauche)
                _currentTargetLocalPosition = _initialLocalPosition + new Vector3(0, 0, randomZ);
                break;
            case 3: // Haut-Droite
                _currentTargetLocalPosition = _initialLocalPosition + new Vector3(sideAmount, upAmount, randomZ);
                break;
        }
        // Debug.Log($"[{gameObject.name} HandleMusicManagerBeat] (Offset Terminé) Phase: {_currentAnimationPhase}. Nouvelle Cible: {_currentTargetLocalPosition}", this);
    }
}