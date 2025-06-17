using UnityEngine;
using UnityEngine.UI;
using Unity.Behavior.GraphFramework;
using ScriptableObjects;
using System.Linq;
using System.Collections.Generic;


public class WinLoseController : MonoBehaviour
{
  
    public static WinLoseController Instance { get; private set; }

    [Header("Débogage")]
    [SerializeField] private KeyCode winDebugKey = KeyCode.F10;
    [SerializeField] private KeyCode loseDebugKey = KeyCode.F11;

    [Header("Références UI (Noms pour Recherche si Non-Persistantes)")]
    [Tooltip("Nom du GameObject pour le panel d'overlay de fin de partie.")]
    [SerializeField] private string name_gameOverOverlayPanel = "WinPanel";
    [SerializeField] private string name_winBannerObject = "WinBanner";
    [SerializeField] private string name_loseBannerObject = "LooseBanner";
    [SerializeField] private string name_inGameUiCanvas = "User Interface";
    [SerializeField] private string name_lobbyBoardObject = "LobbyBoard";
    [SerializeField] private string name_lobbyBoardSleeveObject = "LobbyStick";
    [SerializeField] private string name_nextLevelBoardObject = "NextBoard";
    [SerializeField] private string name_nextLevelBoardSleeveObject = "NextStick";

    private GameObject currentGameOverOverlayPanel;
    private GameObject currentWinBannerObject;
    private GameObject currentLoseBannerObject;
    private GameObject currentInGameUiCanvas;
    private GameObject currentLobbyBoardObject;
    private GameObject currentLobbyBoardSleeveObject;
    private GameObject currentNextLevelBoardObject;
    private GameObject currentNextLevelBoardSleeveObject;

    [Header("Dépendances")]
    [SerializeField] private RhythmGameCameraController gameCameraController;

    public static bool IsGameOverScreenActive { get; private set; } = false;
    private bool isGameOverInstance = false;
    public bool IsGameOver => isGameOverInstance;

    private System.Collections.Generic.List<GameObject> _deactivatedAllyUnits = new System.Collections.Generic.List<GameObject>();
    private System.Collections.Generic.List<GameObject> _deactivatedEnemyUnits = new System.Collections.Generic.List<GameObject>();

    private void Awake() // Anciennement : protected override void Awake()
    {
        // Logique du singleton de scène
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[WinLoseController] Une instance existe déjà. Destruction du duplicata {gameObject.name}.");
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
        
        // La recherche de la caméra peut rester ici ou dans Start
        // FindAndValidateCameraControllerReference(true); 
    }

    private void Start()
    {
        // S'abonner aux événements ici, car l'instance est maintenant liée à la scène
    }

    private void FindAndValidateCameraControllerReference(bool isInitialAttempt = false) {
        bool needsToFindNewReference = false;
        if (gameCameraController == null) {
            needsToFindNewReference = true;
            if (isInitialAttempt) Debug.LogWarning($"[{gameObject.name}] gameCameraController était null à l'initialisation. Tentative via Camera.main.", this);
        } else if (gameCameraController.gameObject == null || !gameCameraController.gameObject.scene.isLoaded || !gameCameraController.gameObject.activeInHierarchy) {
            Debug.LogWarning($"[{gameObject.name}] Référence 'gameCameraController' existante semble obsolète (GO null, scène non chargée ou GO inactif : {gameCameraController.gameObject?.name}). Tentative de re-synchronisation.", this);
            gameCameraController = null;
            needsToFindNewReference = true;
        }

        if (needsToFindNewReference) {
            if (Camera.main != null) {
                gameCameraController = Camera.main.GetComponent<RhythmGameCameraController>();
                if (gameCameraController != null) Debug.Log($"[{gameObject.name}] RhythmGameCameraController TROUVÉ sur Camera.main : {Camera.main.gameObject.name}", this);
                else Debug.LogError($"[{gameObject.name}] Camera.main ({Camera.main.gameObject.name}) existe mais N'A PAS de RhythmGameCameraController !", this);
            } else Debug.LogError($"[{gameObject.name}] Camera.main est null ! Impossible de trouver RhythmGameCameraController.", this);
        }

        if (gameCameraController == null && !isInitialAttempt) Debug.LogError($"[{gameObject.name}] ÉCHEC recherche RhythmGameCameraController valide pour une action en cours !", this);
    }

