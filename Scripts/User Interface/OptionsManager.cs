using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Gestionnaire dédié pour le panneau d'options du jeu.
/// Gère les paramètres : Sound FX, Music, Vibration
/// Fait le lien entre l'UI, PlayerDataManager et AudioManager
/// </summary>
public class OptionsManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Button vibrationButton;
    [SerializeField] private Button beatIndicatorButton; // Ajout de la référence UI
    [SerializeField] private Button backButton;

    [Header("Vibration Button Children")]
    [SerializeField] private GameObject vibrationOnChild;
    [SerializeField] private GameObject vibrationOffChild;

    [Header("Beat Indicator Button Children")] // Ajout des enfants pour le nouveau bouton
    [SerializeField] private GameObject beatIndicatorOnChild;
    [SerializeField] private GameObject beatIndicatorOffChild;

    [Header("Labels")]
    [SerializeField] private TextMeshProUGUI musicVolumeLabel;
    [SerializeField] private TextMeshProUGUI sfxVolumeLabel;
    [SerializeField] private TextMeshProUGUI vibrationLabel;
    [SerializeField] private TextMeshProUGUI beatIndicatorLabel; // Ajout du label

    [Header("Controller Navigation")]
    [SerializeField] private float buttonScaleMultiplier = 1.1f;
    [SerializeField] private float animationSpeed = 0.2f;

    // État de navigation
    private Selectable[] optionsSelectables;
    private GameObject lastSelectedObject;
    private bool isInitialized = false;

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeUI();
        SetupNavigation();
    }

    private void OnEnable()
    {
        // S'abonner aux événements du PlayerDataManager
        PlayerDataManager.OnMusicVolumeChanged += OnMusicVolumeChanged;
        PlayerDataManager.OnSfxVolumeChanged += OnSfxVolumeChanged;
        PlayerDataManager.OnVibrationChanged += OnVibrationChanged;
        PlayerDataManager.OnShowBeatIndicatorChanged += OnShowBeatIndicatorChanged; // S'abonner au nouvel événement

        if (isInitialized)
        {
            LoadCurrentSettings();
            SetInitialSelection();
        }
    }

    private void OnDisable()
    {
        // Se désabonner des événements
        PlayerDataManager.OnMusicVolumeChanged -= OnMusicVolumeChanged;
        PlayerDataManager.OnSfxVolumeChanged -= OnSfxVolumeChanged;
        PlayerDataManager.OnVibrationChanged -= OnVibrationChanged;
        PlayerDataManager.OnShowBeatIndicatorChanged -= OnShowBeatIndicatorChanged; // Se désabonner du nouvel événement
    }

    private void Update()
    {
        HandleControllerNavigation();
        HandleVisualFeedback();
    }

    #endregion

    #region Initialization

    private void InitializeUI()
    {
        // Configurer les listeners
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicSliderChanged);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxSliderChanged);
        if (vibrationButton != null) vibrationButton.onClick.AddListener(OnVibrationButtonClicked);
        if (beatIndicatorButton != null) beatIndicatorButton.onClick.AddListener(OnBeatIndicatorButtonClicked); // Ajouter le listener
        if (backButton != null) backButton.onClick.AddListener(OnBackButtonClicked);
        
        LoadCurrentSettings();
        isInitialized = true;
        Debug.Log("[OptionsManager] Interface utilisateur initialisée");
    }

    private void LoadCurrentSettings()
    {
        if (PlayerDataManager.Instance != null)
        {
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.SetValueWithoutNotify(PlayerDataManager.Instance.GetMusicVolume());
                UpdateMusicLabel(PlayerDataManager.Instance.GetMusicVolume());
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.SetValueWithoutNotify(PlayerDataManager.Instance.GetSfxVolume());
                UpdateSfxLabel(PlayerDataManager.Instance.GetSfxVolume());
            }

            if (vibrationButton != null)
            {
                UpdateVibrationButton(PlayerDataManager.Instance.IsVibrationEnabled());
                UpdateVibrationLabel(PlayerDataManager.Instance.IsVibrationEnabled());
            }
            
            // Mettre à jour l'état du nouveau bouton
            if (beatIndicatorButton != null)
            {
                UpdateBeatIndicatorButton(PlayerDataManager.Instance.IsShowBeatIndicatorEnabled());
                UpdateBeatIndicatorLabel(PlayerDataManager.Instance.IsShowBeatIndicatorEnabled());
            }
        }
    }

    private void SetupNavigation()
    {
        // Mettre à jour la navigation
        optionsSelectables = new Selectable[] { musicVolumeSlider, sfxVolumeSlider, vibrationButton, beatIndicatorButton, backButton };

        // Configurer la navigation explicite
        SetupExplicitNavigation(musicVolumeSlider, null, sfxVolumeSlider, null, null);
        SetupExplicitNavigation(sfxVolumeSlider, musicVolumeSlider, vibrationButton, null, null);
        SetupExplicitNavigation(vibrationButton, sfxVolumeSlider, beatIndicatorButton, null, null);
        SetupExplicitNavigation(beatIndicatorButton, vibrationButton, backButton, null, null); // Navigation pour le nouveau bouton
        SetupExplicitNavigation(backButton, beatIndicatorButton, null, null, null); // Mettre à jour le bouton retour

        Debug.Log("[OptionsManager] Navigation configurée pour les options");
    }

    private void SetupExplicitNavigation(Selectable selectable, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        if (selectable == null) return;

        Navigation nav = selectable.navigation;
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnUp = up;
        nav.selectOnDown = down;
        nav.selectOnLeft = left;
        nav.selectOnRight = right;
        selectable.navigation = nav;
    }

    #endregion

    #region UI Event Handlers

    private void OnMusicSliderChanged(float value)
    {
        PlayerDataManager.Instance?.SetMusicVolume(value);
        UpdateMusicLabel(value);
    }

    private void OnSfxSliderChanged(float value)
    {
        PlayerDataManager.Instance?.SetSfxVolume(value);
        UpdateSfxLabel(value);
    }

    private void OnVibrationButtonClicked()
    {
        if (PlayerDataManager.Instance == null) return;
        bool next = !PlayerDataManager.Instance.IsVibrationEnabled();
        PlayerDataManager.Instance.SetVibrationEnabled(next);
        UpdateVibrationButton(next);
        UpdateVibrationLabel(next);
    }
    
    // Handler pour le nouveau bouton
    private void OnBeatIndicatorButtonClicked()
    {
        if (PlayerDataManager.Instance == null) return;
        bool next = !PlayerDataManager.Instance.IsShowBeatIndicatorEnabled();
        PlayerDataManager.Instance.SetShowBeatIndicator(next);
        UpdateBeatIndicatorButton(next);
        UpdateBeatIndicatorLabel(next);
    }

    private void OnBackButtonClicked()
    {
        if (MenuManager.Instance != null)
        {
            optionsPanel.SetActive(false);
            MenuManager.Instance.transform.Find("MainMenuPanel")?.gameObject.SetActive(true);
        }
    }
    
    private void UpdateVibrationButton(bool enabled)
    {
        if (vibrationOnChild != null) vibrationOnChild.SetActive(enabled);
        if (vibrationOffChild != null) vibrationOffChild.SetActive(!enabled);
    }

    // Méthode pour mettre à jour l'affichage du nouveau bouton
    private void UpdateBeatIndicatorButton(bool enabled)
    {
        if (beatIndicatorOnChild != null) beatIndicatorOnChild.SetActive(enabled);
        if (beatIndicatorOffChild != null) beatIndicatorOffChild.SetActive(!enabled);
    }

    #endregion

    #region PlayerDataManager Event Handlers

    private void OnMusicVolumeChanged(float volume)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(volume);
        if (musicVolumeSlider != null && !Mathf.Approximately(musicVolumeSlider.value, volume))
        {
            musicVolumeSlider.SetValueWithoutNotify(volume);
        }
        UpdateMusicLabel(volume);
    }

    private void OnSfxVolumeChanged(float volume)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetSfxVolume(volume);
        if (sfxVolumeSlider != null && !Mathf.Approximately(sfxVolumeSlider.value, volume))
        {
            sfxVolumeSlider.SetValueWithoutNotify(volume);
        }
        UpdateSfxLabel(volume);
    }

    private void OnVibrationChanged(bool enabled)
    {
        if (vibrationButton != null) UpdateVibrationButton(enabled);
        UpdateVibrationLabel(enabled);
        Debug.Log($"[OptionsManager] Vibrations {(enabled ? "activées" : "désactivées")}");
    }

    // Handler pour l'événement du nouveau bouton
    private void OnShowBeatIndicatorChanged(bool enabled)
    {
        if (beatIndicatorButton != null) UpdateBeatIndicatorButton(enabled);
        UpdateBeatIndicatorLabel(enabled);
        Debug.Log($"[OptionsManager] Indicateur de beat {(enabled ? "activé" : "désactivé")}");
    }

    #endregion

    #region UI Updates

    private void UpdateMusicLabel(float volume)
    {
        if (musicVolumeLabel != null) musicVolumeLabel.text = $"Musique: {volume * 100:F0}%";
    }

    private void UpdateSfxLabel(float volume)
    {
        if (sfxVolumeLabel != null) sfxVolumeLabel.text = $"Effets: {volume * 100:F0}%";
    }

    private void UpdateVibrationLabel(bool enabled)
    {
        if (vibrationLabel != null) vibrationLabel.text = $"Vibrations: {(enabled ? "ON" : "OFF")}";
    }

    // Méthode pour mettre à jour le label du nouveau bouton
    private void UpdateBeatIndicatorLabel(bool enabled)
    {
        if (beatIndicatorLabel != null) beatIndicatorLabel.text = $"Indicateur Beat: {(enabled ? "ON" : "OFF")}";
    }

    #endregion

    #region Controller Navigation

    private void HandleControllerNavigation()
    {
        if (InputManager.Instance == null) return;

        if (InputManager.Instance.UIActions.Cancel.WasPressedThisFrame())
        {
            OnBackButtonClicked();
        }

        Vector2 navigationInput = InputManager.Instance.UIActions.Navigate.ReadValue<Vector2>();
        bool submitPressed = InputManager.Instance.UIActions.Submit.WasPressedThisFrame();

        if (navigationInput != Vector2.zero || submitPressed)
        {
            if (EventSystem.current.currentSelectedGameObject == null)
            {
                SetInitialSelection();
            }
        }
    }

    public void SetInitialSelection()
    {
        StartCoroutine(DelayedInitialSelection());
    }

    private IEnumerator DelayedInitialSelection()
    {
        yield return null;
        EventSystem.current.SetSelectedGameObject(null);
        yield return null;

        Selectable firstSelectable = optionsPanel.GetComponentInChildren<Selectable>(false);
        if (firstSelectable != null && firstSelectable.interactable)
        {
            firstSelectable.Select();
            EventSystem.current.SetSelectedGameObject(firstSelectable.gameObject);
            Debug.Log($"[OptionsManager] Sélection initiale: {firstSelectable.name}");
        }
    }

    private void HandleVisualFeedback()
    {
        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        if (currentSelected != null && currentSelected != lastSelectedObject)
        {
            if (lastSelectedObject != null) AnimateSelectable(lastSelectedObject, Vector3.one, false);
            AnimateSelectable(currentSelected, Vector3.one * buttonScaleMultiplier, true);
            lastSelectedObject = currentSelected;
        }
    }

    private void AnimateSelectable(GameObject selectableObject, Vector3 targetScale, bool isSelected)
    {
        if (selectableObject == null) return;
        LeanTween.cancel(selectableObject);
        LeanTween.scale(selectableObject, targetScale, animationSpeed).setEaseOutBack();
    }

    #endregion

    #region Public Methods

    public void ShowOptions()
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
            LoadCurrentSettings();
            SetInitialSelection();
        }
    }

    public void HideOptions()
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);
        }
    }

    [ContextMenu("Reset to Default Settings")]
    public void ResetToDefaults()
    {
        PlayerDataManager.Instance?.SetMusicVolume(0.7f);
        PlayerDataManager.Instance?.SetSfxVolume(0.75f);
        PlayerDataManager.Instance?.SetVibrationEnabled(true);
        PlayerDataManager.Instance?.SetShowBeatIndicator(false); // Réinitialiser la nouvelle option

        Debug.Log("[OptionsManager] Paramètres réinitialisés aux valeurs par défaut");
    }

    #endregion
}