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
    [Header("UI Panels")]
    [SerializeField] private GameObject panelMainHub;
    [SerializeField] private GameObject panelLevelSelection;
    [SerializeField] private GameObject panelTeamManagement;

    [Header("UI Elements - Global Hub Info (Optionnel)")]
    [SerializeField] private TextMeshProUGUI textCurrency;
    [SerializeField] private TextMeshProUGUI textExperience;
    [SerializeField] private Button buttonBackToMainMenu;
  	[Header("Debug Options")]
    [Tooltip("Liste des SO d'équipement à donner au joueur via le bouton de débogage.")]
    [SerializeField] private List<EquipmentData_SO> testEquipmentToGive;
 	[Header("UI Dynamique du Hub")]
    [SerializeField] private TextMeshProUGUI hubTitleText;
    [SerializeField] private CanvasGroup hubTitleCanvasGroup;
    [SerializeField] private float titleFadeDuration = 0.2f;
    [System.Serializable]
    public class HubPointOfInterest
    {
        public string Name; 
        public HubViewpoint Viewpoint;
        public GameObject UIPanel;
    }

    [Header("Navigation & Points d'Intérêt")]
    [Tooltip("Configurez ici les points d'intérêt du Hub. L'ordre dans la liste définit l'ordre de navigation.")]
    [SerializeField] private List<HubPointOfInterest> hubPointsOfInterest = new List<HubPointOfInterest>();
    
    private HubCameraManager _hubCameraManager;
    private bool _isTransitioning = false;
    private HubViewpoint _currentViewpoint = HubViewpoint.General;

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
 		if (hubTitleCanvasGroup != null)
        {
            UpdateTitle();
            hubTitleCanvasGroup.alpha = 1f;
        }
        _currentViewpoint = HubViewpoint.General;
        ShowCorrectPanelForCurrentView();
    }
	private void UpdateTitle()
    {
        if (hubTitleText == null) return;

        switch (_currentViewpoint)
        {
            case HubViewpoint.General:
                hubTitleText.text = "GENERAL";
                break;
            case HubViewpoint.LevelSelection:
                hubTitleText.text = "LEVEL SELECTION";
                break;
            case HubViewpoint.TeamManagement:
                hubTitleText.text = "TEAM MANAGEMENT";
                break;
        }
    }

	private IEnumerator FadeTitle(bool fadeIn)
    {
        if (hubTitleCanvasGroup == null) yield break;

        float startAlpha = hubTitleCanvasGroup.alpha;
        float endAlpha = fadeIn ? 1f : 0f;
        float elapsedTime = 0f;

        while (elapsedTime < titleFadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            hubTitleCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / titleFadeDuration);
            yield return null;
        }

        hubTitleCanvasGroup.alpha = endAlpha;
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
        if (_isTransitioning) return;

        HandleKeyboardNavigation();
    }

    private void HandleKeyboardNavigation()
    {
        if (hubPointsOfInterest.Count == 0) return;

        HubViewpoint nextView = _currentViewpoint;

        switch (_currentViewpoint)
        {
            case HubViewpoint.General:
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                    nextView = HubViewpoint.LevelSelection; // Gauche = Sélection de niveaux
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                    nextView = HubViewpoint.TeamManagement; // Droite = Gestion d'équipe
                break;

            case HubViewpoint.LevelSelection:
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                    nextView = HubViewpoint.General; // Depuis la gauche, on retourne au centre
                // Si on appuie à gauche, on ne fait rien (on est au bout)
                break;

            case HubViewpoint.TeamManagement:
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                    nextView = HubViewpoint.General; // Depuis la droite, on retourne au centre
                // Si on appuie à droite, on ne fait rien (on est au bout)
                break;
        }

        // Si la vue a changé, on lance la transition
        if (nextView != _currentViewpoint)
        {
            StartCoroutine(TransitionToView(nextView));
        }
        // Si on est sur une vue de section et qu'on appuie sur Espace
        else if (_currentViewpoint != HubViewpoint.General && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
        {
            StartCoroutine(FadeTitle(false));
            ShowCorrectPanelForCurrentView();
        }
    }

 	private IEnumerator TransitionToView(HubViewpoint newView)
    {
        _isTransitioning = true;
        
        HideAllSectionPanels();
		if (_currentViewpoint != newView)
        {
            yield return StartCoroutine(FadeTitle(false));
        }
        // On met à jour l'état logique immédiatement
        _currentViewpoint = newView;
            
        // Demander la transition de caméra et attendre qu'elle soit finie
        yield return StartCoroutine(_hubCameraManager.TransitionTo(newView));
        
        _isTransitioning = false;
        UpdateTitle(); 
        yield return StartCoroutine(FadeTitle(true));
       
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
            // Trouver le point d'intérêt correspondant à la vue actuelle et activer son panel
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
        StartCoroutine(TransitionToView(HubViewpoint.General));
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