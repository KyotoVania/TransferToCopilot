using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections;
using Hub;
using ScriptableObjects;

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

    private readonly List<TeamSlotUI> _instantiatedTeamSlots = new List<TeamSlotUI>();
    private CharacterData_SO _selectedCharacterForDetails = null;

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

    private void OnEnable()
    {
        // S'abonner aux événements
        TeamManager.OnActiveTeamChanged += HandleActiveTeamChanged;
        RefreshAllUI();
        SelectCharacterForDetails(null);
    }

    private void OnDisable()
    {
        // Se désabonner
        if (TeamManager.Instance != null)
        {
            TeamManager.OnActiveTeamChanged -= HandleActiveTeamChanged;
        }
    }

    #endregion

    #region Initialisation et Rafraîchissement de l'UI

    private void RefreshAllUI()
    {
        PopulateActiveTeamSlots();
        UpdateCharacterDetailsPanel();
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
                // Le callback "onAdd" ouvrira le panel de sélection plus tard
                slotUI.Setup(characterInSlot, i, OnRemoveCharacter, OnAddCharacterSlotClicked, OnShowEquipmentPanel);
                _instantiatedTeamSlots.Add(slotUI);
            }
            else
            {
                Debug.LogError($"[TeamManagementUI] Le prefab 'teamSlotPrefab' n'a pas de script TeamSlotUI !");
                Destroy(slotGO);
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
    // Modifiez cette méthode
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
