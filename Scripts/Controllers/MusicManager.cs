using UnityEngine;
using System;
using System.Collections;
using AK.Wwise; // Wwise Unity namespace

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Wwise Configuration")]
    [Tooltip("Event Wwise pour démarrer la lecture du Music Switch Container principal (ex: Play_Level_Music).")]
    [SerializeField] private AK.Wwise.Event playMusicEvent;

    [Header("Music Switches (doivent correspondre au groupe 'MusicState' dans Wwise)")]
    public AK.Wwise.Switch explorationSwitch;
    public AK.Wwise.Switch combatSwitch;
    public AK.Wwise.Switch bossSwitch;
    public AK.Wwise.Switch silenceSwitch;
    
    [Tooltip("Switch Wwise pour la musique du menu principal.")]
    public AK.Wwise.Switch mainMenuSwitch;
    [Tooltip("Switch Wwise pour la musique du Hub.")]
    public AK.Wwise.Switch hubSwitch; 
    [Tooltip("Switch Wwise pour l'état de fin de partie (victoire/défaite).")]
    public AK.Wwise.Switch endGameSwitch;

    [Header("Wwise RTPCs")]
    [Tooltip("RTPC pour l'intensité du mode Fever.")]
    [SerializeField] private AK.Wwise.RTPC feverIntensityRTPC;
    
    
    [Header("Settings")]
    [SerializeField] private float minTimeBetweenBeats = 0.1f;
    [SerializeField] private string initialMusicState = "Exploration";

    // --- ÉVÉNEMENTS PUBLICS ---
    /// <summary>
    /// Événement principal déclenché à chaque battement de la musique Wwise.
    /// Fournit la durée exacte de ce battement en secondes.
    /// </summary>
    public event Action<float> OnBeat;
    public event Action<string> OnMusicStateChanged;


    // --- PROPRIÉTÉS PUBLIQUES (POUR COMPATIBILITÉ) ---
    /// <summary>
    /// Propriété indiquant si le dernier battement a été traité. Utile pour les coroutines attendant un beat.
    /// </summary>
    public static bool LastBeatWasProcessed { get; private set; }

    /// <summary>
    /// Propriété publique pour obtenir la durée du battement actuel en secondes.
    /// </summary>
    public float BeatDuration => currentBeatDuration;

    // --- VARIABLES PRIVÉES ---
    private string currentMusicState;
    private uint playingID_MusicEvent = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
    private float currentBeatDuration = 0.5f;
    public int PublicBeatCount { get; private set; } = 0;
    private bool musicPlaying = false;
    private float lastBeatTime;
    private bool beatOccurredThisFrame = false; // Flag interne pour LastBeatWasProcessed

    public string CurrentWwiseMusicState => currentMusicState;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (AkUnitySoundEngine.IsInitialized())
        {
            InitializeMusicAndSetState(initialMusicState);
        }
        else
        {
            Debug.LogError("[MusicManager] Wwise n'est pas initialisé au Start ! La musique ne démarrera pas.");
        }
    }

    private void Update()
    {
        if (AkUnitySoundEngine.IsInitialized())
        {
            AkUnitySoundEngine.RenderAudio();
        }
    }

    // Mis dans LateUpdate pour s'assurer qu'il est réinitialisé après que tous les Update des autres scripts aient eu lieu.
    private void LateUpdate()
    {
        LastBeatWasProcessed = beatOccurredThisFrame;
        beatOccurredThisFrame = false; // Réinitialiser pour la prochaine frame
    }

    private void InitializeMusicAndSetState(string targetState)
    {
        if (!AkUnitySoundEngine.IsInitialized())
        {
            Debug.LogError($"[MusicManager] InitializeMusicAndSetState({targetState}) appelé mais Wwise n'est pas prêt.");
            musicPlaying = false;
            return;
        }

        if (playMusicEvent == null || !playMusicEvent.IsValid())
        {
            Debug.LogError("[MusicManager] 'playMusicEvent' n'est pas assigné ou n'est pas valide !");
            musicPlaying = false;
            return;
        }

        if (playingID_MusicEvent == AkUnitySoundEngine.AK_INVALID_PLAYING_ID)
        {
            playingID_MusicEvent = playMusicEvent.Post(
                gameObject,
                (uint)(AkCallbackType.AK_MusicSyncBeat | AkCallbackType.AK_EnableGetMusicPlayPosition | AkCallbackType.AK_EndOfEvent),
                OnMusicCallback,
                null
            );

            if (playingID_MusicEvent == AkUnitySoundEngine.AK_INVALID_PLAYING_ID)
            {
                Debug.LogError($"[MusicManager] Échec du Post de playMusicEvent '{playMusicEvent.Name}'.");
                musicPlaying = false;
                return;
            }
            Debug.Log($"[MusicManager] Music Event '{playMusicEvent.Name}' NOUVELLEMENT posté. ID: {playingID_MusicEvent}");
        }

        musicPlaying = true;

        string previousLogicalState = currentMusicState;
        currentMusicState = "";
        SetMusicState(targetState, true);
    }


    private void OnMusicCallback(object in_cookie, AkCallbackType in_type, AkCallbackInfo in_info)
    {
        if (!AkUnitySoundEngine.IsInitialized()) return;

        if (in_type == AkCallbackType.AK_MusicSyncBeat)
        {
            if (playingID_MusicEvent != AkUnitySoundEngine.AK_INVALID_PLAYING_ID)
            {
                AkMusicSyncCallbackInfo musicInfo = in_info as AkMusicSyncCallbackInfo;
                if (musicInfo != null && (Time.time - lastBeatTime >= minTimeBetweenBeats || lastBeatTime == 0f))
                {
                    currentBeatDuration = musicInfo.segmentInfo_fBeatDuration;
                    lastBeatTime = Time.time;
                    PublicBeatCount++;
                    
                    // Déclenchement de l'événement statique avec la durée du beat
                    OnBeat?.Invoke(currentBeatDuration);

                    // Mise à jour du flag pour LastBeatWasProcessed
                    beatOccurredThisFrame = true;
                }
            }
        }
        else if (in_type == AkCallbackType.AK_EndOfEvent)
        {
            AkEventCallbackInfo eventInfo = in_info as AkEventCallbackInfo;
            if (eventInfo != null && eventInfo.playingID == playingID_MusicEvent)
            {
                Debug.LogWarning($"[MusicManager OnMusicCallback] AK_EndOfEvent reçu pour playingID {playingID_MusicEvent} ({playMusicEvent?.Name}). musicPlaying mis à false.");
                musicPlaying = false;
                playingID_MusicEvent = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
            }
        }
    }

    public void SetMusicState(string newState, bool immediate = false)
    {
        if (!AkUnitySoundEngine.IsInitialized())
        {
            Debug.LogError($"[MusicManager] SetMusicState({newState}) appelé mais Wwise n'est pas prêt.");
            return;
        }

        string lowerNewState = newState.ToLower();

        if (currentMusicState == newState && musicPlaying && playingID_MusicEvent != AkUnitySoundEngine.AK_INVALID_PLAYING_ID)
        {
            return;
        }

        ApplyMusicSwitch(newState);

        bool needsWwiseEventToPlay = lowerNewState != "silence";

        if (needsWwiseEventToPlay && (!musicPlaying || playingID_MusicEvent == AkUnitySoundEngine.AK_INVALID_PLAYING_ID))
        {
            Debug.Log($"[MusicManager] L'état '{newState}' nécessite que l'événement Wwise joue, et il ne joue pas (ou ID invalide). Appel de InitializeMusicAndSetState pour '{newState}'.");
            InitializeMusicAndSetState(newState);
        }
        else if (lowerNewState == "silence" && musicPlaying && playingID_MusicEvent != AkUnitySoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.Log($"[MusicManager] État cible est 'Silence'. Arrêt de l'event musical ID: {playingID_MusicEvent}.");
            AkUnitySoundEngine.StopPlayingID(playingID_MusicEvent);
            musicPlaying = false;
            playingID_MusicEvent = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
        }

        if (currentMusicState != newState)
        {
            currentMusicState = newState;
            OnMusicStateChanged?.Invoke(newState);
        }
    }

    private void ApplyMusicSwitch(string stateName)
    {
        AK.Wwise.Switch targetSwitch = null;

        switch (stateName.ToLower())
        {
            case "exploration": targetSwitch = explorationSwitch; break;
            case "combat": targetSwitch = combatSwitch; break;
            case "boss": targetSwitch = bossSwitch; break;
            case "silence": targetSwitch = silenceSwitch; break;
            case "mainmenu": targetSwitch = mainMenuSwitch; break;
            case "hub": targetSwitch = hubSwitch; break;
            case "endgame":
                targetSwitch = endGameSwitch;
                if (endGameSwitch == null || !endGameSwitch.IsValid()) {
                    Debug.LogError($"[MusicManager] ApplyMusicSwitch: Switch 'endGameSwitch' non assigné ou invalide pour l'état '{stateName}'.");
                }
                break;
            default:
                Debug.LogWarning($"[MusicManager] ApplyMusicSwitch: État musical Switch Wwise inconnu : '{stateName}'.");
                return;
        }

        if (targetSwitch != null && targetSwitch.IsValid())
        {
            targetSwitch.SetValue(this.gameObject);
        }
        else if (stateName.ToLower() != "endgame")
        {
             Debug.LogError($"[MusicManager] ApplyMusicSwitch: Le Switch Wwise pour l'état '{stateName}' est null ou invalide !");
        }
    }

    // --- MÉTHODES DE COMPATIBILITÉ ---

    /// <summary>
    /// (Déprécié) Tente de définir le BPM. Le BPM réel est maintenant contrôlé par Wwise.
    /// </summary>
    public void SetBPM(float newBPM)
    {
        Debug.LogWarning($"[MusicManager] L'appel à SetBPM({newBPM}) est déprécié. Le BPM est maintenant contrôlé par le segment musical joué dans Wwise.");
        // On ne change rien à la logique interne.
    }

    /// <summary>
    /// Méthode pour retourner la durée d'un battement en secondes.
    /// </summary>
    public float GetBeatDuration()
    {
        return (musicPlaying && playingID_MusicEvent != AkUnitySoundEngine.AK_INVALID_PLAYING_ID) ? currentBeatDuration : 0.5f;
    }
    
    public float GetNextBeatTime() {
        if (!musicPlaying || playingID_MusicEvent == AkUnitySoundEngine.AK_INVALID_PLAYING_ID || currentBeatDuration <= 0)
            return Time.time + 0.5f;
        float timeSinceLastBeat = Time.time - lastBeatTime;
        float timeUntilNextBeat = currentBeatDuration - (timeSinceLastBeat % currentBeatDuration);
        return Time.time + timeUntilNextBeat;
    }

    public float GetTimeUntilNextBeat()
    {
         if (!this.enabled || !Application.isPlaying || !musicPlaying) return float.MaxValue;
         return Mathf.Max(0, GetNextBeatTime() - Time.time);
    }
    
    public float GetBeatProgress() {
        if (!musicPlaying || playingID_MusicEvent == AkUnitySoundEngine.AK_INVALID_PLAYING_ID || currentBeatDuration <= 0) return 0;
        float timeSinceLastBeat = Time.time - lastBeatTime;
        return Mathf.Clamp01((timeSinceLastBeat % currentBeatDuration) / currentBeatDuration);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (playingID_MusicEvent != AkUnitySoundEngine.AK_INVALID_PLAYING_ID && AkUnitySoundEngine.IsInitialized())
        {
            AkUnitySoundEngine.StopPlayingID(playingID_MusicEvent);
            playingID_MusicEvent = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
        }
    }
    
    /// <summary>
    /// Met à jour l'intensité du Mode Fever dans Wwise via un RTPC.
    /// </summary>
    /// <param name="intensity">Valeur d'intensité entre 0 et 100</param>
    public void SetFeverIntensity(float intensity)
    {
        // Clamp la valeur entre 0 et 100 pour sécurité
        intensity = Mathf.Clamp(intensity, 0f, 100f);
        // Vérifier que le paramètre RTPC est configuré
        if (feverIntensityRTPC == null || !feverIntensityRTPC.IsValid())
        {
            Debug.LogWarning("[MusicManager] Le paramètre RTPC pour le Mode Fever n'est pas configuré.");
            return;
        }

        // Appliquer la valeur RTPC à Wwise
        feverIntensityRTPC.SetGlobalValue(intensity);
    
        Debug.Log($"[MusicManager] Intensité Fever mise à jour : {intensity:F1}%");
    }
}