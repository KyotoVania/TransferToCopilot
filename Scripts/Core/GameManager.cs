using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using ScriptableObjects;

/// <summary>
/// Enumeration representing the different states of the game.
/// </summary>
public enum GameState { Boot, MainMenu, Hub, Loading, InLevel, EndGame }

/// <summary>
/// Enumeration representing the different input modes for the game.
/// </summary>
public enum InputMode { UI, Gameplay, Disabled }

/// <summary>
/// Configuration data for each game state, including music state, transition settings, and input mode.
/// </summary>
[System.Serializable]
public class GameStateConfig
{
    /// <summary>
    /// The music state to set when entering this game state.
    /// </summary>
    public string musicState;
    
    /// <summary>
    /// Whether the music transition should be immediate or gradual.
    /// </summary>
    public bool immediateTransition;
    
    /// <summary>
    /// The input mode to activate when entering this game state.
    /// </summary>
    public InputMode inputMode;

    /// <summary>
    /// Initializes a new instance of the GameStateConfig class.
    /// </summary>
    /// <param name="musicState">The music state to set when entering this game state.</param>
    /// <param name="immediateTransition">Whether the music transition should be immediate or gradual.</param>
    /// <param name="inputMode">The input mode to activate when entering this game state.</param>
    public GameStateConfig(string musicState, bool immediateTransition, InputMode inputMode)
    {
        this.musicState = musicState;
        this.immediateTransition = immediateTransition;
        this.inputMode = inputMode;
    }
}

/// <summary>
/// Central game manager that handles scene transitions, game state management, and coordinates other systems.
/// Inherits from SingletonPersistent to ensure there's only one instance across scenes.
/// </summary>
public class GameManager : SingletonPersistent<GameManager>
{
    /// <summary>
    /// Event triggered when the game state changes.
    /// </summary>
    public static event Action<GameState> OnGameStateChanged;

    /// <summary>
    /// Gets the current game state.
    /// </summary>
    public GameState CurrentState { get; private set; } = GameState.Boot;

    /// <summary>
    /// The name of the currently active scene.
    /// </summary>
    private string _currentActiveSceneName = "";
    
    /// <summary>
    /// Reference to the currently running scene loading coroutine.
    /// </summary>
    private Coroutine _loadSceneCoroutine;

    [Header("Scene Data")]
    /// <summary>
    /// Configuration data for the main menu scene.
    /// </summary>
    [SerializeField] private LevelData_SO mainMenuSceneData;
    
    /// <summary>
    /// Configuration data for the hub scene.
    /// </summary>
    [SerializeField] private LevelData_SO hubSceneData;
    
    /// <summary>
    /// The level data for the currently loading or loaded level.
    /// </summary>
    public static LevelData_SO CurrentLevelToLoad { get; private set; }
    
    [Header("Scene Loading")]
    /// <summary>
    /// The name of the loading scene to display during transitions.
    /// </summary>
    [SerializeField] private string loadingSceneName = "LoadingScene";
    
    /// <summary>
    /// Minimum time in seconds to display the loading screen.
    /// </summary>
    [SerializeField] private float minLoadingTime = 2.0f;

    /// <summary>
    /// Configuration mapping for each game state, defining music state, transition settings, and input mode.
    /// </summary>
    private static readonly Dictionary<GameState, GameStateConfig> _gameStateConfigs = new Dictionary<GameState, GameStateConfig>
    {
        { GameState.Boot, new GameStateConfig("", false, InputMode.Disabled) },
        { GameState.Loading, new GameStateConfig("Silence", false, InputMode.UI) },
        { GameState.MainMenu, new GameStateConfig("MainMenu", true, InputMode.UI) },
        { GameState.Hub, new GameStateConfig("Hub", false, InputMode.UI) },
        { GameState.InLevel, new GameStateConfig("Exploration", false, InputMode.Gameplay) },
        { GameState.EndGame, new GameStateConfig("EndGame", false, InputMode.UI) } // EndGame music géré par WinLoseController
    };

