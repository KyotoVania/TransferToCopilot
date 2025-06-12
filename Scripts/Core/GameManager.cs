using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using ScriptableObjects;

public enum GameState { Boot, MainMenu, Hub, Loading, InLevel, EndGame }

public class GameManager : SingletonPersistent<GameManager>
{
    public static event Action<GameState> OnGameStateChanged;

    public GameState CurrentState { get; private set; } = GameState.Boot;

    private string _currentActiveSceneName = "";
    private Coroutine _loadSceneCoroutine;

    [Header("Scene Data")]
    [SerializeField] private LevelData_SO mainMenuSceneData;
    [SerializeField] private LevelData_SO hubSceneData;
    public static LevelData_SO CurrentLevelToLoad { get; private set; }

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        bool launchedFromDebug = PlayerPrefs.GetInt("DebugMode_LaunchedFromDebug", 0) == 1;

        if (launchedFromDebug)
        {
            Debug.Log("[GameManager] Lancement en mode DEBUG détecté.");

            // Récupérer l'ID du niveau à charger
            string levelIDToLoad = PlayerPrefs.GetString("DebugMode_SelectedLevelID", "");

            // Nettoyer les PlayerPrefs immédiatement
            PlayerPrefs.DeleteKey("DebugMode_LaunchedFromDebug");
            PlayerPrefs.DeleteKey("DebugMode_SelectedLevelID");
            PlayerPrefs.Save();

            if (string.IsNullOrEmpty(levelIDToLoad))
            {
                Debug.LogError("[GameManager] Mode debug détecté, mais aucun ID de niveau trouvé. Chargement du menu principal.");
                LoadMainMenu();
                return;
            }

            // --- ÉTAPE CLÉ : Retrouver l'asset LevelData_SO à partir de son ID ---
            LevelData_SO levelToLoad = FindLevelDataByID(levelIDToLoad);

            if (levelToLoad != null)
            {
                Debug.Log($"[GameManager] LevelData '{levelToLoad.DisplayName}' trouvé. Lancement du niveau...");
                // Maintenant, on appelle la méthode standard pour charger un niveau
                LoadLevel(levelToLoad);
            }
            else
            {
                Debug.LogError($"[GameManager] Impossible de trouver le LevelData_SO avec l'ID '{levelIDToLoad}'. Assurez-vous qu'il est dans un dossier 'Resources'. Chargement du menu principal.");
                LoadMainMenu();
            }
        }
        else
        {
            // Comportement normal si on ne vient pas de la scène de debug
            Debug.Log("[GameManager] Lancement en mode NORMAL. Chargement du menu principal.");
            LoadMainMenu();
        }
         if (CurrentState == GameState.Boot) // Seulement si on est vraiment au tout début
        {
            string sceneToLoad = "MainMenu"; // Scène par défaut
            if (mainMenuSceneData != null && !string.IsNullOrEmpty(mainMenuSceneData.SceneName))
            {
                sceneToLoad = mainMenuSceneData.SceneName;
            }
            else
            {
                Debug.LogWarning("[GameManager] MainMenu LevelData_SO non assigné ou SceneName vide. Tentative de charger 'MainMenu'.");
            }
            Debug.Log($"[GameManager] Start (Boot): Chargement initial de la scène '{sceneToLoad}'.");
            LoadSceneByName(sceneToLoad, GameState.MainMenu);
        }
    }

    public void LoadHub(LevelData_SO specificHubData = null)
    {
        string sceneToLoad = "MainHub"; // Scène Hub par défaut
        LevelData_SO dataToUse = specificHubData ?? hubSceneData;

        if (dataToUse != null && !string.IsNullOrEmpty(dataToUse.SceneName))
        {
            sceneToLoad = dataToUse.SceneName;
        }
        else
        {
            Debug.LogWarning("[GameManager] Hub LevelData_SO non assigné ou SceneName vide. Tentative de charger 'MainHub'.");
        }
        Debug.Log($"[GameManager] LoadHub: Demande de chargement de la scène '{sceneToLoad}'.");
        LoadSceneByName(sceneToLoad, GameState.Hub);
    }

    public void LoadLevel(LevelData_SO levelData)
    {
        if (levelData == null || string.IsNullOrEmpty(levelData.SceneName))
        {
            Debug.LogError("[GameManager] LevelData_SO ou SceneName est invalide pour LoadLevel.");
            return;
        }
        Debug.Log($"[GameManager] LoadLevel: Demande de chargement du niveau '{levelData.DisplayName}', scène '{levelData.SceneName}'.");
        CurrentLevelToLoad = levelData;
        LoadSceneByName(levelData.SceneName, GameState.InLevel);
    }

    public void LoadMainMenu()
    {
        string sceneToLoad = "MainMenu";
        if (mainMenuSceneData != null && !string.IsNullOrEmpty(mainMenuSceneData.SceneName))
        {
            sceneToLoad = mainMenuSceneData.SceneName;
        }
        else
        {
            Debug.LogWarning("[GameManager] MainMenu LevelData_SO non assigné ou SceneName vide. Tentative de charger 'MainMenu'.");
        }
        Debug.Log($"[GameManager] LoadMainMenu: Demande de chargement de la scène '{sceneToLoad}'.");
        LoadSceneByName(sceneToLoad, GameState.MainMenu);
    }

    /// <summary>
    /// Trouve un asset LevelData_SO dans les dossiers Resources du projet en utilisant son ID.
    /// </summary>
    /// <param name="levelID">L'ID du niveau à rechercher.</param>
    /// <returns>L'asset LevelData_SO ou null si non trouvé.</returns>
    private LevelData_SO FindLevelDataByID(string levelID)
    {
        // Charge tous les LevelData_SO depuis tous les sous-dossiers de "Resources".
        // Assurez-vous que le chemin est correct. Par exemple, "Data/Levels"
        LevelData_SO[] allLevels = Resources.LoadAll<LevelData_SO>("Data/Levels"); 

        Debug.Log($"[GameManager] Recherche de '{levelID}' parmi {allLevels.Length} niveaux trouvés dans Resources/Data/Levels.");

        foreach (LevelData_SO levelData in allLevels)
        {
            if (levelData.LevelID == levelID)
            {
                return levelData;
            }
        }

        // Si on ne trouve rien, on retourne null.
        return null;
    }
    private void LoadSceneByName(string sceneName, GameState targetStateAfterLoad)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[GameManager] Tentative de chargement d'une scène avec un nom vide.");
            return;
        }

        Debug.Log($"[GameManager] LoadSceneByName: Reçu demande pour '{sceneName}'. État actuel: {CurrentState}, Scène active actuelle: '{_currentActiveSceneName}'.");

        if (CurrentState == GameState.Loading && _currentActiveSceneName == sceneName)
        {
            Debug.LogWarning($"[GameManager] La scène '{sceneName}' est déjà en cours de chargement ou est la scène active (et état Loading). Aucune action.");
            return;
        }
        if (CurrentState != GameState.Loading && _currentActiveSceneName == sceneName)
        {
            Debug.LogWarning($"[GameManager] La scène '{sceneName}' est déjà chargée et active. Passage à l'état {targetStateAfterLoad}.");
            SetState(targetStateAfterLoad); // Change juste l'état si la scène est déjà la bonne
            return;
        }

        if (_loadSceneCoroutine != null)
        {
            Debug.LogWarning($"[GameManager] Un chargement de scène ('{_currentActiveSceneName}' vers une nouvelle) est déjà en cours. Annulation du nouveau chargement pour '{sceneName}'.");
            return;
        }
        _loadSceneCoroutine = StartCoroutine(LoadSceneRoutine(sceneName, targetStateAfterLoad));
    }

    private IEnumerator LoadSceneRoutine(string sceneNameToLoad, GameState targetStateAfterLoad)
    {
        Debug.Log($"[GameManager] LoadSceneRoutine: DÉBUT pour '{sceneNameToLoad}'. _currentActiveSceneName AVANT déchargement: '{_currentActiveSceneName}'.");
        SetState(GameState.Loading);

        
        if (!string.IsNullOrEmpty(_currentActiveSceneName) && _currentActiveSceneName != sceneNameToLoad) // Ne pas décharger si on recharge la même scène
        {
            Scene sceneToUnload = SceneManager.GetSceneByName(_currentActiveSceneName);
            if (sceneToUnload.IsValid() && sceneToUnload.isLoaded)
            {
                Debug.Log($"[GameManager] LoadSceneRoutine: Déchargement de la scène '{_currentActiveSceneName}'.");
                AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(_currentActiveSceneName);
                while (asyncUnload != null && !asyncUnload.isDone) { yield return null; }
                Debug.Log($"[GameManager] LoadSceneRoutine: Scène '{_currentActiveSceneName}' déchargée.");
            }
            else
            {
                Debug.LogWarning($"[GameManager] LoadSceneRoutine: Tentative de décharger la scène '{_currentActiveSceneName}', mais elle n'est pas valide ou pas chargée. IsValid: {sceneToUnload.IsValid()}, IsLoaded: {sceneToUnload.isLoaded}.");
            }
        }
        else
        {
            Debug.Log("[GameManager] LoadSceneRoutine: _currentActiveSceneName est vide, pas de scène à décharger.");
        }
        // _currentActiveSceneName = ""; // On ne le vide pas ici, mais on le remplace après le chargement de la nouvelle

        // Charger la nouvelle scène additivement
        Debug.Log($"[GameManager] LoadSceneRoutine: Chargement additif de la scène '{sceneNameToLoad}'.");
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneNameToLoad, LoadSceneMode.Additive);
        if (asyncLoad == null) {
            Debug.LogError($"[GameManager] LoadSceneRoutine: Échec au démarrage du chargement pour '{sceneNameToLoad}'. Vérifiez les Build Settings.");
            SetState(GameState.MainMenu);
             _currentActiveSceneName = (mainMenuSceneData != null && !string.IsNullOrEmpty(mainMenuSceneData.SceneName)) ? mainMenuSceneData.SceneName : "MainMenu";
            _loadSceneCoroutine = null;
            yield break;
        }
        while (!asyncLoad.isDone) { yield return null; }
        Debug.Log($"[GameManager] LoadSceneRoutine: Scène '{sceneNameToLoad}' chargée additivement.");

        Scene newActiveScene = SceneManager.GetSceneByName(sceneNameToLoad);
        if (newActiveScene.IsValid() && newActiveScene.isLoaded)
        {
            SceneManager.SetActiveScene(newActiveScene);
            _currentActiveSceneName = newActiveScene.name;
            Debug.Log($"[GameManager] LoadSceneRoutine: Scène '{_currentActiveSceneName}' définie comme active.");
        }
        else
        {
            Debug.LogError($"[GameManager] LoadSceneRoutine: Scène '{sceneNameToLoad}' n'a pas pu être définie active. Fallback MainMenu.");
            SetState(GameState.MainMenu);
            _currentActiveSceneName = (mainMenuSceneData != null && !string.IsNullOrEmpty(mainMenuSceneData.SceneName)) ? mainMenuSceneData.SceneName : "MainMenu";
            _loadSceneCoroutine = null;
            yield break;
        }

        yield return new WaitForSeconds(0.1f); // Petit délai pour l'initialisation de la nouvelle scène

        Debug.Log($"[GameManager] LoadSceneRoutine: FIN pour '{sceneNameToLoad}'. Passage à l'état {targetStateAfterLoad}.");
        SetState(targetStateAfterLoad);
        _loadSceneCoroutine = null;
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState && newState != GameState.Boot && newState != GameState.Loading) // Permettre la transition de Boot/Loading vers le même état final
        {
             Debug.Log($"[GameManager] SetState: État '{newState}' demandé, mais déjà actif. La logique d'état sera quand même exécutée pour assurer la cohérence.");
            // Ne pas return ici pour permettre au WinLoseController de se réinitialiser si nécessaire
        }

        GameState previousState = CurrentState;
        CurrentState = newState;
        Debug.Log($"[GameManager] SetState: État changé de '{previousState}' vers '{newState}'.");
        OnGameStateChanged?.Invoke(newState);

        // Gérer la réinitialisation du WinLoseController
        if (WinLoseController.Instance != null)
        {
            bool shouldResetWLC = (newState == GameState.InLevel || newState == GameState.Hub || newState == GameState.MainMenu);
            if (shouldResetWLC)
            {
                // On réinitialise toujours WinLoseController en entrant dans ces états pour assurer un état propre,
                // que le jeu précédent se soit terminé ou non. ResetGameConditionState est idempotent.
                Debug.Log($"[GameManager] Nouvel état ({newState}). APPEL de WinLoseController.ResetGameConditionState(). IsGameOver avant: {WinLoseController.Instance.IsGameOver}", this);
                WinLoseController.Instance.ResetGameConditionState();
            }
        }
        if (newState == GameState.Hub || newState == GameState.MainMenu)
        {
            // Nettoyer les données du niveau actuel
            CurrentLevelToLoad = null; // Réinitialiser le niveau actuel
            Debug.Log("[GameManager] Retour au Hub/Menu. Les données du niveau actuel ont été nettoyées.");
        }
        // Gérer l'état musical
        if (MusicManager.Instance != null)
        {
            string musicStateToSet = "";
            bool immediateTransition = false; // Par défaut, transitions douces gérées par Wwise

            switch (newState)
            {
                case GameState.Boot: // Géré par Start qui charge MainMenu
                case GameState.Loading:
                    musicStateToSet = "Silence";
                    break;
                case GameState.MainMenu:
                    musicStateToSet = "MainMenu";
                    immediateTransition = true;
                    break;
                case GameState.Hub:
                    musicStateToSet = "Hub";
                    break;
                case GameState.InLevel:
                    musicStateToSet = "Exploration";
                    break;
                case GameState.EndGame:
                    // La musique pour EndGame est déclenchée par WinLoseController.ActivateGameOverSequence
                    // MusicManager.Instance.SetMusicState("EndGame");
                    // On ne veut pas que GameManager rechange cet état s'il vient d'être mis par WinLoseController.
                    // Si previousState n'était pas EndGame, WinLoseController s'en est chargé.
                    // Si newState est EndGame, on laisse ce que WinLoseController a fait.
                    // On ne met à jour musicStateToSet que si ce n'est pas WinLoseController qui gère.
                    // Ici, on considère que WinLoseController gère la musique pour EndGame.
                    break;
                default:
                    Debug.LogWarning($"[GameManager] Aucun état musical Wwise défini pour GameState: {newState}");
                    break;
            }

            if (!string.IsNullOrEmpty(musicStateToSet))
            {
              
                MusicManager.Instance.SetMusicState(musicStateToSet, immediateTransition);
            }
        }
        else
        {
            Debug.LogWarning("[GameManager] MusicManager.Instance est null. Impossible de changer l'état musical.");
        }
    }
}