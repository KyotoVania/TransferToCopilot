using UnityEngine;
using System;
using System.Collections;
using AK.Wwise; // Wwise Unity namespace

public class MusicManager : MonoBehaviour // Si tu utilises SingletonPersistent, hérites-en.
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
    [Tooltip("Switch Wwise pour l'état de fin de partie (victoire/défaite).")]
    public AK.Wwise.Switch endGameSwitch; // À assigner dans l'Inspecteur

    [Header("Settings")]
    [SerializeField] private float minTimeBetweenBeats = 0.1f;
    [SerializeField] private string initialMusicState = "Exploration"; // Utilisé au premier démarrage

    public event Action<float> OnBeat;
    public event Action<string> OnMusicStateChanged;

    private float lastBeatTime;
    private string currentMusicState; // Variable pour l'état logique C#
    private uint playingID_MusicEvent = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
    private float currentBeatDuration = 0.5f;
    private int beatCount = 0;
    private bool musicPlaying = false; // Flag indiquant si l'événement Wwise principal est posté et censé jouer

    // Propriété pour que RhythmicBoardMovement puisse lire l'état
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
            InitializeMusicAndSetState(initialMusicState); // Démarrer avec l'état initial défini
        }
        else
        {
            Debug.LogError("[MusicManager] Wwise n'est pas initialisé au Start ! La musique ne démarrera pas.");
        }
    }

    // Centralise le démarrage/redémarrage de l'événement Wwise principal et l'application de l'état
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

        // Si un event jouait déjà avec un ID valide, on peut le stopper avant de relancer,
        // OU on peut laisser SetMusicState/ApplyMusicSwitch gérer la transition dans Wwise.
        // Pour l'instant, on va supposer que poster un nouvel event (ou le même) avec un switch
        // différent est géré correctement par Wwise (ex: un Music Switch Container).
        // Si l'ID actuel est valide et que nous voulons juste changer de switch, pas besoin de Stop/Post.
        // On ne (re)poste que si l'ID est invalide (donc rien ne joue sous cet ID).
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
                musicPlaying = false; // L'événement n'a pas pu être posté
                return;
            }
            Debug.Log($"[MusicManager] Music Event '{playMusicEvent.Name}' NOUVELLEMENT posté. ID: {playingID_MusicEvent}");
        }
        // À ce point, playingID_MusicEvent devrait être valide.

        musicPlaying = true; // On considère que la musique (l'event Wwise) est active.

        string previousLogicalState = currentMusicState;
        currentMusicState = ""; // Force SetMusicState à voir une différence pour appliquer le switch initial
        SetMusicState(targetState, true); // Applique le switch pour l'état désiré
        // SetMusicState mettra à jour 'currentMusicState' avec 'targetState'.
        // Si targetState était le même que previousLogicalState, ce n'est pas grave.
    }


    private void Update()
    {
        if (AkUnitySoundEngine.IsInitialized())
        {
            AkUnitySoundEngine.RenderAudio();
        }
    }

    private void OnMusicCallback(object in_cookie, AkCallbackType in_type, AkCallbackInfo in_info)
    {
        if (!AkUnitySoundEngine.IsInitialized()) return;

        if (in_type == AkCallbackType.AK_MusicSyncBeat)
        {
            Debug.Log($"[MusicManager OnMusicCallback] AK_MusicSyncBeat REÇU. musicPlaying: {musicPlaying}, playingID: {playingID_MusicEvent}, currentMusicState: {currentMusicState}, Time.timeScale: {Time.timeScale}", this.gameObject);

            // On ne traite le battement que si on considère que la musique est activement gérée (playingID valide)
            if (playingID_MusicEvent != AkUnitySoundEngine.AK_INVALID_PLAYING_ID)
            {
                AkMusicSyncCallbackInfo musicInfo = in_info as AkMusicSyncCallbackInfo;
                if (musicInfo != null && (Time.time - lastBeatTime >= minTimeBetweenBeats || lastBeatTime == 0f))
                {
                    currentBeatDuration = musicInfo.segmentInfo_fBeatDuration;
                    lastBeatTime = Time.time;
                    beatCount++;
                    OnBeat?.Invoke(currentBeatDuration);
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
        // Debug.Log($"[MusicManager] Demande SetMusicState vers: '{newState}'. Actuel: '{currentMusicState}', musicPlaying: {musicPlaying}, EventID: {playingID_MusicEvent}");

        // Si l'état est déjà le bon ET que l'event Wwise est considéré comme jouant, on ne fait rien.
        if (currentMusicState == newState && musicPlaying && playingID_MusicEvent != AkUnitySoundEngine.AK_INVALID_PLAYING_ID)
        {
            // Debug.Log($"[MusicManager] État '{newState}' déjà actif et musique en cours. Aucun changement.");
            return;
        }

        // Appliquer le switch Wwise. Ceci changera ce que le Music Switch Container joue.
        ApplyMusicSwitch(newState);

        // Si le nouvel état N'EST PAS "silence" et que l'event Wwise n'est pas considéré comme jouant (ou ID invalide),
        // alors il faut (re)lancer l'event Wwise principal avec le nouveau switch déjà appliqué.
        bool needsWwiseEventToPlay = lowerNewState != "silence"; // "endgame" aussi a besoin que l'event Wwise tourne

        if (needsWwiseEventToPlay && (!musicPlaying || playingID_MusicEvent == AkUnitySoundEngine.AK_INVALID_PLAYING_ID))
        {
            Debug.Log($"[MusicManager] L'état '{newState}' nécessite que l'événement Wwise joue, et il ne joue pas (ou ID invalide). Appel de InitializeMusicAndSetState pour '{newState}'.");
            InitializeMusicAndSetState(newState); // Relance/reposte l'event et applique le switch
        }
        // Si on passe à "Silence" et que l'event jouait, on doit explicitement arrêter l'event Wwise
        // car le switch "Silence" pourrait juste rendre le segment muet mais laisser l'event ID actif.
        // Pour "EndGame", on veut que la musique continue, donc on ne stoppe PAS l'event ici.
        else if (lowerNewState == "silence" && musicPlaying && playingID_MusicEvent != AkUnitySoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.Log($"[MusicManager] État cible est 'Silence'. Arrêt de l'event musical ID: {playingID_MusicEvent}.");
            AkUnitySoundEngine.StopPlayingID(playingID_MusicEvent);
            musicPlaying = false;
            playingID_MusicEvent = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
        }

        // Mettre à jour l'état logique C# et notifier
        if (currentMusicState != newState)
        {
            currentMusicState = newState;
            OnMusicStateChanged?.Invoke(newState);
            // Debug.Log($"[MusicManager] État logique C# mis à jour en '{currentMusicState}'. musicPlaying: {musicPlaying}.");
        }
    }

    // Les coroutines ne sont plus essentielles si SetMusicState est direct
    private IEnumerator DelayedSetSwitch(string newState) { yield return null; }
    private IEnumerator TransitionStateCoroutine(string newState, bool immediate) { yield return null; }

    private void ApplyMusicSwitch(string stateName)
    {
        AK.Wwise.Switch targetSwitch = null;
        // string switchGroupName = "MusicState"; // Défini dans Wwise

        switch (stateName.ToLower())
        {
            case "exploration": targetSwitch = explorationSwitch; break;
            case "combat": targetSwitch = combatSwitch; break;
            case "boss": targetSwitch = bossSwitch; break;
            case "silence": targetSwitch = silenceSwitch; break;
            case "mainmenu": targetSwitch = silenceSwitch; break; // Ou un switch dédié
            case "hub": targetSwitch = silenceSwitch; break;      // Ou un switch dédié
            // ----- CAS POUR ENDGAME -----
            case "endgame":
                targetSwitch = endGameSwitch;
                if (endGameSwitch == null || !endGameSwitch.IsValid()) {
                    Debug.LogError($"[MusicManager] ApplyMusicSwitch: Switch 'endGameSwitch' non assigné ou invalide pour l'état '{stateName}'.");
                    // Optionnel: fallback sur un autre switch si endGameSwitch n'est pas prêt
                    // targetSwitch = explorationSwitch; // Par exemple, pour que la musique continue
                }
                break;
            // ---------------------------
            default:
                Debug.LogWarning($"[MusicManager] ApplyMusicSwitch: État musical Switch Wwise inconnu : '{stateName}'.");
                return;
        }

        if (targetSwitch != null && targetSwitch.IsValid())
        {
            targetSwitch.SetValue(this.gameObject); // Le GameObject sur lequel playMusicEvent est posté
            // Debug.Log($"[MusicManager] ApplyMusicSwitch: Switch Wwise '{targetSwitch.Name}' (pour état {stateName}) appliqué.");
        }
        else if (stateName.ToLower() != "endgame") // Ne pas spammer si c'est juste endgameSwitch qui manque
        {
             Debug.LogError($"[MusicManager] ApplyMusicSwitch: Le Switch Wwise pour l'état '{stateName}' est null ou invalide !");
        }
    }

    public float GetBeatDuration() { return (musicPlaying && playingID_MusicEvent != AkUnitySoundEngine.AK_INVALID_PLAYING_ID) ? currentBeatDuration : 0.5f; }
    public float GetNextBeatTime() {
        if (!musicPlaying || playingID_MusicEvent == AkUnitySoundEngine.AK_INVALID_PLAYING_ID || currentBeatDuration <= 0)
            return Time.time + 0.5f;
        float timeSinceLastBeat = Time.time - lastBeatTime;
        float timeUntilNextBeat = currentBeatDuration - (timeSinceLastBeat % currentBeatDuration);
        return Time.time + timeUntilNextBeat;
    }
    public float GetTimeUntilNextBeat() { return GetNextBeatTime() - Time.time; }
    public float GetBeatProgress() {
        if (!musicPlaying || playingID_MusicEvent == AkUnitySoundEngine.AK_INVALID_PLAYING_ID || currentBeatDuration <= 0) return 0;
        float timeSinceLastBeat = Time.time - lastBeatTime;
        return Mathf.Clamp01((timeSinceLastBeat % currentBeatDuration) / currentBeatDuration);
    }

    private void OnDestroy() // Ou protected override void OnDestroy()
    {
        // base.OnDestroy(); // Si SingletonPersistent
        if (Instance == this) Instance = null; // Si singleton manuel

        if (playingID_MusicEvent != AkUnitySoundEngine.AK_INVALID_PLAYING_ID && AkUnitySoundEngine.IsInitialized())
        {
            AkUnitySoundEngine.StopPlayingID(playingID_MusicEvent);
            playingID_MusicEvent = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
        }
    }
}