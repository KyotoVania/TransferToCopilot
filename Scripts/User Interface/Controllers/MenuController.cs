using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;

public class MenuController : MonoBehaviour, IMenuObserver
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject optionsPanel;

    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backButton;

    [Header("Audio Settings - Sliders UI")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Visual Feedback")]
    [SerializeField] private float selectedScale = 1.1f;
    [SerializeField] private float animationDuration = 0.2f;
    [SerializeField] private LeanTweenType animationType = LeanTweenType.easeOutBack;

    [Header("Components")]
    [SerializeField] private ButtonAnimator buttonAnimator;
    [SerializeField] private LogoAnimator logoAnimator;
    [SerializeField] private MenuCameraManager menuCameraManager;

    private MenuState currentState = MenuState.MainMenu;
    private GameObject _lastSelectedObject;
    private Coroutine _selectionCoroutine;

    private void Awake()
    {
        if (menuCameraManager == null)
        {
            Debug.LogError("[MenuController] MenuCameraManager non assigné!");
        }
    }

    private void Start()
    {
        InitializeButtonsAndSliderListeners();
        ConfigureNavigation();
    }

    private void OnEnable()
    {
        // S'abonner aux événements audio
        AudioManager.OnMasterVolumeSettingChanged += HandleMasterVolumeUpdateFromManager;
        AudioManager.OnMusicVolumeSettingChanged += HandleMusicVolumeUpdateFromManager;
        AudioManager.OnSfxVolumeSettingChanged += HandleSfxVolumeUpdateFromManager;

        if (MenuSceneTransitionManager.Instance != null)
        {
            MenuSceneTransitionManager.Instance.AddObserver(this);
        }

        InitializeSliderValues();
        ChangeMenuState(currentState);
        
        // Setup de la sélection initiale
        if (_selectionCoroutine != null) StopCoroutine(_selectionCoroutine);
        _selectionCoroutine = StartCoroutine(SetupInitialSelection());
    }

    private void OnDisable()
    {
        // Se désabonner
        AudioManager.OnMasterVolumeSettingChanged -= HandleMasterVolumeUpdateFromManager;
        AudioManager.OnMusicVolumeSettingChanged -= HandleMusicVolumeUpdateFromManager;
        AudioManager.OnSfxVolumeSettingChanged -= HandleSfxVolumeUpdateFromManager;

        if (MenuSceneTransitionManager.Instance != null)
        {
            MenuSceneTransitionManager.Instance.RemoveObserver(this);
        }
        
        _lastSelectedObject = EventSystem.current.currentSelectedGameObject;
    }

    private void Update()
    {
        // Gérer la navigation
        HandleNavigation();
        
        // Mettre à jour les effets visuels basés sur la sélection
        UpdateButtonVisualFeedback();
        
        // S'assurer qu'on a toujours quelque chose de sélectionné
        EnsureSelection();
    }

    #region Navigation Setup

    private void ConfigureNavigation()
    {
        // Configuration de la navigation verticale pour le menu principal
        ConfigureMainMenuNavigation();
        
        // Configuration de la navigation pour le panel d'options
        ConfigureOptionsNavigation();
    }

    private void ConfigureMainMenuNavigation()
    {
        // Navigation verticale entre les boutons du menu principal
        Navigation playNav = playButton.navigation;
        playNav.mode = Navigation.Mode.Explicit;
        playNav.selectOnDown = optionsButton;
        playButton.navigation = playNav;

        Navigation optionsNav = optionsButton.navigation;
        optionsNav.mode = Navigation.Mode.Explicit;
        optionsNav.selectOnUp = playButton;
        optionsNav.selectOnDown = quitButton;
        optionsButton.navigation = optionsNav;

        Navigation quitNav = quitButton.navigation;
        quitNav.mode = Navigation.Mode.Explicit;
        quitNav.selectOnUp = optionsButton;
        quitButton.navigation = quitNav;
    }

    private void ConfigureOptionsNavigation()
    {
        // Navigation entre les sliders
        Navigation masterNav = masterVolumeSlider.navigation;
        masterNav.mode = Navigation.Mode.Explicit;
        masterNav.selectOnDown = musicVolumeSlider;
        masterVolumeSlider.navigation = masterNav;

        Navigation musicNav = musicVolumeSlider.navigation;
        musicNav.mode = Navigation.Mode.Explicit;
        musicNav.selectOnUp = masterVolumeSlider;
        musicNav.selectOnDown = sfxVolumeSlider;
        musicVolumeSlider.navigation = musicNav;

        Navigation sfxNav = sfxVolumeSlider.navigation;
        sfxNav.mode = Navigation.Mode.Explicit;
        sfxNav.selectOnUp = musicVolumeSlider;
        sfxNav.selectOnDown = backButton;
        sfxVolumeSlider.navigation = sfxNav;

        Navigation backNav = backButton.navigation;
        backNav.mode = Navigation.Mode.Explicit;
        backNav.selectOnUp = sfxVolumeSlider;
        backButton.navigation = backNav;
    }

    #endregion

    #region Visual Feedback

    private void UpdateButtonVisualFeedback()
    {
        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        
        // Mettre à jour l'échelle des boutons du menu principal
        UpdateButtonScale(playButton, currentSelected);
        UpdateButtonScale(optionsButton, currentSelected);
        UpdateButtonScale(quitButton, currentSelected);
        UpdateButtonScale(backButton, currentSelected);
    }

    private void UpdateButtonScale(Button button, GameObject currentSelected)
    {
        if (button == null) return;
        
        bool isSelected = (currentSelected == button.gameObject);
        Vector3 targetScale = isSelected ? Vector3.one * selectedScale : Vector3.one;
        
        // Annuler toute animation en cours
        LeanTween.cancel(button.gameObject);
        
        // Animer vers la nouvelle échelle
        LeanTween.scale(button.gameObject, targetScale, animationDuration)
            .setEase(animationType);
    }

    #endregion

    #region Navigation Handling

    private void HandleNavigation()
    {
        if (InputManager.Instance == null) return;
        
        // Gérer l'action Cancel (B/Circle/Escape)
        if (InputManager.Instance.UIActions.Cancel.WasPressedThisFrame())
        {
            OnCancelPressed();
        }
    }

    private void OnCancelPressed()
    {
        if (currentState == MenuState.Options)
        {
            OnBackButtonClicked();
        }
        // Si on est dans le menu principal, on pourrait quitter le jeu
        // mais c'est généralement mieux de ne rien faire
    }

    private IEnumerator SetupInitialSelection()
    {
        yield return null; // Attendre une frame
        
        GameObject targetObject = null;
        
        if (currentState == MenuState.MainMenu)
        {
            // Si on avait quelque chose de sélectionné avant et que c'est toujours actif
            if (_lastSelectedObject != null && _lastSelectedObject.activeInHierarchy &&
                (mainMenuPanel.activeInHierarchy && IsChildOf(_lastSelectedObject, mainMenuPanel)))
            {
                targetObject = _lastSelectedObject;
            }
            else
            {
                // Par défaut, sélectionner le bouton Play
                targetObject = playButton.gameObject;
            }
        }
        else if (currentState == MenuState.Options)
        {
            if (_lastSelectedObject != null && _lastSelectedObject.activeInHierarchy &&
                (optionsPanel.activeInHierarchy && IsChildOf(_lastSelectedObject, optionsPanel)))
            {
                targetObject = _lastSelectedObject;
            }
            else
            {
                // Par défaut, sélectionner le premier slider
                targetObject = masterVolumeSlider.gameObject;
            }
        }
        
        if (targetObject != null)
        {
            EventSystem.current.SetSelectedGameObject(targetObject);
            Debug.Log($"[MenuController] Sélection initiale : {targetObject.name}");
        }
    }

    private bool IsChildOf(GameObject child, GameObject parent)
    {
        Transform current = child.transform;
        while (current != null)
        {
            if (current.gameObject == parent) return true;
            current = current.parent;
        }
        return false;
    }

    private void EnsureSelection()
    {
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            Vector2 navigationInput = InputManager.Instance?.UIActions.Navigate.ReadValue<Vector2>() ?? Vector2.zero;
            bool submitPressed = InputManager.Instance?.UIActions.Submit.WasPressedThisFrame() ?? false;
            
            if (navigationInput != Vector2.zero || submitPressed)
            {
                if (_selectionCoroutine != null) StopCoroutine(_selectionCoroutine);
                _selectionCoroutine = StartCoroutine(SetupInitialSelection());
            }
        }
    }

    #endregion

    #region Button Actions

    private void InitializeButtonsAndSliderListeners()
    {
        if (playButton != null) playButton.onClick.AddListener(OnPlayButtonClicked);
        if (optionsButton != null) optionsButton.onClick.AddListener(OnOptionsButtonClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitButtonClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackButtonClicked);

        // Ajouter les événements de hover si buttonAnimator est assigné
        if (buttonAnimator != null)
        {
            if (playButton != null) AddButtonHoverEvents(playButton);
            if (optionsButton != null) AddButtonHoverEvents(optionsButton);
            if (quitButton != null) AddButtonHoverEvents(quitButton);
            if (backButton != null) AddButtonHoverEvents(backButton);
        }

        // Configuration des sliders
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
            masterVolumeSlider.onValueChanged.AddListener(OnMasterSliderChanged);
        }
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
            musicVolumeSlider.onValueChanged.AddListener(OnMusicSliderChanged);
        }
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxSliderChanged);
        }
    }

    private void OnPlayButtonClicked()
    {
        // Animation de clic
        AnimateButtonClick(playButton);
        buttonAnimator?.OnClick(playButton);
        GameManager.Instance?.LoadHub();
    }

    private void OnOptionsButtonClicked()
    {
        AnimateButtonClick(optionsButton);
        buttonAnimator?.OnClick(optionsButton);
        ChangeMenuState(MenuState.Options);
    }

    private void OnBackButtonClicked()
    {
        AnimateButtonClick(backButton);
        buttonAnimator?.OnClick(backButton);
        ChangeMenuState(MenuState.MainMenu);
    }

    private void OnQuitButtonClicked()
    {
        AnimateButtonClick(quitButton);
        buttonAnimator?.OnClick(quitButton);
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    private void AnimateButtonClick(Button button)
    {
        if (button == null) return;
        
        LeanTween.cancel(button.gameObject);
        LeanTween.scale(button.gameObject, Vector3.one * 0.95f, 0.1f)
            .setEase(LeanTweenType.easeOutQuad)
            .setOnComplete(() => {
                LeanTween.scale(button.gameObject, Vector3.one * selectedScale, 0.1f)
                    .setEase(LeanTweenType.easeOutBack);
            });
    }

    #endregion

    #region State Management

    private void ChangeMenuState(MenuState newState)
    {
        currentState = newState;
        if (mainMenuPanel != null) mainMenuPanel.SetActive(newState == MenuState.MainMenu);
        if (optionsPanel != null) optionsPanel.SetActive(newState == MenuState.Options);

        if (menuCameraManager != null)
        {
            if (newState == MenuState.Options) menuCameraManager.TransitionToOptions();
            else if (newState == MenuState.MainMenu) menuCameraManager.TransitionToMainMenu();
        }
        
        // Réinitialiser la sélection pour le nouveau panel
        if (_selectionCoroutine != null) StopCoroutine(_selectionCoroutine);
        _selectionCoroutine = StartCoroutine(SetupInitialSelection());
    }

    #endregion

    #region Audio & Other Existing Methods

    private void InitializeSliderValues()
    {
        if (AudioManager.Instance != null)
        {
            if (masterVolumeSlider != null && !Mathf.Approximately(masterVolumeSlider.value, AudioManager.Instance.MasterVolume))
                masterVolumeSlider.value = AudioManager.Instance.MasterVolume;
            if (musicVolumeSlider != null && !Mathf.Approximately(musicVolumeSlider.value, AudioManager.Instance.MusicVolume))
                musicVolumeSlider.value = AudioManager.Instance.MusicVolume;
            if (sfxVolumeSlider != null && !Mathf.Approximately(sfxVolumeSlider.value, AudioManager.Instance.SfxVolume))
                sfxVolumeSlider.value = AudioManager.Instance.SfxVolume;
        }
    }

    private void AddButtonHoverEvents(Button button)
    {
        if (buttonAnimator == null) return;

        var eventTrigger = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (eventTrigger == null) eventTrigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        eventTrigger.triggers.Clear();

        var entryEnter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((data) => buttonAnimator.OnHoverEnter(button));
        eventTrigger.triggers.Add(entryEnter);

        var entryExit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
        entryExit.callback.AddListener((data) => buttonAnimator.OnHoverExit(button));
        eventTrigger.triggers.Add(entryExit);
    }

    private void OnMasterSliderChanged(float value) { AudioManager.Instance?.SetMasterVolume(value); }
    private void OnMusicSliderChanged(float value) { AudioManager.Instance?.SetMusicVolume(value); }
    private void OnSfxSliderChanged(float value) { AudioManager.Instance?.SetSfxVolume(value); }

    private void HandleMasterVolumeUpdateFromManager(float newVolumeNormalized)
    {
        if (masterVolumeSlider != null && !Mathf.Approximately(masterVolumeSlider.value, newVolumeNormalized))
        {
            masterVolumeSlider.value = newVolumeNormalized;
        }
    }

    private void HandleMusicVolumeUpdateFromManager(float newVolumeNormalized)
    {
        if (musicVolumeSlider != null && !Mathf.Approximately(musicVolumeSlider.value, newVolumeNormalized))
        {
            musicVolumeSlider.value = newVolumeNormalized;
        }
    }

    private void HandleSfxVolumeUpdateFromManager(float newVolumeNormalized)
    {
        if (sfxVolumeSlider != null && !Mathf.Approximately(sfxVolumeSlider.value, newVolumeNormalized))
        {
            sfxVolumeSlider.value = newVolumeNormalized;
        }
    }

    public void OnMenuStateChanged(MenuState newState) { }
    public void OnVolumeChanged(AudioType type, float value) { }
    
    public void OnSceneTransitionStarted(string sceneName)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }
    
    public void OnSceneTransitionCompleted(string sceneName) { }

    #endregion
}