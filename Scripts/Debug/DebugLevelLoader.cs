using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DebugLevelLoader : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform levelButtonsContainer;
    [SerializeField] private GameObject levelButtonPrefab;
    [SerializeField] private ScrollRect scrollView;
    
    [Header("Info Panel")]
    [SerializeField] private TextMeshProUGUI levelCountText;
    [SerializeField] private TextMeshProUGUI selectedLevelInfoText;
    
    [Header("Configuration")]
    [SerializeField] private string coreSceneName = "Core";
    
    private List<LevelData_SO> discoveredLevels = new List<LevelData_SO>();
    private LevelData_SO selectedLevel = null;

    void Start()
    {
        Debug.Log("[DebugLevelLoader] Initialisation du Debug Level Loader");
        DiscoverLevels();
        CreateLevelButtons();
        UpdateInfoPanel();
    }

    void DiscoverLevels()
    {
        discoveredLevels.Clear();

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:LevelData_SO");
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            LevelData_SO levelData = AssetDatabase.LoadAssetAtPath<LevelData_SO>(assetPath);
            
            if (levelData != null && levelData.TypeOfLevel == LevelType.GameplayLevel)
            {
                discoveredLevels.Add(levelData);
            }
        }
        
        Debug.Log($"[DebugLevelLoader] Découverte via AssetDatabase : {discoveredLevels.Count} niveaux de gameplay trouvés");
#else
        LevelData_SO[] allLevels = Resources.LoadAll<LevelData_SO>("Data/Levels");
        discoveredLevels = allLevels.Where(level => level.TypeOfLevel == LevelType.GameplayLevel).ToList();
        
        Debug.Log($"[DebugLevelLoader] Découverte via Resources : {discoveredLevels.Count} niveaux de gameplay trouvés");
