using UnityEngine;
using System.Collections;

public class BannerEntranceAnimation : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Position locale finale de la bannière (par rapport à son parent direct).")]
    [SerializeField] private Vector3 finalLocalPosition = Vector3.zero;
    [Tooltip("Décalage Y initial par rapport à la position finale (combien de 'plus haut' elle commence).")]
    [SerializeField] private float startYOffset = 10f;
    [Tooltip("Durée de l'animation de chute principale en secondes.")]
    [SerializeField] private float entryDuration = 1.0f; // Réduit pour une chute plus rapide
    [Tooltip("Délai avant de commencer l'animation (en secondes).")]
    [SerializeField] private float startDelay = 0.2f;
    [Tooltip("Si coché, l'animation se jouera automatiquement lorsque l'objet est activé.")]
    [SerializeField] private bool playOnStart = true;

    [Header("Bounce Effect Settings")]
    [Tooltip("Activer l'effet de rebond à la fin de la chute.")]
    [SerializeField] private bool enableBounce = true;
    [Tooltip("Durée totale de l'animation de rebond (va descendre puis remonter).")]
    [SerializeField] private float bounceDuration = 0.5f;
    [Tooltip("De combien la bannière descend en dessous de sa position finale avant de remonter (en % de startYOffset).")]
    [SerializeField] [Range(0f, 0.2f)] private float bounceOvershootPercent = 0.08f; // Ex: 8% de l'offset initial

    [Header("Camera Facing (Billboard)")]
    [SerializeField] private bool billboardTowardsCamera = true;
    [SerializeField] private bool billboardYAxisOnly = true;

    private Vector3 _initialLocalPositionOffScreen;
    private bool _isAnimating = false;
    private Coroutine _animationCoroutine;
    private Camera _mainCamera;

    void Awake()
    {
        // Déterminer la position finale et la position de départ hors écran
        // Si finalLocalPosition est (0,0,0) dans l'inspecteur ET que l'objet a une position locale non nulle,
        // on prend sa position locale actuelle dans l'éditeur comme position finale désirée.
        if (finalLocalPosition == Vector3.zero && transform.localPosition != Vector3.zero)
        {
            finalLocalPosition = transform.localPosition;
        }
        _initialLocalPositionOffScreen = finalLocalPosition + new Vector3(0, startYOffset, 0);

        _mainCamera = Camera.main;
        if (_mainCamera == null && billboardTowardsCamera)
        {
            Debug.LogError($"[{gameObject.name}] Caméra principale non trouvée ! Le billboarding ne fonctionnera pas.", this);
            billboardTowardsCamera = false;
        }
    }

    void OnEnable()
    {
        // Réinitialiser l'état et la position si l'objet est réactivé
        _isAnimating = false;
        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;
        }

        // Toujours placer la bannière à sa position de départ hors-écran quand elle est activée
        transform.localPosition = _initialLocalPositionOffScreen;
        // Debug.Log($"[{gameObject.name}] OnEnable - Position reset à {_initialLocalPositionOffScreen}");


        if (playOnStart && gameObject.activeInHierarchy)
        {
            // Debug.Log($"[{gameObject.name}] OnEnable - playOnStart est true, lancement de PlayEntrance.");
            PlayEntrance();
        }
    }

    // Start peut être utilisé si l'objet est déjà actif au lancement de la scène et playOnStart est vrai.
    // OnEnable couvre déjà ce cas, mais pour être sûr si l'ordre d'exécution varie:
    void Start()
    {
        if (playOnStart && gameObject.activeInHierarchy && !_isAnimating)
        {
             // Vérifie si OnEnable n'a pas déjà lancé l'animation
            // Si on est ici et que _isAnimating est false, et que l'objet était actif dès le début,
            // OnEnable aura déjà appelé PlayEntrance(). Si ce n'est pas le cas, on le fait.
            // Pour éviter un double appel, la logique dans OnEnable est généralement suffisante.
            // On peut s'assurer que PlayEntrance ne se lance qu'une fois.
            // OnEnable est le point d'entrée le plus fiable pour les objets activés dynamiquement.
            // Si l'objet est actif dès le début, Awake -> OnEnable -> Start.
        }
    }


    public void PlayEntrance()
    {
        if (_isAnimating)
        {
            // Debug.Log($"[{gameObject.name}] PlayEntrance appelé mais déjà en cours d'animation.");
            return;
        }
        if (!gameObject.activeInHierarchy)
        {
            // Debug.Log($"[{gameObject.name}] PlayEntrance appelé mais GameObject inactif.");
            return; // Ne pas jouer si l'objet n'est pas actif

        }
        // S'assurer qu'elle est à sa position de départ avant l'animation
        transform.localPosition = _initialLocalPositionOffScreen;
        // Debug.Log($"[{gameObject.name}] PlayEntrance - Début de l'animation depuis {_initialLocalPositionOffScreen} vers {finalLocalPosition}.");

        if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
        _animationCoroutine = StartCoroutine(AnimateBannerFallAndBounce());
    }

    private IEnumerator AnimateBannerFallAndBounce()
    {
        _isAnimating = true;
        // Debug.Log($"[{gameObject.name}] Coroutine AnimateBannerFallAndBounce démarrée.");

        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(startDelay);
        }

        // --- Animation de Chute Principale ---
        float elapsedTime = 0f;
        Vector3 startFallPosition = transform.localPosition; // Position actuelle (devrait être _initialLocalPositionOffScreen)
        // Debug.Log($"[{gameObject.name}] Phase de chute: de {startFallPosition} à {finalLocalPosition} sur {entryDuration}s.");

        while (elapsedTime < entryDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / entryDuration);
            // Ease Out Cubic (commence vite, ralentit à la fin) pour un effet de chute naturelle
            float easedT = 1f - Mathf.Pow(1f - t, 3);
            transform.localPosition = Vector3.LerpUnclamped(startFallPosition, finalLocalPosition, easedT);
            yield return null;
        }
        transform.localPosition = finalLocalPosition; // Assurer la position exacte

        // --- Animation de Rebond (Overshoot & Settle) ---
        if (enableBounce && bounceDuration > 0f && bounceOvershootPercent > 0f)
        {
            float actualBounceMagnitude = startYOffset * bounceOvershootPercent; // Amplitude du rebond vers le bas
            if (actualBounceMagnitude < 0.01f) actualBounceMagnitude = 0.01f; // Minimum pour être visible

            Vector3 overshootTarget = finalLocalPosition - new Vector3(0, actualBounceMagnitude, 0); // Descend un peu plus bas
            Vector3 settleTarget = finalLocalPosition; // Revient à la position finale

            float overshootTime = bounceDuration * 0.4f; // 40% du temps pour descendre
            float settleTime = bounceDuration * 0.6f;    // 60% du temps pour remonter et se stabiliser

            // 1. Overshoot (descendre)
            // Debug.Log($"[{gameObject.name}] Phase d'overshoot: de {finalLocalPosition} à {overshootTarget} sur {overshootTime}s.");
            elapsedTime = 0f;
            Vector3 currentBouncePos = transform.localPosition;
            while (elapsedTime < overshootTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsedTime / overshootTime);
                // Ease Out pour l'overshoot donne un effet "mou"
                float easedT = 1f - Mathf.Pow(1f - t, 2); // Ease Out Quad
                transform.localPosition = Vector3.LerpUnclamped(currentBouncePos, overshootTarget, easedT);
                yield return null;
            }
            transform.localPosition = overshootTarget;

            // 2. Settle (remonter à la position finale)
            // Debug.Log($"[{gameObject.name}] Phase de stabilisation: de {overshootTarget} à {settleTarget} sur {settleTime}s.");
            elapsedTime = 0f;
            currentBouncePos = transform.localPosition;
            while (elapsedTime < settleTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsedTime / settleTime);
                // Ease Out pour la stabilisation
                float easedT = 1f - Mathf.Pow(1f - t, 3); // Ease Out Cubic
                transform.localPosition = Vector3.LerpUnclamped(currentBouncePos, settleTarget, easedT);
                yield return null;
            }
        }

        transform.localPosition = finalLocalPosition; // Assurer la position finale exacte
        // Debug.Log($"[{gameObject.name}] Animation et rebond terminés. Position finale: {transform.localPosition}");
        _isAnimating = false;
        _animationCoroutine = null;
    }

    void LateUpdate()
    {
        if (billboardTowardsCamera && _mainCamera != null && gameObject.activeInHierarchy) // _isInitialized n'existe plus, on peut enlever
        {
            ApplyBillboard();
        }
    }

    private void ApplyBillboard()
    {
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

    // OnValidate pour aider au placement dans l'éditeur
    void OnValidate()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying && transform.parent != null)
        {
            Vector3 editorFinalPos = (finalLocalPosition == Vector3.zero && transform.localPosition != Vector3.zero) ? transform.localPosition : finalLocalPosition;
            // Pour la prévisualisation de la position de départ (hors écran)
            // transform.localPosition = editorFinalPos + new Vector3(0, startYOffset, 0);
            // Ou pour voir la position finale:
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