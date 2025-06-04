using UnityEngine;
using System;
using System.Collections; // Ajouté pour IEnumerator
using AK.Wwise; // Wwise Unity namespace


public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Wwise Configuration")]
    [Tooltip("Event Wwise pour démarrer la lecture du Music Switch Container principal (ex: Play_Level_Music).")]
    [SerializeField] private AK.Wwise.Event playMusicEvent;

    [Header("Music Switches (doivent correspondre au groupe 'MusicState' dans Wwise)")]
    [Tooltip("Switch Wwise pour l'état d'exploration.")]
    public AK.Wwise.Switch explorationSwitch;
    [Tooltip("Switch Wwise pour l'état de combat.")]
    public AK.Wwise.Switch combatSwitch;
    [Tooltip("Switch Wwise pour l'état de boss.")]
    public AK.Wwise.Switch bossSwitch;
    [Tooltip("Switch Wwise pour l'état de silence.")]
    public AK.Wwise.Switch silenceSwitch;
    // Ajoutez d'autres switches si vous avez plus d'états musicaux (ex: Menu, Hub)

    [Header("Settings")]
    [SerializeField] private float minTimeBetweenBeats = 0.1f; // Pour éviter les doubles détections de beat
    [SerializeField] private string initialMusicState = "Exploration"; // État musical au démarrage

    // Événements pour la synchronisation
    public event Action<float> OnBeat; // Déclenché à chaque beat Wwise
    public event Action<string> OnMusicStateChanged; // Déclenché quand l'état musical change

    private float lastBeatTime;
    private string currentMusicState;
    private uint playingID_MusicEvent; // Stocke l'ID de l'instance de playMusicEvent

    // Variables pour le suivi des beats (si utilisées par d'autres systèmes)
    private float currentBeatDuration = 0.5f; // Default, will be updated by callbacks
    private int beatCount = 0;
    private bool musicPlaying = false;

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
        InitializeMusic();
    }

    private void InitializeMusic()
    {
        if (playMusicEvent == null || !playMusicEvent.IsValid())
        {
            Debug.LogError("[MusicManager] 'playMusicEvent' (Play_Level_Music) n'est pas assigné ou n'est pas valide ! Impossible de démarrer la musique.");
            return;
        }

        // Poster l'événement pour démarrer le Music Switch Container.
        // Les Callbacks sont importants pour la synchronisation rythmique.
        playingID_MusicEvent = playMusicEvent.Post(
            gameObject,
            (uint)(AkCallbackType.AK_MusicSyncBeat | AkCallbackType.AK_EnableGetMusicPlayPosition | AkCallbackType.AK_EndOfEvent),
            OnMusicCallback,
            null
        );

        if (playingID_MusicEvent == AkUnitySoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogError($"[MusicManager] Échec du Post de playMusicEvent '{playMusicEvent.Name}'. Playing ID invalide.");
            musicPlaying = false;
        }
        else
        {
            musicPlaying = true;
            Debug.Log($"[MusicManager] Music Event '{playMusicEvent.Name}' posté avec succès. Playing ID: {playingID_MusicEvent}");
            // Définir l'état musical initial
            currentMusicState = ""; // Forcer le changement pour le premier SetMusicState
            SetMusicState(initialMusicState, true); // true pour application immédiate du switch
        }
    }

    private void Update()
    {
        // Optionnel: Wwise devrait gérer cela, mais si vous rencontrez des suspensions audio :
        // AkSoundEngine.RenderAudio();
    }

    private void OnMusicCallback(object in_cookie, AkCallbackType in_type, AkCallbackInfo in_info)
    {
        if (!musicPlaying) return;

        if (in_type == AkCallbackType.AK_MusicSyncBeat)
        {
            AkMusicSyncCallbackInfo musicInfo = in_info as AkMusicSyncCallbackInfo;
            if (musicInfo != null && Time.time - lastBeatTime >= minTimeBetweenBeats)
            {
                currentBeatDuration = musicInfo.segmentInfo_fBeatDuration;
                lastBeatTime = Time.time;
                beatCount++;
                OnBeat?.Invoke(currentBeatDuration);
            }
        }
        else if (in_type == AkCallbackType.AK_EndOfEvent)
        {
            AkEventCallbackInfo eventInfo = in_info as AkEventCallbackInfo;
            if (eventInfo != null && eventInfo.playingID == playingID_MusicEvent)
            {
                Debug.LogWarning($"[MusicManager] L'Event musical principal '{playMusicEvent.Name}' (ID: {playingID_MusicEvent}) s'est terminé. S'il doit jouer en boucle, vérifiez sa configuration dans Wwise ou celle du Music Switch Container.");
                musicPlaying = false;
                // Vous pourriez vouloir le relancer ici si ce n'est pas une boucle dans Wwise
                // InitializeMusic(); // Attention, cela pourrait créer des problèmes si mal géré.
            }
        }
    }

    /// <summary>
    /// Change l'état musical en utilisant les Switches Wwise.
    /// </summary>
    /// <param name="newState">Le nom de l'état (doit correspondre aux noms des Switches ou à une logique interne).</param>
    /// <param name="immediate">Ce paramètre est moins pertinent si les transitions sont gérées par Wwise, mais conservé pour la compatibilité.</param>
    public void SetMusicState(string newState, bool immediate = false)
    {
        if (currentMusicState == newState && musicPlaying) // Ne rien faire si l'état est déjà actif et que la musique joue
        {
            // Si la musique ne joue pas mais qu'on demande le même état, on pourrait vouloir la relancer.
            if (!musicPlaying) {
                 Debug.LogWarning($"[MusicManager] SetMusicState pour '{newState}', mais la musique ne jouait pas. Tentative de redémarrage.");
                 InitializeMusic(); // Tente de relancer la musique avec l'état courant (qui sera réappliqué)
            }
            return;
        }
        
        if (!musicPlaying && playMusicEvent != null && playMusicEvent.IsValid())
        {
            Debug.Log($"[MusicManager] La musique ne jouait pas. Démarrage avec l'état {newState}.");
            InitializeMusic(); // Assure que la musique est lancée avant de tenter de changer le switch
            // On attend une frame pour que le post de l'event soit traité par Wwise avant de changer le switch
            StartCoroutine(DelayedSetSwitch(newState));
            return;
        }


        // La coroutine n'est plus vraiment nécessaire si on ne fait que SetValue.
        // Mais on la garde pour la structure, au cas où on ajouterait des délais/logiques plus tard.
        StartCoroutine(TransitionStateCoroutine(newState, immediate));
    }

    private IEnumerator DelayedSetSwitch(string newState)
    {
        yield return null; // Attendre une frame
        ApplyMusicSwitch(newState);
        currentMusicState = newState;
        OnMusicStateChanged?.Invoke(newState);
    }


    private IEnumerator TransitionStateCoroutine(string newState, bool immediate)
    {
        // Si la musique ne joue pas, on ne peut pas changer de Switch.
        // Normalement, InitializeMusic devrait déjà avoir été appelé.
        if (!musicPlaying)
        {
            Debug.LogWarning($"[MusicManager] Tentative de changer l'état musical vers '{newState}', mais la musique ne joue pas. L'état sera appliqué au prochain démarrage de la musique.");
            // On stocke l'état demandé pour qu'il soit appliqué si la musique redémarre
            initialMusicState = newState; // Mémorise l'état désiré
            currentMusicState = newState; // Met à jour l'état courant pour refléter la demande
            OnMusicStateChanged?.Invoke(newState); // Notifie du changement d'intention
            yield break; // Sortir si la musique ne joue pas activement
        }

        ApplyMusicSwitch(newState);

        currentMusicState = newState; // Mettre à jour l'état interne après l'application du Switch
        OnMusicStateChanged?.Invoke(newState); // Notifier les autres systèmes
        yield return null; // La coroutine est simple maintenant
    }

    private void ApplyMusicSwitch(string stateName)
    {
        AK.Wwise.Switch targetSwitch = null;
        string switchGroupName = "MusicState"; // Assurez-vous que c'est le nom de votre Switch Group dans Wwise

        switch (stateName.ToLower()) // Utiliser ToLower() pour être moins sensible à la casse
        {
            case "exploration":
                targetSwitch = explorationSwitch;
                break;
            case "combat":
                targetSwitch = combatSwitch;
                break;
            case "boss":
                targetSwitch = bossSwitch;
                break;
            case "silence":
                targetSwitch = silenceSwitch;
                break;
            default:
                Debug.LogWarning($"[MusicManager] État musical inconnu : '{stateName}'. Aucun Switch Wwise appliqué.");
                return;
        }

        if (targetSwitch != null && targetSwitch.IsValid())
        {
            targetSwitch.SetValue(gameObject); // Appliquer le Switch sur le GameObject du MusicManager
            Debug.Log($"[MusicManager] Switch Wwise '{targetSwitch.Name}' (État: {stateName}) appliqué sur le groupe '{switchGroupName}'.");
        }
        else
        {
            Debug.LogError($"[MusicManager] Le Switch Wwise pour l'état '{stateName}' n'est pas assigné ou n'est pas valide dans l'inspecteur !");
        }
    }

    public float GetBeatDuration()
    {
        return musicPlaying ? currentBeatDuration : 0.5f; // Retourne 0.5f par défaut si la musique ne joue pas
    }

    public float GetNextBeatTime()
    {
        if (!musicPlaying || currentBeatDuration <= 0)
            return Time.time + 0.5f;

        float timeSinceLastBeat = Time.time - lastBeatTime;
        float timeUntilNextBeat = currentBeatDuration - (timeSinceLastBeat % currentBeatDuration);
        return Time.time + timeUntilNextBeat;
    }

    public float GetTimeUntilNextBeat()
    {
        return GetNextBeatTime() - Time.time;
    }

    public float GetBeatProgress()
    {
        if (!musicPlaying || currentBeatDuration <= 0)
            return 0;
        float timeSinceLastBeat = Time.time - lastBeatTime;
        return Mathf.Clamp01((timeSinceLastBeat % currentBeatDuration) / currentBeatDuration);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (musicPlaying && playingID_MusicEvent != AkUnitySoundEngine.AK_INVALID_PLAYING_ID)
        {
            AkUnitySoundEngine.StopPlayingID(playingID_MusicEvent);
        }
    }
}