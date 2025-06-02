using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Events;

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
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            currentInterestPointIndex++;
            if (currentInterestPointIndex >= hubPointsOfInterest.Count)
            {
                currentInterestPointIndex = -1;
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
            ActivateCurrentInterestPoint();
        }

        if (currentInterestPointIndex != -1 && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
        {
            Debug.Log($"[HubManager] Interaction avec {hubPointsOfInterest[currentInterestPointIndex].Name} confirmée par clavier.");
            // Ici, vous pourriez déclencher une action "d'entrée" spécifique si nécessaire,
            // par exemple, si le panel a un bouton par défaut.
            // Pour l'instant, l'activation du panel se fait déjà par ActivateCurrentInterestPoint.
        }
    }

    private void ActivateCurrentInterestPoint()
    {
        if (currentInterestPointIndex == -1) // Vue générale
        {
            HideAllSectionPanels(); // S'assure que seul le panel principal (si existant) est actif
            _hubCameraManager?.TransitionToGeneralHubView();
            Debug.Log("[HubManager] Navigation clavier : Vue Générale activée.");
        }
        else if (currentInterestPointIndex >= 0 && currentInterestPointIndex < hubPointsOfInterest.Count)
        {
            HubInterestPoint point = hubPointsOfInterest[currentInterestPointIndex];
            HideAllSectionPanels(); // Cacher les autres avant d'afficher le nouveau
            point.ShowPanelAction?.Invoke(); // Invoque l'UnityEvent pour afficher le panel UI
            point.TransitionCameraAction?.Invoke(); // Invoque l'UnityEvent pour la transition caméra
            Debug.Log($"[HubManager] Navigation clavier : Point d'intérêt '{point.Name}' activé.");
        }
    }

    private void HideAllSectionPanels()
    {
        panelLevelSelection?.SetActive(false);
        panelTeamManagement?.SetActive(false);
        // panelMainHub?.SetActive(true); // Activer le panel principal seulement si on retourne à la vue générale.
                                        // Ou le gérer comme un autre point d'intérêt.
                                        // Pour l'instant, si index = -1, on ne fait qu'appeler la transition caméra.
                                        // Assurez-vous que votre "vue générale" ne nécessite pas un panel spécifique
                                        // ou que vous l'activez/désactivez correctement.
        if (panelMainHub != null)
        {
            panelMainHub.SetActive(currentInterestPointIndex == -1 && panelMainHub != panelLevelSelection && panelMainHub != panelTeamManagement);
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
}