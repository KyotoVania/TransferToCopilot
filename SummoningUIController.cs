
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using ScriptableObjects;
using Gameplay;

/// <summary>
/// Manages the UI panel for the 4 player units, showing their status,
/// cost, cooldown, and providing feedback during sequence input.
/// </summary>
public class SummoningUIController : MonoBehaviour
{
    [System.Serializable]
    public class SummoningCardUI
    {
        [Tooltip("The root GameObject for this card.")]
        public GameObject CardRoot;
        [Tooltip("Image component for the character's icon.")]
        public Image CharacterIcon;
        [Tooltip("Text for the character's gold cost.")]
        public TextMeshProUGUI CostText;
        [Tooltip("The glowing feedback image, disabled by default.")]
        public Image FeedbackGlow;
        [Tooltip("The semi-transparent overlay used to indicate cooldown.")]
        public Image CooldownOverlay;
        [Tooltip("Text to display the remaining cooldown time.")]
        public TextMeshProUGUI CooldownTimerText;
        [HideInInspector] public CharacterData_SO CharacterData;
    }

    [Header("Card References")]
    [Tooltip("Assign the 4 summoning card UI elements here.")]
    [SerializeField] private List<SummoningCardUI> summonCards = new List<SummoningCardUI>(4);
    
    [Header("Info Panel")]
    [Tooltip("Faites glisser ici le GameObject PanelFrame_02_White")]
    [SerializeField] private GameObject infoPanelObject;
    [Tooltip("Le composant Text_Title du panel d'info")]
    [SerializeField] private TextMeshProUGUI infoPanelTitleText;
    [Tooltip("Le composant Text du panel d'info")]
    [SerializeField] private TextMeshProUGUI infoPanelSequenceText;
    [Tooltip("La vitesse de l'animation d'apparition/disparition du panel")]
    [SerializeField] private float infoPanelAnimationSpeed = 0.2f;

    // --- DÉPENDANCES CORRIGÉES ---
    private TeamManager _teamManager;
    private UnitSpawner _unitSpawner; // Dépendance directe au spawner
    private SequenceController _sequenceController;
    // private GameplayManager _gameplayManager; // Supprimé !

    // Variables pour l'animation du panneau d'info
    private CanvasGroup infoPanelCanvasGroup;
    private Coroutine currentPanelAnimation;
    private Transform originalInfoPanelParent;
    private RectTransform infoPanelRectTransform;

    #region Unity Lifecycle
    void Awake()
    {
        // Récupérer les instances des managers
        _teamManager = TeamManager.Instance;
        _unitSpawner = FindFirstObjectByType<UnitSpawner>(); // Initialisé ici
        _sequenceController = FindFirstObjectByType<SequenceController>();

        // Valider les dépendances
        if (_teamManager == null) Debug.LogError("[SummoningUIController] TeamManager.Instance is null!", this);
        if (_unitSpawner == null) Debug.LogError("[SummoningUIController] Could not find UnitSpawner in the scene!", this);
        if (_sequenceController == null) Debug.LogError("[SummoningUIController] Could not find SequenceController in the scene!", this);
        
        if (infoPanelObject != null)
        {
            originalInfoPanelParent = infoPanelObject.transform.parent;
            infoPanelRectTransform = infoPanelObject.GetComponent<RectTransform>();
            infoPanelCanvasGroup = infoPanelObject.GetComponent<CanvasGroup>() ?? infoPanelObject.AddComponent<CanvasGroup>();
            infoPanelObject.SetActive(false);
            infoPanelCanvasGroup.alpha = 0;
        }
        else
        {
            Debug.LogError("La référence 'infoPanelObject' n'est pas assignée dans le SummoningUIController !", this);
        }
    }

    void OnEnable()
    {
        // S'abonner aux événements
        if (_teamManager != null)
        {
            TeamManager.OnActiveTeamChanged += HandleTeamChanged;
            // Appel initial pour peupler l'UI avec l'équipe actuelle
            HandleTeamChanged(_teamManager.ActiveTeam);
        }
    }

    void OnDisable()
    {
        // Se désabonner pour éviter les fuites de mémoire
        if (_teamManager != null)
        {
            TeamManager.OnActiveTeamChanged -= HandleTeamChanged;
        }
    }

    void Update()
    {
        UpdateGlowFeedback();
        UpdateCooldownVisuals();
    }
    #endregion

    #region Event Handlers
    private void HandleTeamChanged(List<CharacterData_SO> activeTeam)
    {
        PopulateSummonCards(activeTeam);
    }
    #endregion

    #region UI Logic
    private void PopulateSummonCards(List<CharacterData_SO> activeTeam)
    {
        for (int i = 0; i < summonCards.Count; i++)
        {
            if (i < activeTeam.Count && activeTeam[i] != null)
            {
                CharacterData_SO data = activeTeam[i];
                SummoningCardUI card = summonCards[i];

                card.CharacterData = data;
                card.CardRoot.SetActive(true);
                card.CharacterIcon.sprite = data.Icon;
                card.CostText.text = data.GoldCost.ToString();
                AddHoverEventsToCard(card);
            }
            else
            {
                summonCards[i].CardRoot.SetActive(false);
                summonCards[i].CharacterData = null;
            }
        }
    }

