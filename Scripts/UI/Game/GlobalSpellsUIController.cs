using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using System.Collections;

public class GlobalSpellsUIController : MonoBehaviour
{
    // Classe helper pour les références d'un slot de sort
    [System.Serializable]
    public class SpellCardUI
    {
        public GameObject CardRoot;
        public Image SpellIcon; // Renommé pour la clarté
        public Image FeedbackGlow;
        public Image CooldownOverlay;
        public TextMeshProUGUI CooldownTimerText;
        [HideInInspector] public GlobalSpellData_SO SpellData;
    }

    [Header("Spell Card References")]
    [SerializeField] private List<SpellCardUI> spellCards = new List<SpellCardUI>();

    [Header("Info Panel (comme pour les unités)")]
    [SerializeField] private GameObject infoPanelObject;
    [SerializeField] private TextMeshProUGUI infoPanelTitleText;
    [SerializeField] private TextMeshProUGUI infoPanelSequenceText;
    [SerializeField] private float infoPanelAnimationSpeed = 0.2f;
    [SerializeField] private float panelVerticalOffset = 0f;

    private GameplayManager _gameplayManager;
    private SequenceController _sequenceController;
    private CanvasGroup infoPanelCanvasGroup;
    private Coroutine currentPanelAnimation;
    private RectTransform infoPanelRectTransform;

    void Awake()
    {
        _gameplayManager = FindObjectOfType<GameplayManager>();
        _sequenceController = FindObjectOfType<SequenceController>();

        if (infoPanelObject != null)
        {
            infoPanelRectTransform = infoPanelObject.GetComponent<RectTransform>();
            infoPanelCanvasGroup = infoPanelObject.GetComponent<CanvasGroup>();
            if (infoPanelCanvasGroup == null) infoPanelCanvasGroup = infoPanelObject.AddComponent<CanvasGroup>();
            infoPanelObject.SetActive(false);
            infoPanelCanvasGroup.alpha = 0;
        }
    }

    void Start()
    {
        // On peuple les cartes au démarrage
        if (_gameplayManager != null)
        {
            PopulateSpellCards(_gameplayManager.AvailableGlobalSpells);
        }
    }

    void Update()
    {
        UpdateGlowFeedback();
        UpdateCooldownVisuals();
    }

    private void PopulateSpellCards(IReadOnlyList<GlobalSpellData_SO> availableSpells)
    {
        for (int i = 0; i < spellCards.Count; i++)
        {
            if (i < availableSpells.Count && availableSpells[i] != null)
            {
                GlobalSpellData_SO data = availableSpells[i];
                SpellCardUI card = spellCards[i];

                card.SpellData = data;
                card.CardRoot.SetActive(true);
                card.SpellIcon.sprite = data.Icon;
                
                AddHoverEventsToCard(card);
            }
            else
            {
                spellCards[i].CardRoot.SetActive(false);
                spellCards[i].SpellData = null;
            }
        }
    }

    private void UpdateGlowFeedback()
    {
        if (_sequenceController == null) return;
        IReadOnlyList<KeyCode> currentSequence = _sequenceController.CurrentSequence;

        foreach (var card in spellCards)
        {
            if (card.CardRoot.activeSelf && card.SpellData != null)
            {
                bool shouldGlow = DoesSequenceMatch(currentSequence, card.SpellData.SpellSequence);
                card.FeedbackGlow.enabled = shouldGlow;
            }
        }
    }

