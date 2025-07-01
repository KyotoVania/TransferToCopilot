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

public class TeamManagementUI : MonoBehaviour
{
    [Header("Références des Managers")]
    private PlayerDataManager _playerDataManager;
    private TeamManager _teamManager;
    private HubManager _hubManager;

    [Header("Prefabs UI")]
    [Tooltip("Prefab pour un slot de l'équipe active (le nouveau prefab avec le script TeamSlotUI).")]
    [SerializeField] private GameObject teamSlotPrefab;

    [Header("Conteneurs UI")]
    [Tooltip("Parent des 4 slots de l'équipe active. Doit avoir un Horizontal Layout Group.")]
    [SerializeField] private Transform activeTeamSlotsContainer;

    [Header("Panel de Détails Personnage (Optionnel)")]
    // Gardé pour l'affichage des détails si vous cliquez sur un perso
    [SerializeField] private GameObject characterDetailsPanel;
    [SerializeField] private TextMeshProUGUI detailCharacterNameText;
    [SerializeField] private Image detailCharacterIconImage;
    [SerializeField] private TextMeshProUGUI detailCharacterDescriptionText;
    [SerializeField] private TextMeshProUGUI detailCharacterStatsText;

    [Header("Boutons UI")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button readyButton; // Le bouton "Ready" du nouveau design
    
    [Header("Panels Connectés")]
    [SerializeField] private GameObject characterSelectionPanel;
    [SerializeField] private EquipmentPanelUI equipmentPanel;

    [Header("Navigation à la manette")]
    [Tooltip("Le premier élément sélectionné quand on ouvre le panel")]
    [SerializeField] private GameObject defaultSelectedObject;

    private readonly List<TeamSlotUI> _instantiatedTeamSlots = new List<TeamSlotUI>();
    private CharacterData_SO _selectedCharacterForDetails = null;
    
    // Pour tracker la sélection actuelle
    private GameObject _lastSelectedObject;

    #region Cycle de Vie Unity

    private void Awake()
    {
        _playerDataManager = PlayerDataManager.Instance;
        _teamManager = TeamManager.Instance;
        _hubManager = FindFirstObjectByType<HubManager>();

        // --- Validation des références ---
        if (_teamManager == null) Debug.LogError("[TeamManagementUI] TeamManager.Instance est null !");
        if (_hubManager == null) Debug.LogError("[TeamManagementUI] HubManager non trouvé !");
        if (teamSlotPrefab == null) Debug.LogError("[TeamManagementUI] Prefab 'teamSlotPrefab' non assigné !");
        if (activeTeamSlotsContainer == null) Debug.LogError("[TeamManagementUI] Conteneur 'activeTeamSlotsContainer' non assigné !");

        backButton?.onClick.AddListener(OnBackButtonClicked);
        readyButton?.onClick.AddListener(OnBackButtonClicked); // Le bouton Ready fait la même chose pour l'instant
    }

// Extension du TeamSlotUI pour exposer les boutons nécessaires à la navigation
public static class TeamSlotUIExtensions
{
    // Ces méthodes devront être ajoutées à TeamSlotUI.cs
    // public Button GetMainButton() { return mainCardButton; }
    // public Button GetAddButton() { return addButton; }
    // public bool HasCharacter() { return _characterData != null; }
}

    private void OnEnable()
    {
        // S'abonner aux événements
        TeamManager.OnActiveTeamChanged += HandleActiveTeamChanged;
        RefreshAllUI();
        SelectCharacterForDetails(null);
        if (HubManager.Instance != null)
        {
            HubManager.Instance.DisableHubControls();
        }
        // Configuration de la navigation à la manette
        StartCoroutine(SetupInitialSelection());
    }

    private void OnDisable()
    {
        // Se désabonner
        if (TeamManager.Instance != null)
        {
            TeamManager.OnActiveTeamChanged -= HandleActiveTeamChanged;
        }
        if (HubManager.Instance != null)
        {
            HubManager.Instance.EnableHubControls();
        }
        
        // Sauvegarder la sélection actuelle
        _lastSelectedObject = EventSystem.current.currentSelectedGameObject;
    }

    private void Update()
    {
        // Gérer l'action Cancel (B sur Xbox, Circle sur PS, Escape sur clavier)
        if (InputManager.Instance != null && InputManager.Instance.UIActions.Cancel.WasPressedThisFrame())
        {
            OnCancelPressed();
        }
        
        // S'assurer qu'on a toujours quelque chose de sélectionné pour la navigation manette
        EnsureSelection();
    }

    #endregion

    #region Initialisation et Rafraîchissement de l'UI

    private void RefreshAllUI()
    {
        PopulateActiveTeamSlots();
        UpdateCharacterDetailsPanel();
        ConfigureNavigation();
    }

    private void PopulateActiveTeamSlots()
    {
        // Nettoyer les anciens slots
        foreach (Transform child in activeTeamSlotsContainer)
        {
            Destroy(child.gameObject);
        }
        _instantiatedTeamSlots.Clear();

        if (_teamManager == null) return;

        List<CharacterData_SO> activeTeam = _teamManager.ActiveTeam;

        // Toujours instancier 4 slots
        for (int i = 0; i < 4; i++)
        {
            GameObject slotGO = Instantiate(teamSlotPrefab, activeTeamSlotsContainer);
            TeamSlotUI slotUI = slotGO.GetComponent<TeamSlotUI>();

            if (slotUI != null)
            {
                CharacterData_SO characterInSlot = (i < activeTeam.Count) ? activeTeam[i] : null;
            
				int characterLevel = 1; // Niveau par défaut si aucune progression n'est trouvée
            	if (characterInSlot != null && _playerDataManager.Data.CharacterProgressData.ContainsKey(characterInSlot.CharacterID))
            	{
                	characterLevel = _playerDataManager.Data.CharacterProgressData[characterInSlot.CharacterID].CurrentLevel;
            	}
            	// Passe le niveau au slot UI
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
        // Configurer la navigation automatique entre les slots
        // Unity gère automatiquement la navigation horizontale grâce au Horizontal Layout Group
        
        // Mais on peut aussi configurer la navigation explicite si nécessaire
        /*for (int i = 0; i < _instantiatedTeamSlots.Count; i++)
        {
            var slotUI = _instantiatedTeamSlots[i];
            if (slotUI == null) continue;
            
            // Récupérer le bouton principal du slot
            Button mainButton = slotUI.GetMainButton();
            if (mainButton != null)
            {
                Navigation nav = mainButton.navigation;
                nav.mode = Navigation.Mode.Explicit;
                
                // Navigation horizontale entre les slots
                if (i > 0) // Pas le premier
                {
                    Button leftButton = _instantiatedTeamSlots[i - 1].GetMainButton();
                    nav.selectOnLeft = leftButton;
                }
                
                if (i < _instantiatedTeamSlots.Count - 1) // Pas le dernier
                {
                    Button rightButton = _instantiatedTeamSlots[i + 1].GetMainButton();
                    nav.selectOnRight = rightButton;
                }
                
                // Navigation vers les boutons du bas
                nav.selectOnDown = backButton; // Ou readyButton selon votre préférence
                
                mainButton.navigation = nav;
            }
        }
        
        // Configurer la navigation pour les boutons Back et Ready
        if (backButton != null && readyButton != null)
        {
            Navigation backNav = backButton.navigation;
            backNav.mode = Navigation.Mode.Explicit;
            backNav.selectOnRight = readyButton;
            if (_instantiatedTeamSlots.Count > 0 && _instantiatedTeamSlots[0].GetMainButton() != null)
            {
                backNav.selectOnUp = _instantiatedTeamSlots[0].GetMainButton();
            }
            backButton.navigation = backNav;
            
            Navigation readyNav = readyButton.navigation;
            readyNav.mode = Navigation.Mode.Explicit;
            readyNav.selectOnLeft = backButton;
            if (_instantiatedTeamSlots.Count > 0 && _instantiatedTeamSlots[_instantiatedTeamSlots.Count - 1].GetMainButton() != null)
            {
                readyNav.selectOnUp = _instantiatedTeamSlots[_instantiatedTeamSlots.Count - 1].GetMainButton();
            }
            readyButton.navigation = readyNav;
        }
        */
    }

    private IEnumerator SetupInitialSelection()
    {
        // Attendre une frame pour s'assurer que tout est bien initialisé
        yield return null;
        
        GameObject objectToSelect = null;
        
        // Priorité de sélection:
        // 1. L'objet qu'on avait sélectionné avant (si toujours valide)
        // 2. Le defaultSelectedObject configuré dans l'inspecteur
        // 3. Le premier slot avec un personnage
        // 4. Le premier slot vide (bouton Add)
        // 5. Le bouton Back
        
        if (_lastSelectedObject != null && _lastSelectedObject.activeInHierarchy)
        {
            objectToSelect = _lastSelectedObject;
        }
        else if (defaultSelectedObject != null && defaultSelectedObject.activeInHierarchy)
        {
            objectToSelect = defaultSelectedObject;
        }
        else
        {
            // Chercher le premier slot avec un personnage
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
            
            // Si aucun personnage, prendre le premier slot vide
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
            
            // En dernier recours, sélectionner le bouton Back
            if (objectToSelect == null && backButton != null)
            {
                objectToSelect = backButton.gameObject;
            }
        }
        
        // Sélectionner l'objet
        if (objectToSelect != null)
        {
            EventSystem.current.SetSelectedGameObject(objectToSelect);
        }
    }

    private void EnsureSelection()
    {
        // Si rien n'est sélectionné et qu'on utilise une manette/clavier
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            // Vérifier si on utilise la navigation (pas la souris)
            Vector2 navigationInput = InputManager.Instance?.UIActions.Navigate.ReadValue<Vector2>() ?? Vector2.zero;
            bool submitPressed = InputManager.Instance?.UIActions.Submit.WasPressedThisFrame() ?? false;
            
            if (navigationInput != Vector2.zero || submitPressed)
            {
                StartCoroutine(SetupInitialSelection());
            }
        }
    }

    #endregion

    #region Callbacks des Slots UI
    
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
            // La mise à jour de l'UI est gérée par l'événement OnActiveTeamChanged
            if (_selectedCharacterForDetails == characterData)
            {
                SelectCharacterForDetails(null); // Cache les détails si on supprime le perso affiché
            }
        }
    }

