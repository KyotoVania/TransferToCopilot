using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections;
using Hub;
using ScriptableObjects;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the team composition interface in the Hub, allowing players to view, select, and organize their active team.
/// Provides navigation between character details, equipment panels, and character selection screens.
/// Implements focus memory system for seamless navigation between sub-panels.
/// </summary>
public class TeamManagementUI : MonoBehaviour
{
    #region Manager References
    
    [Header("Références des Managers")]
    /// <summary>
    /// Reference to the PlayerDataManager singleton for accessing player progression and unlocked characters.
    /// </summary>
    private PlayerDataManager _playerDataManager;
    
    /// <summary>
    /// Reference to the TeamManager singleton for managing active team composition and character assignments.
    /// </summary>
    private TeamManager _teamManager;
    
    /// <summary>
    /// Reference to the HubManager for controlling navigation flow and UI state management within the Hub.
    /// </summary>
    private HubManager _hubManager;

    #endregion

    #region UI Prefabs
    
    [Header("Prefabs UI")]
    /// <summary>
    /// Prefab template for team slot UI elements. Must contain a TeamSlotUI component.
    /// Used to instantiate the 4 active team slots dynamically.
    /// </summary>
    [Tooltip("Prefab pour un slot de l'équipe active (le nouveau prefab avec le script TeamSlotUI).")]
    [SerializeField] private GameObject teamSlotPrefab;

    #endregion

    #region UI Containers
    
    [Header("Conteneurs UI")]
    /// <summary>
    /// Parent transform for the 4 active team slots. Should have a Horizontal Layout Group component.
    /// Team slot prefabs will be instantiated as children of this container.
    /// </summary>
    [Tooltip("Parent des 4 slots de l'équipe active. Doit avoir un Horizontal Layout Group.")]
    [SerializeField] private Transform activeTeamSlotsContainer;

    #endregion

    #region Character Details Panel
    
    [Header("Panel de Détails Personnage (Optionnel)")]
    /// <summary>
    /// Optional panel that displays detailed information about the currently selected character.
    /// Can be null if character details are handled by another system.
    /// </summary>
    [SerializeField] private GameObject characterDetailsPanel;
    
    /// <summary>
    /// Text component displaying the selected character's display name.
    /// </summary>
    [SerializeField] private TextMeshProUGUI detailCharacterNameText;
    
    /// <summary>
    /// Image component showing the selected character's portrait or icon.
    /// </summary>
    [SerializeField] private Image detailCharacterIconImage;
    
    /// <summary>
    /// Text component displaying the selected character's lore description.
    /// </summary>
    [SerializeField] private TextMeshProUGUI detailCharacterDescriptionText;
    
    /// <summary>
    /// Text component showing the selected character's combat statistics and abilities.
    /// </summary>
    [SerializeField] private TextMeshProUGUI detailCharacterStatsText;

    #endregion

    #region UI Buttons
    
    [Header("Boutons UI")]
    /// <summary>
    /// Button to return to the previous Hub screen or close the team management panel.
    /// </summary>
    [SerializeField] private Button backButton;
    
    /// <summary>
    /// Button to confirm team composition and proceed to level selection or gameplay.
    /// </summary>
    [SerializeField] private Button readyButton;

    #endregion

    #region Connected Panels
    
    [Header("Panels Connectés")]
    /// <summary>
    /// Reference to the character selection panel for adding new characters to the team.
    /// Used for seamless navigation between team management and character recruitment.
    /// </summary>
    [SerializeField] private GameObject characterSelectionPanel;
    
    /// <summary>
    /// Reference to the equipment management panel for customizing character loadouts.
    /// Allows players to modify weapons, armor, and abilities for team members.
    /// </summary>
    [SerializeField] private EquipmentPanelUI equipmentPanel;

    #endregion

    #region Navigation System
    
    [Header("Navigation à la manette")]
    /// <summary>
    /// The default UI element to select when the panel is first opened.
    /// Ensures proper gamepad/keyboard navigation initialization.
    /// </summary>
    [Tooltip("Le premier élément sélectionné quand on ouvre le panel")]
    [SerializeField] private GameObject defaultSelectedObject;

    #endregion

    #region Private Fields
    
    /// <summary>
    /// Collection of instantiated team slot UI components for dynamic management.
    /// Cleared and repopulated when the team composition changes.
    /// </summary>
    private readonly List<TeamSlotUI> _instantiatedTeamSlots = new List<TeamSlotUI>();
    
