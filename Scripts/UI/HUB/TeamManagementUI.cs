using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq; 
using System;

public class TeamManagementUI : MonoBehaviour
{
    [Header("Références des Managers")]
    private PlayerDataManager _playerDataManager;
    private TeamManager _teamManager;
    private HubManager _hubManager; // Si le bouton retour interagit avec HubManager

    [Header("Prefabs UI")]
    [Tooltip("Prefab pour un item de la liste des personnages disponibles.")]
    [SerializeField] private GameObject availableCharacterItemPrefab;
    [Tooltip("Prefab pour un slot de l'équipe active.")]
    [SerializeField] private GameObject teamSlotItemPrefab;

    [Header("Conteneurs UI")]
    [Tooltip("Parent des items des personnages disponibles (avec un LayoutGroup).")]
    [SerializeField] private Transform availableCharactersContainer;
    [Tooltip("Parent des 4 slots de l'équipe active (avec un LayoutGroup).")]
    [SerializeField] private Transform activeTeamSlotsContainer;

    [Header("Panel de Détails Personnage")]
    [Tooltip("Panel pour afficher les détails du personnage sélectionné.")]
    [SerializeField] private GameObject characterDetailsPanel;
    [SerializeField] private TextMeshProUGUI detailCharacterNameText;
    [SerializeField] private Image detailCharacterIconImage; // Pour l'icône du personnage
    [SerializeField] private TextMeshProUGUI detailCharacterDescriptionText;
    [SerializeField] private TextMeshProUGUI detailCharacterStatsText; // Pour afficher Santé, Attaque, Défense, etc.
    // Ajoute ici d'autres champs pour les détails si nécessaire (ex: Séquence d'invocation)

    [Header("Boutons UI")]
    [Tooltip("Bouton pour retourner à la vue générale du Hub ou fermer ce panel.")]
    [SerializeField] private Button backButton;

    private List<AvailableCharacterItemUI> _instantiatedAvailableItems = new List<AvailableCharacterItemUI>();
    private List<TeamSlotItemUI> _instantiatedTeamSlots = new List<TeamSlotItemUI>();

    private CharacterData_SO _selectedCharacterForDetails = null;

    #region Cycle de Vie Unity

    private void Awake()
    {
        // Récupérer les instances des managers (assure-toi qu'ils sont bien des Singletons)
        _playerDataManager = PlayerDataManager.Instance;
        _teamManager = TeamManager.Instance;
        // Correction de l'avertissement CS0618
        _hubManager = FindFirstObjectByType<HubManager>();

        if (_playerDataManager == null) Debug.LogError("[TeamManagementUI] PlayerDataManager.Instance est null !");
        if (_teamManager == null) Debug.LogError("[TeamManagementUI] TeamManager.Instance est null !");
        if (_hubManager == null) Debug.LogError("[TeamManagementUI] HubManager non trouvé !");

        if (availableCharacterItemPrefab == null) Debug.LogError("[TeamManagementUI] Prefab 'availableCharacterItemPrefab' non assigné !");
        if (teamSlotItemPrefab == null) Debug.LogError("[TeamManagementUI] Prefab 'teamSlotItemPrefab' non assigné !");
        if (availableCharactersContainer == null) Debug.LogError("[TeamManagementUI] Conteneur 'availableCharactersContainer' non assigné !");
        if (activeTeamSlotsContainer == null) Debug.LogError("[TeamManagementUI] Conteneur 'activeTeamSlotsContainer' non assigné !");

        if (backButton != null) backButton.onClick.AddListener(OnBackButtonClicked);
        else Debug.LogWarning("[TeamManagementUI] Bouton 'backButton' non assigné.");

        if (characterDetailsPanel != null) characterDetailsPanel.SetActive(false);
    }

    private void OnEnable()
    {
        // S'abonner aux événements lorsque le panel devient actif
        PlayerDataManager.OnCharacterUnlocked += HandleCharacterUnlocked; // Pour rafraîchir si un perso est débloqué
        TeamManager.OnActiveTeamChanged += HandleActiveTeamChanged;       // Pour rafraîchir si l'équipe change par un autre moyen

        RefreshAllUI(); // Charger et afficher les personnages et l'équipe
        SelectCharacterForDetails(null); // Aucun personnage sélectionné au début
    }

