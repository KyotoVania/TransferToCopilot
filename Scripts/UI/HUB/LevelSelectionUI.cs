using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using ScriptableObjects;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class LevelSelectionUI : MonoBehaviour
{
    [Header("Configuration des Données")]
    [SerializeField] private string levelDataPath = "Data/Levels";

    [Header("Prefabs des États de Niveau")]
    [SerializeField] private GameObject stageLockPrefab;
    [Tooltip("Prefab pour un niveau débloqué mais non complété (StageNeutral).")]
    [SerializeField] private GameObject stageNeutralPrefab;
    [SerializeField] private GameObject stageCompletePrefab;
    
    [Header("Références UI")]
    [SerializeField] private Transform levelItemsContainer;
    [SerializeField] private Button backButton;
    [SerializeField] private Button launchLevelButton;
    
    [Header("Navigation Manette")]
    [SerializeField] private ScrollRect levelScrollRect;
    [SerializeField] private float scrollSpeed = 5f;
    
    // === SYSTÈME DE NAVIGATION MANETTE ===
    private LevelData_SO _selectedLevel;
    private List<LevelSelectItemUI> _instantiatedItems = new List<LevelSelectItemUI>();
    private List<LevelData_SO> _loadedLevels = new List<LevelData_SO>();
    private GameObject _lastSelectedObject;
    private HubManager _hubManager;

    #region Cycle de Vie Unity

    void Awake()
    {
        _hubManager = FindFirstObjectByType<HubManager>();
        if (_hubManager == null) Debug.LogError("[LevelSelectionUI] HubManager non trouvé!");
        
        if (stageLockPrefab == null || stageNeutralPrefab == null || stageCompletePrefab == null)
            Debug.LogError("[LevelSelectionUI] Un ou plusieurs prefabs d'état de niveau ne sont pas assignés !");

        backButton?.onClick.AddListener(OnBackButtonClicked);
        launchLevelButton?.onClick.AddListener(OnLaunchLevelButtonClicked);
        
        // Trouver le ScrollRect automatiquement si pas assigné
        if (levelScrollRect == null && levelItemsContainer != null)
        {
            levelScrollRect = levelItemsContainer.GetComponentInParent<ScrollRect>();
        }
    }

    private void OnEnable()
    {
        Debug.Log("[LevelSelectionUI] Panel activé - Prise de contrôle");
        
        // 🎯 PRISE DE CONTRÔLE + CORRECTION ALPHA
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
        
        TakeControlFromHub();
        
        LoadAndDisplayLevels();
        
        // Setup de la sélection initiale
        StartCoroutine(SetupInitialSelection());
    }

    private void OnDisable()
    {
        Debug.Log("[LevelSelectionUI] Panel désactivé");
        
        _lastSelectedObject = EventSystem.current.currentSelectedGameObject;
        
        // Rendre le contrôle au Hub
        ReturnControlToHub();
    }

    private void Update()
    {
        // Gérer l'action Cancel
        if (InputManager.Instance != null && InputManager.Instance.UIActions.Cancel.WasPressedThisFrame())
        {
            OnBackButtonClicked();
        }
        
        // S'assurer qu'on a toujours quelque chose de sélectionné
        EnsureSelection();
        
        // Gérer le scroll automatique
        HandleLevelGridScrolling();
    }

    #endregion

    #region Contrôle Hub

    private void TakeControlFromHub()
    {
        if (_hubManager != null)
        {
            _hubManager.DisableHubControls();
            Debug.Log("[LevelSelectionUI] ✅ Contrôles du Hub désactivés");
        }
    }

    private void ReturnControlToHub()
    {
        if (_hubManager != null)
        {
            _hubManager.EnableHubControls();
            Debug.Log("[LevelSelectionUI] ✅ Contrôles du Hub réactivés");
        }
    }

    #endregion

    #region Navigation et Sélection

    private IEnumerator SetupInitialSelection()
    {
        yield return null; // Attendre que tout soit initialisé
        
        GameObject targetObject = null;
        
        // Priorité: dernier objet sélectionné → premier niveau débloqué → bouton retour
        if (_lastSelectedObject != null && _lastSelectedObject.activeInHierarchy)
        {
            targetObject = _lastSelectedObject;
        }
        else
        {
            // Chercher le premier niveau débloqué (pas locked)
            foreach (var item in _instantiatedItems)
            {
                if (item != null && item.GetLevelData() != null) // Pas un niveau bloqué
                {
                    Button itemButton = item.GetComponent<Button>();
                    if (itemButton != null && itemButton.interactable)
                    {
                        targetObject = itemButton.gameObject;
                        break;
                    }
                }
            }
            
            // Fallback vers le bouton retour
            if (targetObject == null && backButton != null)
            {
                targetObject = backButton.gameObject;
            }
        }
        
        if (targetObject != null)
        {
            EventSystem.current.SetSelectedGameObject(targetObject);
            Debug.Log($"[LevelSelectionUI] ✅ Sélection initiale : {targetObject.name}");
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

    private void HandleLevelGridScrolling()
    {
        if (levelScrollRect == null) return;
        
        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        if (currentSelected == null) return;
        
        // Vérifier si l'objet sélectionné est un niveau
        bool isLevelItem = false;
        foreach (var item in _instantiatedItems)
        {
            if (item != null && item.gameObject == currentSelected)
            {
                isLevelItem = true;
                break;
            }
        }
        
        if (!isLevelItem) return;
        
        RectTransform selectedRect = currentSelected.GetComponent<RectTransform>();
        if (selectedRect == null) return;

        // Calculer la position pour le scroll automatique
        RectTransform contentRect = levelScrollRect.content;
        RectTransform viewportRect = levelScrollRect.viewport;
        
        if (contentRect == null || viewportRect == null) return;
        
        // Obtenir les positions relatives  
        Vector3[] contentCorners = new Vector3[4];
        contentRect.GetWorldCorners(contentCorners);
        
        Vector3[] itemCorners = new Vector3[4];
        selectedRect.GetWorldCorners(itemCorners);
        
        Vector3[] viewportCorners = new Vector3[4];
        viewportRect.GetWorldCorners(viewportCorners);
        
        // Vérifier si l'item est visible dans le viewport
        float itemTop = itemCorners[1].y;
        float itemBottom = itemCorners[0].y;
        float viewportTop = viewportCorners[1].y;
        float viewportBottom = viewportCorners[0].y;
        
        // Si l'item n'est pas entièrement visible, ajuster le scroll
        if (itemTop > viewportTop || itemBottom < viewportBottom)
        {
            float contentHeight = contentCorners[1].y - contentCorners[0].y;
            float viewportHeight = viewportTop - viewportBottom;
            
            if (contentHeight > viewportHeight)
            {
                float itemCenterY = (itemTop + itemBottom) / 2f;
                float relativePosition = (itemCenterY - contentCorners[0].y) / contentHeight;
                float targetScroll = Mathf.Clamp01(1f - relativePosition);
                
                levelScrollRect.verticalNormalizedPosition = Mathf.Lerp(
                    levelScrollRect.verticalNormalizedPosition, 
                    targetScroll, 
                    Time.deltaTime * scrollSpeed
                );
            }
        }
    }

    #endregion

    #region Chargement et Affichage des Niveaux

    private void LoadAndDisplayLevels()
    {
        LoadLevelData();
        PopulateLevelGrid();
        ConfigureLevelGridNavigation();
    }

    private void LoadLevelData()
    {
        _loadedLevels.Clear();
        LevelData_SO[] allLevels = Resources.LoadAll<LevelData_SO>(levelDataPath);
        _loadedLevels = allLevels
                        .Where(level => level.TypeOfLevel == LevelType.GameplayLevel)
                        .OrderBy(level => level.OrderIndex)
                        .ToList();
                        
        Debug.Log($"[LevelSelectionUI] Chargé {_loadedLevels.Count} niveaux");
    }

    private void ClearLevelItems()
    {
        foreach (Transform child in levelItemsContainer)
        {
            Destroy(child.gameObject);
        }
        _instantiatedItems.Clear();
    }

    private void PopulateLevelGrid()
    {
        ClearLevelItems();
        var playerData = PlayerDataManager.Instance?.Data;
        if (playerData == null)
        {
            Debug.LogError("[LevelSelectionUI] PlayerDataManager non disponible.");
            return;
        }

        foreach (var levelData in _loadedLevels)
        {
            bool isUnlocked = CheckLevelUnlockConditions(levelData, playerData);
            GameObject itemGO = null;
            
            if (!isUnlocked)
            {
                // Niveau bloqué
                itemGO = Instantiate(stageLockPrefab, levelItemsContainer);
                Debug.Log($"[LevelSelectionUI] Niveau {levelData.OrderIndex} : BLOQUÉ");
            }
            else
            {
                playerData.CompletedLevels.TryGetValue(levelData.LevelID, out int stars);
                bool isCompleted = stars > 0;

                if (isCompleted)
                {
                    // Niveau complété
                    itemGO = Instantiate(stageCompletePrefab, levelItemsContainer);
                    Debug.Log($"[LevelSelectionUI] Niveau {levelData.OrderIndex} : COMPLÉTÉ ({stars} étoiles)");
                }
                else
                {
                    // Niveau débloqué mais pas complété
                    itemGO = Instantiate(stageNeutralPrefab, levelItemsContainer);
                    Debug.Log($"[LevelSelectionUI] Niveau {levelData.OrderIndex} : DISPONIBLE");
                }

                // Configurer l'item s'il a le script LevelSelectItemUI
                LevelSelectItemUI itemUI = itemGO.GetComponent<LevelSelectItemUI>();
                if (itemUI != null)
                {
                    itemUI.Setup(levelData, stars, OnLevelSelected);
                    _instantiatedItems.Add(itemUI);
                }
            }
        }
        
        Debug.Log($"[LevelSelectionUI] Créé {_instantiatedItems.Count} items de niveau interactifs");
    }

    /// <summary>
    /// Configure la navigation en grille pour les niveaux
    /// </summary>
    private void ConfigureLevelGridNavigation()
    {
        if (_instantiatedItems.Count == 0) return;
        
        // Déterminer le nombre de colonnes de la grid
        GridLayoutGroup gridLayout = levelItemsContainer.GetComponent<GridLayoutGroup>();
        int columnsCount = 3; // Valeur par défaut
        
        if (gridLayout != null)
        {
            // Calculer le nombre de colonnes basé sur la largeur
            RectTransform containerRect = levelItemsContainer.GetComponent<RectTransform>();
            if (containerRect != null)
            {
                float containerWidth = containerRect.rect.width;
                float cellWidth = gridLayout.cellSize.x + gridLayout.spacing.x;
                columnsCount = Mathf.Max(1, Mathf.FloorToInt((containerWidth + gridLayout.spacing.x) / cellWidth));
            }
        }
        
        Debug.Log($"[LevelSelectionUI] Configuration navigation grille - {columnsCount} colonnes, {_instantiatedItems.Count} niveaux");
        
        // Configurer la navigation pour chaque niveau
        for (int i = 0; i < _instantiatedItems.Count; i++)
        {
            Button currentButton = _instantiatedItems[i].GetComponent<Button>();
            if (currentButton == null) continue;
            
            Navigation nav = currentButton.navigation;
            nav.mode = Navigation.Mode.Explicit;
            
            // Calculer la position dans la grille
            int row = i / columnsCount;
            int col = i % columnsCount;
            
            // Navigation horizontale (gauche/droite)
            if (col > 0) // Pas la première colonne
            {
                int leftIndex = i - 1;
                Button leftButton = _instantiatedItems[leftIndex].GetComponent<Button>();
                nav.selectOnLeft = leftButton;
            }
            
            if (col < columnsCount - 1) // Pas la dernière colonne
            {
                int rightIndex = i + 1;
                if (rightIndex < _instantiatedItems.Count)
                {
                    Button rightButton = _instantiatedItems[rightIndex].GetComponent<Button>();
                    nav.selectOnRight = rightButton;
                }
            }
            
            // Navigation verticale (haut/bas)
            if (row > 0) // Pas la première ligne
            {
                int upIndex = i - columnsCount;
                if (upIndex >= 0)
                {
                    Button upButton = _instantiatedItems[upIndex].GetComponent<Button>();
                    nav.selectOnUp = upButton;
                }
            }
            
            if (row < (_instantiatedItems.Count - 1) / columnsCount) // Pas la dernière ligne
            {
                int downIndex = i + columnsCount;
                if (downIndex < _instantiatedItems.Count)
                {
                    Button downButton = _instantiatedItems[downIndex].GetComponent<Button>();
                    nav.selectOnDown = downButton;
                }
                else if (backButton != null)
                {
                    // Dernière ligne : connecter au bouton retour
                    nav.selectOnDown = backButton;
                }
            }
            else if (backButton != null)
            {
                // Dernière ligne : connecter au bouton retour
                nav.selectOnDown = backButton;
            }
            
            currentButton.navigation = nav;
        }
        
        // Configurer la navigation du bouton retour
        if (backButton != null && _instantiatedItems.Count > 0)
        {
            Navigation backNav = backButton.navigation;
            backNav.mode = Navigation.Mode.Explicit;
            
            // Connecter à la dernière ligne de niveaux
            int lastRowStartIndex = (_instantiatedItems.Count - 1) / columnsCount * columnsCount;
            Button lastRowButton = _instantiatedItems[lastRowStartIndex].GetComponent<Button>();
            backNav.selectOnUp = lastRowButton;
            
            backButton.navigation = backNav;
        }
    }

    #endregion

    #region Gestion des Niveaux

    private bool CheckLevelUnlockConditions(LevelData_SO levelToCheck, PlayerSaveData playerData)
    {
        if (levelToCheck.RequiredPreviousLevel != null)
        {
            if (!playerData.CompletedLevels.ContainsKey(levelToCheck.RequiredPreviousLevel.LevelID))
            {
                return false;
            }
        }
        return true;
    }
    
    private void OnLevelSelected(LevelData_SO selectedLevel)
    {
        _selectedLevel = selectedLevel;

        // Mettre à jour l'état visuel de tous les items
        foreach(var item in _instantiatedItems)
        {
            if (item != null)
            {
                item.SetSelected(item.GetLevelData() == _selectedLevel);
            }
        }

        // Activer/désactiver le bouton de lancement
        if (launchLevelButton != null)
        {
            launchLevelButton.interactable = (_selectedLevel != null);
        }
        
        Debug.Log($"[LevelSelectionUI] Niveau sélectionné : {_selectedLevel?.DisplayName ?? "None"}");
        
        // Auto-lancement du niveau (comme dans l'ancien code)
        if (_selectedLevel != null)
        {
            _hubManager?.StartLevel(_selectedLevel);
        }
    }

    private void OnLaunchLevelButtonClicked()
    {
        if (_selectedLevel != null)
        {
            Debug.Log($"[LevelSelectionUI] Lancement manuel du niveau '{_selectedLevel.DisplayName}'");
            _hubManager?.StartLevel(_selectedLevel);
        }
        else
        {
            Debug.LogWarning("[LevelSelectionUI] Bouton Launch cliqué, mais aucun niveau sélectionné.");
        }
    }

    private void OnBackButtonClicked()
    {
        Debug.Log("[LevelSelectionUI] Retour au Hub");
        _hubManager?.GoToGeneralView();
    }

    #endregion
}