    /// <summary>
    /// Currently selected character for detailed view. Can be null if no character is selected.
    /// Used to populate the character details panel and track selection state.
    /// </summary>
    private CharacterData_SO _selectedCharacterForDetails = null;

    #endregion

    #region Focus Memory System
    
    /// <summary>
    /// Data structure for remembering UI focus state when transitioning between panels.
    /// Enables seamless navigation experience by restoring the previous selection when returning from sub-panels.
    /// </summary>
    [System.Serializable]
    private class FocusMemory
    {
        /// <summary>
        /// Index of the last selected team slot. -1 indicates no slot was selected.
        /// </summary>
        public int lastSelectedSlotIndex = -1;
        
        /// <summary>
        /// Whether the last selected slot was empty (add character slot) or contained a character.
        /// </summary>
        public bool wasLastSelectedSlotEmpty = false;
        
        /// <summary>
        /// Whether the back button was the last selected UI element.
        /// </summary>
        public bool wasBackButtonSelected = false;
        
        /// <summary>
        /// Whether the ready button was the last selected UI element.
        /// </summary>
        public bool wasReadyButtonSelected = false;
        
        /// <summary>
        /// Resets all focus memory values to their default state.
        /// </summary>
        public void Reset()
        {
            lastSelectedSlotIndex = -1;
            wasLastSelectedSlotEmpty = false;
            wasBackButtonSelected = false;
            wasReadyButtonSelected = false;
        }
        
        /// <summary>
        /// Checks if there is valid focus information stored in memory.
        /// </summary>
        /// <returns>True if there is a valid focus state to restore, false otherwise.</returns>
        public bool HasValidMemory()
        {
            return lastSelectedSlotIndex >= 0 || wasBackButtonSelected || wasReadyButtonSelected;
        }
    }
    
    /// <summary>
    /// Instance of the focus memory system for tracking UI navigation state.
    /// </summary>
    private FocusMemory _focusMemory = new FocusMemory();
    
    /// <summary>
    /// Flag indicating whether we're currently transitioning to a sub-panel.
    /// Prevents the Hub controls from being re-enabled prematurely.
    /// </summary>
    private bool _isTransitioningToSubPanel = false;

    #endregion

    #region Cycle de Vie Unity

    private void Awake()
    {
        _playerDataManager = PlayerDataManager.Instance;
        _teamManager = TeamManager.Instance;
        _hubManager = FindFirstObjectByType<HubManager>();

        // Validation des références
        if (_teamManager == null) Debug.LogError("[TeamManagementUI] TeamManager.Instance est null !");
        if (_hubManager == null) Debug.LogError("[TeamManagementUI] HubManager non trouvé !");
        if (teamSlotPrefab == null) Debug.LogError("[TeamManagementUI] Prefab 'teamSlotPrefab' non assigné !");
        if (activeTeamSlotsContainer == null) Debug.LogError("[TeamManagementUI] Conteneur 'activeTeamSlotsContainer' non assigné !");

        backButton?.onClick.AddListener(OnBackButtonClicked);
        readyButton?.onClick.AddListener(OnBackButtonClicked);
    }

    private void OnEnable()
    {
        Debug.Log("[TeamManagementUI] Panel activé - Prise de contrôle");
        
        // 🎯 PRISE DE CONTRÔLE IMMÉDIATE
        TakeControlFromHub();
        
        // S'abonner aux événements
        TeamManager.OnActiveTeamChanged += HandleActiveTeamChanged;
        
        // Rafraîchir l'UI
        RefreshAllUI();
        SelectCharacterForDetails(null);
        
        // ⭐ LOGIQUE DE RESTAURATION DU FOCUS
        if (_isTransitioningToSubPanel)
        {
            // On revient d'un sous-panel, restaurer le focus mémorisé
            _isTransitioningToSubPanel = false;
            StartCoroutine(RestoreRememberedFocus());
        }
        else
        {
            // Première ouverture du panel, sélection initiale normale
            StartCoroutine(SetupInitialSelection());
        }
    }

    private void OnDisable()
    {
        Debug.Log("[TeamManagementUI] Panel désactivé - Libération du contrôle");
        
        // Se désabonner
        if (TeamManager.Instance != null)
        {
            TeamManager.OnActiveTeamChanged -= HandleActiveTeamChanged;
        }
        
        // ⚠️ IMPORTANT : On ne rend le contrôle au Hub QUE si on ne va pas vers un sous-panel
        if (!_isTransitioningToSubPanel)
        {
            ReturnControlToHub();
        }
    }