    private bool FindAndUpdateUIGameObjectReference(ref GameObject currentReferenceField, string objectName, string contextForLogs) {
        // On ne vérifie pas si la référence existante est valide ici, car si la scène a changé,
        // même une référence non-nulle peut pointer vers un objet détruit. On cherche toujours.
        GameObject foundObj = GameObject.Find(objectName); // GameObject.Find ne trouve QUE les objets ACTIFS.
        if (foundObj != null) {
            currentReferenceField = foundObj;
            Debug.Log($"[{gameObject.name} FindAndUpdateUIGameObjectReference] '{objectName}' TROUVÉ pour {contextForLogs}: {currentReferenceField.name}", this);
            return true;
        } else {
            Debug.LogWarning($"[{gameObject.name} FindAndUpdateUIGameObjectReference] GameObject '{objectName}' NON TROUVÉ (ou inactif) pour {contextForLogs}. La référence sera null.", this);
            currentReferenceField = null;
            return false;
        }
    }

    private bool FindAndUpdateBuildingReference(ref Building buildingReferenceField, string buildingGameObjectName, string contextForLogs)
    {
        // Idem, on cherche toujours car la scène a pu changer.
        if (string.IsNullOrEmpty(buildingGameObjectName))
        {
            //Debug.LogWarning($"[{gameObject.name} FindAndUpdateBuildingReference] Nom de recherche vide pour {contextForLogs}. Référence mise à null.", this);
            buildingReferenceField = null;
            return false;
        }

        GameObject foundGO = GameObject.Find(buildingGameObjectName); // Ne trouve que les objets actifs
        if (foundGO != null)
        {
            Building buildingComponent = foundGO.GetComponent<Building>();
            if (buildingComponent != null)
            {
                buildingReferenceField = buildingComponent;
                Debug.Log($"[{gameObject.name} FindAndUpdateBuildingReference] '{buildingGameObjectName}' trouvé et son composant Building assigné à '{contextForLogs}': {buildingReferenceField.name}", this);
                return true;
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name} FindAndUpdateBuildingReference] GameObject '{buildingGameObjectName}' TROUVÉ pour '{contextForLogs}' mais n'a pas de composant 'Building'.", this);
                buildingReferenceField = null;
                return false;
            }
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name} FindAndUpdateBuildingReference] GameObject '{buildingGameObjectName}' NON TROUVÉ (ou inactif) pour '{contextForLogs}'.", this);
            buildingReferenceField = null;
            return false;
        }
    }

    private void SetInitialScreenState()
    {
        Debug.Log($"[{gameObject.name} WinLoseController.SetInitialScreenState] Début de la réinitialisation. isGameOverInstance: {isGameOverInstance}, IsGameOverScreenActive: {IsGameOverScreenActive}.", this);

        // Retrouver TOUTES les références pour la scène actuelle.
        // Important: Les objets doivent être ACTIFS dans la scène pour être trouvés par GameObject.Find().
        // S'ils sont désactivés par défaut, cette logique échouera.
        // L'alternative est de les rendre enfants d'un objet parent spécifique et de chercher via ce parent.
        FindAndValidateCameraControllerReference(); // S'assurer d'avoir la bonne caméra

        FindAndUpdateUIGameObjectReference(ref currentGameOverOverlayPanel, name_gameOverOverlayPanel, "Overlay");
        FindAndUpdateUIGameObjectReference(ref currentWinBannerObject, name_winBannerObject, "Win Banner");
        FindAndUpdateUIGameObjectReference(ref currentLoseBannerObject, name_loseBannerObject, "Lose Banner");
        FindAndUpdateUIGameObjectReference(ref currentInGameUiCanvas, name_inGameUiCanvas, "In-Game UI");
        FindAndUpdateUIGameObjectReference(ref currentLobbyBoardObject, name_lobbyBoardObject, "Lobby Board");
        FindAndUpdateUIGameObjectReference(ref currentLobbyBoardSleeveObject, name_lobbyBoardSleeveObject, "Lobby Sleeve");
        FindAndUpdateUIGameObjectReference(ref currentNextLevelBoardObject, name_nextLevelBoardObject, "NextLvl Board");
        FindAndUpdateUIGameObjectReference(ref currentNextLevelBoardSleeveObject, name_nextLevelBoardSleeveObject, "NextLvl Sleeve");

        //FindAndUpdateBuildingReference(ref targetToDestroyForWin, name_targetToDestroyForWin, "Cible Victoire");
        //FindAndUpdateBuildingReference(ref targetToProtectForLose, name_targetToProtectForLose, "Cible Défaite");

        // Maintenant, (dés)activer en utilisant les références potentiellement mises à jour
        if (currentGameOverOverlayPanel != null) currentGameOverOverlayPanel.SetActive(false); else Debug.LogWarning($"[{gameObject.name}] Overlay Panel ('{name_gameOverOverlayPanel}') non trouvé/assigné pour SetActive(false).");
        if (currentWinBannerObject != null) currentWinBannerObject.SetActive(false);
        if (currentLoseBannerObject != null) currentLoseBannerObject.SetActive(false);
        if (currentLobbyBoardObject != null) currentLobbyBoardObject.SetActive(false);
        if (currentLobbyBoardSleeveObject != null) currentLobbyBoardSleeveObject.SetActive(false);
        if (currentNextLevelBoardObject != null) currentNextLevelBoardObject.SetActive(false);
        if (currentNextLevelBoardSleeveObject != null) currentNextLevelBoardSleeveObject.SetActive(false);

        if (currentInGameUiCanvas != null) currentInGameUiCanvas.SetActive(true); else Debug.LogWarning($"[{gameObject.name}] In-Game UI Canvas ('{name_inGameUiCanvas}') non trouvé/assigné pour SetActive(true).");

        isGameOverInstance = false;
        IsGameOverScreenActive = false;
        // Time.timeScale = 1f; // Le temps n'est plus mis à 0, donc pas besoin de le remettre à 1 explicitement ici.

        _deactivatedAllyUnits.Clear();
        _deactivatedEnemyUnits.Clear();
        Debug.Log($"[{gameObject.name} WinLoseController.SetInitialScreenState] Terminé. isGameOverInstance: {isGameOverInstance}, IsGameOverScreenActive: {IsGameOverScreenActive}.", this);
    }

    private void Update()
    {
        if (isGameOverInstance) return;
        if (Input.GetKeyDown(winDebugKey)) TriggerWinCondition();
        if (Input.GetKeyDown(loseDebugKey)) TriggerLoseCondition();
    }
