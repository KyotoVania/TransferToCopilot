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
/// Supports both mouse hover (individual panels) and gamepad toggle (all panels).
/// </summary>
public class SummoningUIController : MonoBehaviour
{
    [System.Serializable]
    public class SummoningCardUI
    {
        [Tooltip("The root GameObject for this card.")]
        public GameObject CardRoot;
        [Tooltip("Le conteneur qui parenta le visuel du personnage (icône statique ou préfab animé).")]
        public Transform characterVisualsContainer;
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
        
        [HideInInspector] public GameObject ClonedInfoPanel;
        [HideInInspector] public CanvasGroup ClonedPanelCanvasGroup;
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

    private TeamManager _teamManager;
    private UnitSpawner _unitSpawner; 
    private SequenceController _sequenceController;
    private MusicManager _musicManager;
    private InputManager _inputManager;

    private CanvasGroup infoPanelCanvasGroup;
    private Coroutine currentPanelAnimation;
    private Transform originalInfoPanelParent;
    private RectTransform infoPanelRectTransform;
    
    private bool _isGlobalDisplayMode = false;

    #region Unity Lifecycle
    void Awake()
    {
        // Récupérer les instances des managers
        _teamManager = TeamManager.Instance;
        _unitSpawner = FindFirstObjectByType<UnitSpawner>();
        _sequenceController = FindFirstObjectByType<SequenceController>();
        _musicManager = MusicManager.Instance;
        _inputManager = InputManager.Instance;

        // Valider les dépendances
        if (_teamManager == null) Debug.LogError("[SummoningUIController] TeamManager.Instance is null!", this);
        if (_unitSpawner == null) Debug.LogError("[SummoningUIController] Could not find UnitSpawner in the scene!", this);
        if (_sequenceController == null) Debug.LogError("[SummoningUIController] Could not find SequenceController in the scene!", this);
        if (_inputManager == null) Debug.LogError("[SummoningUIController] InputManager.Instance is null!", this);
        
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
        if (_teamManager != null)
        {
            TeamManager.OnActiveTeamChanged += HandleTeamChanged;
            HandleTeamChanged(_teamManager.ActiveTeam);
        }
        
        if (_inputManager != null)
        {
            _inputManager.GameplayActions.TriggerUi.performed += OnTriggerUiPerformed;
        }
    }

    void OnDisable()
    {
        if (_teamManager != null)
        {
            TeamManager.OnActiveTeamChanged -= HandleTeamChanged;
        }
        
        if (_inputManager != null)
        {
            _inputManager.GameplayActions.TriggerUi.performed -= OnTriggerUiPerformed;
        }
        
        CleanupAllClonedPanels();
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
        Debug.Log($"[SummoningUIController] Active team changed. New team size: {activeTeam.Count}");
        PopulateSummonCards(activeTeam);
    }
    
    private void OnTriggerUiPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        ToggleGlobalDisplayMode();
    }
    #endregion

    #region Global Display Mode - NOUVEAU
    private void ToggleGlobalDisplayMode()
    {
        _isGlobalDisplayMode = !_isGlobalDisplayMode;
        
        Debug.Log($"[SummoningUIController] Mode global : {(_isGlobalDisplayMode ? "ON" : "OFF")}");
        
        if (_isGlobalDisplayMode)
        {
            // Cacher le panneau original s'il est visible
            if (currentPanelAnimation != null) StopCoroutine(currentPanelAnimation);
            currentPanelAnimation = StartCoroutine(AnimateInfoPanel(false));
            
            // Créer et afficher tous les panneaux clonés
            ShowAllClonedPanels();
        }
        else
        {
            // Cacher et détruire tous les panneaux clonés
            HideAllClonedPanels();
        }
    }
    
    private void ShowAllClonedPanels()
    {
        foreach (var card in summonCards)
        {
            if (card.CardRoot.activeSelf && card.CharacterData != null)
            {
                CreateAndShowClonedPanel(card);
            }
        }
    }
    
    private void HideAllClonedPanels()
    {
        foreach (var card in summonCards)
        {
            if (card.ClonedInfoPanel != null)
            {
                Destroy(card.ClonedInfoPanel);
                card.ClonedInfoPanel = null;
            }
        }
    }
    
