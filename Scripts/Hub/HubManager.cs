using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Events;
using System.Linq;
using ScriptableObjects;
using System.Collections;

public class HubManager : MonoBehaviour
{
    [System.Serializable]
    public class HubPointOfInterest
    {
        public string Name;
        public HubViewpoint Viewpoint;
        public GameObject UIPanel;
    }

    [Header("Navigation & Points d'Intérêt")]
    [SerializeField] private List<HubPointOfInterest> hubPointsOfInterest = new List<HubPointOfInterest>();

    [Header("Panels & Dépendances")]
    [SerializeField] private GameObject panelMainHub;
    
    // --- NOUVEAU ---
    [Header("Timing des Transitions")]
    [Tooltip("Petite pause en secondes sur la vue générale pour stabiliser la caméra avant la transition finale.")]
    [SerializeField] private float pivotPauseDuration = 0.1f;

    private HubViewpoint _currentViewpoint = HubViewpoint.General;
    private HubCameraManager _hubCameraManager;
    private bool _isTransitioning = false;
	[Header("Debug Options")]
    [Tooltip("Liste des SO d'équipement à donner au joueur via le bouton de débogage.")]
    [SerializeField] private List<EquipmentData_SO> testEquipmentToGive;

    void Start()
    {
        _hubCameraManager = HubCameraManager.Instance; // S'assurer que HubCameraManager utilise un Singleton local
        if (_hubCameraManager == null)
        {
            Debug.LogError("[HubManager] HubCameraManager.Instance non trouvé ! La navigation caméra ne fonctionnera pas.");
        }

        if (GameManager.Instance == null || PlayerDataManager.Instance == null /* ... etc */)
        {
            Debug.LogError("[HubManager] Managers globaux non trouvés!");
            enabled = false;
            return;
        }
        Debug.Log("[HubManager] Initialisé.");
        GameManager.Instance.SetState(GameState.Hub);


        _currentViewpoint = HubViewpoint.General;
        ShowCorrectPanelForCurrentView();
    }

