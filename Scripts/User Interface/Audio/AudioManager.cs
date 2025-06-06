using UnityEngine;
using System.Collections.Generic;
using AK.Wwise; // Nécessaire pour les types Wwise



public class AudioManager : SingletonPersistent<AudioManager>
{
    [Header("Wwise SoundBanks")]
    [Tooltip("Liste de TOUTES les SoundBanks Wwise à charger au démarrage du jeu.")]
    [SerializeField] private List<Bank> allSoundBanksToLoad = new List<Bank>();
        

    [Header("Wwise RTPCs - Volumes")]
    [SerializeField] private RTPC masterVolumeRTPC;
    [SerializeField] private RTPC musicVolumeRTPC;
    [SerializeField] private RTPC sfxVolumeRTPC;

    [Header("Wwise States - Game States")]
    [SerializeField] private State gameStateBoot;
    [SerializeField] private State gameStateMainMenu;
    [SerializeField] private State gameStateHub;
    [SerializeField] private State gameStateLoading;
    [SerializeField] private State gameStateInLevel;

    [Header("Volume Settings - PlayerPrefs Keys")]
    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";

    [Header("Default Volume Values (0.0 to 1.0)")]
    [Range(0f, 1f)] public float defaultMasterVolume = 0.8f;
    [Range(0f, 1f)] public float defaultMusicVolume = 0.7f;
    [Range(0f, 1f)] public float defaultSfxVolume = 0.75f;

    public float MasterVolume { get; private set; }
    public float MusicVolume { get; private set; }
    public float SfxVolume { get; private set; }

    // Événements statiques pour notifier les changements de volume (valeurs normalisées 0-1)
    public static event System.Action<float> OnMasterVolumeSettingChanged;
    public static event System.Action<float> OnMusicVolumeSettingChanged;
    public static event System.Action<float> OnSfxVolumeSettingChanged;

    protected override void Awake()
    {
        base.Awake();
        LoadVolumesFromPlayerPrefs();
        LoadGlobalSoundBanks();
        Debug.Log("[AudioManager] Awake: Volumes chargés, banques en cours de chargement.");
    }

    private void Start()
    {
        // Appliquer les volumes initiaux aux RTPC Wwise
        ApplyInitialRTPCValues();

        // Notifier les valeurs initiales pour que les UI puissent s'initialiser
        NotifyInitialVolumeSettings();

        if (GameManager.Instance != null)
        {
            GameManager.OnGameStateChanged += HandleGameStateChanged;
            HandleGameStateChanged(GameManager.Instance.CurrentState); // Appliquer l'état initial
            Debug.Log($"[AudioManager] Start: Abonné à GameManager.OnGameStateChanged. État Wwise initial pour {GameManager.Instance.CurrentState} appliqué.");
        }
        else
        {
            Debug.LogError("[AudioManager] GameManager.Instance est null au moment de Start. Impossible de s'abonner ou de définir l'état Wwise initial.");
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
        }
        UnloadGlobalSoundBanks();
        Debug.Log("[AudioManager] OnDestroy: Désabonné et banques déchargées.");
    }

