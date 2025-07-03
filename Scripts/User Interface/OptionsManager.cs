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
    // Remplacer le Toggle par un Button
    [SerializeField] private Button vibrationButton;
    [SerializeField] private Button backButton;

    [Header("Vibration Button Children")]
    // Les deux enfants du bouton (ON et OFF)
    [SerializeField] private GameObject vibrationOnChild;
    [SerializeField] private GameObject vibrationOffChild;

    [Header("Labels")]
    [SerializeField] private TextMeshProUGUI musicVolumeLabel;
    [SerializeField] private TextMeshProUGUI sfxVolumeLabel;
    [SerializeField] private TextMeshProUGUI vibrationLabel;

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

        if (isInitialized)
        {
            LoadCurrentSettings();
            SetInitialSelection(); // CORRECTION: Pas de StartCoroutine
        }
    }

    private void OnDisable()
    {
        // Se désabonner des événements
        PlayerDataManager.OnMusicVolumeChanged -= OnMusicVolumeChanged;
        PlayerDataManager.OnSfxVolumeChanged -= OnSfxVolumeChanged;
        PlayerDataManager.OnVibrationChanged -= OnVibrationChanged;
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
        // Configurer les listeners des sliders et toggle
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(OnMusicSliderChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxSliderChanged);
        }

        if (vibrationButton != null)
        {
            vibrationButton.onClick.AddListener(OnVibrationButtonClicked);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackButtonClicked);
        }

        // Charger les paramètres actuels
        LoadCurrentSettings();

        isInitialized = true;
        Debug.Log("[OptionsManager] Interface utilisateur initialisée");
    }

    private void LoadCurrentSettings()
    {
        if (PlayerDataManager.Instance != null)
        {
            // Charger les valeurs sans déclencher les événements
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

            // Mettre à jour l'état du bouton vibration
            if (vibrationButton != null)
            {
                UpdateVibrationButton(PlayerDataManager.Instance.IsVibrationEnabled());
                UpdateVibrationLabel(PlayerDataManager.Instance.IsVibrationEnabled());
            }
        }
    }

    private void SetupNavigation()
    {
        // Mettre à jour la navigation pour utiliser le bouton vibration
        optionsSelectables = new Selectable[] { musicVolumeSlider, sfxVolumeSlider, vibrationButton, backButton };

        // Configurer la navigation explicite
        SetupExplicitNavigation(musicVolumeSlider, null, sfxVolumeSlider, null, null);
        SetupExplicitNavigation(sfxVolumeSlider, musicVolumeSlider, vibrationButton, null, null);
        SetupExplicitNavigation(vibrationButton, sfxVolumeSlider, backButton, null, null);
        SetupExplicitNavigation(backButton, vibrationButton, null, null, null);

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
        // Mettre à jour le PlayerDataManager qui notifiera l'AudioManager
        PlayerDataManager.Instance?.SetMusicVolume(value);

        // Mettre à jour le label immédiatement
        UpdateMusicLabel(value);
    }

    private void OnSfxSliderChanged(float value)
    {
        // Mettre à jour le PlayerDataManager qui notifiera l'AudioManager
        PlayerDataManager.Instance?.SetSfxVolume(value);

        // Mettre à jour le label immédiatement
        UpdateSfxLabel(value);
    }

    // Nouveau handler pour le bouton vibration
    private void OnVibrationButtonClicked()
    {
        if (PlayerDataManager.Instance == null) return;
        bool current = PlayerDataManager.Instance.IsVibrationEnabled();
        bool next = !current;
        PlayerDataManager.Instance.SetVibrationEnabled(next);
        UpdateVibrationButton(next);
        UpdateVibrationLabel(next);
    }

    private void OnBackButtonClicked()
    {
        // Fermer le panneau d'options
        if (MenuManager.Instance != null)
        {
            optionsPanel.SetActive(false);
            MenuManager.Instance.transform.Find("MainMenuPanel")?.gameObject.SetActive(true);
        }
    }

    // Met à jour l'affichage des enfants du bouton selon l'état
    private void UpdateVibrationButton(bool enabled)
    {
        if (vibrationOnChild != null) vibrationOnChild.SetActive(enabled);
        if (vibrationOffChild != null) vibrationOffChild.SetActive(!enabled);
    }

    #endregion

    #region PlayerDataManager Event Handlers

    private void OnMusicVolumeChanged(float volume)
    {
        // Synchroniser l'AudioManager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(volume);
        }

        // Mettre à jour l'UI si nécessaire
        if (musicVolumeSlider != null && !Mathf.Approximately(musicVolumeSlider.value, volume))
        {
            musicVolumeSlider.SetValueWithoutNotify(volume);
        }

        UpdateMusicLabel(volume);
    }

    private void OnSfxVolumeChanged(float volume)
    {
        // Synchroniser l'AudioManager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSfxVolume(volume);
        }

        // Mettre à jour l'UI si nécessaire
        if (sfxVolumeSlider != null && !Mathf.Approximately(sfxVolumeSlider.value, volume))
        {
            sfxVolumeSlider.SetValueWithoutNotify(volume);
        }

        UpdateSfxLabel(volume);
    }

    private void OnVibrationChanged(bool enabled)
    {
        // Ici vous pouvez ajouter la logique pour gérer les vibrations
        // Par exemple, configurer le système de vibration de Unity ou d'un package tiers

        // Mettre à jour l'UI si nécessaire
        if (vibrationButton != null)
        {
            UpdateVibrationButton(enabled);
        }
        UpdateVibrationLabel(enabled);

        Debug.Log($"[OptionsManager] Vibrations {(enabled ? "activées" : "désactivées")}");
    }

    #endregion

    #region UI Updates

    private void UpdateMusicLabel(float volume)
    {
        if (musicVolumeLabel != null)
        {
            musicVolumeLabel.text = $"Musique: {volume * 100:F0}%";
        }
    }

    private void UpdateSfxLabel(float volume)
    {
        if (sfxVolumeLabel != null)
        {
            sfxVolumeLabel.text = $"Effets: {volume * 100:F0}%";
        }
    }

    private void UpdateVibrationLabel(bool enabled)
    {
        if (vibrationLabel != null)
        {
            vibrationLabel.text = $"Vibrations: {(enabled ? "ON" : "OFF")}";
        }
    }

    #endregion

    #region Controller Navigation

    private void HandleControllerNavigation()
    {
        if (InputManager.Instance == null) return;

        // Gérer l'action Cancel (retour)
        if (InputManager.Instance.UIActions.Cancel.WasPressedThisFrame())
        {
            OnBackButtonClicked();
        }

        // Détecter si on utilise la manette pour réactiver la sélection
        Vector2 navigationInput = InputManager.Instance.UIActions.Navigate.ReadValue<Vector2>();
        bool submitPressed = InputManager.Instance.UIActions.Submit.WasPressedThisFrame();

        if (navigationInput != Vector2.zero || submitPressed)
        {
            if (EventSystem.current.currentSelectedGameObject == null)
            {
                SetInitialSelection(); // CORRECTION: Pas de StartCoroutine
            }
        }
    }

    public void SetInitialSelection()
    {
        StartCoroutine(DelayedInitialSelection());
    }

    private IEnumerator DelayedInitialSelection()
    {
        // Attendre une frame pour que tout soit initialisé
        yield return null;
    
        // Désélectionner d'abord tout
        EventSystem.current.SetSelectedGameObject(null);
    
        yield return null;
    
        // Sélectionner le premier élément interactif du menu options
        // Cela pourrait être le premier slider, toggle ou le bouton Back
        Selectable firstSelectable = null;
    
        // Chercher d'abord dans les sliders (volume musique, volume SFX, etc.)
        Slider[] sliders = optionsPanel.GetComponentsInChildren<Slider>(false);
        if (sliders.Length > 0)
        {
            firstSelectable = sliders[0];
        }
    
        // Si pas de slider, chercher les toggles
        if (firstSelectable == null)
        {
            Toggle[] toggles = optionsPanel.GetComponentsInChildren<Toggle>(false);
            if (toggles.Length > 0)
            {
                firstSelectable = toggles[0];
            }
        }
    
        // Si toujours rien, prendre le bouton Back
        if (firstSelectable == null)
        {
            Button[] buttons = optionsPanel.GetComponentsInChildren<Button>(false);
            if (buttons.Length > 0)
            {
                firstSelectable = buttons[0];
            }
        }
    
        // Sélectionner l'élément trouvé
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

        // Animer l'élément actuellement sélectionné
        if (currentSelected != null && currentSelected != lastSelectedObject)
        {
            // Remettre l'ancien élément à sa taille normale
            if (lastSelectedObject != null)
            {
                AnimateSelectable(lastSelectedObject, Vector3.one, false);
            }

            // Agrandir le nouvel élément sélectionné
            AnimateSelectable(currentSelected, Vector3.one * buttonScaleMultiplier, true);

            lastSelectedObject = currentSelected;
        }
    }

    private void AnimateSelectable(GameObject selectableObject, Vector3 targetScale, bool isSelected)
    {
        if (selectableObject == null) return;

        // Arrêter toute animation en cours
        LeanTween.cancel(selectableObject);

        // Animer vers la nouvelle échelle
        LeanTween.scale(selectableObject, targetScale, animationSpeed)
            .setEaseOutBack();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Ouvre le panneau d'options et configure la navigation
    /// </summary>
    public void ShowOptions()
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
            LoadCurrentSettings();
            SetInitialSelection(); // CORRECTION: Pas de StartCoroutine
        }
    }

    /// <summary>
    /// Ferme le panneau d'options
    /// </summary>
    public void HideOptions()
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Réinitialise tous les paramètres aux valeurs par défaut
    /// </summary>
    [ContextMenu("Reset to Default Settings")]
    public void ResetToDefaults()
    {
        PlayerDataManager.Instance?.SetMusicVolume(0.7f);
        PlayerDataManager.Instance?.SetSfxVolume(0.75f);
        PlayerDataManager.Instance?.SetVibrationEnabled(true);

        Debug.Log("[OptionsManager] Paramètres réinitialisés aux valeurs par défaut");
    }

    #endregion
}