    private void OnDestroy()
    {
        
    }

    
    void Update()
    {
        if (_isTransitioning) return;
        HandleKeyboardNavigation();
    }
    private void HandleKeyboardNavigation()
    {
        if (hubPointsOfInterest.Count == 0) return;

        HubViewpoint nextView = _currentViewpoint;
        HubViewpoint previousView = _currentViewpoint; // --- NOUVEAU ---

        switch (_currentViewpoint)
        {
            case HubViewpoint.General:
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                    nextView = HubViewpoint.LevelSelection;
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                    nextView = HubViewpoint.TeamManagement;
                break;

            case HubViewpoint.LevelSelection:
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                    nextView = HubViewpoint.General;
                break;

            case HubViewpoint.TeamManagement:
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                    nextView = HubViewpoint.General;
                break;
        }

        if (nextView != _currentViewpoint)
        {
            StartCoroutine(TransitionToView(nextView, previousView));
        }
        else if (_currentViewpoint != HubViewpoint.General && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
        {
            ShowCorrectPanelForCurrentView();
        }
    }

 	 private IEnumerator TransitionToView(HubViewpoint newView, HubViewpoint previousView)
    {
        _isTransitioning = true;
        HideAllSectionPanels();

        // ÉTAPE 1 : Si on n'est pas déjà sur la vue générale et qu'on ne va pas vers elle,
        // on y retourne d'abord comme point de pivot.
        if (previousView != HubViewpoint.General && newView != HubViewpoint.General)
        {
            Debug.Log($"[HubManager] Pivot: de {previousView} vers General.");
            // Demande la transition vers la vue générale et attend la fin
            yield return StartCoroutine(_hubCameraManager.TransitionTo(HubViewpoint.General));
            
            // Fait une petite pause pour que Cinemachine se stabilise
            if (pivotPauseDuration > 0)
            {
                yield return new WaitForSeconds(pivotPauseDuration);
            }
        }
        
        // ÉTAPE 2 : Maintenant, on lance la transition vers notre destination finale.
        Debug.Log($"[HubManager] Transition finale: vers {newView}.");
        _currentViewpoint = newView; // Mettre à jour l'état logique *avant* la transition finale
        yield return StartCoroutine(_hubCameraManager.TransitionTo(newView));

        _isTransitioning = false;
        
        // Affiche le panel de la vue générale si on y est, sinon ne rien afficher.
        if (_currentViewpoint == HubViewpoint.General)
        {
            ShowCorrectPanelForCurrentView();
        }
    }

     private void ShowCorrectPanelForCurrentView()
    {
        HideAllSectionPanels();

        if (_currentViewpoint == HubViewpoint.General)
        {
            if (panelMainHub != null) panelMainHub.SetActive(true);
        }
        else
        {
            var point = hubPointsOfInterest.FirstOrDefault(p => p.Viewpoint == _currentViewpoint);
            if (point != null && point.UIPanel != null)
            {
                point.UIPanel.SetActive(true);
            }
        }
    }

    private void HideAllSectionPanels()
    {
        if (panelMainHub != null) panelMainHub.SetActive(false);
        foreach (var point in hubPointsOfInterest)
        {
            point.UIPanel?.SetActive(false);
        }
    }
    
    public void GoToGeneralView()
    {
        if (_isTransitioning) return;
        // Pour le bouton retour, on ne passe pas par le pivot, on va juste à la vue générale.
        StartCoroutine(TransitionToView(HubViewpoint.General, _currentViewpoint));
    }
    

    
    public void StartLevel(LevelData_SO levelData)
    {
        if (levelData == null) { Debug.LogError("[HubManager] LevelData_SO est null."); return; }
        GameManager.Instance.LoadLevel(levelData);
    }

    public void GoBackToMainMenu()
    {	
        GameManager.Instance?.LoadMainMenu();
    }

 	public void OnDebugAddItemsClicked()
    {
        if (testEquipmentToGive == null || testEquipmentToGive.Count == 0)
        {
            Debug.LogWarning("[HubManager] La liste 'testEquipmentToGive' est vide. Aucun item à ajouter.");
            return;
        }

        if (PlayerDataManager.Instance != null)
        {
            // Extraire les IDs de la liste de ScriptableObjects
            List<string> idsToUnlock = testEquipmentToGive.Select(item => item.EquipmentID).ToList();
            
            // Appeler la nouvelle méthode du PlayerDataManager
            PlayerDataManager.Instance.UnlockMultipleEquipment(idsToUnlock);

            Debug.Log($"[HubManager] Tentative de déblocage de {idsToUnlock.Count} items de test.");
            
            // On peut ajouter un petit feedback visuel simple
            // par exemple en désactivant le bouton après usage.
            // (Logique plus avancée : rafraîchir l'UI de l'inventaire si elle est visible).
        }
        else
        {
            Debug.LogError("[HubManager] PlayerDataManager.Instance non trouvé ! Impossible d'ajouter les items.");
        }
    }

	/// <summary>
    /// Appelée par le bouton de débogage pour ajouter de l'XP à tous les personnages de l'équipe active.
    /// </summary>
    public void OnDebugAddXPClicked()
    {
        // Montant d'XP à donner à chaque fois, vous pouvez l'ajuster
        const int xpToGive = 100;

        if (TeamManager.Instance == null || PlayerDataManager.Instance == null)
        {
            Debug.LogError("[HubManager] TeamManager ou PlayerDataManager non disponible ! Impossible d'ajouter de l'XP.");
            return;
        }

        List<CharacterData_SO> activeTeam = TeamManager.Instance.ActiveTeam;
        if (activeTeam.Count == 0)
        {
            Debug.LogWarning("[HubManager] L'équipe active est vide. Aucun XP n'a été ajouté.");
            return;
        }

        Debug.Log($"[HubManager] Ajout de {xpToGive} XP à {activeTeam.Count(c => c != null)} personnage(s) de l'équipe active...");

        // Parcourir chaque personnage de l'équipe active et lui ajouter de l'XP
        foreach (CharacterData_SO character in activeTeam)
        {
            if (character != null)
            {
                PlayerDataManager.Instance.AddXPToCharacter(character.CharacterID, xpToGive);
                // Le log de la montée de niveau est déjà dans PlayerDataManager.
            }
        }

        Debug.Log("[HubManager] Distribution d'XP terminée.");
        
        // Optionnel : Mettre à jour l'UI si elle est ouverte et affiche des niveaux/XP.
        // Si vous êtes sur le panel d'équipement, il faudrait le notifier pour qu'il se rafraîchisse.
        // Pour l'instant, quitter et rouvrir le panel suffira pour voir les changements.
    }

}