    // Appelé quand on clique sur un slot "Add"
    private void OnAddCharacterSlotClicked(int slotIndex)
    {
        Debug.Log($"Clic sur le slot vide numéro {slotIndex}. Ouverture du panel de sélection.");
    
        if (characterSelectionPanel != null)
        {
            // Cacher le panel actuel et afficher le panel de sélection
            StartCoroutine(TransitionToPanel(characterSelectionPanel));
        }
        else
        {
            Debug.LogError("[TeamManagementUI] La référence vers 'characterSelectionPanel' n'est pas assignée !");
        }
    }

    #endregion

    #region Panel de Détails (Logique conservée pour l'instant)

    private void SelectCharacterForDetails(CharacterData_SO characterData)
    {
        _selectedCharacterForDetails = characterData;
        UpdateCharacterDetailsPanel();
    }

    private void UpdateCharacterDetailsPanel()
    {
        // Cette logique est optionnelle. Vous pouvez la supprimer si le nouveau design
        // n'inclut pas de panel de détails séparé.
        if (characterDetailsPanel == null) return;
        
        // Mettre le code d'affichage des détails ici si vous le conservez.
        // Pour l'instant, on le laisse désactivé.
        characterDetailsPanel.SetActive(false);
    }
    
  	private void OnShowEquipmentPanel(CharacterData_SO character)
    {
        if (equipmentPanel != null)
        {
            gameObject.SetActive(false); // Hide this panel
            equipmentPanel.ShowPanelFor(character);
        }
        else
        {
            Debug.LogError("[TeamManagementUI] EquipmentPanel reference is not set!");
        }
    }
    
