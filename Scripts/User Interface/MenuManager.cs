using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using TMPro;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject optionsPanel;

    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backButton;
    
    [Header("Cinematic")]
    [SerializeField] private MenuIntroCinematic introCinematic;

    [Header("Timeline Management")]
    [SerializeField] private TimelineManager timelineManager;
    
    [Header("Options Management")]
    [SerializeField] private OptionsManager optionsManager;
    
    [Header("Controller Navigation")]
    [SerializeField] private MenuVisualEffects visualEffects;
    [SerializeField] private float buttonScaleMultiplier = 1.2f;
    [SerializeField] private float animationSpeed = 0.15f;
    
    // État de navigation
    private Button[] mainMenuButtons;
    private GameObject lastSelectedObject;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        Debug.Log("Start Menu Manager");
        InitializeButtons();
        InitializeNavigation();
        LoadSettings();
        
        if (timelineManager == null)
        {
            timelineManager = FindObjectOfType<TimelineManager>();
        }
        
        // Sélectionner le bouton Play par défaut
        StartCoroutine(SetInitialSelection());
    }
    
    private void Update()
    {
        HandleControllerNavigation();
        HandleVisualFeedback();
        EnsureSelection();
    }
    
    private void InitializeButtons()
    {
        playButton.onClick.AddListener(OnPlayButtonClicked);
        optionsButton.onClick.AddListener(OnOptionsButtonClicked);
        quitButton.onClick.AddListener(OnQuitButtonClicked);
        backButton.onClick.AddListener(OnBackButtonClicked);
        
        // Trouver l'OptionsManager si non assigné
        if (optionsManager == null)
        {
            optionsManager = FindObjectOfType<OptionsManager>();
        }
    }

    private void LoadSettings()
    {
        // Les paramètres sont maintenant gérés par OptionsManager et PlayerDataManager
        Debug.Log("[MenuManager] Paramètres chargés via OptionsManager");
    }
    
    private void OnPlayButtonClicked()
    {
        Debug.Log("Start OnPlayButtonClicked");
        if (timelineManager != null)
        {
            timelineManager.SwitchToPlayTimelines();
        }
        // Démarrer la cinématique au lieu de charger directement la scène
        if (introCinematic != null)
        {
            Debug.Log("Start introCinematic");
            // Cacher l'interface du menu pendant la cinématique
            HideMenuUI();
            
            // Lancer la cinématique
            introCinematic.PlayCinematic();
        }
        else
        {
            // Fallback si la cinématique n'est pas configurée
            Debug.Log("Démarrage du jeu !!!!");
            // TODO: Implémenter la logique de démarrage du jeu
        }
    }

    private void HideMenuUI()
    {
        // Cacher les éléments d'UI qui pourraient interférer avec la cinématique
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    private void OnOptionsButtonClicked()
    {
        mainMenuPanel.SetActive(false);
        optionsPanel.SetActive(true);
        StartCoroutine(SetInitialSelection());
    }

    private void OnBackButtonClicked()
    {
        optionsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        StartCoroutine(SetInitialSelection());
    }

    private void OnQuitButtonClicked()
    {
        Debug.Log("start OnQuitButtonClicked");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    #region Navigation Controller
    
    private void InitializeNavigation()
    {
        // Initialiser les tableaux de boutons pour le menu principal uniquement
        mainMenuButtons = new Button[] { playButton, optionsButton, quitButton };
        
        // Configurer la navigation explicite pour les boutons du menu principal
        SetupButtonNavigation(playButton, null, optionsButton, null, null);
        SetupButtonNavigation(optionsButton, playButton, quitButton, null, null);
        SetupButtonNavigation(quitButton, optionsButton, null, null, null);
        
        // La navigation du menu options est maintenant gérée par OptionsManager
        SetupButtonNavigation(backButton, null, null, null, null);
    }
    
    private void SetupButtonNavigation(Button button, Button selectOnUp, Button selectOnDown, Button selectOnLeft, Button selectOnRight)
    {
        if (button == null) return;
        
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnUp = selectOnUp;
        nav.selectOnDown = selectOnDown;
        nav.selectOnLeft = selectOnLeft;
        nav.selectOnRight = selectOnRight;
        button.navigation = nav;
    }
    
    private IEnumerator SetInitialSelection()
    {
        // Attendre une frame pour que tout soit initialisé
        yield return null;
        
        if (mainMenuPanel.activeInHierarchy && playButton != null)
        {
            EventSystem.current.SetSelectedGameObject(playButton.gameObject);
            Debug.Log("[MenuManager] Sélection initiale: " + playButton.name);
        }
        else if (optionsPanel.activeInHierarchy && backButton != null)
        {
            EventSystem.current.SetSelectedGameObject(backButton.gameObject);
            Debug.Log("[MenuManager] Sélection initiale: " + backButton.name);
        }
    }
    
    private void HandleControllerNavigation()
    {
        if (InputManager.Instance == null) return;
        
        // Gérer l'action Cancel (retour)
        if (InputManager.Instance.UIActions.Cancel.WasPressedThisFrame())
        {
            if (optionsPanel.activeInHierarchy)
            {
                OnBackButtonClicked();
            }
        }
        
        // Détecter si on utilise la manette pour réactiver la sélection
        Vector2 navigationInput = InputManager.Instance.UIActions.Navigate.ReadValue<Vector2>();
        bool submitPressed = InputManager.Instance.UIActions.Submit.WasPressedThisFrame();
        
        if (navigationInput != Vector2.zero || submitPressed)
        {
            if (EventSystem.current.currentSelectedGameObject == null)
            {
                StartCoroutine(SetInitialSelection());
            }
        }
    }
    
    private void HandleVisualFeedback()
    {
        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        
        // Animer le bouton actuellement sélectionné
        if (currentSelected != null && currentSelected != lastSelectedObject)
        {
            // Remettre l'ancien bouton à sa taille normale
            if (lastSelectedObject != null)
            {
                AnimateButton(lastSelectedObject, Vector3.one, false);
            }
            
            // Agrandir le nouveau bouton sélectionné
            AnimateButton(currentSelected, Vector3.one * buttonScaleMultiplier, true);
            
            lastSelectedObject = currentSelected;
        }
    }
    
    private void AnimateButton(GameObject buttonObject, Vector3 targetScale, bool isSelected)
    {
        if (buttonObject == null) return;
        
        // Arrêter toute animation en cours sur ce bouton
        LeanTween.cancel(buttonObject);
        
        // Animer vers la nouvelle échelle
        LeanTween.scale(buttonObject, targetScale, animationSpeed)
            .setEaseOutBack();
        
        // Utiliser MenuVisualEffects si disponible
        if (visualEffects != null)
        {
            Button button = buttonObject.GetComponent<Button>();
            if (button != null)
            {
                if (isSelected)
                {
                    visualEffects.OnButtonHoverEnter(button);
                }
                else
                {
                    visualEffects.OnButtonHoverExit(button);
                }
            }
        }
    }
    
    private void EnsureSelection()
    {
        // S'assurer qu'il y a toujours un bouton sélectionné quand on utilise la manette
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            if (InputManager.Instance != null)
            {
                Vector2 navigationInput = InputManager.Instance.UIActions.Navigate.ReadValue<Vector2>();
                bool submitPressed = InputManager.Instance.UIActions.Submit.WasPressedThisFrame();
                
                if (navigationInput != Vector2.zero || submitPressed)
                {
                    StartCoroutine(SetInitialSelection());
                }
            }
        }
    }
    
    #endregion
}