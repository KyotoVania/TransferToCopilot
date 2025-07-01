using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Events;
using System.Linq;
using ScriptableObjects;
using System.Collections;
using UnityEngine.InputSystem;

public class HubManager : MonoBehaviour
{
    
    public static HubManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning("[HubManager] Une autre instance de HubManager existe déjà. Destruction de la nouvelle instance.");
            Destroy(gameObject);
        }
    }
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
    private bool _hubControlsActive = true; 

    private HubCameraManager _hubCameraManager;
    private bool _isTransitioning = false;
    private HubViewpoint _currentViewpoint = HubViewpoint.General;
    
    // Variables pour la gestion de l'input
    private float _navigationCooldown = 0.2f; // Empêche la navigation trop rapide
    private float _lastNavigationTime = 0f;

    void Start()
    {
        _hubCameraManager = HubCameraManager.Instance; 
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
        //GameManager.Instance.SetState(GameState.Hub);

        if (buttonBackToMainMenu != null) buttonBackToMainMenu.onClick.AddListener(GoBackToMainMenu);

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

        HandleInputSystemNavigation();
    }

    private void HandleInputSystemNavigation()
    {
        if (hubPointsOfInterest.Count == 0) return;
        
        // Ne pas traiter les inputs si un panel de section est actif
        if (IsSectionPanelActive()) return;
        
        // Vérifier le cooldown pour éviter la navigation trop rapide
        if (Time.time - _lastNavigationTime < _navigationCooldown) return;

        // Récupérer l'InputManager
        var inputManager = InputManager.Instance;
        if (inputManager == null) return;

        // Récupérer la valeur de navigation (Vector2)
        Vector2 navigationInput = inputManager.UIActions.Navigate.ReadValue<Vector2>();
        HubViewpoint nextView = _currentViewpoint;

        // Navigation horizontale uniquement (axe X)
        if (Mathf.Abs(navigationInput.x) > 0.5f) // Seuil pour éviter les inputs accidentels
        {
            switch (_currentViewpoint)
            {
                case HubViewpoint.General:
                    if (navigationInput.x < 0) // Gauche
                        nextView = HubViewpoint.LevelSelection;
                    else if (navigationInput.x > 0) // Droite
                        nextView = HubViewpoint.TeamManagement;
                    break;

                case HubViewpoint.LevelSelection:
                    if (navigationInput.x > 0) // Droite depuis la gauche = retour au centre
                        nextView = HubViewpoint.General;
                    break;

                case HubViewpoint.TeamManagement:
                    if (navigationInput.x < 0) // Gauche depuis la droite = retour au centre
                        nextView = HubViewpoint.General;
                    break;
            }

            // Si la vue a changé, on lance la transition
            if (nextView != _currentViewpoint)
            {
                _lastNavigationTime = Time.time;
                StartCoroutine(TransitionToView(nextView));
            }
        }

        // Gestion de l'action Submit (validation)
        if (_currentViewpoint != HubViewpoint.General && inputManager.UIActions.Submit.WasPressedThisFrame())
        {
            StartCoroutine(FadeTitle(false));
            ShowCorrectPanelForCurrentView();
        }
    }
    
    private bool IsSectionPanelActive()
    {
        // Vérifier si un des panels de section est actif
        if (_currentViewpoint != HubViewpoint.General)
        {
            if (!_hubControlsActive)
            {
                // Si les contrôles du hub sont désactivés, on ne permet pas la navigation
                return true;
            }
            var point = hubPointsOfInterest.FirstOrDefault(p => p.Viewpoint == _currentViewpoint);
            if (point != null && point.UIPanel != null && point.UIPanel.activeSelf)
            {
                return true;
            }
            
        }
        return false;
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
    
    public void DisableHubControls()
    {
        _hubControlsActive = false;
    }

    public void EnableHubControls()
    {
        _hubControlsActive = true;
    }
}