    #endregion

    #region Navigation

    private void OnCancelPressed()
    {
        // Retourner au HubManager
        OnBackButtonClicked();
    }

    private void OnBackButtonClicked()
    {
        _hubManager?.GoToGeneralView();
    }

    #endregion
    
    private IEnumerator TransitionToPanel(GameObject panelToShow)
    {
        CanvasGroup currentPanelCanvasGroup = GetComponent<CanvasGroup>();
        if (currentPanelCanvasGroup == null) currentPanelCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        CanvasGroup nextPanelCanvasGroup = panelToShow.GetComponent<CanvasGroup>();
        if (nextPanelCanvasGroup == null) nextPanelCanvasGroup = panelToShow.AddComponent<CanvasGroup>();

        float duration = 0.25f;
        float elapsedTime = 0f;

        // ÉTAPE 1 : Préparer le nouveau panel. On l'active, mais on le rend transparent.
        panelToShow.SetActive(true);
        nextPanelCanvasGroup.alpha = 0;

        // ÉTAPE 2 : Animer les deux fondus en même temps dans une seule boucle.
        while (elapsedTime < duration)
        {
            // Le panel actuel devient de plus en plus transparent.
            currentPanelCanvasGroup.alpha = 1f - (elapsedTime / duration);
        
            // Le nouveau panel devient de plus en plus opaque.
            nextPanelCanvasGroup.alpha = elapsedTime / duration;

            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }

        // ÉTAPE 3 : S'assurer que les états finaux sont parfaits.
        currentPanelCanvasGroup.alpha = 0;
        nextPanelCanvasGroup.alpha = 1;

        // C'est SEULEMENT MAINTENANT, à la toute fin, qu'on désactive l'ancien panel.
        gameObject.SetActive(false);
    }
}