#endif

        discoveredLevels = discoveredLevels.OrderBy(level => level.DisplayName).ToList();
    }

    void CreateLevelButtons()
    {
        if (levelButtonsContainer == null || levelButtonPrefab == null)
        {
            Debug.LogError("[DebugLevelLoader] Références UI manquantes pour la création des boutons");
            return;
        }

        // Nettoyer les boutons existants
        foreach (Transform child in levelButtonsContainer)
        {
            Destroy(child.gameObject);
        }

        // Créer un bouton pour chaque niveau
        foreach (LevelData_SO levelData in discoveredLevels)
        {
            GameObject buttonObj = Instantiate(levelButtonPrefab, levelButtonsContainer);
            
            // Configurer le texte du bouton
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = $"{levelData.DisplayName}\n<size=70%><color=#888888>({levelData.LevelID})</color></size>";
            }

            // Configurer l'action du bouton
            Button button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                LevelData_SO capturedLevelData = levelData;
                button.onClick.AddListener(() => OnLevelButtonClicked(capturedLevelData));
            }

            // Optionnel : coloration différente selon la difficulté
            Image buttonImage = buttonObj.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = GetDifficultyColor(levelData.Difficulty);
            }
        }
    }

    Color GetDifficultyColor(int difficulty)
    {
        switch (difficulty)
        {
            case 1: return new Color(0.7f, 1f, 0.7f, 1f);     // Vert clair (Facile)
            case 2: return new Color(1f, 1f, 0.7f, 1f);       // Jaune clair (Normal)
            case 3: return new Color(1f, 0.8f, 0.6f, 1f);     // Orange clair (Difficile)
            case 4: return new Color(1f, 0.7f, 0.7f, 1f);     // Rouge clair (Très difficile)
            case 5: return new Color(1f, 0.6f, 1f, 1f);       // Magenta clair (Extrême)
            default: return Color.white;
        }
    }

    void OnLevelButtonClicked(LevelData_SO levelData)
    {
        selectedLevel = levelData;
        UpdateSelectedLevelInfo();
        
        Debug.Log($"[DebugLevelLoader] Niveau sélectionné : {levelData.DisplayName}");
        
        // Démarrer la coroutine pour charger Core puis le niveau
    }

    /// <summary>
    /// Coroutine qui charge d'abord Core, attend que GameManager soit prêt, puis lance le niveau
    /// </summary>
    IEnumerator LoadCoreAndLevel()
    {
        if (selectedLevel == null)
        {
            Debug.LogWarning("[DebugLevelLoader] Aucun niveau sélectionné pour le lancement");
            yield break;
        }

        Debug.Log($"[DebugLevelLoader] Étape 1: Chargement de la scène {coreSceneName}...");
        // ✅ SOLUTION ROBUSTE : Utiliser PlayerPrefs pour persister l'info
        Debug.Log($"[DebugLevelLoader] Étape 1: Signalement du mode debug via PlayerPrefs...");
        PlayerPrefs.SetInt("DebugMode_LaunchedFromDebug", 1);
        PlayerPrefs.SetString("DebugMode_SelectedLevel", selectedLevel.LevelID);
        PlayerPrefs.Save(); 
        // 1. Charger la scène Core
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(coreSceneName);
        
        // Attendre que le chargement soit terminé
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
        
        Debug.Log($"[DebugLevelLoader] Scène {coreSceneName} chargée, attente de GameManager...");
        
        // 2. Attendre que GameManager soit initialisé
        float timeout = 5f; // Timeout de sécurité
        float timeWaited = 0f;
        
        while (GameManager.Instance == null && timeWaited < timeout)
        {
            yield return new WaitForFixedUpdate(); // Attendre un frame
            timeWaited += Time.fixedDeltaTime;
        }
        
        // 3. Vérifier si GameManager est prêt
        if (GameManager.Instance == null)
        {
            Debug.LogError("[DebugLevelLoader] Timeout: GameManager.Instance toujours null après chargement de Core !");
            yield break;
        }
        
        Debug.Log("[DebugLevelLoader] GameManager trouvé ! Application des options de debug...");
        
        // 4. Appliquer les options de debug
        ApplyDebugOptions();
        
        // 5. Assigner le niveau à charger
        GameManager.Instance.LoadLevel(selectedLevel);
        Debug.Log($"[DebugLevelLoader] GameManager.CurrentLevelToLoad assigné à : {selectedLevel.DisplayName}");
        
        // 6. Lancer le niveau via GameManager
        Debug.Log($"[DebugLevelLoader] Lancement du niveau via GameManager.LoadLevel()");
        GameManager.Instance.LoadLevel(selectedLevel);
    }

    void ApplyDebugOptions()
    {
        // Pour l'instant, pas d'options de debug (vous les ajouterez plus tard)
        DebugManager.InfiniteGold = false;
        DebugManager.Invincibility = false;
        DebugManager.SkipIntro = false;
        DebugManager.FastBeat = false;

        Debug.Log($"[DebugLevelLoader] Options de debug appliquées - " +
                  $"Or Infini: {DebugManager.InfiniteGold}, " +
                  $"Invincibilité: {DebugManager.Invincibility}, " +
                  $"Skip Intro: {DebugManager.SkipIntro}, " +
                  $"Beat Rapide: {DebugManager.FastBeat}");
    }

    void UpdateInfoPanel()
    {
        if (levelCountText != null)
        {
            levelCountText.text = $"Niveaux disponibles : {discoveredLevels.Count}";
        }
    }

    void UpdateSelectedLevelInfo()
    {
        if (selectedLevelInfoText == null || selectedLevel == null) return;

        string info = $"<b>{selectedLevel.DisplayName}</b>\n" +
                      $"ID: {selectedLevel.LevelID}\n" +
                      $"Scène: {selectedLevel.SceneName}\n" +
                      $"Difficulté: {selectedLevel.Difficulty}/5\n" +
                      $"BPM: {selectedLevel.RhythmBPM}\n" +
                      $"XP Récompense: {selectedLevel.ExperienceReward}\n" +
                      $"Or Récompense: {selectedLevel.CurrencyReward}\n\n" +
                      $"<i>{selectedLevel.Description}</i>";

        selectedLevelInfoText.text = info;
        LaunchLevel();
    }
    void LaunchLevel()
    {
        if (selectedLevel == null)
        {
            Debug.LogError("[DebugLevelLoader] Aucun niveau sélectionné pour le lancement !");
            return;
        }

        // 1. Sauvegarder les informations pour que GameManager puisse les lire
        PlayerPrefs.SetInt("DebugMode_LaunchedFromDebug", 1);
        PlayerPrefs.SetString("DebugMode_SelectedLevelID", selectedLevel.LevelID); // On sauvegarde l'ID
    
        // Appliquer les autres options de debug si vous en avez
        // PlayerPrefs.SetInt("DebugMode_InfiniteGold", infiniteGoldToggle.isOn ? 1 : 0);
    
        PlayerPrefs.Save(); 
    
        // 2. Charger la scène Core. Le travail de ce script est terminé.
        SceneManager.LoadScene(coreSceneName);
    }

    public void RefreshLevelList()
    {
        DiscoverLevels();
        CreateLevelButtons();
        UpdateInfoPanel();
        selectedLevel = null;
        if (selectedLevelInfoText != null) selectedLevelInfoText.text = "Aucun niveau sélectionné";
    }
}

/// <summary>
/// Classe statique pour stocker les options de debug
/// </summary>
public static class DebugManager
{
    public static bool InfiniteGold { get; set; } = false;
    public static bool Invincibility { get; set; } = false;
    public static bool SkipIntro { get; set; } = false;
    public static bool FastBeat { get; set; } = false;

    public static void ResetAllFlags()
    {
        InfiniteGold = false;
        Invincibility = false;
        SkipIntro = false;
        FastBeat = false;
    }

    public static void LogCurrentState()
    {
        Debug.Log($"[DebugManager] État actuel - " +
                  $"Or Infini: {InfiniteGold}, " +
                  $"Invincibilité: {Invincibility}, " +
                  $"Skip Intro: {SkipIntro}, " +
                  $"Beat Rapide: {FastBeat}");
    }
}