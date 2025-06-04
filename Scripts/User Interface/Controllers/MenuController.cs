using UnityEngine;
using UnityEngine.UI; // Nécessaire pour Slider
// using TMPro; // Décommentez si vous utilisez TextMeshPro pour les labels des sliders

// Définition de MenuState si elle n'est pas déjà dans un fichier global d'enums
// public enum MenuState { MainMenu, Options, Loading }

// Définition de AudioType si IMenuObserver.OnVolumeChanged l'utilise encore et qu'il n'est pas global
// public enum AudioType { Music, SFX }


public class MenuController : MonoBehaviour, IMenuObserver // IMenuObserver pour MenuSceneTransitionManager
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

    [Header("Components")]
    [SerializeField] private ButtonAnimator buttonAnimator; // Doit être assigné
    [SerializeField] private LogoAnimator logoAnimator;     // Doit être assigné

    private MenuState currentState = MenuState.MainMenu;
    [SerializeField] private MenuCameraManager menuCameraManager;


    private void Awake()
    {
         if (menuCameraManager == null)
        {
        Debug.LogError("[MenuController] MenuCameraManager non assigné ! Les transitions de caméra ne fonctionneront pas.");
        }
    }

    private void Start()
    {
        InitializeButtonsAndSliderListeners();
        // L'initialisation des valeurs des sliders se fait dans OnEnable pour plus de robustesse
    }

    private void OnEnable()
    {
        // S'abonner aux événements statiques de l'AudioManager
        AudioManager.OnMasterVolumeSettingChanged += HandleMasterVolumeUpdateFromManager;
        AudioManager.OnMusicVolumeSettingChanged += HandleMusicVolumeUpdateFromManager;
        AudioManager.OnSfxVolumeSettingChanged += HandleSfxVolumeUpdateFromManager;

        if (MenuSceneTransitionManager.Instance != null)
        {
            MenuSceneTransitionManager.Instance.AddObserver(this);
        }
        else
        {
            Debug.LogWarning("[MenuController] MenuSceneTransitionManager.Instance est null. Les transitions de scène ne seront pas observées.");
        }

        InitializeSliderValues(); // Mettre à jour les sliders à l'activation
        ChangeMenuState(currentState); // S'assurer que le bon panel est actif
    }

    private void OnDisable()
    {
        // Se désabonner pour éviter les erreurs
        AudioManager.OnMasterVolumeSettingChanged -= HandleMasterVolumeUpdateFromManager;
        AudioManager.OnMusicVolumeSettingChanged -= HandleMusicVolumeUpdateFromManager;
        AudioManager.OnSfxVolumeSettingChanged -= HandleSfxVolumeUpdateFromManager;

        if (MenuSceneTransitionManager.Instance != null)
        {
            MenuSceneTransitionManager.Instance.RemoveObserver(this);
        }
    }

    private void InitializeButtonsAndSliderListeners()
    {
        if (playButton != null) playButton.onClick.AddListener(OnPlayButtonClicked);
        if (optionsButton != null) optionsButton.onClick.AddListener(OnOptionsButtonClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitButtonClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackButtonClicked);

        if (buttonAnimator != null) // S'assurer que buttonAnimator est assigné
        {
            if (playButton != null) AddButtonHoverEvents(playButton);
            if (optionsButton != null) AddButtonHoverEvents(optionsButton);
            if (quitButton != null) AddButtonHoverEvents(quitButton);
            if (backButton != null) AddButtonHoverEvents(backButton);
        } else {
            Debug.LogWarning("[MenuController] ButtonAnimator non assigné. Les effets de survol des boutons ne fonctionneront pas.");
        }


        // Attacher les listeners des sliders pour appeler les méthodes de l'AudioManager
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveAllListeners(); // Nettoyer avant
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
            Debug.Log("[MenuController] Sliders de volume initialisés/mis à jour avec les valeurs de AudioManager.");
        }
        else
        {
            Debug.LogWarning("[MenuController] AudioManager.Instance est null pendant InitializeSliderValues. Les sliders ne seront pas initialisés.");
        }
    }

    private void AddButtonHoverEvents(Button button)
    {
        // Assurez-vous que buttonAnimator est assigné dans l'inspecteur
        if (buttonAnimator == null) return;

        var eventTrigger = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (eventTrigger == null) eventTrigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        // Nettoyer les anciens triggers pour éviter les doublons si AddButtonHoverEvents est appelé plusieurs fois
        eventTrigger.triggers.Clear();

        var entryEnter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((data) => buttonAnimator.OnHoverEnter(button));
        eventTrigger.triggers.Add(entryEnter);

        var entryExit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
        entryExit.callback.AddListener((data) => buttonAnimator.OnHoverExit(button));
        eventTrigger.triggers.Add(entryExit);
    }

    // Méthodes appelées par les sliders de l'UI pour commander l'AudioManager
    private void OnMasterSliderChanged(float value) { AudioManager.Instance?.SetMasterVolume(value); }
    private void OnMusicSliderChanged(float value) { AudioManager.Instance?.SetMusicVolume(value); }
    private void OnSfxSliderChanged(float value) { AudioManager.Instance?.SetSfxVolume(value); }

    // Méthodes appelées par les événements de l'AudioManager pour mettre à jour l'UI (les sliders)
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

    private void OnPlayButtonClicked()
    {
        buttonAnimator?.OnClick(playButton); // Utilisation de l'opérateur conditionnel null
        GameManager.Instance?.LoadHub();
    }

    private void OnOptionsButtonClicked()
    {
        buttonAnimator?.OnClick(optionsButton);
        ChangeMenuState(MenuState.Options); // Change l'état interne et les panels UI
        menuCameraManager?.TransitionToOptions(); // Demande au CameraManager de faire la transition
    }

    private void OnBackButtonClicked()
    {
        buttonAnimator?.OnClick(backButton);
        ChangeMenuState(MenuState.MainMenu); // Change l'état interne et les panels UI
        menuCameraManager?.TransitionToMainMenu(); // Demande au CameraManager de faire la transition
    }

    private void OnQuitButtonClicked()
    {
        buttonAnimator?.OnClick(quitButton);
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    private void ChangeMenuState(MenuState newState)
    {
        currentState = newState;
        if (mainMenuPanel != null) mainMenuPanel.SetActive(newState == MenuState.MainMenu);
        if (optionsPanel != null) optionsPanel.SetActive(newState == MenuState.Options);

        // CORRECTION : Utiliser menuCameraManager (sans l'underscore)
        if (menuCameraManager != null)
        {
            if (newState == MenuState.Options) menuCameraManager.TransitionToOptions();
            else if (newState == MenuState.MainMenu) menuCameraManager.TransitionToMainMenu();
        }
    }

    // --- Implémentation de IMenuObserver ---
    public void OnMenuStateChanged(MenuState newState)
    {
        // Cette méthode serait appelée si MenuController observait un autre MenuStateManager.
        // Pour l'instant, il gère son propre état.
        // ChangeMenuState(newState);
    }

    // Cette méthode fait partie de IMenuObserver.
    // Elle ne sera pas appelée par le nouveau AudioManager pour les changements de volume des sliders.
    public void OnVolumeChanged(AudioType type, float value)
    {
        Debug.LogWarning($"[MenuController] IMenuObserver.OnVolumeChanged(AudioType, float) a été appelée. Ce n'est pas la méthode utilisée par le nouveau système de volume AudioManager. Type: {type}, Valeur: {value}");
    }

    public void OnSceneTransitionStarted(string sceneName)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        Debug.Log($"[MenuController] Transition vers la scène '{sceneName}' démarrée.");
        // Ici, tu pourrais activer un Panel "Chargement..." si tu en as un dans cette scène de menu.
    }

    public void OnSceneTransitionCompleted(string sceneName)
    {
        Debug.Log($"[MenuController] Transition vers la scène '{sceneName}' complétée.");
        // Si tu avais activé un Panel "Chargement...", tu le désactiverais ici.
        // Note: si cette scène Menu est déchargée, ce OnSceneTransitionCompleted ne sera pas appelé ici.
        // Il serait appelé sur un observateur qui persiste ou qui est dans la nouvelle scène.
    }
}