    private void Update()
    {
        // Gérer l'action Cancel (B sur Xbox, Circle sur PS, Escape sur clavier)
        if (InputManager.Instance != null && InputManager.Instance.UIActions.Cancel.WasPressedThisFrame())
        {
            OnCancelPressed();
        }
        
        // S'assurer qu'on a toujours quelque chose de sélectionné
        EnsureSelection();
    }

    #endregion

    #region Système de Contrôle Hub

    /// <summary>
    /// Prend le contrôle total de la navigation et désactive les contrôles du Hub
    /// </summary>
    private void TakeControlFromHub()
    {
        if (_hubManager != null)
        {
            _hubManager.DisableHubControls();
            Debug.Log("[TeamManagementUI] ✅ Contrôles du Hub désactivés");
        }
    }

    /// <summary>
    /// Rend le contrôle au Hub quand on quitte définitivement ce panel
    /// </summary>
    private void ReturnControlToHub()
    {
        if (_hubManager != null)
        {
            _hubManager.EnableHubControls();
            Debug.Log("[TeamManagementUI] ✅ Contrôles du Hub réactivés");
        }
    }

    #endregion

    #region Système de Mémorisation du Focus

    /// <summary>
    /// Mémorise l'état de sélection actuel avant de partir vers un sous-panel
    /// </summary>
    private void MemorizeCurrentFocus()
    {
        _focusMemory.Reset();
        
        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        if (currentSelected == null)
        {
            Debug.LogWarning("[TeamManagementUI] Aucun objet sélectionné à mémoriser");
            return;
        }

        // Vérifier si c'est un des boutons principaux
        if (currentSelected == backButton?.gameObject)
        {
            _focusMemory.wasBackButtonSelected = true;
            Debug.Log("[TeamManagementUI] 💾 Mémorisé : BackButton");
            return;
        }
        
        if (currentSelected == readyButton?.gameObject)
        {
            _focusMemory.wasReadyButtonSelected = true;
            Debug.Log("[TeamManagementUI] 💾 Mémorisé : ReadyButton");
            return;
        }

        // Vérifier si c'est un slot d'équipe
        for (int i = 0; i < _instantiatedTeamSlots.Count; i++)
        {
            var slot = _instantiatedTeamSlots[i];
            if (slot != null)
            {
                Button slotButton = slot.GetMainButton();
                if (slotButton != null && currentSelected == slotButton.gameObject)
                {
                    _focusMemory.lastSelectedSlotIndex = i;
                    _focusMemory.wasLastSelectedSlotEmpty = !slot.HasCharacter();
                    Debug.Log($"[TeamManagementUI] 💾 Mémorisé : Slot {i} (vide: {_focusMemory.wasLastSelectedSlotEmpty})");
                    return;
                }
            }
        }
        
        Debug.LogWarning($"[TeamManagementUI] Objet sélectionné non reconnu : {currentSelected.name}");
    }

    /// <summary>
    /// Restaure le focus mémorisé après retour d'un sous-panel
    /// </summary>
    private IEnumerator RestoreRememberedFocus()
    {
        yield return null; // Attendre une frame pour que tout soit initialisé
        
        if (!_focusMemory.HasValidMemory())
        {
            Debug.LogWarning("[TeamManagementUI] Aucune mémoire valide, utilisation de la sélection par défaut");
            yield return StartCoroutine(SetupInitialSelection());
            yield break;
        }

        GameObject targetObject = null;

        // Restaurer la sélection des boutons principaux
        if (_focusMemory.wasBackButtonSelected && backButton != null)
        {
            targetObject = backButton.gameObject;
            Debug.Log("[TeamManagementUI] 🎯 Restauration : BackButton");
        }
        else if (_focusMemory.wasReadyButtonSelected && readyButton != null)
        {
            targetObject = readyButton.gameObject;
            Debug.Log("[TeamManagementUI] 🎯 Restauration : ReadyButton");
        }
        // Restaurer la sélection d'un slot
        else if (_focusMemory.lastSelectedSlotIndex >= 0 && _focusMemory.lastSelectedSlotIndex < _instantiatedTeamSlots.Count)
        {
            var slot = _instantiatedTeamSlots[_focusMemory.lastSelectedSlotIndex];
            if (slot != null)
            {
                // ⚡ LOGIQUE INTELLIGENTE : Adapter selon l'état actuel du slot
                bool slotIsCurrentlyEmpty = !slot.HasCharacter();
                
                if (_focusMemory.wasLastSelectedSlotEmpty && slotIsCurrentlyEmpty)
                {
                    // Le slot était vide et l'est toujours → sélectionner le bouton Add
                    targetObject = slot.GetAddButton()?.gameObject;
                    Debug.Log($"[TeamManagementUI] 🎯 Restauration : Slot {_focusMemory.lastSelectedSlotIndex} (Add Button)");
                }
                else if (!_focusMemory.wasLastSelectedSlotEmpty && !slotIsCurrentlyEmpty)
                {
                    // Le slot avait un personnage et en a toujours un → sélectionner le bouton principal
                    targetObject = slot.GetMainButton()?.gameObject;
                    Debug.Log($"[TeamManagementUI] 🎯 Restauration : Slot {_focusMemory.lastSelectedSlotIndex} (Main Button)");
                }
                else
                {
                    // L'état du slot a changé → adapter intelligemment
                    targetObject = slot.GetMainButton()?.gameObject;
                    Debug.Log($"[TeamManagementUI] 🎯 Restauration adaptée : Slot {_focusMemory.lastSelectedSlotIndex} (état changé)");
                }
            }
        }

        // Appliquer la sélection
        if (targetObject != null && targetObject.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(targetObject);
            Debug.Log($"[TeamManagementUI] ✅ Focus restauré sur : {targetObject.name}");
        }
        else
        {
            Debug.LogWarning("[TeamManagementUI] Impossible de restaurer le focus, fallback vers sélection par défaut");
            yield return StartCoroutine(SetupInitialSelection());
        }
    }