    private void UpdateCooldownVisuals()
    {
        if (_gameplayManager == null) return;
        var cooldowns = _gameplayManager.SpellCooldowns;

        foreach (var card in spellCards)
        {
            if (card.CardRoot.activeSelf && card.SpellData != null)
            {
                if (cooldowns.TryGetValue(card.SpellData.SpellID, out float cooldownEndTime))
                {
                    float remainingTime = cooldownEndTime - Time.time;
                    if (remainingTime > 0)
                    {
                        card.CooldownOverlay.enabled = true;
                        card.CooldownTimerText.enabled = true;
                        card.CooldownOverlay.fillAmount = remainingTime / card.SpellData.BeatCooldown;
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

    // --- Logique de survol et d'animation (identique à SummoningUIController) ---

    private void AddHoverEventsToCard(SpellCardUI card)
    {
        EventTrigger trigger = card.CardRoot.GetComponent<EventTrigger>() ?? card.CardRoot.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        EventTrigger.Entry pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        pointerEnter.callback.AddListener((data) => OnCardHoverEnter(card));
        trigger.triggers.Add(pointerEnter);

        EventTrigger.Entry pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        pointerExit.callback.AddListener((data) => OnCardHoverExit());
        trigger.triggers.Add(pointerExit);
    }
    
    private void OnCardHoverEnter(SpellCardUI hoveredCard)
    {
        if (infoPanelObject == null || hoveredCard.SpellData == null) return;
        
        infoPanelTitleText.text = hoveredCard.SpellData.DisplayName;
        infoPanelSequenceText.text = FormatSequence(hoveredCard.SpellData.SpellSequence);
        Debug.Log($"Hovered over spell: {hoveredCard.SpellData.DisplayName} with sequence: {FormatSequence(hoveredCard.SpellData.SpellSequence)}");
        if (currentPanelAnimation != null) StopCoroutine(currentPanelAnimation);
        currentPanelAnimation = StartCoroutine(AnimateInfoPanel(true, hoveredCard.CardRoot.transform as RectTransform));
    }

    private void OnCardHoverExit()
    {
        if (infoPanelObject == null) return;
        if (currentPanelAnimation != null) StopCoroutine(currentPanelAnimation);
        currentPanelAnimation = StartCoroutine(AnimateInfoPanel(false, null));
    }
    
    private IEnumerator AnimateInfoPanel(bool show, RectTransform anchor)
    {
        // ... (Cette coroutine est 100% IDENTIQUE à celle de SummoningUIController)
        // ... vous pouvez la copier-coller directement.
        // --- Calcul de la position ---
        Vector3 startPosition;
        Vector3 endPosition;

        if (show)
        {
            infoPanelObject.SetActive(true);
            startPosition = anchor.position + new Vector3(0, panelVerticalOffset, 0);
            endPosition = startPosition + new Vector3(0, 150f * infoPanelRectTransform.lossyScale.y, 0);
            infoPanelRectTransform.position = startPosition;
        }
        else
        {
            startPosition = infoPanelRectTransform.position;
            endPosition = startPosition - new Vector3(0, 150f * infoPanelRectTransform.lossyScale.y, 0);
        }

        float startAlpha = infoPanelCanvasGroup.alpha;
        float endAlpha = show ? 1f : 0f;

        float elapsedTime = 0f;
        while (elapsedTime < infoPanelAnimationSpeed)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / infoPanelAnimationSpeed);
            float easedT = Mathf.SmoothStep(0.0f, 1.0f, t);

            infoPanelCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, easedT);
            infoPanelRectTransform.position = Vector3.Lerp(startPosition, endPosition, easedT);
            
            yield return null;
        }

        infoPanelCanvasGroup.alpha = endAlpha;
        infoPanelRectTransform.position = endPosition;

        if (!show)
        {
            infoPanelObject.SetActive(false);
        }
        currentPanelAnimation = null;
    }

    // --- Fonctions utilitaires (identiques à SummoningUIController) ---
    
    private bool DoesSequenceMatch(IReadOnlyList<KeyCode> playerSequence, List<InputType> targetSequence)
    {
        // ... (Logique identique)
        if (playerSequence.Count == 0 || playerSequence.Count > targetSequence.Count) return false;
        for (int i = 0; i < playerSequence.Count; i++)
        {
            if (playerSequence[i] != ConvertInputTypeToKeyCode(targetSequence[i])) return false;
        }
        return true;
    }

    private KeyCode ConvertInputTypeToKeyCode(InputType inputType)
    {
        // ... (Logique identique)
        switch (inputType)
        {
            case InputType.X: return KeyCode.X;
            case InputType.C: return KeyCode.C;
            case InputType.V: return KeyCode.V;
            default: return KeyCode.None;
        }
    }

    private string FormatSequence(List<InputType> sequence)
    {
        // ... (Logique identique)
        if (sequence == null || sequence.Count == 0) return "N/A";
        return string.Join(" - ", sequence.Select(s => s.ToString()));
    }
}