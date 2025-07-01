using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UI.HUB;
using System.Collections;
using ScriptableObjects;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CharacterSelectionUI : MonoBehaviour
{
    [Header("Panels & Conteneurs")]
    [Tooltip("Panel principal de gestion d'équipe (celui avec les 4 slots)")]
    [SerializeField] private GameObject teamManagementPanel;
    [Tooltip("Conteneur pour la liste des personnages disponibles (le ScrollRect)")]
    [SerializeField] private Transform availableCharactersContainer;
    [Tooltip("Le panel de droite affichant les détails")]
    [SerializeField] private GameObject detailPanel;

    [Header("Prefab de l'item de liste")]
    [SerializeField] private GameObject availableCharacterItemPrefab;

    [Header("Éléments du Panel de Détails")]
    [SerializeField] private TextMeshProUGUI detailCharacterNameText;
    [SerializeField] private TextMeshProUGUI detailCharacterDescriptionText;
    // Références vers les 4 textes dans la grille
    [SerializeField] private TextMeshProUGUI statHealthText;
    [SerializeField] private TextMeshProUGUI statAttackText;
    [SerializeField] private TextMeshProUGUI statDefenseText;
    [SerializeField] private TextMeshProUGUI statCostText;
    
    [Header("Boutons")]
    [SerializeField] private Button addToTeamButton;
    [SerializeField] private Button backButton;
    
    [Header("Prévisualisation 3D")]
    [SerializeField] private Character3DPreview characterPreview; // Référence à notre nouveau script
    
    [Header("Navigation Manette")]
    [SerializeField] private ScrollRect scrollRect; // Référence au ScrollRect pour le contrôle automatique
    [SerializeField] private float scrollSpeed = 5f; // Vitesse de défilement automatique
    
    private readonly List<AvailableCharacterListItemUI> _instantiatedListItems = new List<AvailableCharacterListItemUI>();

    // Références aux managers
    private TeamManager _teamManager;

    private CharacterData_SO _selectedCharacter;
    private int _selectedIndex = -1;
    private GameObject _lastSelectedObject;
    
    void Awake()
    {
        _teamManager = TeamManager.Instance;
        if (_teamManager == null) Debug.LogError("[CharacterSelectionUI] TeamManager non trouvé !");

        addToTeamButton?.onClick.AddListener(OnAddToTeamClicked);
        backButton?.onClick.AddListener(OnBackClicked);
        
        // Récupérer automatiquement le ScrollRect si pas assigné
        if (scrollRect == null && availableCharactersContainer != null)
        {
            scrollRect = availableCharactersContainer.GetComponentInParent<ScrollRect>();
        }
    }

    private void OnEnable()
    {
        // Quand le panel est activé, on rafraîchit la liste
        PopulateAvailableCharactersList();
        // On désélectionne par défaut
        SelectCharacter(null);
        
        // Setup initial de la sélection pour la navigation manette
        StartCoroutine(SetupInitialSelection());
    }
    
    private void OnDisable()
    {
        // Sauvegarder la sélection actuelle
        _lastSelectedObject = EventSystem.current.currentSelectedGameObject;
        
        if (characterPreview != null)
        {
            characterPreview.ClearPreview();
        }
    }
    
    private void Update()
    {
        // Gérer l'action Cancel (B sur Xbox, Circle sur PS, Escape sur clavier)
        if (InputManager.Instance != null && InputManager.Instance.UIActions.Cancel.WasPressedThisFrame())
        {
            OnBackClicked();
        }
        
        // S'assurer qu'on a toujours quelque chose de sélectionné
        EnsureSelection();
        
        // Gérer le défilement automatique de la liste
        HandleScrolling();
    }

    private void PopulateAvailableCharactersList()
    {
        // Nettoyer l'ancienne liste
        foreach (var item in _instantiatedListItems)
        {
            if (item != null) Destroy(item.gameObject);
        }
        _instantiatedListItems.Clear();

        // Obtenir les personnages disponibles qui ne sont PAS déjà dans l'équipe
        var available = _teamManager.AvailableCharacters;
        var activeTeam = _teamManager.ActiveTeam;
        var charactersToShow = available.Except(activeTeam).ToList();

        // Instancier un item pour chaque personnage à afficher
        for (int i = 0; i < charactersToShow.Count; i++)
        {
            var character = charactersToShow[i];
            if (character == null) continue;

            GameObject itemGO = Instantiate(availableCharacterItemPrefab, availableCharactersContainer);
            var itemUI = itemGO.GetComponent<AvailableCharacterListItemUI>();
            if (itemUI != null)
            {
                int characterLevel = 1;
                if (TeamManager.Instance != null &&
                    PlayerDataManager.Instance.Data.CharacterProgressData.ContainsKey(character.CharacterID))
                {
                    characterLevel = PlayerDataManager.Instance.Data.CharacterProgressData[character.CharacterID]
                        .CurrentLevel;
                }

                // Passe le niveau à l'item UI avec une callback pour la sélection
                itemUI.Setup(character, OnCharacterItemClicked, characterLevel);

                _instantiatedListItems.Add(itemUI);
                
                // Configurer la navigation pour cet item
                ConfigureItemNavigation(itemUI, i);
            }
        }
        
        // Configurer la navigation globale après avoir créé tous les items
        ConfigureGlobalNavigation();
    }
    
    private void ConfigureItemNavigation(AvailableCharacterListItemUI item, int index)
    {
        Button itemButton = item.GetComponent<Button>();
        if (itemButton == null) return;
        
        Navigation nav = itemButton.navigation;
        nav.mode = Navigation.Mode.Explicit;
        
        // Navigation verticale dans la liste
        if (index > 0 && _instantiatedListItems[index - 1] != null)
        {
            nav.selectOnUp = _instantiatedListItems[index - 1].GetComponent<Button>();
        }
        
        // On configure selectOnDown après avoir créé tous les items
        
        itemButton.navigation = nav;
    }
    
    private void ConfigureGlobalNavigation()
    {
        // Compléter la navigation verticale
        for (int i = 0; i < _instantiatedListItems.Count - 1; i++)
        {
            Button currentButton = _instantiatedListItems[i].GetComponent<Button>();
            Button nextButton = _instantiatedListItems[i + 1].GetComponent<Button>();
            
            if (currentButton != null && nextButton != null)
            {
                Navigation nav = currentButton.navigation;
                nav.selectOnDown = nextButton;
                currentButton.navigation = nav;
            }
        }
        
        // Navigation horizontale de la liste vers les boutons
        foreach (var item in _instantiatedListItems)
        {
            Button itemButton = item.GetComponent<Button>();
            if (itemButton != null)
            {
                Navigation nav = itemButton.navigation;
                nav.selectOnRight = addToTeamButton; // Navigation vers la droite = bouton Add
                itemButton.navigation = nav;
            }
        }
        
        // Navigation depuis les boutons vers la liste
        if (_instantiatedListItems.Count > 0)
        {
            // Le dernier item de la liste pointe vers le bouton Back en bas
            Button lastItem = _instantiatedListItems[_instantiatedListItems.Count - 1].GetComponent<Button>();
            if (lastItem != null && backButton != null)
            {
                Navigation nav = lastItem.navigation;
                nav.selectOnDown = backButton;
                lastItem.navigation = nav;
            }
            
            // Le bouton Add
            if (addToTeamButton != null)
            {
                Navigation addNav = addToTeamButton.navigation;
                addNav.mode = Navigation.Mode.Explicit;
                
                // Trouver l'item le plus proche verticalement pour la navigation vers la gauche
                int middleIndex = _instantiatedListItems.Count / 2;
                if (_instantiatedListItems[middleIndex] != null)
                {
                    addNav.selectOnLeft = _instantiatedListItems[middleIndex].GetComponent<Button>();
                }
                
                addNav.selectOnDown = backButton;
                addToTeamButton.navigation = addNav;
            }
        }
        
        // Navigation du bouton Back
        if (backButton != null)
        {
            Navigation backNav = backButton.navigation;
            backNav.mode = Navigation.Mode.Explicit;
            backNav.selectOnRight = addToTeamButton;
            
            if (_instantiatedListItems.Count > 0)
            {
                backNav.selectOnUp = _instantiatedListItems[_instantiatedListItems.Count - 1].GetComponent<Button>();
                
                // Navigation depuis le bouton Back vers la liste (à gauche)
                int middleIndex = _instantiatedListItems.Count / 2;
                if (_instantiatedListItems[middleIndex] != null)
                {
                    backNav.selectOnLeft = _instantiatedListItems[middleIndex].GetComponent<Button>();
                }
            }
            
            backButton.navigation = backNav;
        }
        
        // Le bouton Add peut naviguer vers le haut
        if (addToTeamButton != null && _instantiatedListItems.Count > 0)
        {
            Navigation addNav = addToTeamButton.navigation;
            addNav.selectOnUp = backButton;
            addToTeamButton.navigation = addNav;
        }
    }
    
    private IEnumerator SetupInitialSelection()
    {
        // Attendre une frame pour s'assurer que tout est initialisé
        yield return null;
        
        GameObject objectToSelect = null;
        
        // Priorité: Dernière sélection → Premier personnage → Bouton Back
        if (_lastSelectedObject != null && _lastSelectedObject.activeInHierarchy)
        {
            objectToSelect = _lastSelectedObject;
        }
        else if (_instantiatedListItems.Count > 0)
        {
            objectToSelect = _instantiatedListItems[0].gameObject;
        }
        else if (backButton != null)
        {
            objectToSelect = backButton.gameObject;
        }
        
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
            Vector2 navigationInput = InputManager.Instance?.UIActions.Navigate.ReadValue<Vector2>() ?? Vector2.zero;
            bool submitPressed = InputManager.Instance?.UIActions.Submit.WasPressedThisFrame() ?? false;
            
            if (navigationInput != Vector2.zero || submitPressed)
            {
                StartCoroutine(SetupInitialSelection());
            }
        }
    }
    
    private void HandleScrolling()
    {
        if (scrollRect == null || EventSystem.current.currentSelectedGameObject == null) return;
        
        // Vérifier si l'objet sélectionné est un de nos items
        AvailableCharacterListItemUI selectedItem = EventSystem.current.currentSelectedGameObject.GetComponent<AvailableCharacterListItemUI>();
        if (selectedItem == null || !_instantiatedListItems.Contains(selectedItem)) return;
        
        // Obtenir l'index de l'item sélectionné
        int selectedIndex = _instantiatedListItems.IndexOf(selectedItem);
        if (selectedIndex < 0) return;
        
        // Calculer la position normalisée de cet item dans la liste
        float normalizedPosition = 1f - ((float)selectedIndex / Mathf.Max(1f, _instantiatedListItems.Count - 1f));
        
        // Interpoler doucement vers cette position
        float currentPos = scrollRect.verticalNormalizedPosition;
        float targetPos = normalizedPosition;
        
        // Zone de tolérance pour éviter les micro-ajustements
        float tolerance = 0.1f / _instantiatedListItems.Count;
        
        if (Mathf.Abs(currentPos - targetPos) > tolerance)
        {
            scrollRect.verticalNormalizedPosition = Mathf.Lerp(currentPos, targetPos, Time.deltaTime * scrollSpeed);
        }
    }

    // Appelée par un item de la liste quand il est cliqué ou sélectionné
    private void OnCharacterItemClicked(CharacterData_SO characterData)
    {
        SelectCharacter(characterData);
        
        // Focus sur l'item qui vient d'être cliqué pour la navigation
        var clickedItem = _instantiatedListItems.FirstOrDefault(item => item.GetCharacterData() == characterData);
        if (clickedItem != null)
        {
            EventSystem.current.SetSelectedGameObject(clickedItem.gameObject);
        }
    }
    
    // Appelée pour sélectionner un personnage et mettre à jour l'UI
    private void SelectCharacter(CharacterData_SO characterData)
    {
        _selectedCharacter = characterData;
        UpdateDetailPanel();
        
        // Mettre à jour l'état visuel des items
        foreach (var itemUI in _instantiatedListItems)
        {
            if (itemUI != null)
            {
                itemUI.SetSelected(itemUI.GetCharacterData() == _selectedCharacter);
            }
        }
        
        // Mettre à jour la prévisualisation 3D
        if (characterPreview != null)
        {
            if (_selectedCharacter != null)
            {
                characterPreview.ShowCharacter(_selectedCharacter.HubVisualPrefab);
            }
            else
            {
                characterPreview.ClearPreview();
            }
        }
        
        // Activer/désactiver le bouton Add
        if (addToTeamButton != null)
        {
            addToTeamButton.interactable = (_selectedCharacter != null);
        }
    }

    private void UpdateDetailPanel()
    {
        if (_selectedCharacter != null)
        {
            detailPanel.SetActive(true);

            if (detailCharacterNameText != null)
                detailCharacterNameText.text = _selectedCharacter.DisplayName;

            if (detailCharacterDescriptionText != null)
                detailCharacterDescriptionText.text = _selectedCharacter.Description;
            
            // Mise à jour des stats
            if (_selectedCharacter.Stats != null)
            {
                int characterLevel = 1;
                if (PlayerDataManager.Instance.Data.CharacterProgressData.TryGetValue(_selectedCharacter.CharacterID, out var progress))
                {
                    characterLevel = progress.CurrentLevel;
                }

                RuntimeStats finalStats = StatsCalculator.GetFinalStats(_selectedCharacter, characterLevel, null);

                statHealthText.text = $"HP: {finalStats.MaxHealth}";
                statAttackText.text = $"ACK: {finalStats.Attack}";
                statDefenseText.text = $"Def: {finalStats.Defense}";
                statCostText.text = $"Cost: {_selectedCharacter.Stats.AttackDelay} or";
            }
            else
            {
                statHealthText.text = "HP: N/A";
                statAttackText.text = "ACK: N/A";
                statDefenseText.text = "Def: N/A";
                statCostText.text = "Cost: N/A";
            }
        }
        else
        {
            // Cacher les détails et désactiver le bouton d'ajout si rien n'est sélectionné
            detailPanel.SetActive(false);
            addToTeamButton.interactable = false;
        }
    }

    private void OnAddToTeamClicked()
    {
        if (_selectedCharacter != null)
        {
            bool added = _teamManager.TryAddCharacterToActiveTeam(_selectedCharacter);
            if (added)
            {
                // Revenir à l'écran de gestion d'équipe
                StartCoroutine(TransitionToPanel(teamManagementPanel));
            }
            else
            {
                // Optionnel : Afficher un message "Équipe pleine"
                Debug.Log("Impossible d'ajouter le personnage, l'équipe est probablement pleine.");
            }
        }
    }

    private void OnBackClicked()
    {
        StartCoroutine(TransitionToPanel(teamManagementPanel));
    }

    private IEnumerator TransitionToPanel(GameObject panelToShow)
    {
        CanvasGroup currentPanelCanvasGroup = GetComponent<CanvasGroup>();
        if (currentPanelCanvasGroup == null) currentPanelCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        CanvasGroup nextPanelCanvasGroup = panelToShow.GetComponent<CanvasGroup>();
        if (nextPanelCanvasGroup == null) nextPanelCanvasGroup = panelToShow.AddComponent<CanvasGroup>();

        float duration = 0.25f;
        float elapsedTime = 0f;

        panelToShow.SetActive(true);
        nextPanelCanvasGroup.alpha = 0;

        while (elapsedTime < duration)
        {
            currentPanelCanvasGroup.alpha = 1f - (elapsedTime / duration);
            nextPanelCanvasGroup.alpha = elapsedTime / duration;

            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }

        currentPanelCanvasGroup.alpha = 0;
        nextPanelCanvasGroup.alpha = 1;

        gameObject.SetActive(false);
    }
}