    #endregion

    #region Méthodes de Transition Améliorées

    /// <summary>
    /// Transition améliorée vers un sous-panel avec mémorisation du focus
    /// </summary>
    private IEnumerator TransitionToSubPanel(GameObject panelToShow)
    {
        // 🧠 MÉMORISER LE FOCUS AVANT DE PARTIR
        MemorizeCurrentFocus();
        
        // Marquer qu'on va vers un sous-panel
        _isTransitioningToSubPanel = true;
        
        // Effectuer la transition visuelle
        CanvasGroup currentCanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        CanvasGroup nextCanvasGroup = panelToShow.GetComponent<CanvasGroup>() ?? panelToShow.AddComponent<CanvasGroup>();
        
        float duration = 0.25f;
        float elapsedTime = 0f;

        panelToShow.SetActive(true);
        nextCanvasGroup.alpha = 0;

        while (elapsedTime < duration)
        {
            currentCanvasGroup.alpha = 1f - (elapsedTime / duration);
            nextCanvasGroup.alpha = elapsedTime / duration;
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }

        currentCanvasGroup.alpha = 0;
        nextCanvasGroup.alpha = 1;
        gameObject.SetActive(false);
    }

    #endregion

    #region Callbacks des Slots UI (Modifiés)
    
    private void HandleActiveTeamChanged(List<CharacterData_SO> newActiveTeam)
    {
        Debug.Log("[TeamManagementUI] L'équipe active a changé. Rafraîchissement de l'UI.");
        RefreshAllUI();
        
        if (_selectedCharacterForDetails != null && !newActiveTeam.Contains(_selectedCharacterForDetails))
        {
            SelectCharacterForDetails(null);
        }
    }
    
    private void OnRemoveCharacter(CharacterData_SO characterData)
    {
        if (characterData != null)
        {
            _teamManager.TryRemoveCharacterFromActiveTeam(characterData);
            if (_selectedCharacterForDetails == characterData)
            {
                SelectCharacterForDetails(null);
            }
        }
    }

    /// <summary>
    /// Appelé quand on clique sur un slot "Add" - Version améliorée
    /// </summary>
    private void OnAddCharacterSlotClicked(int slotIndex)
    {
        Debug.Log($"[TeamManagementUI] Clic sur le slot vide numéro {slotIndex}. Transition vers CharacterSelection.");
    
        if (characterSelectionPanel != null)
        {
            StartCoroutine(TransitionToSubPanel(characterSelectionPanel));
        }
        else
        {
            Debug.LogError("[TeamManagementUI] La référence vers 'characterSelectionPanel' n'est pas assignée !");
        }
    }

    private void OnShowEquipmentPanel(CharacterData_SO character)
    {
        if (equipmentPanel != null)
        {
            Debug.Log($"[TeamManagementUI] Transition vers EquipmentPanel pour {character.DisplayName}");
            
            // Même logique de mémorisation pour l'equipment panel
            MemorizeCurrentFocus();
            _isTransitioningToSubPanel = true;
            
            gameObject.SetActive(false);
            equipmentPanel.ShowPanelFor(character);
        }
        else
        {
            Debug.LogError("[TeamManagementUI] EquipmentPanel reference is not set!");
        }
    }

