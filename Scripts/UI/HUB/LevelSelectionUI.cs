using UnityEngine;
using UnityEngine.UI; // Pour les éléments UI comme Button, VerticalLayoutGroup
using TMPro;          // Pour TextMeshProUGUI
using System.Collections.Generic;
using System.Linq;    // Pourra être utile pour trier ou filtrer les niveaux

public class LevelSelectionUI : MonoBehaviour
{
    [Header("Configuration des Données")]
    [Tooltip("Chemin dans le dossier Resources pour charger les LevelData_SO. Exemple: Data/Levels")]
    [SerializeField] private string levelDataPath = "Data/Levels"; // Chemin configurable

    [Header("Références UI")]
    [Tooltip("Prefab pour un élément de la liste des niveaux.")]
    [SerializeField] private GameObject levelSelectItemPrefab;
    [Tooltip("Conteneur où les items de niveau seront instanciés (doit avoir un LayoutGroup).")]
    [SerializeField] private Transform levelItemsContainer;
    [Tooltip("Bouton pour retourner à la vue générale du Hub.")]
    [SerializeField] private Button backButton;

    [Header("Affichage Détails Niveau (Optionnel)")]
    [Tooltip("Panel pour afficher les détails du niveau sélectionné.")]
    [SerializeField] private GameObject levelDetailsPanel; // GameObject parent du panel de détails
    [SerializeField] private TextMeshProUGUI detailLevelNameText;
    [SerializeField] private Image detailLevelIconImage; // Ajout pour une icône potentielle
    [SerializeField] private TextMeshProUGUI detailLevelDescriptionText;
    [SerializeField] private TextMeshProUGUI detailRewardsText; // Pourrait afficher XP, monnaie, etc.
    [SerializeField] private Button launchLevelButton; // Bouton pour lancer le niveau depuis les détails

    private List<LevelData_SO> _loadedLevels = new List<LevelData_SO>();
    private LevelData_SO _selectedLevel = null; // Niveau actuellement sélectionné

    private HubManager _hubManager;

    #region Cycle de Vie Unity

    private void Awake()
    {
        // Correction de l'avertissement CS0618
        _hubManager = FindFirstObjectByType<HubManager>();
        if (_hubManager == null)
        {
            Debug.LogError("[LevelSelectionUI] HubManager non trouvé dans la scène !");
            enabled = false;
            return;
        }

        if (levelSelectItemPrefab == null)
        {
            Debug.LogError("[LevelSelectionUI] Le prefab 'levelSelectItemPrefab' n'est pas assigné !");
            enabled = false;
            return;
        }

        if (levelItemsContainer == null)
        {
            Debug.LogError("[LevelSelectionUI] Le conteneur 'levelItemsContainer' n'est pas assigné !");
            enabled = false;
            return;
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackButtonClicked);
        }
        else
        {
            Debug.LogWarning("[LevelSelectionUI] Le bouton 'backButton' n'est pas assigné.");
        }

        if (launchLevelButton != null)
        {
            launchLevelButton.onClick.AddListener(OnLaunchLevelButtonClicked);
        }

        if (levelDetailsPanel != null)
        {
            levelDetailsPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        Debug.Log("[LevelSelectionUI] Panel activé. Chargement et affichage des niveaux...");
        LoadAndDisplayLevels();
    }

    private void OnDisable()
    {
        Debug.Log("[LevelSelectionUI] Panel désactivé. Nettoyage des items de niveau...");
        ClearLevelItems();
    }

    #endregion

    #region Logique Principale

    private void LoadAndDisplayLevels()
    {
        LoadLevelData();
        PopulateLevelList();
        SelectLevel(null);
    }

    private void LoadLevelData()
    {
        _loadedLevels.Clear();
        LevelData_SO[] allLevelsInResources = Resources.LoadAll<LevelData_SO>(levelDataPath);

        if (allLevelsInResources.Length == 0)
        {
            Debug.LogWarning($"[LevelSelectionUI] Aucun LevelData_SO trouvé dans Resources/{levelDataPath}");
        }
        else
        {
            // Filtrer pour ne garder que les GameplayLevel
            _loadedLevels = allLevelsInResources
                                .Where(level => level.TypeOfLevel == LevelType.GameplayLevel)
                                .OrderBy(level => level.DisplayName) // Exemple de tri par nom
                                .ToList();
            Debug.Log($"[LevelSelectionUI] {allLevelsInResources.Length} niveaux trouvés, {_loadedLevels.Count} sont des GameplayLevels et ont été chargés depuis Resources/{levelDataPath}");
        }
    }

