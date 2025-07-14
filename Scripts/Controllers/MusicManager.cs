using UnityEngine;
using System;
using System.Collections;
using AK.Wwise;

/// <summary>
/// Singleton manager for music and audio control using Wwise.
/// Handles music state transitions, beat synchronization, and audio events throughout the game.
/// </summary>
public class MusicManager : MonoBehaviour
{
    /// <summary>
    /// Gets the singleton instance of the MusicManager.
    /// </summary>
    public static MusicManager Instance { get; private set; }

    [Header("Wwise Configuration")]
    /// <summary>
    /// Wwise event for starting playback of the main Music Switch Container.
    /// </summary>
    [Tooltip("Event Wwise pour démarrer la lecture du Music Switch Container principal (ex: Play_Level_Music).")]
    [SerializeField] private AK.Wwise.Event playMusicEvent;

    [Header("Music Switches (must match 'MusicState' group in Wwise)")]
    /// <summary>
    /// Wwise switch for exploration music state.
    /// </summary>
    public AK.Wwise.Switch explorationSwitch;
    
    /// <summary>
    /// Wwise switch for combat music state.
    /// </summary>
    public AK.Wwise.Switch combatSwitch;
    
    /// <summary>
    /// Wwise switch for boss music state.
    /// </summary>
    public AK.Wwise.Switch bossSwitch;
    
    /// <summary>
    /// Wwise switch for silence state.
    /// </summary>
    public AK.Wwise.Switch silenceSwitch;
    
    /// <summary>
    /// Wwise switch for main menu music.
    /// </summary>
    [Tooltip("Switch Wwise pour la musique du menu principal.")]
    public AK.Wwise.Switch mainMenuSwitch;
    
    /// <summary>
    /// Wwise switch for hub music.
    /// </summary>
    [Tooltip("Switch Wwise pour la musique du Hub.")]
    public AK.Wwise.Switch hubSwitch;
    
    /// <summary>
    /// Wwise switch for end game state (victory/defeat).
    /// </summary>
    [Tooltip("Switch Wwise pour l'état de fin de partie (victoire/défaite).")]
    public AK.Wwise.Switch endGameSwitch;
    

    

    [Header("Wwise RTPCs")]
    /// <summary>
    /// RTPC for fever mode intensity control.
    /// </summary>
    [Tooltip("RTPC pour l'intensité du mode Fever.")]
    [SerializeField] private AK.Wwise.RTPC feverIntensityRTPC;
    
    [Header("Settings")]
    /// <summary>
    /// Minimum time between beat events to prevent spam.
    /// </summary>
    [SerializeField] private float minTimeBetweenBeats = 0.1f;
    
    /// <summary>
    /// Initial music state when the manager starts.
    /// </summary>
    [SerializeField] private string initialMusicState = "Exploration";

    // --- PUBLIC EVENTS ---
    /// <summary>
    /// Main event triggered on each beat of the Wwise music.
    /// Provides the exact duration of this beat in seconds.
    /// </summary>
    public event Action<float> OnBeat;
    
    /// <summary>
    /// Event triggered when the music state changes.
    /// </summary>
    public event Action<string> OnMusicStateChanged;

    // --- PUBLIC PROPERTIES ---
    /// <summary>
    /// Property indicating if the last beat was processed. Useful for coroutines waiting for a beat.
    /// </summary>
    public static bool LastBeatWasProcessed { get; private set; }

    /// <summary>
    /// Public property to get the current beat duration in seconds.
    /// </summary>
    public float BeatDuration => currentBeatDuration;

    // --- PRIVATE VARIABLES ---
    /// <summary>
    /// The current music state identifier.
    /// </summary>
    private string currentMusicState;
    
    /// <summary>
    /// The Wwise playing ID for the current music event.
    /// </summary>
    private uint playingID_MusicEvent = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
    
    /// <summary>
    /// The current beat duration in seconds.
    /// </summary>
    private float currentBeatDuration = 0.5f;
    
    /// <summary>
    /// Public counter for the number of beats processed.
    /// </summary>
    public int PublicBeatCount { get; private set; } = 0;
    
    /// <summary>
    /// Flag indicating if music is currently playing.
    /// </summary>
    private bool musicPlaying = false;
    
    /// <summary>
    /// Time of the last beat event.
    /// </summary>
    private float lastBeatTime;
    
    /// <summary>
    /// Internal flag for LastBeatWasProcessed property.
    /// </summary>
    private bool beatOccurredThisFrame = false;

    /// <summary>
    /// Gets the current Wwise music state.
    /// </summary>
    public string CurrentWwiseMusicState => currentMusicState;

    /// <summary>
    /// Initializes the MusicManager singleton instance.
    /// </summary>
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

    /// <summary>
    /// Initializes the music system and sets the initial state.
    /// </summary>
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

    /// <summary>
    /// Updates the Wwise audio engine each frame.
    /// </summary>
    private void Update()
    {
        if (AkUnitySoundEngine.IsInitialized())
        {
            AkUnitySoundEngine.RenderAudio();
        }
    }

    /// <summary>
    /// Updates the beat processing flag after all other Update calls.
    /// </summary>
    private void LateUpdate()
    {
        LastBeatWasProcessed = beatOccurredThisFrame;
        beatOccurredThisFrame = false;
    }

    /// <summary>
    /// Initializes the music system and sets the target state.
    /// </summary>
    /// <param name="targetState">The target music state to initialize with.</param>
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


    /// <summary>
    /// Handles Wwise music callbacks for beat synchronization and event management.
    /// </summary>
    /// <param name="in_cookie">Cookie object passed to the callback.</param>
    /// <param name="in_type">Type of the callback.</param>
    /// <param name="in_info">Information about the callback.</param>
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
                    
                    // Trigger the static event with beat duration
                    OnBeat?.Invoke(currentBeatDuration);

                    // Update flag for LastBeatWasProcessed
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

    /// <summary>
    /// Sets the music state and handles appropriate Wwise switch changes.
    /// </summary>
    /// <param name="newState">The new music state to set.</param>
    /// <param name="immediate">Whether the transition should be immediate.</param>
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

    /// <summary>
    /// Applies the appropriate Wwise switch for the given state name.
    /// </summary>
    /// <param name="stateName">The name of the state to apply the switch for.</param>
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

public void UpdateWwiseListener(GameObject newListener)
{
    if (newListener == null)
    {
        Debug.LogWarning("[MusicManager] Tentative de mise à jour de l'auditeur avec un objet null.");
        return;
    }

    if (AkUnitySoundEngine.IsInitialized())
    {
        // On récupère l'ID Wwise du nouvel auditeur (la nouvelle caméra)
        ulong listenerId = AkUnitySoundEngine.GetAkGameObjectID(newListener);
        if (listenerId != AkUnitySoundEngine.AK_INVALID_GAME_OBJECT)
        {
            // On définit ce nouvel auditeur comme l'unique auditeur pour ce GameObject
            AkUnitySoundEngine.SetListeners(gameObject, new ulong[] { listenerId }, 1);
            Debug.Log($"[MusicManager] L'auditeur Wwise a été mis à jour avec succès vers : {newListener.name}");
        }
        else
        {
            Debug.LogWarning($"[MusicManager] L'objet {newListener.name} ne semble pas avoir d'AkAudioListener valide.");
        }
    }
}
}