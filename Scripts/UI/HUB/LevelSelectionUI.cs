using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using ScriptableObjects;


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
    [SerializeField] private Button launchLevelButton; // Now used for the selected level.
    
    // --- Logic for selection and launching ---
    private LevelData_SO _selectedLevel;
    private List<LevelSelectItemUI> _instantiatedItems = new List<LevelSelectItemUI>();
    private List<LevelData_SO> _loadedLevels = new List<LevelData_SO>();

    private HubManager _hubManager;

    void Awake()
    {
        _hubManager = FindFirstObjectByType<HubManager>();
        if (_hubManager == null) Debug.LogError("[LevelSelectionUI] HubManager non trouvé!");
        
        if (stageLockPrefab == null || stageNeutralPrefab == null || stageCompletePrefab == null)
             Debug.LogError("[LevelSelectionUI] Un ou plusieurs prefabs d'état de niveau ne sont pas assignés !");

        backButton?.onClick.AddListener(OnBackButtonClicked);
        launchLevelButton?.onClick.AddListener(OnLaunchLevelButtonClicked);
    }

    private void OnEnable()
    {
        LoadAndDisplayLevels();
        // Initially, no level is selected.
    }

    private void OnDisable()
    {
        ClearLevelItems();
    }

    private void LoadAndDisplayLevels()
    {
        LoadLevelData();
        PopulateLevelGrid();
    }

    private void LoadLevelData()
    {
        _loadedLevels.Clear();
        LevelData_SO[] allLevels = Resources.LoadAll<LevelData_SO>(levelDataPath);
        _loadedLevels = allLevels
                        .Where(level => level.TypeOfLevel == LevelType.GameplayLevel)
                        .OrderBy(level => level.OrderIndex)
                        .ToList();
    }

    private void ClearLevelItems()
    {
        foreach (Transform child in levelItemsContainer)
        {
            Destroy(child.gameObject);
        }
        _instantiatedItems.Clear();
    }

    // REWRITTEN LOGIC
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
            
            if (!isUnlocked)
            {
                Instantiate(stageLockPrefab, levelItemsContainer);
                continue;
            }

            playerData.CompletedLevels.TryGetValue(levelData.LevelID, out int stars);
            bool isCompleted = stars > 0;

            GameObject itemGO;
            if (isCompleted)
            {
                // Instantiate a 'Complete' button.
                itemGO = Instantiate(stageCompletePrefab, levelItemsContainer);
                itemGO.GetComponent<LevelSelectItemUI>()?.Setup(levelData, stars, OnLevelSelected);
            }
            else
            {
                // Instantiate a 'Neutral' (available but not complete) button.
                itemGO = Instantiate(stageNeutralPrefab, levelItemsContainer);
                itemGO.GetComponent<LevelSelectItemUI>()?.Setup(levelData, 0, OnLevelSelected);
            }

            if (itemGO.GetComponent<LevelSelectItemUI>() != null)
            {
                _instantiatedItems.Add(itemGO.GetComponent<LevelSelectItemUI>());
            }
        }
    }

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

        // Update the visual selection state for all items.
        foreach(var item in _instantiatedItems)
        {
            item.SetSelected(item.GetLevelData() == _selectedLevel);
        }

        // Enable/disable the launch button based on selection.
        if (launchLevelButton != null)
        {
            launchLevelButton.interactable = (_selectedLevel != null);
        }
        
        Debug.Log($"[LevelSelectionUI] Level '{_selectedLevel?.DisplayName ?? "None"}' selected.");
        _hubManager?.StartLevel(_selectedLevel);
    }

    // MODIFIED: This is now triggered by the dedicated launch button.
    private void OnLaunchLevelButtonClicked()
    {
        if (_selectedLevel != null)
        {
            Debug.Log($"[LevelSelectionUI] Launch button clicked for '{_selectedLevel.DisplayName}'. Launching game.");
            _hubManager?.StartLevel(_selectedLevel);
        }
        else
        {
            Debug.LogWarning("[LevelSelectionUI] Launch button clicked, but no level is selected.");
        }
    }

    private void OnBackButtonClicked()
    {
        _hubManager?.GoToGeneralView();
    }
    
}