    #endregion

    #region Logique Existante (Inchangée)

    private void RefreshAllUI()
    {
        PopulateActiveTeamSlots();
        UpdateCharacterDetailsPanel();
        ConfigureNavigation();
    }

    private void PopulateActiveTeamSlots()
    {
        foreach (Transform child in activeTeamSlotsContainer)
        {
            Destroy(child.gameObject);
        }
        _instantiatedTeamSlots.Clear();

        if (_teamManager == null) return;

        List<CharacterData_SO> activeTeam = _teamManager.ActiveTeam;

        for (int i = 0; i < 4; i++)
        {
            GameObject slotGO = Instantiate(teamSlotPrefab, activeTeamSlotsContainer);
            TeamSlotUI slotUI = slotGO.GetComponent<TeamSlotUI>();

            if (slotUI != null)
            {
                CharacterData_SO characterInSlot = (i < activeTeam.Count) ? activeTeam[i] : null;
            
                int characterLevel = 1;
                if (characterInSlot != null && _playerDataManager.Data.CharacterProgressData.ContainsKey(characterInSlot.CharacterID))
                {
                    characterLevel = _playerDataManager.Data.CharacterProgressData[characterInSlot.CharacterID].CurrentLevel;
                }
                
                slotUI.Setup(characterInSlot, i, OnRemoveCharacter, OnAddCharacterSlotClicked, OnShowEquipmentPanel, characterLevel);
                _instantiatedTeamSlots.Add(slotUI);
            }
            else
            {
                Debug.LogError($"[TeamManagementUI] Le prefab 'teamSlotPrefab' n'a pas de script TeamSlotUI !");
                Destroy(slotGO);
            }
        }
    }

    private void ConfigureNavigation()
    {
        // Navigation automatique gérée par Unity via Horizontal Layout Group
    }

    private IEnumerator SetupInitialSelection()
    {
        yield return null;
        
        GameObject objectToSelect = null;
        
        if (defaultSelectedObject != null && defaultSelectedObject.activeInHierarchy)
        {
            objectToSelect = defaultSelectedObject;
        }
        else
        {
            foreach (var slot in _instantiatedTeamSlots)
            {
                if (slot != null && slot.HasCharacter())
                {
                    Button mainButton = slot.GetMainButton();
                    if (mainButton != null)
                    {
                        objectToSelect = mainButton.gameObject;
                        break;
                    }
                }
            }
            
            if (objectToSelect == null)
            {
                foreach (var slot in _instantiatedTeamSlots)
                {
                    if (slot != null && !slot.HasCharacter())
                    {
                        Button addButton = slot.GetAddButton();
                        if (addButton != null)
                        {
                            objectToSelect = addButton.gameObject;
                            break;
                        }
                    }
                }
            }
            
            if (objectToSelect == null && backButton != null)
            {
                objectToSelect = backButton.gameObject;
            }
        }
        
        if (objectToSelect != null)
        {
            EventSystem.current.SetSelectedGameObject(objectToSelect);
            Debug.Log($"[TeamManagementUI] Sélection initiale : {objectToSelect.name}");
        }
    }

    private void EnsureSelection()
    {
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            Vector2 navigationInput = InputManager.Instance?.UIActions.Navigate.ReadValue<Vector2>() ?? Vector2.zero;
            bool submitPressed = InputManager.Instance?.UIActions.Submit.WasPressedThisFrame() ?? false;
            
            if (navigationInput != Vector2.zero || submitPressed)
            {
                StartCoroutine(SetupInitialSelection());
            }
        }
    }

    private void SelectCharacterForDetails(CharacterData_SO characterData)
    {
        _selectedCharacterForDetails = characterData;
        UpdateCharacterDetailsPanel();
    }

    private void UpdateCharacterDetailsPanel()
    {
        if (characterDetailsPanel == null) return;
        characterDetailsPanel.SetActive(false);
    }

    private void OnCancelPressed()
    {
        OnBackButtonClicked();
    }

    private void OnBackButtonClicked()
    {
        // Marquer qu'on ne va PAS vers un sous-panel mais qu'on retourne au Hub
        _isTransitioningToSubPanel = false;
        _hubManager?.GoToGeneralView();
    }

    #endregion
}