    private void CreateAndShowClonedPanel(SummoningCardUI card)
    {
        if (infoPanelObject == null || card.CharacterData == null) return;
        
        // Créer le clone
        card.ClonedInfoPanel = Instantiate(infoPanelObject);
        card.ClonedInfoPanel.name = $"InfoPanel_Clone_{summonCards.IndexOf(card)}";
        
        // Setup du clone
        card.ClonedPanelCanvasGroup = card.ClonedInfoPanel.GetComponent<CanvasGroup>() ?? 
                                     card.ClonedInfoPanel.AddComponent<CanvasGroup>();
        
        card.ClonedInfoPanel.transform.SetParent(card.CardRoot.transform);
        card.ClonedInfoPanel.transform.localScale = Vector3.one; // Ensure correct scale
        RectTransform clonedRect = card.ClonedInfoPanel.GetComponent<RectTransform>();
        if (clonedRect != null && infoPanelRectTransform != null)
        {
            // Copier toutes les propriétés importantes du RectTransform
            clonedRect.sizeDelta = infoPanelRectTransform.sizeDelta;
            clonedRect.anchorMin = infoPanelRectTransform.anchorMin;
            clonedRect.anchorMax = infoPanelRectTransform.anchorMax;
            clonedRect.pivot = infoPanelRectTransform.pivot;
            clonedRect.anchoredPosition = new Vector2(0, 150f); // Position finale de l'animation hover
        }
        
        // Mettre à jour le contenu
        UpdateClonedPanelContent(card);
        
        // Afficher immédiatement (pas d'animation pour cette première étape)
        card.ClonedInfoPanel.SetActive(true);
        card.ClonedPanelCanvasGroup.alpha = 1f;
    }
    
    private void UpdateClonedPanelContent(SummoningCardUI card)
    {
        if (card.ClonedInfoPanel == null || card.CharacterData == null) return;
        
        // Trouver les composants de texte dans le clone
        TextMeshProUGUI clonedTitle = card.ClonedInfoPanel.transform.Find("Text_Title")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI clonedSequence = card.ClonedInfoPanel.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
        
        if (clonedTitle != null) clonedTitle.text = card.CharacterData.DisplayName;
        if (clonedSequence != null) clonedSequence.text = FormatSequence(card.CharacterData.InvocationSequence);
    }
    
    private void CleanupAllClonedPanels()
    {
        foreach (var card in summonCards)
        {
            if (card.ClonedInfoPanel != null)
            {
                Destroy(card.ClonedInfoPanel);
                card.ClonedInfoPanel = null;
            }
        }
    }
    #endregion

    #region UI Logic
	private void PopulateSummonCards(List<CharacterData_SO> activeTeam)
    {
        for (int i = 0; i < summonCards.Count; i++)
        {
            SummoningCardUI card = summonCards[i];
            if (i < activeTeam.Count && activeTeam[i] != null)
            {
                CharacterData_SO data = activeTeam[i];
                card.CharacterData = data;
                card.CardRoot.SetActive(true);
                card.CostText.text = data.GoldCost.ToString();
                

                // 1. Nettoyer le conteneur des anciens visuels
                foreach (Transform child in card.characterVisualsContainer)
                {
                    Destroy(child.gameObject);
                }

                // 2. Vérifier si un préfab d'animation est disponible
                if (data.MenuAnimationPrefab != null)
                {
                    // 2a. Instancier le préfab d'animation
                    GameObject animInstance = Instantiate(data.MenuAnimationPrefab, card.characterVisualsContainer);
                    
                    // Réinitialiser la transformation locale pour un affichage correct
                    animInstance.transform.localPosition = Vector3.zero;
                    animInstance.transform.localRotation = Quaternion.identity;
					animInstance.transform.localScale = Vector3.one * 0.5f;


                    // Désactiver l'icône statique pour ne pas qu'elle s'affiche derrière
                    card.CharacterIcon.enabled = false;
                    Debug.Log($"[SummoningUIController] Card {i + 1}: Prefab d'animation '{data.MenuAnimationPrefab.name}' instancié pour {data.DisplayName}.");
                }
                else
                {
                    // 2b. Fallback : Utiliser l'icône statique
                    card.CharacterIcon.enabled = true;
                    card.CharacterIcon.sprite = data.Icon;
                    Debug.Log($"[SummoningUIController] Card {i + 1}: Utilisation de l'icône statique pour {data.DisplayName} (pas de préfab d'animation).");
                }

                AddHoverEventsToCard(card);
            }
            else
            {
                card.CardRoot.SetActive(false);
                card.CharacterData = null;
                if (card.ClonedInfoPanel != null)
                {
                    Destroy(card.ClonedInfoPanel);
                    card.ClonedInfoPanel = null;
                }
            }
        }
        
        if (_isGlobalDisplayMode)
        {
            ShowAllClonedPanels();
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
        if (_unitSpawner == null) return;
        var cooldowns = _unitSpawner.UnitCooldowns; 

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

                        float totalCooldown = card.CharacterData.InvocationCooldown * _musicManager.GetBeatDuration();
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
        if (_isGlobalDisplayMode) return;
        
        if (infoPanelObject == null || hoveredCard.CharacterData == null) return;

        infoPanelTitleText.text = hoveredCard.CharacterData.DisplayName;
        infoPanelSequenceText.text = FormatSequence(hoveredCard.CharacterData.InvocationSequence);

        infoPanelRectTransform.SetParent(hoveredCard.CardRoot.transform);

        if (currentPanelAnimation != null) StopCoroutine(currentPanelAnimation);
        currentPanelAnimation = StartCoroutine(AnimateInfoPanel(true));
    }

    private void OnCardHoverExit()
    {
        if (_isGlobalDisplayMode) return;
        
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