using UnityEngine;
using UnityEngine.UI;
using Unity.Behavior.GraphFramework;
using ScriptableObjects;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class WinLoseController : MonoBehaviour
{
    public static WinLoseController Instance { get; private set; }

    [Header("Débogage")]
    [SerializeField] private KeyCode winDebugKey = KeyCode.F10;
    [SerializeField] private KeyCode loseDebugKey = KeyCode.F11;

    [Header("Références UI (Assignées via l'Inspecteur)")]
    [Tooltip("Le panel qui s'affiche en fin de partie (contient les bannières Win/Lose).")]
    [SerializeField] private GameObject gameOverOverlayPanel;
    [Tooltip("L'objet spécifique à la bannière de victoire.")]
    [SerializeField] private GameObject winBannerObject;
    [Tooltip("L'objet spécifique à la bannière de défaite.")]
    [SerializeField] private GameObject loseBannerObject;
    [Tooltip("Le Canvas racine de l'interface en jeu (pour le désactiver).")]
    [SerializeField] private GameObject inGameUiCanvas;
    [Tooltip("Le panneau pour retourner au Hub/Lobby.")]
    [SerializeField] private GameObject lobbyBoardObject;
    [Tooltip("Le support du panneau Lobby.")]
    [SerializeField] private GameObject lobbyBoardSleeveObject;
    [Tooltip("Le panneau pour le niveau suivant.")]
    [SerializeField] private GameObject nextLevelBoardObject;
    [Tooltip("Le support du panneau pour le niveau suivant.")]
    [SerializeField] private GameObject nextLevelBoardSleeveObject;
    [Tooltip("Le conteneur global des éléments 3D de victoire/défaite (à masquer pendant les transitions).")]
    [SerializeField] private GameObject winScreenGlobalPositionner;

    [Header("Dépendances")]
    [SerializeField] private RhythmGameCameraController gameCameraController;

    public static bool IsGameOverScreenActive { get; private set; } = false;
    private bool isGameOverInstance = false;
    public bool IsGameOver => isGameOverInstance;

    [Header("UI Navigation")]
    [SerializeField] private GameObject defaultButtonOnWin; // Assign "Next Level" button
    [SerializeField] private GameObject defaultButtonOnLose; // Assign "Lobby" button

    private readonly List<GameObject> _deactivatedAllyUnits = new List<GameObject>();
    private readonly List<GameObject> _deactivatedEnemyUnits = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        SetInitialScreenState();
    }

    private void Start()
    {
        // Les abonnements aux événements sont gérés par GameManager ou d'autres managers au besoin.
        // L'état initial est défini par ResetGameConditionState, appelé par GameManager.
    }

    private void SetInitialScreenState()
    {
        Debug.Log($"[{gameObject.name}] Réinitialisation de l'état de l'écran de fin de partie.", this);

        // Désactiver tous les panneaux de fin de partie en utilisant les références directes
        if (gameOverOverlayPanel != null) gameOverOverlayPanel.SetActive(false);
        if (winBannerObject != null) winBannerObject.SetActive(false);
        if (loseBannerObject != null) loseBannerObject.SetActive(false);
        if (lobbyBoardObject != null) lobbyBoardObject.SetActive(false);
        if (lobbyBoardSleeveObject != null) lobbyBoardSleeveObject.SetActive(false);
        if (nextLevelBoardObject != null) nextLevelBoardObject.SetActive(false);
        if (nextLevelBoardSleeveObject != null) nextLevelBoardSleeveObject.SetActive(false);
        if (winScreenGlobalPositionner != null) winScreenGlobalPositionner.SetActive(false); // Masquer le conteneur global

        // Activer l'interface de jeu
        if (inGameUiCanvas != null) inGameUiCanvas.SetActive(true);

        isGameOverInstance = false;
        IsGameOverScreenActive = false;

        _deactivatedAllyUnits.Clear();
        _deactivatedEnemyUnits.Clear();
    }

    private void Update()
    {
        if (isGameOverInstance) return;
        if (Input.GetKeyDown(winDebugKey)) TriggerWinCondition();
        if (Input.GetKeyDown(loseDebugKey)) TriggerLoseCondition();
    }

    public void TriggerWinCondition()
    {
        if (isGameOverInstance) return;
        isGameOverInstance = true;
        IsGameOverScreenActive = true;

        Debug.Log("[WinLoseController] CONDITIONS DE VICTOIRE REMPLIES !", this);
        
        // NEW: Disable targeting system and unlock camera before game over sequence
        DisableTargetingSystemOnGameOver();
        
        HandleRewards();

        DeactivateAllUnitGameObjects();
        if (winScreenGlobalPositionner != null) winScreenGlobalPositionner.SetActive(true);
        if (winBannerObject != null) winBannerObject.SetActive(true);
        if (lobbyBoardObject != null) lobbyBoardObject.SetActive(true);
        if (lobbyBoardSleeveObject != null) lobbyBoardSleeveObject.SetActive(true);
        if (nextLevelBoardObject != null) nextLevelBoardObject.SetActive(true);
        if (nextLevelBoardSleeveObject != null) nextLevelBoardSleeveObject.SetActive(true);

        ActivateGameOverSequence("VICTOIRE !");
    }

    public void TriggerLoseCondition()
    {
        if (isGameOverInstance) return;
        isGameOverInstance = true;
        IsGameOverScreenActive = true;

        Debug.Log("[WinLoseController] CONDITIONS DE DÉFAITE REMPLIES !", this);

        // NEW: Disable targeting system and unlock camera before game over sequence
        DisableTargetingSystemOnGameOver();

        DeactivateAllUnitGameObjects();
        if (winScreenGlobalPositionner != null) winScreenGlobalPositionner.SetActive(true);

        if (loseBannerObject != null) loseBannerObject.SetActive(true);
        if (lobbyBoardObject != null) lobbyBoardObject.SetActive(true);
        if (lobbyBoardSleeveObject != null) lobbyBoardSleeveObject.SetActive(true);
        if (nextLevelBoardObject != null) nextLevelBoardObject.SetActive(false);
        if (nextLevelBoardSleeveObject != null) nextLevelBoardSleeveObject.SetActive(false);

        ActivateGameOverSequence("DÉFAITE !");
    }

    /// <summary>
    /// NEW: Properly disables the targeting system and unlocks camera when game ends
    /// </summary>
    private void DisableTargetingSystemOnGameOver()
    {
        // Disable targeting system in BannerController
        if (BannerController.Exists)
        {
            var bannerController = BannerController.Instance;
            // Force exit targeting mode by calling the private method through reflection or add a public method
            // For now, we'll use a simple approach by clearing the banner which should reset the state
            bannerController.ClearBanner();
            Debug.Log("[WinLoseController] BannerController targeting system disabled");
        }

        // Unlock camera completely
        if (gameCameraController != null)
        {
            gameCameraController.UnlockCamera(); // This unlocks targeting mode
            Debug.Log("[WinLoseController] Camera targeting unlocked");
        }
    }

    private void HandleRewards()
    {
        if (GameManager.Instance == null || PlayerDataManager.Instance == null || TeamManager.Instance == null)
        {
            Debug.LogError("[WinLoseController] Un manager requis est manquant. Impossible d'accorder les récompenses.");
            return;
        }

        LevelData_SO completedLevel = GameManager.CurrentLevelToLoad;
        if (completedLevel == null)
        {
            Debug.LogWarning("[WinLoseController] GameManager.CurrentLevelToLoad est null. Impossible d'accorder les récompenses.");
            return;
        }

        PlayerDataManager.Instance.CompleteLevel(completedLevel.LevelID, 1);
        if (completedLevel.ExperienceReward > 0)
        {
            var activeTeam = TeamManager.Instance.ActiveTeam;
            foreach (var character in activeTeam.Where(c => c != null))
            {
                PlayerDataManager.Instance.AddXPToCharacter(character.CharacterID, completedLevel.ExperienceReward);
            }
        }
        if (completedLevel.CurrencyReward > 0)
        {
            PlayerDataManager.Instance.AddCurrency(completedLevel.CurrencyReward);
        }
        if (completedLevel.ItemRewards != null && completedLevel.ItemRewards.Count > 0)
        {
            PlayerDataManager.Instance.UnlockMultipleEquipment(completedLevel.ItemRewards.Select(item => item.EquipmentID).ToList());
        }
        if (completedLevel.CharacterUnlockReward != null)
        {
            PlayerDataManager.Instance.UnlockCharacter(completedLevel.CharacterUnlockReward.CharacterID);
        }
        PlayerDataManager.Instance.SaveData();
    }

    private void ActivateGameOverSequence(string message)
    {
        Debug.Log($"[WinLoseController] Activation de la séquence de fin de partie : {message}", this);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameState.EndGame);
        }
        if (inGameUiCanvas != null) inGameUiCanvas.SetActive(false);
        if (gameCameraController != null) gameCameraController.ZoomOutToMaxAndLockZoomOnly(true, 1.5f);
        if (gameOverOverlayPanel != null) gameOverOverlayPanel.SetActive(true);
        StartCoroutine(SelectDefaultButtonDelayed(isGameOverInstance ? defaultButtonOnWin : defaultButtonOnLose));
    }

    private IEnumerator SelectDefaultButtonDelayed(GameObject buttonToSelect)
    {
        // Wait for animations to start
        yield return new WaitForSecondsRealtime(0.5f);
    
        if (buttonToSelect != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(buttonToSelect);
            Debug.Log($"[WinLoseController] Default button selected: {buttonToSelect.name}");
        }
    }


    private void DeactivateAllUnitGameObjects()
    {
        _deactivatedAllyUnits.Clear();
        _deactivatedEnemyUnits.Clear();
        foreach (GameObject unitGO in GameObject.FindGameObjectsWithTag("AllyUnit"))
        {
            if (unitGO.activeSelf)
            {
                unitGO.SetActive(false);
                _deactivatedAllyUnits.Add(unitGO);
            }
        }
        foreach (GameObject unitGO in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            if (unitGO.activeSelf)
            {
                unitGO.SetActive(false);
                _deactivatedEnemyUnits.Add(unitGO);
            }
        }
    }

    private void ReactivateAllUnitGameObjects()
    {
        foreach (GameObject unitGO in _deactivatedAllyUnits)
        {
            if (unitGO != null) unitGO.SetActive(true);
        }
        _deactivatedAllyUnits.Clear();

        foreach (GameObject unitGO in _deactivatedEnemyUnits)
        {
            if (unitGO != null) unitGO.SetActive(true);
        }
        _deactivatedEnemyUnits.Clear();
    }

    public void ResetGameConditionState()
    {
        Debug.Log($"[WinLoseController] ResetGameConditionState appelé.", this);
        SetInitialScreenState();
        ReactivateAllUnitGameObjects();
        if (gameCameraController != null)
        {
            gameCameraController.UnlockZoomOnly();
            gameCameraController.ResetCameraToInitialState();
        }
    }

    /// <summary>
    /// Masque immédiatement tous les éléments d'écran de fin de partie sans réinitialiser l'état du jeu.
    /// Utilisé pendant les transitions pour éviter l'affichage temporaire des éléments 3D.
    /// </summary>
    public void HideWinLoseElementsImmediately()
    {
        Debug.Log("[WinLoseController] Masquage immédiat de tous les éléments d'écran de fin.", this);
        
        if (gameOverOverlayPanel != null) gameOverOverlayPanel.SetActive(false);
        if (winBannerObject != null) winBannerObject.SetActive(false);
        if (loseBannerObject != null) loseBannerObject.SetActive(false);
        if (lobbyBoardObject != null) lobbyBoardObject.SetActive(false);
        if (lobbyBoardSleeveObject != null) lobbyBoardSleeveObject.SetActive(false);
        if (nextLevelBoardObject != null) nextLevelBoardObject.SetActive(false);
        if (nextLevelBoardSleeveObject != null) nextLevelBoardSleeveObject.SetActive(false);
        if (winScreenGlobalPositionner != null) winScreenGlobalPositionner.SetActive(false);
        
        IsGameOverScreenActive = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}