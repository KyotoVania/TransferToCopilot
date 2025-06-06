using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

public enum GameState { Boot, MainMenu, Hub, Loading, InLevel }

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
        string sceneToLoad = "MainMenu"; // Default fallback
        if (mainMenuSceneData != null && !string.IsNullOrEmpty(mainMenuSceneData.SceneName))
        {
            sceneToLoad = mainMenuSceneData.SceneName;
        }
        else
        {
            Debug.LogWarning("[GameManager] MainMenu LevelData_SO non assigné ou SceneName vide. Chargement de 'MainMenu' par défaut.");
        }
        Debug.Log($"[GameManager] Start: Chargement initial de la scène '{sceneToLoad}'.");
        LoadSceneByName(sceneToLoad, GameState.MainMenu);
    }

    public void LoadHub(LevelData_SO specificHubData = null)
    {
        string sceneToLoad = "MainHub"; // Default fallback
        LevelData_SO dataToUse = specificHubData ?? hubSceneData;

        if (dataToUse != null && !string.IsNullOrEmpty(dataToUse.SceneName))
        {
            sceneToLoad = dataToUse.SceneName;
        }
        else
        {
            Debug.LogWarning("[GameManager] Hub LevelData_SO non assigné ou SceneName vide. Chargement de 'MainHub' par défaut.");
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
        CurrentLevelToLoad = levelData; // Stocker l'info
        LoadSceneByName(levelData.SceneName, GameState.InLevel);
    }

    public void LoadMainMenu()
    {
        string sceneToLoad = "MainMenu"; // Default fallback
        if (mainMenuSceneData != null && !string.IsNullOrEmpty(mainMenuSceneData.SceneName))
        {
            sceneToLoad = mainMenuSceneData.SceneName;
        }
        else
        {
            Debug.LogWarning("[GameManager] MainMenu LevelData_SO non assigné ou SceneName vide. Chargement de 'MainMenu' par défaut.");
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

        // Décharger l'ancienne scène active
        if (!string.IsNullOrEmpty(_currentActiveSceneName))
        {
            Scene sceneToUnload = SceneManager.GetSceneByName(_currentActiveSceneName);
            if (sceneToUnload.IsValid() && sceneToUnload.isLoaded)
            {
                Debug.Log($"[GameManager] LoadSceneRoutine: Déchargement de la scène '{_currentActiveSceneName}'.");
                AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(_currentActiveSceneName);
                while (asyncUnload != null && !asyncUnload.isDone)
                {
                    yield return null;
                }
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

        // Charger la nouvelle scène
        Debug.Log($"[GameManager] LoadSceneRoutine: Chargement additif de la scène '{sceneNameToLoad}'.");
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneNameToLoad, LoadSceneMode.Additive);
        if (asyncLoad == null)
        {
            Debug.LogError($"[GameManager] LoadSceneRoutine: Échec du démarrage du chargement pour la scène '{sceneNameToLoad}'. Vérifiez qu'elle est dans les Build Settings.");
            SetState(GameState.MainMenu); // Retour à un état sûr
            // Tenter de charger MainMenu si le chargement de sceneNameToLoad a échoué
            string fallbackMainMenu = (mainMenuSceneData != null && !string.IsNullOrEmpty(mainMenuSceneData.SceneName)) ? mainMenuSceneData.SceneName : "MainMenu";
             _currentActiveSceneName = fallbackMainMenu; // Suppose que MainMenu existe et sera chargé
            _loadSceneCoroutine = null;
            yield break;
        }

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
        Debug.Log($"[GameManager] LoadSceneRoutine: Scène '{sceneNameToLoad}' chargée additivement.");

        // Définir la nouvelle scène comme active
        Scene newActiveScene = SceneManager.GetSceneByName(sceneNameToLoad);
        Debug.Log($"[GameManager] LoadSceneRoutine: Tentative de définition de la scène active. Nom recherché: '{sceneNameToLoad}'. Scène trouvée: IsValid={newActiveScene.IsValid()}, IsLoaded={newActiveScene.isLoaded}, Name='{newActiveScene.name}'.");

        if (newActiveScene.IsValid() && newActiveScene.isLoaded)
        {
            SceneManager.SetActiveScene(newActiveScene);
            _currentActiveSceneName = newActiveScene.name; // Utiliser newActiveScene.name pour être sûr du nom exact.
            Debug.Log($"[GameManager] LoadSceneRoutine: Scène '{newActiveScene.name}' définie comme active. _currentActiveSceneName MIS À JOUR en: '{_currentActiveSceneName}'.");
        }
        else
        {
            Debug.LogError($"[GameManager] LoadSceneRoutine: La scène '{sceneNameToLoad}' (recherchée) n'a pas pu être définie comme active. Scène trouvée: '{newActiveScene.name}', IsValid: {newActiveScene.IsValid()}, IsLoaded: {newActiveScene.isLoaded}. _currentActiveSceneName RESTE: '{_currentActiveSceneName}'.");
            SetState(GameState.MainMenu); // Fallback
            string fallbackMainMenu = (mainMenuSceneData != null && !string.IsNullOrEmpty(mainMenuSceneData.SceneName)) ? mainMenuSceneData.SceneName : "MainMenu";
            _currentActiveSceneName = fallbackMainMenu;
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
        if (CurrentState == newState) return;

        GameState previousState = CurrentState;
        CurrentState = newState;
        Debug.Log($"[GameManager] SetState: État changé de '{previousState}' vers '{newState}'.");
        OnGameStateChanged?.Invoke(newState);

        if (MusicManager.Instance != null)
        {
            string musicStateToSet = "";
            bool immediateTransition = false; // Par défaut, transitions douces gérées par Wwise

            switch (newState)
            {
                case GameState.Boot:
                case GameState.Loading:
                    // Optionnel: Arrêter la musique ou mettre un état "Silence" / "Loading"
                    // Pour l'instant, on ne change pas l'état musical, ou on pourrait l'arrêter.
                    // MusicManager.Instance.StopMusic(); // Si vous implémentez une telle méthode
                    musicStateToSet = "Silence"; // Exemple, si vous avez un état Wwise pour ça
                    break;
                case GameState.MainMenu:
                    // TODO: Définir quel état musical pour MainMenu. Ex: "Exploration" ou "MenuMusic"
                    musicStateToSet = "Silence"; // Changé de "Exploration" à "Silence"
                    immediateTransition = true; // Souvent, on veut que la musique du menu démarre vite
                    break;
                case GameState.Hub:
                    // TODO: Définir quel état musical pour Hub. Ex: "Exploration" ou "HubMusic"
                    musicStateToSet = "Silence"; // Ou un état "Hub"
                    break;
                case GameState.InLevel:
                    // Lorsque l'on entre dans un niveau, on commence généralement par l'exploration.
                    // La logique interne au niveau (non gérée ici) changera ensuite vers "Combat" ou "Boss".
                    musicStateToSet = "Exploration";
                    break;
                default:
                    Debug.LogWarning($"[GameManager] Aucun état musical Wwise défini pour GameState: {newState}");
                    break;
            }

            if (!string.IsNullOrEmpty(musicStateToSet))
            {
                // Le booléen 'immediate' dans SetMusicState est maintenant moins critique
                // car Wwise gère les transitions du Music Switch Container.
                // Vous pouvez le passer à 'false' pour vous fier aux réglages Wwise par défaut,
                // ou 'true' si vous avez une logique spécifique dans MusicManager qui l'utilise.
                MusicManager.Instance.SetMusicState(musicStateToSet, immediateTransition);
            }
        }
        else
        {
            Debug.LogWarning("[GameManager] MusicManager.Instance est null. Impossible de changer l'état musical.");
        }
    }
}