    private void OnDisable()
    {
        // Se désabonner lorsque le panel devient inactif
        if (PlayerDataManager.Instance != null) // Vérifier si l'instance existe toujours
        {
            PlayerDataManager.OnCharacterUnlocked -= HandleCharacterUnlocked;
        }
        if (TeamManager.Instance != null)
        {
            TeamManager.OnActiveTeamChanged -= HandleActiveTeamChanged;
        }

        ClearInstantiatedItems(); // Nettoyer les items pour éviter les problèmes
    }

    #endregion

    #region Initialisation et Rafraîchissement de l'UI

    private void RefreshAllUI()
    {
        PopulateAvailableCharactersList();
        PopulateActiveTeamSlots();
        UpdateCharacterDetailsPanel(); // Mettre à jour le panel de détails avec le perso sélectionné (ou le cacher)
    }

    private void ClearInstantiatedItems()
    {
        foreach (var item in _instantiatedAvailableItems) if (item != null) Destroy(item.gameObject);
        _instantiatedAvailableItems.Clear();

        foreach (var slot in _instantiatedTeamSlots) if (slot != null) Destroy(slot.gameObject);
        _instantiatedTeamSlots.Clear();
    }

    private void PopulateAvailableCharactersList()
    {
        // Nettoyer les anciens items
        foreach (var item in _instantiatedAvailableItems) if (item != null) Destroy(item.gameObject);
        _instantiatedAvailableItems.Clear();

        if (_playerDataManager == null || _teamManager == null)
        {
            Debug.LogError("[TeamManagementUI] PlayerDataManager ou TeamManager est null dans PopulateAvailableCharactersList.");
            return;
        }

        List<string> unlockedIDs = _playerDataManager.GetUnlockedCharacterIDs();
        Debug.Log($"[TeamManagementUI] PopulateAvailable - Unlocked Character IDs from PlayerDataManager: {string.Join(", ", unlockedIDs)} (Count: {unlockedIDs.Count})");

        List<CharacterData_SO> allUnlockedCharacters = new List<CharacterData_SO>();
        foreach (string id in unlockedIDs)
        {
            // Adapte "Data/Characters/" si ton chemin dans Resources est différent
            CharacterData_SO characterData = Resources.Load<CharacterData_SO>($"Data/Characters/{id}");
            if (characterData != null)
            {
                allUnlockedCharacters.Add(characterData);
            }
            else
            {
                Debug.LogWarning($"[TeamManagementUI] PopulateAvailable - CharacterData_SO non trouvé pour l'ID '{id}' dans Resources/Data/Characters/");
            }
        }
        Debug.Log($"[TeamManagementUI] PopulateAvailable - All Unlocked Characters SOs loaded: {allUnlockedCharacters.Count} - Names: {string.Join(", ", allUnlockedCharacters.Select(c => c.DisplayName))}");

        List<CharacterData_SO> activeTeam = _teamManager.ActiveTeam; // Doit être une liste de 4, potentiellement avec des nulls
        Debug.Log($"[TeamManagementUI] PopulateAvailable - Active Team SOs from TeamManager: {activeTeam.Count(c => c != null)} non-null members - Names: {string.Join(", ", activeTeam.Where(c => c != null).Select(c => c.DisplayName))}");

        // Filtrer les personnages déjà dans l'équipe active
        List<CharacterData_SO> charactersToShow = allUnlockedCharacters.Except(activeTeam.Where(c => c != null)).ToList();
        Debug.Log($"[TeamManagementUI] PopulateAvailable - Characters to Show (Available & Not in Team): {charactersToShow.Count} - Names: {string.Join(", ", charactersToShow.Select(c => c.DisplayName))}");

        if (charactersToShow.Count == 0)
        {
            Debug.LogWarning("[TeamManagementUI] PopulateAvailable - Aucun personnage à afficher dans la liste des disponibles (soit aucun débloqué, soit tous dans l'équipe active).");
        }

        foreach (CharacterData_SO charData in charactersToShow)
        {
        if (charData == null)
        {
            Debug.LogWarning("[TeamManagementUI] PopulateAvailable - Tentative d'instancier un item pour un charData null dans charactersToShow.");
            continue;
        }

        Debug.Log($"[TeamManagementUI] PopulateAvailable - Instanciation de l'item pour : {charData.DisplayName}");
        GameObject itemGO = Instantiate(availableCharacterItemPrefab, availableCharactersContainer);
        AvailableCharacterItemUI itemUI = itemGO.GetComponent<AvailableCharacterItemUI>();
        if (itemUI != null)
        {
            itemUI.Setup(charData, OnAvailableCharacterSelected, OnShowCharacterDetails);
            _instantiatedAvailableItems.Add(itemUI);
        }
        else
        {
            Debug.LogError($"[TeamManagementUI] PopulateAvailable - Le prefab 'availableCharacterItemPrefab' n'a pas de script AvailableCharacterItemUI ! Item pour {charData.DisplayName} non créé correctement.");
            Destroy(itemGO);
            }
        }
    }
    private void PopulateActiveTeamSlots()
    {
        // Nettoyer les anciens slots
        foreach (var slot in _instantiatedTeamSlots) if (slot != null) Destroy(slot.gameObject);
        _instantiatedTeamSlots.Clear();

        if (_teamManager == null) return;

        List<CharacterData_SO> activeTeam = _teamManager.ActiveTeam; // Doit toujours retourner 4 éléments (potentiellement null)

        for (int i = 0; i < 4; i++) // Toujours créer 4 slots
        {
            GameObject slotGO = Instantiate(teamSlotItemPrefab, activeTeamSlotsContainer);
            TeamSlotItemUI slotUI = slotGO.GetComponent<TeamSlotItemUI>();
            if (slotUI != null)
            {
                CharacterData_SO characterInSlot = (i < activeTeam.Count) ? activeTeam[i] : null;
                slotUI.Setup(characterInSlot, i, OnTeamSlotCharacterClicked, OnShowCharacterDetails);
                _instantiatedTeamSlots.Add(slotUI);
            }
            else
            {
                 Debug.LogError($"[TeamManagementUI] Le prefab 'teamSlotItemPrefab' n'a pas de script TeamSlotItemUI !");
                Destroy(slotGO);
            }
        }
    }

