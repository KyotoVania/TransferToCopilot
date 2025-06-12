using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BannerUIController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Canvas bannerCanvas;
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backButton;

    [Header("References")]
    [SerializeField] private BannerMenuController bannerController;
    [SerializeField] private MenuStateManager menuStateManager;
    [SerializeField] private MenuCameraManager cameraManager;

    private void Start()
    {
        ValidateReferences();
        InitializeUI();
        SubscribeToMenuStateChanges();
    }

    private void OnDestroy()
    {
        if (menuStateManager != null)
        {
            menuStateManager.RemoveStateChangeListener(OnMenuStateChanged);
        }
    }

    private void ValidateReferences()
    {
        if (bannerController == null)
        {
            Debug.LogError($"[BannerUIController] La référence Banner Controller n'est pas assignée sur {gameObject.name}!");
            return;
        }

        if (menuStateManager == null)
        {
            Debug.LogError($"[BannerUIController] La référence Menu State Manager n'est pas assignée sur {gameObject.name}!");
            return;
        }

        if (cameraManager == null)
        {
            Debug.LogError($"[BannerUIController] La référence Camera Manager n'est pas assignée sur {gameObject.name}!");
            return;
        }

        if (bannerCanvas == null)
        {
            Debug.LogError($"[BannerUIController] La référence Banner Canvas n'est pas assignée sur {gameObject.name}!");
            return;
        }

        if (playButton == null || optionsButton == null || quitButton == null || backButton == null)
        {
            Debug.LogError($"[BannerUIController] Un ou plusieurs boutons ne sont pas assignés sur {gameObject.name}!");
            return;
        }
    }

    private void InitializeUI()
    {
        bannerCanvas.renderMode = RenderMode.WorldSpace;
        
        // Configuration des boutons
        playButton.onClick.AddListener(OnPlayButtonClicked);
        optionsButton.onClick.AddListener(OnOptionsButtonClicked);
        quitButton.onClick.AddListener(OnQuitButtonClicked);
        backButton.onClick.AddListener(OnBackButtonClicked);

        // Ajout des événements de survol
        AddButtonHoverEvents(playButton);
        AddButtonHoverEvents(optionsButton);
        AddButtonHoverEvents(quitButton);
        AddButtonHoverEvents(backButton);

        // Configuration initiale des boutons selon l'état
        UpdateButtonsVisibility(menuStateManager.CurrentState);
    }

    private void SubscribeToMenuStateChanges()
    {
        if (menuStateManager != null)
        {
            menuStateManager.AddStateChangeListener(OnMenuStateChanged);
        }
    }

    private void OnMenuStateChanged(MenuState newState)
    {
        UpdateButtonsVisibility(newState);
    }

    private void UpdateButtonsVisibility(MenuState state)
    {
        switch (state)
        {
            case MenuState.MainMenu:
                playButton.gameObject.SetActive(true);
                optionsButton.gameObject.SetActive(true);
                quitButton.gameObject.SetActive(true);
                backButton.gameObject.SetActive(false);
                break;
            case MenuState.Options:
                playButton.gameObject.SetActive(false);
                optionsButton.gameObject.SetActive(false);
                quitButton.gameObject.SetActive(false);
                backButton.gameObject.SetActive(true);
                break;
        }
    }

    private void AddButtonHoverEvents(Button button)
    {
        if (bannerController == null) return;

        var eventTrigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        
        var entryEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
        entryEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        entryEnter.callback.AddListener((data) => bannerController.OnBannerHoverEnter());
        eventTrigger.triggers.Add(entryEnter);

        var entryExit = new UnityEngine.EventSystems.EventTrigger.Entry();
        entryExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        entryExit.callback.AddListener((data) => bannerController.OnBannerHoverExit());
        eventTrigger.triggers.Add(entryExit);
    }

    private void OnPlayButtonClicked()
    {
        if (bannerController == null) return;
        bannerController.OnBannerClicked();
        // TODO: Implémenter la logique de démarrage du jeu
    }

    private void OnOptionsButtonClicked()
    {
        if (bannerController == null || menuStateManager == null || cameraManager == null) return;
        bannerController.OnBannerClicked();
        menuStateManager.SetState(MenuState.Options);
        cameraManager.TransitionToOptions();
    }

    private void OnBackButtonClicked()
    {
        if (bannerController == null || menuStateManager == null || cameraManager == null) return;
        bannerController.OnBannerClicked();
        menuStateManager.SetState(MenuState.MainMenu);
        cameraManager.TransitionToMainMenu();
    }

    private void OnQuitButtonClicked()
    {
        Debug.Log("start OnQuitButtonClicked BannerUIController");
        if (bannerController == null) return;
        bannerController.OnBannerClicked();
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
} 