    private void LoadVolumesFromPlayerPrefs()
    {
        MasterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, defaultMasterVolume);
        MusicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, defaultMusicVolume);
        SfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, defaultSfxVolume);
    }

    private void ApplyInitialRTPCValues()
    {
        SetWwiseRTPCValue(masterVolumeRTPC, MasterVolume * 100f);
        SetWwiseRTPCValue(musicVolumeRTPC, MusicVolume * 100f);
        SetWwiseRTPCValue(sfxVolumeRTPC, SfxVolume * 100f);
    }

    private void NotifyInitialVolumeSettings()
    {
        OnMasterVolumeSettingChanged?.Invoke(MasterVolume);
        OnMusicVolumeSettingChanged?.Invoke(MusicVolume);
        OnSfxVolumeSettingChanged?.Invoke(SfxVolume);
    }

    public void SetMasterVolume(float volumeNormalized)
    {
        volumeNormalized = Mathf.Clamp01(volumeNormalized);
        // Notifier seulement si la valeur a réellement changé pour éviter des updates inutiles
        if (!Mathf.Approximately(MasterVolume, volumeNormalized))
        {
            MasterVolume = volumeNormalized;
            PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, MasterVolume);
            SetWwiseRTPCValue(masterVolumeRTPC, MasterVolume * 100f);
            OnMasterVolumeSettingChanged?.Invoke(MasterVolume);
            Debug.Log($"[AudioManager] Master Volume réglé et notifié : {MasterVolume * 100:F0}%");
        }
    }

    public void SetMusicVolume(float volumeNormalized)
    {
        volumeNormalized = Mathf.Clamp01(volumeNormalized);
        if (!Mathf.Approximately(MusicVolume, volumeNormalized))
        {
            MusicVolume = volumeNormalized;
            PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, MusicVolume);
            SetWwiseRTPCValue(musicVolumeRTPC, MusicVolume * 100f);
            OnMusicVolumeSettingChanged?.Invoke(MusicVolume);
            Debug.Log($"[AudioManager] Music Volume réglé et notifié : {MusicVolume * 100:F0}%");
        }
    }

    public void SetSfxVolume(float volumeNormalized)
    {
        volumeNormalized = Mathf.Clamp01(volumeNormalized);
        if (!Mathf.Approximately(SfxVolume, volumeNormalized))
        {
            SfxVolume = volumeNormalized;
            PlayerPrefs.SetFloat(SFX_VOLUME_KEY, SfxVolume);
            SetWwiseRTPCValue(sfxVolumeRTPC, SfxVolume * 100f);
            OnSfxVolumeSettingChanged?.Invoke(SfxVolume);
            Debug.Log($"[AudioManager] SFX Volume réglé et notifié : {SfxVolume * 100:F0}%");
        }
    }

    private void SetWwiseRTPCValue(RTPC rtpc, float value)
    {
        if (rtpc != null && rtpc.IsValid())
        {
            rtpc.SetGlobalValue(value);
        }
        else if (rtpc != null && !string.IsNullOrEmpty(rtpc.Name))
        {
            Debug.LogWarning($"[AudioManager] RTPC Wwise '{rtpc.Name}' assigné mais non valide. Tentative de SetGlobalValue par nom (string).");
            AkUnitySoundEngine.SetRTPCValue(rtpc.Name, value, null); // null = Global GameObject
            
        }
        else
        {
            Debug.LogWarning($"[AudioManager] Tentative de régler un RTPC Wwise non défini ou invalide.");
        }
    }

        private void LoadGlobalSoundBanks()
    {
        if (allSoundBanksToLoad == null || allSoundBanksToLoad.Count == 0)
        {
            Debug.Log("[AudioManager] Aucune SoundBank à charger.");
            return;
        }
        Debug.Log("[AudioManager] Chargement de toutes les SoundBanks assignées...");
        foreach (var bank in allSoundBanksToLoad)
        {
            if (bank != null && bank.IsValid())
            {
                bank.LoadAsync(BankLoadCallback); // Ou bank.Load() si synchrone est ok
                Debug.Log($"[AudioManager] Chargement de la SoundBank : {bank.Name}");
            }
            else
            {
                Debug.LogWarning($"[AudioManager] Une SoundBank dans la liste n'est pas valide ou assignée.");
            }
        }
    }

    private void BankLoadCallback(uint bankID, System.IntPtr memoryAddress, AKRESULT result, object cookie)
    {
        string bankName = "Inconnue (ID: " + bankID + ")";
        // Pour retrouver le nom, il faudrait une structure qui mappe ID et nom,
        // ou passer le nom via `cookie` si `LoadAsync` le permettait directement (ce n'est pas le cas simplement).
        // Pour l'instant, on se contente de l'ID.
        if (result == AKRESULT.AK_Success)
        {
            Debug.Log($"[AudioManager] SoundBank {bankName} chargée avec succès.");
        }
        else
        {
            Debug.LogError($"[AudioManager] Échec du chargement de la SoundBank {bankName}. Erreur: {result}");
        }
    }

   private void UnloadGlobalSoundBanks()
    {
        if (allSoundBanksToLoad == null || allSoundBanksToLoad.Count == 0) return;
        Debug.Log("[AudioManager] Déchargement de toutes les SoundBanks...");
        foreach (var bank in allSoundBanksToLoad)
        {
            if (bank != null && bank.IsValid()) // Vérifier aussi si la banque est chargée avant de décharger
            {
                // Pour savoir si elle est chargée, il faudrait potentiellement un suivi plus fin
                // ou se fier à Wwise qui gère les déchargements multiples sans erreur.
                bank.Unload();
                Debug.Log($"[AudioManager] Déchargement de la SoundBank : {bank.Name}");
            }
        }
    }

    private void HandleGameStateChanged(GameState newState)
    {
        Debug.Log($"[AudioManager] Changement de GameState détecté : {newState}. Application de l'état Wwise correspondant.");
        State targetWwiseState = null;
        string stateNameForLog = "Non défini";

        switch (newState)
        {
            case GameState.Boot: targetWwiseState = gameStateBoot; stateNameForLog = gameStateBoot?.Name ?? "Boot (non assigné)"; break;
            case GameState.MainMenu: targetWwiseState = gameStateMainMenu; stateNameForLog = gameStateMainMenu?.Name ?? "MainMenu (non assigné)"; break;
            case GameState.Hub: targetWwiseState = gameStateHub; stateNameForLog = gameStateHub?.Name ?? "Hub (non assigné)"; break;
            case GameState.Loading: targetWwiseState = gameStateLoading; stateNameForLog = gameStateLoading?.Name ?? "Loading (non assigné)"; break;
            case GameState.InLevel: targetWwiseState = gameStateInLevel; stateNameForLog = gameStateInLevel?.Name ?? "InLevel (non assigné)"; break;
            default: Debug.LogWarning($"[AudioManager] Aucun état Wwise mappé pour GameState: {newState}"); break;
        }

        if (targetWwiseState != null && targetWwiseState.IsValid())
        {
            targetWwiseState.SetValue();
            Debug.Log($"[AudioManager] État Wwise '{targetWwiseState.Name}' appliqué.");
        }
        else
        {
            Debug.LogWarning($"[AudioManager] État Wwise pour {newState} (devrait être '{stateNameForLog}') non assigné ou invalide dans l'inspecteur.");
        }
    }
}