    #endregion

    #region Gestion des Événements des Managers

    private void HandleCharacterUnlocked(string characterId)
    {
        Debug.Log($"[TeamManagementUI] Personnage débloqué : {characterId}. Rafraîchissement de l'UI.");
        RefreshAllUI();
    }

    private void HandleActiveTeamChanged(List<CharacterData_SO> newActiveTeam)
    {
        Debug.Log("[TeamManagementUI] L'équipe active a changé. Rafraîchissement de l'UI.");
        RefreshAllUI();
        // Si un personnage était sélectionné pour les détails et qu'il n'est plus dans l'équipe
        // ou si sa situation a changé, on pourrait vouloir mettre à jour le panel de détails.
        if (_selectedCharacterForDetails != null)
        {
            // Si le personnage sélectionné n'est plus dans l'équipe et n'est pas dans la liste des disponibles non plus
            // (ce qui serait étrange, mais pour être sûr)
            bool stillAvailable = _playerDataManager.GetUnlockedCharacterIDs().Contains(_selectedCharacterForDetails.CharacterID);
            if (!newActiveTeam.Contains(_selectedCharacterForDetails) && !stillAvailable)
            {
                SelectCharacterForDetails(null); // Désélectionner
            } else {
                UpdateCharacterDetailsPanel(); // Juste rafraîchir
            }
        }
    }

    #endregion

    #region Callbacks des Items UI

