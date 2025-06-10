using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using ScriptableObjects;
/// <summary>
/// Manages the UI panel for the 4 player units, showing their status,
/// cost, cooldown, and providing feedback during sequence input.
/// </summary>
public class SummoningUIController : MonoBehaviour
{
    // A helper class to neatly store all UI element references for a single card.
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

        // Internal reference to the character data this card represents.
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

    // --- NOUVELLES VARIABLES POUR L'ANIMATION ---
    private CanvasGroup infoPanelCanvasGroup;
    private Coroutine currentPanelAnimation;
    // References to core managers.
    private TeamManager _teamManager;
    private GameplayManager _gameplayManager;
    private SequenceController _sequenceController;
    private Transform originalInfoPanelParent;
    private RectTransform infoPanelRectTransform;

    #region Unity Lifecycle
    void Awake()
    {
        // Get singleton instances for managers that are persistent.
        _teamManager = TeamManager.Instance;
        
        _gameplayManager = Object.FindFirstObjectByType<GameplayManager>();
        _sequenceController = Object.FindFirstObjectByType<SequenceController>();

        // Validate that all necessary managers were found.
        if (_teamManager == null) Debug.LogError("[SummoningUIController] TeamManager.Instance is null!");
        if (_gameplayManager == null) Debug.LogError("[SummoningUIController] Could not find GameplayManager in the scene!");
        if (_sequenceController == null) Debug.LogError("[SummoningUIController] Could not find SequenceController in the scene!");
        if (infoPanelObject != null)
        {
            // Récupérer le CanvasGroup que nous avons ajouté au prefab
            originalInfoPanelParent = infoPanelObject.transform.parent;
            infoPanelRectTransform = infoPanelObject.GetComponent<RectTransform>();
            infoPanelCanvasGroup = infoPanelObject.GetComponent<CanvasGroup>();
            if (infoPanelCanvasGroup == null)
            {
                infoPanelCanvasGroup = infoPanelObject.AddComponent<CanvasGroup>();
            }
            infoPanelObject.SetActive(false);
            infoPanelCanvasGroup.alpha = 0;
        }
        else
        {
            Debug.LogError("La référence 'infoPanelObject' n'est pas assignée dans le SummoningUIController !");
        }
    }

    void OnEnable()
    {
        // Subscribe to events when the UI becomes active.
        TeamManager.OnActiveTeamChanged += HandleTeamChanged;
        
        // Initial setup
        if (_teamManager != null)
        {
            HandleTeamChanged(_teamManager.ActiveTeam);
        }
    }

    void OnDisable()
    {
        // Unsubscribe to prevent errors when the UI is inactive or destroyed.
        if (TeamManager.Instance != null)
        {
            TeamManager.OnActiveTeamChanged -= HandleTeamChanged;
        }
    }

    void Update()
    {
        // These methods are polled every frame to provide instant feedback.
        UpdateGlowFeedback();
        UpdateCooldownVisuals();
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// Called when the player's active team is loaded or changed.
    /// </summary>
    private void HandleTeamChanged(List<CharacterData_SO> activeTeam)
    {
        PopulateSummonCards(activeTeam);
    }
    #endregion

    #region UI Logic
    /// <summary>
    /// Sets up the 4 summon cards with data from the player's active team.
    /// </summary>
    private void PopulateSummonCards(List<CharacterData_SO> activeTeam)
    {
        for (int i = 0; i < summonCards.Count; i++)
        {
            // Check if a character exists for this slot in the team list.
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
                // If no character is in this slot, hide the card.
                summonCards[i].CardRoot.SetActive(false);
                summonCards[i].CharacterData = null;
            }
        }
    }

    /// <summary>
    /// Updates the glowing effect on cards based on the current input sequence.
    /// </summary>
    private void UpdateGlowFeedback()
    {
        if (_sequenceController == null) return;

        IReadOnlyList<KeyCode> currentSequence = _sequenceController.CurrentSequence;

        foreach (var card in summonCards)
        {
            if (card.CardRoot.activeSelf && card.CharacterData != null)
            {
                // A card glows if the current sequence is a prefix of its invocation sequence.
                bool shouldGlow = DoesSequenceMatch(currentSequence, card.CharacterData.InvocationSequence);
                card.FeedbackGlow.enabled = shouldGlow;
            }
        }
    }