/*
    private void HandleBuildingDestroyed(Building destroyedBuilding)
    {
        if (isGameOverInstance || destroyedBuilding == null) return;
        // Debug.Log($"[{gameObject.name}] Bâtiment détruit: {destroyedBuilding.name}. Vérification...", this);
        bool winMet = (targetToDestroyForWin != null && destroyedBuilding == targetToDestroyForWin);
        bool loseMet = (targetToProtectForLose != null && destroyedBuilding == targetToProtectForLose);

        if (winMet) { Debug.Log($"[WinLoseController] WIN par destruction de {destroyedBuilding.name}", this); TriggerWinCondition(); }
        else if (loseMet) { Debug.Log($"[WinLoseController] LOSE par destruction de {destroyedBuilding.name}", this); TriggerLoseCondition(); }
    }*/

    public void TriggerWinCondition()
    { 
        if (isGameOverInstance) { Debug.LogWarning($"[WinLoseController] TriggerWinCondition appelé mais isGameOverInstance est déjà {isGameOverInstance}.", this); return; }
        isGameOverInstance = true; 
        IsGameOverScreenActive = true;
        Debug.Log("[WinLoseController] CONDITIONS DE VICTOIRE REMPLIES !", this);
        if (GameManager.Instance != null && PlayerDataManager.Instance != null && TeamManager.Instance != null)
        {
            LevelData_SO completedLevel = GameManager.CurrentLevelToLoad;
            if (completedLevel != null)
            {
                // 1. Marquer le niveau comme complété (ici avec 1 étoile par défaut)
                PlayerDataManager.Instance.CompleteLevel(completedLevel.LevelID, 1);
                Debug.Log($"[WinLoseController] Niveau '{completedLevel.LevelID}' marqué comme complété.");

                // 2. Donner l'XP à chaque personnage de l'équipe active
                if (completedLevel.ExperienceReward > 0)
                {
                    var activeTeam = TeamManager.Instance.ActiveTeam;
                    foreach (var character in activeTeam)
                    {
                        if (character != null)
                        {
                            PlayerDataManager.Instance.AddXPToCharacter(character.CharacterID, completedLevel.ExperienceReward);
                        }
                    }
                    Debug.Log($"[WinLoseController] {completedLevel.ExperienceReward} XP accordés à chaque membre de l'équipe.");
                }

                // 3. Donner l'or
                if (completedLevel.CurrencyReward > 0)
                {
                    PlayerDataManager.Instance.AddCurrency(completedLevel.CurrencyReward);
                }

                // 4. Donner les objets en récompense
                if (completedLevel.ItemRewards != null && completedLevel.ItemRewards.Count > 0)
                {
                    List<string> itemIDsToUnlock = completedLevel.ItemRewards.Select(item => item.EquipmentID).ToList();
                    PlayerDataManager.Instance.UnlockMultipleEquipment(itemIDsToUnlock);
                    Debug.Log($"[WinLoseController] {itemIDsToUnlock.Count} item(s) débloqué(s).");
                }
                if (completedLevel.CharacterUnlockReward != null)
                {
                    PlayerDataManager.Instance.UnlockCharacter(completedLevel.CharacterUnlockReward.CharacterID);
                    Debug.Log($"[WinLoseController] Personnage débloqué : {completedLevel.CharacterUnlockReward.DisplayName} !");
                }
                // 5. Sauvegarder toutes les données du joueur
                PlayerDataManager.Instance.SaveData();
            }
            else
            {
                Debug.LogWarning("[WinLoseController] GameManager.CurrentLevelToLoad est null. Impossible d'accorder les récompenses.");
            }
        }
        else
        {
            Debug.LogError("[WinLoseController] Un manager requis est manquant (GameManager, PlayerDataManager, ou TeamManager). Impossible d'accorder les récompenses.");
        }
        Debug.Log("[WinLoseController] CONDITIONS DE VICTOIRE REMPLIES !", this);
        DeactivateAllUnitGameObjects();
        if (currentWinBannerObject != null) currentWinBannerObject.SetActive(true); else Debug.LogWarning($"[TriggerWin] currentWinBannerObject est null (nom recherché: {name_winBannerObject})");
        if (currentLobbyBoardObject != null) currentLobbyBoardObject.SetActive(true); else Debug.LogWarning($"[TriggerWin] currentLobbyBoardObject est null (nom recherché: {name_lobbyBoardObject})");
        if (currentLobbyBoardSleeveObject != null) currentLobbyBoardSleeveObject.SetActive(true);
        if (currentNextLevelBoardObject != null) currentNextLevelBoardObject.SetActive(true);
        if (currentNextLevelBoardSleeveObject != null) currentNextLevelBoardSleeveObject.SetActive(true);
        
        ActivateGameOverSequence("VICTOIRE !");
    }

    public void TriggerLoseCondition()
    {
        if (isGameOverInstance) { Debug.LogWarning($"[WinLoseController] TriggerLoseCondition appelé mais isGameOverInstance est déjà {isGameOverInstance}.", this); return; }
        isGameOverInstance = true; IsGameOverScreenActive = true;
        Debug.Log("[WinLoseController] CONDITIONS DE DÉFAITE REMPLIES !", this);
        DeactivateAllUnitGameObjects();
        if (currentLoseBannerObject != null) currentLoseBannerObject.SetActive(true); else Debug.LogWarning($"[TriggerLose] currentLoseBannerObject est null (nom recherché: {name_loseBannerObject})");
        if (currentLobbyBoardObject != null) currentLobbyBoardObject.SetActive(true); else Debug.LogWarning($"[TriggerLose] currentLobbyBoardObject est null (nom recherché: {name_lobbyBoardObject})");
        if (currentLobbyBoardSleeveObject != null) currentLobbyBoardSleeveObject.SetActive(true);
        if (currentNextLevelBoardObject != null) currentNextLevelBoardObject.SetActive(false);
        if (currentNextLevelBoardSleeveObject != null) currentNextLevelBoardSleeveObject.SetActive(false);
        ActivateGameOverSequence("DÉFAITE !");
    }

    private void ActivateGameOverSequence(string message)
    {
        Debug.Log($"[WinLoseController] Activation de la séquence de fin de partie : {message}", this);
        FindAndValidateCameraControllerReference();
        if (MusicManager.Instance != null) MusicManager.Instance.SetMusicState("EndGame");
        if (currentInGameUiCanvas != null) currentInGameUiCanvas.SetActive(false); else Debug.LogWarning($"[ActivateEndSeq] currentInGameUiCanvas est null (nom recherché: {name_inGameUiCanvas})");
        if (gameCameraController != null) gameCameraController.ZoomOutToMaxAndLockZoomOnly(true, 1.5f);
        if (currentGameOverOverlayPanel != null) currentGameOverOverlayPanel.SetActive(true); else Debug.LogWarning($"[ActivateEndSeq] currentGameOverOverlayPanel est null (nom recherché: {name_gameOverOverlayPanel})");
    }

    private void DeactivateAllUnitGameObjects() { /* ... comme avant ... */
        _deactivatedAllyUnits.Clear(); _deactivatedEnemyUnits.Clear(); int allies=0, enemies=0;
        foreach (GameObject unitGO in GameObject.FindGameObjectsWithTag("AllyUnit")) if (unitGO.activeSelf) { unitGO.SetActive(false); _deactivatedAllyUnits.Add(unitGO); allies++; }
        foreach (GameObject unitGO in GameObject.FindGameObjectsWithTag("Enemy")) if (unitGO.activeSelf) { unitGO.SetActive(false); _deactivatedEnemyUnits.Add(unitGO); enemies++; }
        Debug.Log($"[WinLoseController] Unités désactivées: {allies} alliés, {enemies} ennemis.", this);
    }
    private void ReactivateAllUnitGameObjects() { /* ... comme avant ... */
        int allies=0, enemies=0;
        foreach (GameObject unitGO in _deactivatedAllyUnits) if (unitGO != null) { unitGO.SetActive(true); allies++; } _deactivatedAllyUnits.Clear();
        foreach (GameObject unitGO in _deactivatedEnemyUnits) if (unitGO != null) { unitGO.SetActive(true); enemies++; } _deactivatedEnemyUnits.Clear();
        Debug.Log($"[WinLoseController] Unités réactivées: {allies} alliés, {enemies} ennemis.", this);
    }

    public void ResetGameConditionState()
    {
        Debug.Log($"[WinLoseController] ResetGameConditionState APPELÉ. isGameOverInstance AVANT: {isGameOverInstance}, IsGameOverScreenActive AVANT: {IsGameOverScreenActive}.", this);
        FindAndValidateCameraControllerReference(); // Assurer une réf. caméra fraîche avant toute action
        SetInitialScreenState(); // Recherche les UI, les cache, et reset les flags.
        ReactivateAllUnitGameObjects();
        if (gameCameraController != null) { gameCameraController.UnlockZoomOnly(); gameCameraController.ResetCameraToInitialState(); }
        Debug.Log($"[WinLoseController] FIN ResetGameConditionState. isGameOverInstance APRÈS: {isGameOverInstance}, IsGameOverScreenActive APRÈS: {IsGameOverScreenActive}.", this);
    }

    private void OnDestroy()
    {
        Debug.Log($"[{gameObject.name}] WinLoseController.OnDestroy: Instance nettoyée.", this);
        if (Instance == this)
        {
            Instance = null;
        }
        IsGameOverScreenActive = false;
        isGameOverInstance = false;
        // Building.OnBuildingDestroyed -= HandleBuildingDestroyed; // Désabonnement si nécessaire
    }
}