    /// <summary>
    /// Initializes the GameManager singleton instance.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
    }

    /// <summary>
    /// Handles initial game startup, including debug mode detection and scene loading.
    /// </summary>
    private void Start()
    {
        // Check if the game was launched from debug mode
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

    /// <summary>
    /// Loads the hub scene with the specified or default hub data.
    /// </summary>
    /// <param name="specificHubData">Optional specific hub data to use instead of the default.</param>
    public void LoadHub(LevelData_SO specificHubData = null)
    {
        string sceneToLoad = "MainHub"; // Default hub scene name
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

    /// <summary>
    /// Loads a specific level using the provided level data.
    /// </summary>
    /// <param name="levelData">The level data containing scene information and configuration.</param>
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

    /// <summary>
    /// Loads the main menu scene using the configured main menu data.
    /// </summary>
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
    /// <summary>
    /// Finds a LevelData_SO asset in the Resources folder using its ID.
    /// </summary>
    /// <param name="levelID">The ID of the level to search for.</param>
    /// <returns>The LevelData_SO asset or null if not found.</returns>
    private LevelData_SO FindLevelDataByID(string levelID)
    {
        // Load all LevelData_SO from all subfolders of "Resources".
        // Make sure the path is correct. For example, "Data/Levels"
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
    /// <summary>
    /// Loads a scene by name and sets the target game state after loading.
    /// </summary>
    /// <param name="sceneName">The name of the scene to load.</param>
    /// <param name="targetStateAfterLoad">The game state to set after the scene is loaded.</param>
    private void LoadSceneByName(string sceneName, GameState targetStateAfterLoad)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[GameManager] Tentative de chargement d'une scène avec un nom vide.");
            return;
        }

        Debug.Log($"[GameManager] LoadSceneByName: Reçu demande pour '{sceneName}'. État actuel: {CurrentState}, Scène active actuelle: '{_currentActiveSceneName}'.");

        if (CurrentState == GameState.Loading)
        {
            Debug.LogWarning($"[GameManager] Un chargement est déjà en cours. Aucune action pour '{sceneName}'.");
            return;
        }

        if (CurrentState != GameState.Loading && _currentActiveSceneName == sceneName)
        {
            Debug.LogWarning($"[GameManager] La scène '{sceneName}' est déjà chargée et active. Passage à l'état {targetStateAfterLoad}.");
            SetState(targetStateAfterLoad);
            return;
        }

        if (_loadSceneCoroutine != null)
        {
            Debug.LogWarning($"[GameManager] Un chargement de scène est déjà en cours. Annulation du nouveau chargement pour '{sceneName}'.");
            return;
        }

        _loadSceneCoroutine = StartCoroutine(LoadSceneWithLoadingScreen(sceneName, targetStateAfterLoad));
    }

    /// <summary>
    /// Coroutine principale qui gère le chargement de scène avec écran de chargement asynchrone.
    /// Cette méthode orchestre tout le processus de transition entre scènes.
    /// </summary>
    private IEnumerator LoadSceneWithLoadingScreen(string sceneNameToLoad, GameState targetStateAfterLoad)
    {
        Debug.Log($"[GameManager] LoadSceneWithLoadingScreen: DÉBUT pour '{sceneNameToLoad}'.");
        
        // Étape 1 : Passer en état Loading
        SetState(GameState.Loading);

        // Étape 2 : Charger la scène de chargement
        Debug.Log($"[GameManager] Chargement de la scène de chargement '{loadingSceneName}'.");
        AsyncOperation loadingSceneLoad = SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Additive);
        
        if (loadingSceneLoad == null)
        {
            Debug.LogError($"[GameManager] Impossible de charger la scène de chargement '{loadingSceneName}'. Fallback vers chargement direct.");
            yield return StartCoroutine(LoadSceneRoutineFallback(sceneNameToLoad, targetStateAfterLoad));
            yield break;
        }

        yield return loadingSceneLoad;
        Debug.Log($"[GameManager] Scène de chargement '{loadingSceneName}' chargée.");

        // Étape 3 : Décharger l'ancienne scène si elle existe
        if (!string.IsNullOrEmpty(_currentActiveSceneName) && _currentActiveSceneName != sceneNameToLoad)
        {
            Scene sceneToUnload = SceneManager.GetSceneByName(_currentActiveSceneName);
            if (sceneToUnload.IsValid() && sceneToUnload.isLoaded)
            {
                Debug.Log($"[GameManager] Déchargement de la scène '{_currentActiveSceneName}'.");
                AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(_currentActiveSceneName);
                yield return asyncUnload;
                Debug.Log($"[GameManager] Scène '{_currentActiveSceneName}' déchargée.");
            }
        }

        // Étape 4 : Lancer le chargement de la nouvelle scène (LA LIGNE CLÉ)
        Debug.Log($"[GameManager] Début du chargement asynchrone de '{sceneNameToLoad}'.");
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneNameToLoad, LoadSceneMode.Additive);
        
        if (asyncLoad == null)
        {
            Debug.LogError($"[GameManager] Échec du chargement de '{sceneNameToLoad}'. Retour au menu principal.");
            yield return StartCoroutine(UnloadLoadingScreenAndFallback());
            yield break;
        }

        // ⭐ LIGNE MAGIQUE : Empêcher l'activation automatique de la scène
        asyncLoad.allowSceneActivation = false;

        // Étape 5 : Simuler la progression et respecter le délai minimum
        float startTime = Time.time;
        LoadingScreenManager loadingManager = FindFirstObjectByType<LoadingScreenManager>();

        // Mettre à jour la progression jusqu'à 90%
        while (asyncLoad.progress < 0.9f)
        {
            float progress = asyncLoad.progress;
            if (loadingManager != null)
            {
                loadingManager.UpdateProgress(progress);
            }
            Debug.Log($"[GameManager] Progression du chargement : {(progress * 100):F1}%");
            yield return null;
        }

        // La scène est chargée à 90%, mais pas encore activée
        Debug.Log("[GameManager] Scène chargée à 90%. Attente du délai minimum...");
        
        // Attendre le délai minimum restant
        float elapsedTime = Time.time - startTime;
        float remainingTime = minLoadingTime - elapsedTime;
        
        if (remainingTime > 0)
        {
            // Simuler la progression jusqu'à 100% pendant l'attente
            float simulationTime = 0f;
            while (simulationTime < remainingTime)
            {
                simulationTime += Time.deltaTime;
                float simulatedProgress = Mathf.Lerp(0.9f, 1.0f, simulationTime / remainingTime);
                
                if (loadingManager != null)
                {
                    loadingManager.UpdateProgress(simulatedProgress);
                }
                
                yield return null;
            }
        }

        // Étape 6 : Activer la nouvelle scène (Le moment magique!)
        Debug.Log($"[GameManager] Activation de la scène '{sceneNameToLoad}'.");
        asyncLoad.allowSceneActivation = true;

        // Attendre que l'activation soit complètement terminée
        yield return asyncLoad;

        // Étape 7 : Finaliser la transition
        Scene newActiveScene = SceneManager.GetSceneByName(sceneNameToLoad);
        if (newActiveScene.IsValid() && newActiveScene.isLoaded)
        {
            SceneManager.SetActiveScene(newActiveScene);
            _currentActiveSceneName = newActiveScene.name;
            Debug.Log($"[GameManager] Scène '{_currentActiveSceneName}' définie comme active.");
        }
        else
        {
            Debug.LogError($"[GameManager] Impossible de définir '{sceneNameToLoad}' comme scène active.");
        }

        // Étape 8 : Nettoyage - Décharger la scène de chargement
        Debug.Log($"[GameManager] Déchargement de la scène de chargement '{loadingSceneName}'.");
        AsyncOperation unloadLoadingScreen = SceneManager.UnloadSceneAsync(loadingSceneName);
        yield return unloadLoadingScreen;

        // Étape 9 : Mettre à jour l'état final
        yield return new WaitForSeconds(0.1f); // Petit délai pour l'initialisation
        Debug.Log($"[GameManager] Transition terminée. Passage à l'état {targetStateAfterLoad}.");
        SetState(targetStateAfterLoad);
        
        _loadSceneCoroutine = null;
    }

    /// <summary>
    /// Méthode de fallback en cas d'échec du chargement de l'écran de chargement
    /// </summary>
    private IEnumerator LoadSceneRoutineFallback(string sceneNameToLoad, GameState targetStateAfterLoad)
    {
        Debug.Log($"[GameManager] LoadSceneRoutineFallback: Chargement direct de '{sceneNameToLoad}'.");

        // Décharger l'ancienne scène si elle existe
        if (!string.IsNullOrEmpty(_currentActiveSceneName) && _currentActiveSceneName != sceneNameToLoad)
        {
            Scene sceneToUnload = SceneManager.GetSceneByName(_currentActiveSceneName);
            if (sceneToUnload.IsValid() && sceneToUnload.isLoaded)
            {
                AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(_currentActiveSceneName);
                yield return asyncUnload;
            }
        }

        // Charger la nouvelle scène
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneNameToLoad, LoadSceneMode.Additive);
        if (asyncLoad != null)
        {
            yield return asyncLoad;
            
            Scene newActiveScene = SceneManager.GetSceneByName(sceneNameToLoad);
            if (newActiveScene.IsValid() && newActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(newActiveScene);
                _currentActiveSceneName = newActiveScene.name;
            }
        }

        yield return new WaitForSeconds(0.1f);
        SetState(targetStateAfterLoad);
        _loadSceneCoroutine = null;
    }

    /// <summary>
    /// Décharge l'écran de chargement et retourne au menu principal en cas d'erreur
    /// </summary>
    private IEnumerator UnloadLoadingScreenAndFallback()
    {
        AsyncOperation unloadLoadingScreen = SceneManager.UnloadSceneAsync(loadingSceneName);
        yield return unloadLoadingScreen;
        
        SetState(GameState.MainMenu);
        _currentActiveSceneName = (mainMenuSceneData != null && !string.IsNullOrEmpty(mainMenuSceneData.SceneName)) 
            ? mainMenuSceneData.SceneName : "MainMenu";
        _loadSceneCoroutine = null;
    }

    // Garder l'ancienne méthode pour compatibilité, mais simplifiée
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

    /// <summary>
    /// Sets the current game state and handles all associated state transitions including music, input, and system resets.
    /// </summary>
    /// <param name="newState">The new game state to set.</param>
    public void SetState(GameState newState)
    {
        if (CurrentState == newState && newState != GameState.Boot && newState != GameState.Loading) // Allow transition from Boot/Loading to the same final state
        {
             Debug.Log($"[GameManager] SetState: État '{newState}' demandé, mais déjà actif. La logique d'état sera quand même exécutée pour assurer la cohérence.");
            // Don't return here to allow WinLoseController to reset if necessary
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
        // Gérer les configurations d'état (musique et input)
        if (_gameStateConfigs.TryGetValue(newState, out GameStateConfig config))
        {
            // Gérer la musique
            if (MusicManager.Instance != null)
            {
                if (!string.IsNullOrEmpty(config.musicState))
                {
                    MusicManager.Instance.SetMusicState(config.musicState, config.immediateTransition);
                    Debug.Log($"[GameManager] Musique changée vers '{config.musicState}' (transition immédiate: {config.immediateTransition})");
                }
            }
            else
            {
                Debug.LogWarning("[GameManager] MusicManager.Instance est null. Impossible de changer l'état musical.");
            }

            // Gérer les Action Maps d'Input
            if (InputManager.Instance != null)
            {
                switch (config.inputMode)
                {
                    case InputMode.UI:
                        InputManager.Instance.UIActions.Enable();
                        InputManager.Instance.GameplayActions.Disable();
                        Debug.Log("[GameManager] Action Maps: UI activée, Gameplay désactivée");
                        break;
                    
                    case InputMode.Gameplay:
                        InputManager.Instance.GameplayActions.Enable();
                        InputManager.Instance.UIActions.Disable();
                        Debug.Log("[GameManager] Action Maps: Gameplay activée, UI désactivée");
                        break;
                    
                    case InputMode.Disabled:
                        InputManager.Instance.UIActions.Disable();
                        InputManager.Instance.GameplayActions.Disable();
                        Debug.Log("[GameManager] Action Maps: Toutes désactivées");
                        break;
                }
            }
            else
            {
                Debug.LogWarning("[GameManager] InputManager.Instance est null. Impossible de changer les Action Maps.");
            }
        }
        else
        {
            Debug.LogWarning($"[GameManager] Aucune configuration trouvée pour GameState: {newState}");
        }
    }
}