    /// <summary>
    /// Checks if the player's input sequence is a valid start for a character's summon sequence.
    /// </summary>
    private bool DoesSequenceMatch(IReadOnlyList<KeyCode> playerSequence, List<InputType> characterSequence)
    {
        if (playerSequence.Count == 0 || playerSequence.Count > characterSequence.Count)
        {
            return false;
        }

        for (int i = 0; i < playerSequence.Count; i++)
        {
            // Convert the character's InputType enum to a KeyCode for comparison.
            KeyCode requiredKey = ConvertInputTypeToKeyCode(characterSequence[i]);
            if (playerSequence[i] != requiredKey)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Updates the cooldown overlay and timer text for each card.
    /// </summary>
    private void UpdateCooldownVisuals()
    {
        if (_gameplayManager == null) return;
        var cooldowns = _gameplayManager.UnitCooldowns;

        foreach (var card in summonCards)
        {
            if (card.CardRoot.activeSelf && card.CharacterData != null)
            {
                if (cooldowns.TryGetValue(card.CharacterData.CharacterID, out float cooldownEndTime))
                {
                    float remainingTime = cooldownEndTime - Time.time;
                    if (remainingTime > 0)
                    {
                        // Unit is on cooldown.
                        card.CooldownOverlay.enabled = true;
                        card.CooldownTimerText.enabled = true;

                        // Update the radial fill and timer text.
                        float totalCooldown = card.CharacterData.InvocationCooldown * (60f / RhythmManager.Instance.bpm); // Approximate total duration
                        card.CooldownOverlay.fillAmount = remainingTime / totalCooldown;
                        card.CooldownTimerText.text = Mathf.CeilToInt(remainingTime).ToString();
                    }
                    else
                    {
                        // Cooldown is finished.
                        card.CooldownOverlay.enabled = false;
                        card.CooldownTimerText.enabled = false;
                    }
                }
                else
                {
                    // No cooldown entry exists for this unit.
                    card.CooldownOverlay.enabled = false;
                    card.CooldownTimerText.enabled = false;
                }
            }
        }
    }
// <summary>
    /// Ajoute dynamiquement les composants et listeners nécessaires pour détecter le survol.
    /// </summary>
    private void AddHoverEventsToCard(SummoningCardUI card)
    {
        EventTrigger trigger = card.CardRoot.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = card.CardRoot.AddComponent<EventTrigger>();
        }
        trigger.triggers.Clear(); // Nettoyer les anciens listeners pour éviter les doublons

        // Créer l'événement pour l'entrée de la souris
        EventTrigger.Entry pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        pointerEnter.callback.AddListener((eventData) => { OnCardHoverEnter(card); });
        trigger.triggers.Add(pointerEnter);

        // Créer l'événement pour la sortie de la souris
        EventTrigger.Entry pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        pointerExit.callback.AddListener((eventData) => { OnCardHoverExit(); });
        trigger.triggers.Add(pointerExit);
    }

    private void OnCardHoverEnter(SummoningCardUI hoveredCard)
    {
        if (infoPanelObject == null || hoveredCard.CharacterData == null) return;

        // Mettre à jour le contenu
        infoPanelTitleText.text = hoveredCard.CharacterData.DisplayName;
        infoPanelSequenceText.text = FormatSequence(hoveredCard.CharacterData.InvocationSequence);

        // On attache le panel d'info à la carte survolée
        infoPanelRectTransform.SetParent(hoveredCard.CardRoot.transform);

        // Animer l'apparition
        if (currentPanelAnimation != null) StopCoroutine(currentPanelAnimation);
        currentPanelAnimation = StartCoroutine(AnimateInfoPanel(true));
    }

    private void OnCardHoverExit()
    {
        if (infoPanelObject == null) return;
        
        // Animer la disparition
        if (currentPanelAnimation != null) StopCoroutine(currentPanelAnimation);
        currentPanelAnimation = StartCoroutine(AnimateInfoPanel(false));
    }

    /// <summary>
    /// Coroutine pour animer l'apparition ou la disparition du panel d'info.
    /// </summary>
    private IEnumerator AnimateInfoPanel(bool show)
    {
        if (show)
        {
            infoPanelObject.SetActive(true);
            // Position de départ : centrée sur le nouveau parent (la carte)
            infoPanelRectTransform.anchoredPosition = Vector2.zero;
        }

        float startAlpha = infoPanelCanvasGroup.alpha;
        float endAlpha = show ? 1f : 0f;

        Vector2 startPosition = infoPanelRectTransform.anchoredPosition;
        // Position de fin : 150 unités au-dessus de sa position de départ (relative à la carte)
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
            infoPanelRectTransform.SetParent(originalInfoPanelParent); // On le remet à sa place d'origine
        }
        currentPanelAnimation = null;
    }

    /// <summary>
    /// Formate la liste d'inputs en une chaîne de caractères lisible.
    /// </summary>
    private string FormatSequence(List<InputType> sequence)
    {
        if (sequence == null || sequence.Count == 0) return "N/A";
        return string.Join(" - ", sequence);
    }
    /// <summary>
    /// Helper to convert our InputType enum to Unity's KeyCode.
    /// </summary>
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

