
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using System.Text;
using ScriptableObjects;
using Gameplay;

/// <summary>
/// Gère le panneau d'interface utilisateur pour les sorts globaux, affichant leur statut,
/// leur cooldown, et fournissant un retour visuel pendant la saisie de la séquence.
/// </summary>
public class GlobalSpellsUIController : MonoBehaviour
{
    // Classe helper pour organiser proprement les références de chaque carte de sort.
    [System.Serializable]
    public class SpellCardUI
    {
        [Tooltip("Le GameObject racine de cette carte.")]
        public GameObject CardRoot;
        [Tooltip("Le composant Image pour l'icône du sort.")]
        public Image SpellIcon;
        [Tooltip("L'image pour l'effet de lueur, désactivée par défaut.")]
        public Image FeedbackGlow;
        [Tooltip("L'overlay semi-transparent pour indiquer le cooldown.")]
        public Image CooldownOverlay;
        [Tooltip("Le texte pour afficher le temps de rechargement restant.")]
        public TextMeshProUGUI CooldownTimerText;

        // Référence interne aux données du sort que cette carte représente.
        [HideInInspector] public GlobalSpellData_SO SpellData;
    }

    [Header("UI References")]
    [Tooltip("Assignez ici les 4 cartes de l'interface des sorts.")]
    [SerializeField] private List<SpellCardUI> spellCards = new List<SpellCardUI>();
    
    [Header("Info Panel")]
    [Tooltip("Le GameObject du panneau d'informations qui apparaît au survol.")]
    [SerializeField] private GameObject infoPanelObject;
    [Tooltip("Le composant TextMeshPro pour le titre dans le panneau d'info.")]
    [SerializeField] private TextMeshProUGUI infoPanelTitleText;
    [Tooltip("Le composant TextMeshPro pour la séquence dans le panneau d'info.")]
    [SerializeField] private TextMeshProUGUI infoPanelSequenceText;
    [Tooltip("La vitesse de l'animation d'apparition/disparition du panneau.")]
    [SerializeField] private float infoPanelAnimationSpeed = 0.2f;

    // Références aux managers et variables internes
    private GlobalSpellManager globalSpellManager; // Référence directe, c'est correct.
    private SequenceController _sequenceController;
    
    // Variables pour l'animation du panneau d'info
    private CanvasGroup infoPanelCanvasGroup;
    private Coroutine currentPanelAnimation;
    private RectTransform infoPanelRectTransform;
    private Transform originalInfoPanelParent; 

    // Pour optimiser la mise à jour du texte
    private readonly StringBuilder stringBuilder = new StringBuilder();

    #region Cycle de Vie Unity

    void Awake()
    {
        // Récupérer les instances des managers.
        globalSpellManager = FindFirstObjectByType<GlobalSpellManager>();
        _sequenceController = FindObjectOfType<SequenceController>();

        // Valider que les managers nécessaires ont été trouvés.
        if (globalSpellManager == null) Debug.LogError("[GlobalSpellsUIController] Could not find GlobalSpellManager in the scene!", this);
        if (_sequenceController == null) Debug.LogError("[GlobalSpellsUIController] Could not find SequenceController in the scene!", this);
        
        // Préparer le panneau d'information.
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
            Debug.LogError("[GlobalSpellsUIController] La référence 'infoPanelObject' n'est pas assignée !", this);
        }
    }

    void OnEnable()
    {
        // S'abonner aux événements lorsque l'UI devient active.
        // --- CORRECTION : On utilise la bonne méthode handler ---
        GlobalSpellManager.OnGlobalSpellsLoaded += PopulateSpellCards; 

        if (_sequenceController != null)
        {
            SequenceController.OnSequenceKeyPressed += HandleSequenceKeyPress;
            SequenceController.OnSequenceDisplayCleared += HandleSequenceCleared;
        }
    }

    void OnDisable()
    {
        // Se désabonner pour éviter les erreurs.
        // --- CORRECTION : On utilise la bonne méthode handler ---
        GlobalSpellManager.OnGlobalSpellsLoaded -= PopulateSpellCards;

        if (_sequenceController != null)
        {
            SequenceController.OnSequenceKeyPressed -= HandleSequenceKeyPress;
            SequenceController.OnSequenceDisplayCleared -= HandleSequenceCleared;
        }
    }

    void Update()
    {
        UpdateCooldownVisuals();
    }
    #endregion

    #region Gestionnaires d'Événements
    
    private void HandleSequenceKeyPress(string key, Color timingColor)
    {
        UpdateGlowFeedback();
    }

    private void HandleSequenceCleared()
    {
        UpdateGlowFeedback();
    }
    #endregion

    #region Logique de l'UI

    /// <summary>
    /// Remplit les 4 cartes de sorts avec les données des sorts disponibles.
    /// C'est la méthode correcte à appeler lors de l'événement OnGlobalSpellsLoaded.
    /// </summary>
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
                bool shouldGlow = DoesSequenceMatchPrefix(currentSequence, card.SpellData.SpellSequence);
                card.FeedbackGlow.enabled = shouldGlow;
            }
        }
    }

    private void UpdateCooldownVisuals()
    {
        if (globalSpellManager == null) return;
        IReadOnlyDictionary<string, float> cooldowns = globalSpellManager.SpellCooldowns;
        
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

                        float totalCooldown = card.SpellData.BeatCooldown * MusicManager.Instance.GetBeatDuration();
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
    #endregion

    #region Logique de Survol et d'Animation

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
        Vector2 endPosition = startPosition + new Vector2(0, show ? 150f : 0f); // Déplacement vers le haut

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
    #endregion

    #region Fonctions Utilitaires

    private bool DoesSequenceMatchPrefix(IReadOnlyList<KeyCode> playerSequence, List<InputType> targetSequence)
    {
        if (playerSequence.Count == 0 || playerSequence.Count > targetSequence.Count) return false;
        
        for (int i = 0; i < playerSequence.Count; i++)
        {
            if (playerSequence[i] != ConvertInputTypeToKeyCode(targetSequence[i])) return false;
        }
        return true;
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
    
    private string FormatSequence(List<InputType> sequence)
    {
        if (sequence == null || sequence.Count == 0) return "N/A";
        return string.Join(" - ", sequence);
    }
    #endregion
}