    private void ClearLevelItems()
    {
        foreach (Transform child in levelItemsContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private void PopulateLevelList()
    {
        ClearLevelItems();

        if (PlayerDataManager.Instance == null)
        {
            Debug.LogError("[LevelSelectionUI] PlayerDataManager.Instance est null. Impossible de vérifier les conditions de déblocage.");
            return;
        }

        if (_loadedLevels.Count == 0)
        {
            Debug.Log("[LevelSelectionUI] Aucun niveau de type GameplayLevel à afficher.");
            // Optionnel : Afficher un message à l'utilisateur si aucun niveau n'est disponible.
            return;
        }

        foreach (LevelData_SO levelData in _loadedLevels)
        {
            GameObject itemGO = Instantiate(levelSelectItemPrefab, levelItemsContainer);
            LevelSelectItemUI itemUI = itemGO.GetComponent<LevelSelectItemUI>();

            if (itemUI != null)
            {
                bool isUnlocked = CheckLevelUnlockConditions(levelData, PlayerDataManager.Instance.Data);
                itemUI.Setup(levelData, isUnlocked, this.SelectLevel);
            }
            else
            {
                // Fallback si LevelSelectItemUI n'est pas sur le prefab
                TextMeshProUGUI nameText = itemGO.transform.Find("LevelNameText")?.GetComponent<TextMeshProUGUI>();
                Button itemButton = itemGO.GetComponent<Button>();

                if (nameText != null) nameText.text = levelData.DisplayName;

                bool isUnlocked = CheckLevelUnlockConditions(levelData, PlayerDataManager.Instance.Data);

                if (itemButton != null)
                {
                    itemButton.interactable = isUnlocked;
                    itemButton.onClick.RemoveAllListeners();
                    itemButton.onClick.AddListener(() => SelectLevel(levelData));

                    var colors = itemButton.colors;
                    colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    itemButton.colors = colors;
                }
                Debug.Log($"[LevelSelectionUI] Niveau '{levelData.DisplayName}' (GameplayLevel) instancié. Débloqué: {isUnlocked}");
            }
        }
    }

    private bool CheckLevelUnlockConditions(LevelData_SO levelToCheck, PlayerSaveData playerData)
    {
        if (levelToCheck == null || playerData == null) return false;

        // 1. Vérifier le niveau de joueur requis (XP)
        if (playerData.Experience < levelToCheck.RequiredPlayerLevel)
        {
            Debug.Log($"[LevelSelectionUI] Niveau '{levelToCheck.DisplayName}' bloqué: XP requise {levelToCheck.RequiredPlayerLevel}, XP joueur {playerData.Experience}.");
            return false;
        }

        // 2. Vérifier si le niveau précédent requis est complété
        if (levelToCheck.RequiredPreviousLevel != null)
        {
            if (!playerData.CompletedLevelIDs.Contains(levelToCheck.RequiredPreviousLevel.LevelID))
            {
                Debug.Log($"[LevelSelectionUI] Niveau '{levelToCheck.DisplayName}' bloqué: Niveau requis '{levelToCheck.RequiredPreviousLevel.DisplayName}' non complété.");
                return false;
            }
        }
        return true;
    }

    public void SelectLevel(LevelData_SO levelData)
    {
        _selectedLevel = levelData;

        if (_selectedLevel != null)
        {
            Debug.Log($"[LevelSelectionUI] Niveau sélectionné : {_selectedLevel.DisplayName}");
            if (levelDetailsPanel != null) levelDetailsPanel.SetActive(true);

            if (detailLevelNameText != null) detailLevelNameText.text = _selectedLevel.DisplayName;
            // if (detailLevelIconImage != null) // Si tu ajoutes une icône au LevelData_SO et un champ Image ici
            // {
            //     detailLevelIconImage.sprite = _selectedLevel.LevelIcon; // Supposant que LevelIcon existe dans LevelData_SO
            //     detailLevelIconImage.enabled = (_selectedLevel.LevelIcon != null);
            // }
            if (detailLevelDescriptionText != null) detailLevelDescriptionText.text = _selectedLevel.Description;
            if (detailRewardsText != null)
            {
                string rewards = $"XP: {_selectedLevel.ExperienceReward}\nMonnaie: {_selectedLevel.CurrencyReward}";
                if (_selectedLevel.CharacterUnlockReward != null)
                {
                    rewards += $"\nDébloque: {_selectedLevel.CharacterUnlockReward.DisplayName}";
                }
                detailRewardsText.text = rewards;
            }

            if (launchLevelButton != null && PlayerDataManager.Instance != null)
            {
                launchLevelButton.interactable = CheckLevelUnlockConditions(_selectedLevel, PlayerDataManager.Instance.Data);
            }
        }
        else
        {
            Debug.Log("[LevelSelectionUI] Aucun niveau sélectionné (ou sélection désélectionnée).");
            if (levelDetailsPanel != null) levelDetailsPanel.SetActive(false);
        }
    }

    #endregion

    #region Gestion des Clics UI

    private void OnBackButtonClicked()
    {
        Debug.Log("[LevelSelectionUI] Bouton Retour cliqué.");
        _hubManager?.GoToGeneralView(); // Demande au HubManager de retourner à la vue générale
    }

    private void OnLaunchLevelButtonClicked()
    {
        if (_selectedLevel != null && PlayerDataManager.Instance != null)
        {
            if (CheckLevelUnlockConditions(_selectedLevel, PlayerDataManager.Instance.Data))
            {
                Debug.Log($"[LevelSelectionUI] Lancement du niveau : {_selectedLevel.DisplayName}");
                _hubManager?.StartLevel(_selectedLevel);
            }
            else
            {
                Debug.LogWarning($"[LevelSelectionUI] Tentative de lancer le niveau '{_selectedLevel.DisplayName}' mais il n'est pas débloqué.");
            }
        }
        else
        {
            Debug.LogWarning("[LevelSelectionUI] Aucun niveau sélectionné pour le lancement.");
        }
    }
    #endregion
}