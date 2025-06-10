using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Events;
using System.Linq;
using ScriptableObjects;

public class HubManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject panelMainHub; // Peut être nul si la vue générale n'a pas de panel dédié
    [SerializeField] private GameObject panelLevelSelection;
    [SerializeField] private GameObject panelTeamManagement;

    [Header("UI Elements - Global Hub Info (Optionnel)")]
    [SerializeField] private TextMeshProUGUI textCurrency;
    [SerializeField] private TextMeshProUGUI textExperience;
    [SerializeField] private Button buttonBackToMainMenu;
  	[Header("Debug Options")]
    [Tooltip("Liste des SO d'équipement à donner au joueur via le bouton de débogage.")]
    [SerializeField] private List<EquipmentData_SO> testEquipmentToGive;
    // Classe interne pour les points d'intérêt
    [System.Serializable]
    public class HubInterestPoint
    {
        public string Name; // Pour l'inspecteur

        // Utiliser UnityEvent pour pouvoir les assigner dans l'inspecteur
        public UnityEvent ShowPanelAction;        // Action pour afficher le panel UI
        public UnityEvent TransitionCameraAction; // Action pour la transition de caméra
    }

    [Header("Navigation & Points d'Intérêt du Hub")]
    [SerializeField] private List<HubInterestPoint> hubPointsOfInterest = new List<HubInterestPoint>();
    private int currentInterestPointIndex = -1;

    private HubCameraManager _hubCameraManager; // Sera assigné ou trouvé

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

        if (buttonBackToMainMenu != null) buttonBackToMainMenu.onClick.AddListener(GoBackToMainMenu);

        // Plus besoin d'InitializeHubPoints() si tout est configuré via l'inspecteur.
        // Si vous vouliez ajouter dynamiquement des points, cette méthode serait utile.

        UpdateCurrencyDisplay(PlayerDataManager.Instance.Data.Currency);
        UpdateExperienceDisplay(PlayerDataManager.Instance.Data.Experience);
        PlayerDataManager.OnCurrencyChanged += UpdateCurrencyDisplay;
        PlayerDataManager.OnExperienceGained += UpdateExperienceDisplay;

        currentInterestPointIndex = -1;
        HideAllSectionPanels(); // Cache tous les panels spécifiques
        _hubCameraManager?.TransitionToGeneralHubView(true);
    }

    private void OnDestroy()
    {
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.OnCurrencyChanged -= UpdateCurrencyDisplay;
            PlayerDataManager.OnExperienceGained -= UpdateExperienceDisplay;
        }
    }

    void Update()
    {
        HandleKeyboardNavigation();
    }

    private void HandleKeyboardNavigation()
    {
        if (hubPointsOfInterest.Count == 0) return;

        bool navigated = false;
        int previousIndex = currentInterestPointIndex;

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            currentInterestPointIndex++;
            if (currentInterestPointIndex >= hubPointsOfInterest.Count)
            {
                currentInterestPointIndex = -1; // Retour à la vue générale
            }
            navigated = true;
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            currentInterestPointIndex--;
            if (currentInterestPointIndex < -1)
            {
                currentInterestPointIndex = hubPointsOfInterest.Count - 1;
            }
            navigated = true;
        }

        if (navigated)
        {
            // On a changé de point d'intérêt.
            // On lance UNIQUEMENT la transition de caméra et on cache les panels.
            TransitionToCurrentInterestPoint();
        }

        // NOUVELLE LOGIQUE : Confirmation pour afficher le panel
        if (currentInterestPointIndex != -1 && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
        {
            // Vérifie qu'on n'est pas déjà sur un panel affiché par erreur
            // et que le point d'intérêt est valide.
            if (currentInterestPointIndex >= 0 && currentInterestPointIndex < hubPointsOfInterest.Count)
            {
                Debug.Log($"[HubManager] Interaction avec {hubPointsOfInterest[currentInterestPointIndex].Name} confirmée.");
                // Affiche le panel correspondant au point d'intérêt actuel.
                hubPointsOfInterest[currentInterestPointIndex].ShowPanelAction?.Invoke();
            }
        }
    }
    private void TransitionToCurrentInterestPoint()
    {
        HideAllSectionPanels(); // Cacher tous les panels de section

        if (currentInterestPointIndex == -1) // Vue générale
        {
            _hubCameraManager?.TransitionToGeneralHubView();
            Debug.Log("[HubManager] Navigation clavier : Transition vers la Vue Générale.");
        }
        else if (currentInterestPointIndex >= 0 && currentInterestPointIndex < hubPointsOfInterest.Count)
        {
            HubInterestPoint point = hubPointsOfInterest[currentInterestPointIndex];
            point.TransitionCameraAction?.Invoke();
            Debug.Log($"[HubManager] Navigation clavier : Transition vers le point d'intérêt '{point.Name}'.");
        }
    }
    

    private void HideAllSectionPanels()
    {
        panelLevelSelection?.SetActive(false);
        panelTeamManagement?.SetActive(false);
        
        if (panelMainHub != null)
        {
            // Affiche le panel principal seulement si on est en vue générale
            panelMainHub.SetActive(currentInterestPointIndex == -1);
        }
    }
    
    // Méthodes publiques pour être appelées par UnityEvent ou HubInteractable
    public void GoToLevelSelection() // Appelée par clic sur objet 3D ou par UnityEvent
    {
        // Optionnel: mettre à jour currentInterestPointIndex si appelé par clic
        currentInterestPointIndex = hubPointsOfInterest.FindIndex(p => p.Name == "Level Selection"); // Adapter le nom si besoin

        HideAllSectionPanels();
        if (panelMainHub != null) panelMainHub.SetActive(false);
        panelLevelSelection?.SetActive(true);
        _hubCameraManager?.TransitionToLevelSelectionView();
        Debug.Log("[HubManager] Affichage Sélection de Niveaux.");
    }

    public void GoToTeamManagement() // Appelée par clic sur objet 3D ou par UnityEvent
    {
        // Optionnel: mettre à jour currentInterestPointIndex
        currentInterestPointIndex = hubPointsOfInterest.FindIndex(p => p.Name == "Team Management"); // Adapter le nom

        HideAllSectionPanels();
        if (panelMainHub != null) panelMainHub.SetActive(false);
        panelTeamManagement?.SetActive(true);
        _hubCameraManager?.TransitionToTeamManagementView();
        Debug.Log("[HubManager] Affichage Gestion d'Équipe.");
    }
     public void GoToGeneralView() // Pour revenir à la vue générale via un bouton UI par exemple
    {
        currentInterestPointIndex = -1;
        HideAllSectionPanels();
        if (panelMainHub != null) panelMainHub.SetActive(true);
        _hubCameraManager?.TransitionToGeneralHubView();
        Debug.Log("[HubManager] Retour à la vue générale demandée.");
    }


    private void UpdateCurrencyDisplay(int newAmount)
    {
        if (textCurrency != null) textCurrency.text = $"Monnaie: {newAmount}";
    }

    private void UpdateExperienceDisplay(int newAmount)
    {
        if (textExperience != null) textExperience.text = $"XP: {newAmount}";
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