using UnityEngine;
using System.Collections;

public class BoardEntranceAnimation : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Position locale finale du board (par rapport à son parent direct). Placez votre board à cette position dans l'éditeur. Le script s'attend à ce que la 'Rotation Locale' de cet objet soit déjà à sa valeur finale souhaitée.")]
    [SerializeField] private Vector3 finalLocalPosition = Vector3.zero;
    [Tooltip("Distance de départ SOUS sa position finale, le long de son propre axe 'bas' (défini par sa rotation finale). Mettre une valeur POSITIVE (ex: 5).")]
    [SerializeField] private float startDistanceBelow = 5f;
    [Tooltip("Durée de l'animation d'entrée en secondes.")]
    [SerializeField] private float entryDuration = 0.8f;
    [Tooltip("Délai avant de commencer l'animation (en secondes).")]
    [SerializeField] private float startDelay = 0.5f;
    [Tooltip("Si coché, l'animation se jouera automatiquement lorsque l'objet est activé.")]
    [SerializeField] private bool playOnStart = true;

    [Header("Camera Facing (Billboard)")]
    [SerializeField] private bool billboardTowardsCamera = false;
    [SerializeField] private bool billboardYAxisOnly = false;

    private Vector3 _initialLocalPositionOffScreen;
    private bool _isAnimating = false;
    private Coroutine _animationCoroutine;
    private Camera _mainCamera;

    void Awake()
    {
        if (finalLocalPosition == Vector3.zero && transform.localPosition != Vector3.zero)
        {
            finalLocalPosition = transform.localPosition;
        }

        Vector3 localDownDirectionBasedOnFinalRotation = transform.localRotation * Vector3.down;
        _initialLocalPositionOffScreen = finalLocalPosition + (localDownDirectionBasedOnFinalRotation.normalized * startDistanceBelow);

        Debug.Log($"[{gameObject.name} AWAKE] Rotation Locale Finale Attendue: {transform.localRotation.eulerAngles}. Calculated _initialLocalPositionOffScreen: {_initialLocalPositionOffScreen} (Basé sur finalLocalPos: {finalLocalPosition} et startDistanceBelow: {startDistanceBelow})", this);

        _mainCamera = Camera.main;
        if (_mainCamera == null && billboardTowardsCamera)
        {
            Debug.LogError($"[{gameObject.name}] Caméra principale non trouvée ! Le billboarding ne fonctionnera pas.", this);
            billboardTowardsCamera = false;
        }
    }

    void OnEnable()
    {
        _isAnimating = false;
        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;
        }
        transform.localPosition = _initialLocalPositionOffScreen;
        Debug.Log($"[{gameObject.name} ONENABLE] Position locale réglée sur _initialLocalPositionOffScreen: {transform.localPosition}. playOnStart: {playOnStart}, activeInHierarchy: {gameObject.activeInHierarchy}", this);

        if (playOnStart && gameObject.activeInHierarchy)
        {
            PlayEntrance();
        }
    }

    public void PlayEntrance()
    {
        if (_isAnimating) {
            Debug.Log($"[{gameObject.name} PLAYENTRANCE] Déjà en cours d'animation. Appel ignoré.", this);
            return;
        }
        if (!gameObject.activeInHierarchy) {
            Debug.Log($"[{gameObject.name} PLAYENTRANCE] GameObject inactif. Appel ignoré.", this);
            return; // Ne pas jouer si l'objet n'est pas actif
        }

        transform.localPosition = _initialLocalPositionOffScreen;
        Debug.Log($"[{gameObject.name} PLAYENTRANCE] Lancement de l'animation. Départ de: {_initialLocalPositionOffScreen}. Cible finale: {finalLocalPosition}. Durée: {entryDuration}, Délai: {startDelay}", this);

        if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
        _animationCoroutine = StartCoroutine(AnimateBoardUpwards());
    }

    private IEnumerator AnimateBoardUpwards()
    {
        _isAnimating = true;
        Debug.Log($"[{gameObject.name} COROUTINE START] Animation 'AnimateBoardUpwards'. startDelay: {startDelay}. Time.timeScale: {Time.timeScale}", this);

        if (startDelay > 0f)
        {
            Debug.Log($"[{gameObject.name} COROUTINE] Attente du startDelay: {startDelay}s (temps réel)", this);
            yield return new WaitForSecondsRealtime(startDelay);
            Debug.Log($"[{gameObject.name} COROUTINE] Fin du startDelay.", this);
        }

        float elapsedTime = 0f;
        // Utiliser la position locale actuelle comme point de départ effectif de l'animation de mouvement
        // Cela garantit que même si quelque chose a modifié la position entre PlayEntrance et le début effectif de cette phase,
        // l'animation partira de la position actuelle vers la cible.
        Vector3 currentAnimatedStartPosition = transform.localPosition;
        Debug.Log($"[{gameObject.name} COROUTINE] Phase de mouvement démarrée. De: {currentAnimatedStartPosition} Vers: {finalLocalPosition} Durée: {entryDuration}", this);

        if (entryDuration <= 0.001f) // Vérification pour une durée très faible ou nulle
        {
            Debug.LogWarning($"[{gameObject.name} COROUTINE] entryDuration ({entryDuration}s) est très faible ou nulle. Positionnement instantané à la position finale.", this);
            transform.localPosition = finalLocalPosition;
            _isAnimating = false;
            _animationCoroutine = null;
            Debug.Log($"[{gameObject.name} COROUTINE END] Animation terminée (instantanée à cause de la durée). Position finale: {transform.localPosition}", this);
            yield break; // Sortir de la coroutine
        }

        while (elapsedTime < entryDuration)
        {
            elapsedTime += Time.unscaledDeltaTime; // Important: utiliser unscaledDeltaTime
            float t = Mathf.Clamp01(elapsedTime / entryDuration);
            float easedT = 1f - Mathf.Pow(1f - t, 3);
            transform.localPosition = Vector3.LerpUnclamped(currentAnimatedStartPosition, finalLocalPosition, easedT);
            // Le log suivant peut être très verbeux, décommente-le seulement si nécessaire pour un débogage fin du mouvement.
            // Debug.Log($"[{gameObject.name} COROUTINE LOOP] t: {t:F3}, easedT: {easedT:F3}, newPos: {transform.localPosition}", this);
            yield return null;
        }
        transform.localPosition = finalLocalPosition;
        _isAnimating = false;
        _animationCoroutine = null;
        Debug.Log($"[{gameObject.name} COROUTINE END] Animation terminée. Position finale: {transform.localPosition}", this);
    }

    void LateUpdate()
    {
        if (billboardTowardsCamera && _mainCamera != null && gameObject.activeInHierarchy)
        {
            ApplyBillboard();
        }
    }

    private void ApplyBillboard()
    {
        // ... (code existant)
        Vector3 directionToCamera = _mainCamera.transform.position - transform.position;
        if (billboardYAxisOnly)
        {
            directionToCamera.y = 0;
        }
        if (directionToCamera.magnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(-directionToCamera.normalized);
        }
    }

    void OnValidate()
    {
        // ... (code existant)
        #if UNITY_EDITOR
        if (!Application.isPlaying && transform.parent != null)
        {
            Vector3 editorFinalPos = (finalLocalPosition == Vector3.zero && transform.localPosition != Vector3.zero) ? transform.localPosition : finalLocalPosition;
            transform.localPosition = editorFinalPos;

            if (billboardTowardsCamera && Camera.main != null)
            {
                Vector3 dirToCam = Camera.main.transform.position - transform.position;
                if (billboardYAxisOnly) dirToCam.y = 0;
                if(dirToCam != Vector3.zero) transform.rotation = Quaternion.LookRotation(-dirToCam.normalized);
            }
        }
        #endif
    }
}