    private void UpdateGlowFeedback()
    {
        if (_sequenceController == null) return;
        IReadOnlyList<KeyCode> currentSequence = _sequenceController.CurrentSequence;

        foreach (var card in summonCards)
        {
            if (card.CardRoot.activeSelf && card.CharacterData != null)
            {
                bool shouldGlow = DoesSequenceMatch(currentSequence, card.CharacterData.InvocationSequence);
                card.FeedbackGlow.enabled = shouldGlow;
            }
        }
    }

    private bool DoesSequenceMatch(IReadOnlyList<KeyCode> playerSequence, List<InputType> characterSequence)
    {
        if (playerSequence.Count == 0 || playerSequence.Count > characterSequence.Count) return false;

        for (int i = 0; i < playerSequence.Count; i++)
        {
            if (playerSequence[i] != ConvertInputTypeToKeyCode(characterSequence[i])) return false;
        }
        return true;
    }

    private void UpdateCooldownVisuals()
    {
        // --- MISE À JOUR DE LA SOURCE DE DONNÉES ---
        if (_unitSpawner == null) return;
        var cooldowns = _unitSpawner.UnitCooldowns; // On utilise le spawner directement

        foreach (var card in summonCards)
        {
            if (card.CardRoot.activeSelf && card.CharacterData != null)
            {
                if (cooldowns.TryGetValue(card.CharacterData.CharacterID, out float cooldownEndTime))
                {
                    float remainingTime = cooldownEndTime - Time.time;
                    if (remainingTime > 0)
                    {
                        card.CooldownOverlay.enabled = true;
                        card.CooldownTimerText.enabled = true;

                        float totalCooldown = card.CharacterData.InvocationCooldown * MusicManager.Instance.GetBeatDuration();
                        card.CooldownOverlay.fillAmount = remainingTime / totalCooldown;
                        card.CooldownTimerText.text = Mathf.CeilToInt(remainingTime).ToString();
                    }
                    else
                    {
                        card.CooldownOverlay.enabled = false;
                        card.CooldownTimerText.enabled = false;
                    }
                }
                else
                {
                    card.CooldownOverlay.enabled = false;
                    card.CooldownTimerText.enabled = false;
                }
            }
        }
    }

    private void AddHoverEventsToCard(SummoningCardUI card)
    {
        EventTrigger trigger = card.CardRoot.GetComponent<EventTrigger>() ?? card.CardRoot.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        EventTrigger.Entry pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        pointerEnter.callback.AddListener((eventData) => { OnCardHoverEnter(card); });
        trigger.triggers.Add(pointerEnter);

        EventTrigger.Entry pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        pointerExit.callback.AddListener((eventData) => { OnCardHoverExit(); });
        trigger.triggers.Add(pointerExit);
    }

    private void OnCardHoverEnter(SummoningCardUI hoveredCard)
    {
        if (infoPanelObject == null || hoveredCard.CharacterData == null) return;

        infoPanelTitleText.text = hoveredCard.CharacterData.DisplayName;
        infoPanelSequenceText.text = FormatSequence(hoveredCard.CharacterData.InvocationSequence);

        infoPanelRectTransform.SetParent(hoveredCard.CardRoot.transform);

        if (currentPanelAnimation != null) StopCoroutine(currentPanelAnimation);
        currentPanelAnimation = StartCoroutine(AnimateInfoPanel(true));
    }

    private void OnCardHoverExit()
    {
        if (infoPanelObject == null) return;
        if (currentPanelAnimation != null) StopCoroutine(currentPanelAnimation);
        currentPanelAnimation = StartCoroutine(AnimateInfoPanel(false));
    }

    private IEnumerator AnimateInfoPanel(bool show)
    {
        if (show)
        {
            infoPanelObject.SetActive(true);
            infoPanelRectTransform.anchoredPosition = Vector2.zero;
        }

        float startAlpha = infoPanelCanvasGroup.alpha;
        float endAlpha = show ? 1f : 0f;
        Vector2 startPosition = infoPanelRectTransform.anchoredPosition;
        Vector2 endPosition = startPosition + new Vector2(0, 150f);

        float elapsedTime = 0f;
        while (elapsedTime < infoPanelAnimationSpeed)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / infoPanelAnimationSpeed);
            float easedT = Mathf.SmoothStep(0.0f, 1.0f, t);
            infoPanelCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, easedT);
            infoPanelRectTransform.anchoredPosition = Vector2.Lerp(startPosition, endPosition, easedT);
            yield return null;
        }

        infoPanelCanvasGroup.alpha = endAlpha;

        if (!show)
        {
            infoPanelObject.SetActive(false);
            infoPanelRectTransform.SetParent(originalInfoPanelParent);
        }
        currentPanelAnimation = null;
    }

    private string FormatSequence(List<InputType> sequence)
    {
        if (sequence == null || sequence.Count == 0) return "N/A";
        return string.Join(" - ", sequence);
    }

    private KeyCode ConvertInputTypeToKeyCode(InputType inputType)
    {
        switch (inputType)
        {
            case InputType.X: return KeyCode.X;
            case InputType.C: return KeyCode.C;
            case InputType.V: return KeyCode.V;
            default: return KeyCode.None;
        }
    }
    #endregion
}