    // Appelé quand on clique sur un personnage dans la liste des "disponibles"
    private void OnAvailableCharacterSelected(CharacterData_SO characterData)
    {
        if (_teamManager.TryAddCharacterToActiveTeam(characterData))
        {
            Debug.Log($"[TeamManagementUI] Personnage '{characterData.DisplayName}' ajouté à l'équipe active.");
            // TeamManager.OnActiveTeamChanged devrait être déclenché, ce qui rafraîchira l'UI.
            // Sélectionner le personnage qu'on vient d'ajouter pour voir ses détails.
            SelectCharacterForDetails(characterData);
        }
        else
        {
            Debug.LogWarning($"[TeamManagementUI] Impossible d'ajouter '{characterData.DisplayName}' à l'équipe (peut-être pleine ou déjà dedans).");
        }
    }

    // Appelé quand on clique sur un personnage dans un slot de l'équipe active
    private void OnTeamSlotCharacterClicked(CharacterData_SO characterData, int slotIndex)
    {
        if (characterData != null) // Si le slot n'est pas vide
        {
            if (_teamManager.TryRemoveCharacterFromActiveTeam(characterData))
            {
                Debug.Log($"[TeamManagementUI] Personnage '{characterData.DisplayName}' retiré de l'équipe active.");
                // TeamManager.OnActiveTeamChanged devrait rafraîchir l'UI.
                // Si le personnage retiré était celui affiché dans les détails, désélectionner.
                if (_selectedCharacterForDetails == characterData)
                {
                    SelectCharacterForDetails(null);
                }
            }
        }
        // Si le slot est vide, on pourrait implémenter une logique pour "sélectionner le slot vide"
        // afin de choisir ensuite un personnage dans la liste des disponibles. Pour l'instant, on ne fait rien.
    }

    // Appelé par n'importe quel item (disponible ou slot d'équipe) pour afficher ses détails
    private void OnShowCharacterDetails(CharacterData_SO characterData)
    {
        SelectCharacterForDetails(characterData);
    }

    #endregion

    #region Panel de Détails

    private void SelectCharacterForDetails(CharacterData_SO characterData)
    {
        _selectedCharacterForDetails = characterData;
        UpdateCharacterDetailsPanel();
    }

    private void UpdateCharacterDetailsPanel()
    {
        if (characterDetailsPanel == null) return;

        if (_selectedCharacterForDetails != null)
        {
            characterDetailsPanel.SetActive(true);
            if (detailCharacterNameText != null) detailCharacterNameText.text = _selectedCharacterForDetails.DisplayName;
            if (detailCharacterIconImage != null)
            {
                detailCharacterIconImage.sprite = _selectedCharacterForDetails.Icon;
                detailCharacterIconImage.enabled = (_selectedCharacterForDetails.Icon != null);
            }
            if (detailCharacterDescriptionText != null) detailCharacterDescriptionText.text = _selectedCharacterForDetails.Description;

            if (detailCharacterStatsText != null && _selectedCharacterForDetails.BaseStats != null)
            {
                UnitStats_SO stats = _selectedCharacterForDetails.BaseStats;
                detailCharacterStatsText.text = $"Santé: {stats.Health}\n" +
                                                $"Attaque: {stats.Attack}\n" +
                                                $"Défense: {stats.Defense}\n" +
                                                $"Portée Att.: {stats.AttackRange}\n" +
                                                $"Délai Att.: {stats.AttackDelay} beats\n" +
                                                $"Délai Mvmt: {stats.MovementDelay} beats\n" +
                                                $"Détection: {stats.DetectionRange} tiles";
            }
            else if (detailCharacterStatsText != null)
            {
                detailCharacterStatsText.text = "Statistiques non disponibles.";
            }
        }
        else
        {
            characterDetailsPanel.SetActive(false);
        }
    }

    #endregion

    #region Navigation

    private void OnBackButtonClicked()
    {
        Debug.Log("[TeamManagementUI] Bouton Retour cliqué.");
        // Informer HubManager de cacher ce panel et de retourner à la vue générale du Hub,
        // ou simplement désactiver ce panel si HubManager gère l'activation/désactivation.
        if (_hubManager != null)
        {
             _hubManager.GoToGeneralView(); // Assure-toi que HubManager a une méthode pour cela
        }
        else
        {
            gameObject.SetActive(false); // Fallback si pas de HubManager
        }
    }

    #endregion
}