using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Audio Settings")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    
    [Header("Cinematic")]
    [SerializeField] private MenuIntroCinematic introCinematic;

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
        InitializeButtons();
        LoadSettings();
    }

    private void InitializeButtons()
    {
        playButton.onClick.AddListener(OnPlayButtonClicked);
        optionsButton.onClick.AddListener(OnOptionsButtonClicked);
        quitButton.onClick.AddListener(OnQuitButtonClicked);
        backButton.onClick.AddListener(OnBackButtonClicked);

        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
    }

    private void LoadSettings()
    {
        // Charger les paramètres sauvegardés
        musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
        sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
    }

    private void OnPlayButtonClicked()
    {
        // Démarrer la cinématique au lieu de charger directement la scène
        if (introCinematic != null)
        {
            // Cacher l'interface du menu pendant la cinématique
            HideMenuUI();
            
            // Lancer la cinématique
            introCinematic.PlayCinematic();
        }
        else
        {
            // Fallback si la cinématique n'est pas configurée
            Debug.Log("Démarrage du jeu");
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
    }

    private void OnBackButtonClicked()
    {
        optionsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    private void OnQuitButtonClicked()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    private void OnMusicVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("MusicVolume", value);
        // TODO: Mettre à jour le volume de la musique
    }

    private void OnSFXVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("SFXVolume", value);
        // TODO: Mettre à jour le volume des effets sonores
    }
}