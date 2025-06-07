using UnityEngine;
using Unity;
using System;
using System.Collections;
using AK.Wwise;

public class RhythmManager : MonoBehaviour
{
    public float bpm = 120f; // Defines the tempo in BPM.
    private float interval;  // Duration between each beat in seconds.
    private float timer = 0f; // Timer pour suivre le temps écoulé depuis le dernier battement
    private int beatCount = 0; // Counter for the number of beats.

    // AJOUT : Pour un suivi précis du moment du prochain battement.
    [HideInInspector] public float nextBeatTime = 0f;

    // Singleton Instance pour un accès global facile.
    public static RhythmManager Instance { get; private set; }

    // Ce flag est utilisé par WaitForBeat.cs (ancienne version).
    // Il est important de le gérer correctement.
    public static bool LastBeatWasProcessed { get; private set; }

    // Événement pour notifier d'autres scripts quand un battement se produit.
    // WaitForBeatNode s'abonne à cet événement.
    public delegate void BeatAction();

    public static event BeatAction OnBeat;

    // (Optionnel) Événement pour une notification anticipée avant le battement.
    public delegate void PreBeatAction();
    public static event PreBeatAction OnPreBeat;
    private const float PRE_BEAT_TIME = 0.05f; // 50ms avant le battement.

    [Header("Wwise Configuration")]
    public Bank rhythmBank; // Assign the SoundBank in the Unity Inspector.
    public AK.Wwise.Event mainBeatEvent; // Event for every beat.
    public AK.Wwise.Event clapEvent;    // Event for every 2 beats.
    public AK.Wwise.Event shakerEvent;  // Event for every 4 beats.
    // public string SoundBankName = "RhythmBank"; // Moins flexible que d'assigner directement le BankAsset.

    public float BeatDuration => interval; // Propriété publique pour la durée d'un battement.

    // Debug
    [Header("Debugging")]
    [SerializeField] private bool debugLogBeats = false;
    [SerializeField] private bool showBeatVisualIndicator = false; // Pour un indicateur visuel à l'écran
    private GUIStyle debugStyle;
    private bool visualBeatIndicatorActive = false;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Optionnel: DontDestroyOnLoad(gameObject); si le RhythmManager doit persister entre les scènes.
    }

    private void Start()
    {
        // Charger la SoundBank Wwise si assignée.
        if (rhythmBank != null)
        {
            // S'assurer que AkSoundEngine est initialisé.
            if (!AkUnitySoundEngine.IsInitialized())
            {
                Debug.LogError("[RhythmManager] AkSoundEngine is not initialized. Wwise won't work.");
                // Vous pourriez vouloir empêcher le jeu de continuer ou tenter une initialisation ici.
            }
            else
            {
                rhythmBank.Load();
                if(debugLogBeats) Debug.Log($"[RhythmManager] SoundBank '{rhythmBank.Name}' loaded.");
            }
        }
        else
        {
            Debug.LogWarning("[RhythmManager] No Wwise SoundBank assigned.");
        }

        // Calculer l'intervalle basé sur le BPM.
        if (bpm <= 0)
        {
            Debug.LogError("[RhythmManager] BPM must be greater than 0. Defaulting to 120 BPM.");
            bpm = 120f;
        }
        interval = 60f / bpm;
        timer = 0f; // Commencer le timer à 0.
        nextBeatTime = Time.time + interval; // Initialiser le premier nextBeatTime.
        LastBeatWasProcessed = false; // Initialiser le flag.

        if (showBeatVisualIndicator)
        {
            debugStyle = new GUIStyle();
            debugStyle.fontSize = 24;
            debugStyle.normal.textColor = Color.green;
            debugStyle.alignment = TextAnchor.UpperCenter;
        }
    }

    private void Update()
    {
        // S'assurer que AkSoundEngine est toujours valide si vous l'utilisez intensivement.
        // if (!AkSoundEngine.IsInitialized() && rhythmBank != null) return; // Arrêter si Wwise n'est pas prêt

        float currentTime = Time.time;
        timer += Time.unscaledDeltaTime;

        // Logique pour le PreBeat (notification anticipée)
        if (OnPreBeat != null)
        {
            float timeUntilNextBeat = nextBeatTime - currentTime;
            // Déclencher PreBeat si nous sommes dans la fenêtre PRE_BEAT_TIME avant le prochain battement
            // et que le timer principal est suffisamment avancé pour indiquer que nous approchons de la fin de l'intervalle actuel.
            if (timeUntilNextBeat > 0 && timeUntilNextBeat <= PRE_BEAT_TIME)
            {
                // Pour éviter de déclencher plusieurs fois, on pourrait ajouter un flag "preBeatTriggeredThisInterval"
                // ou se baser sur le fait que PRE_BEAT_TIME est court.
                // Pour l'instant, on le déclenche potentiellement à chaque frame dans cette fenêtre.
                // Si cela cause des problèmes, il faudra ajouter un flag de contrôle.
                // OnPreBeat.Invoke();
            }
        }

        if (currentTime >= nextBeatTime)
        {
            LastBeatWasProcessed = false;
            OnBeat?.Invoke();
            if(debugLogBeats) Debug.Log($"[{Time.frameCount}] RhythmManager: Beat {beatCount + 1} invoked at {Time.time:F3}. Delta from expected: {(Time.time - nextBeatTime):F4}s. Time.timeScale: {Time.timeScale}");

            HandleWwiseBeatEvents();
            beatCount++;
            nextBeatTime += interval;

            while (nextBeatTime < currentTime) {
                nextBeatTime += interval;
                if(debugLogBeats) Debug.LogWarning($"[{Time.frameCount}] RhythmManager: Lag detected or BPM too high. Skipped one or more beat calculations to catch up.");
            }

            // Le recalcul de timer utilise déjà currentTime (unscaled) et nextBeatTime (basé sur unscaled)
            timer = currentTime - (nextBeatTime - interval);
            LastBeatWasProcessed = true;
        }
    }

    // Mis dans LateUpdate pour s'assurer qu'il est réinitialisé après que tous les Update des autres scripts aient eu lieu.
    private void LateUpdate()
    {
        if (LastBeatWasProcessed)
        {
            LastBeatWasProcessed = false;
        }
    }

    private void HandleWwiseBeatEvents()
    {
        if (!AkUnitySoundEngine.IsInitialized()) return;

        if (mainBeatEvent != null && mainBeatEvent.IsValid())
        {
            mainBeatEvent.Post(gameObject);
        }

        if (beatCount % 2 == 0 && clapEvent != null && clapEvent.IsValid())
        {
            clapEvent.Post(gameObject);
        }

        if (beatCount % 4 == 0 && shakerEvent != null && shakerEvent.IsValid())
        {
            shakerEvent.Post(gameObject);
        }
    }

    private void OnGUI()
    {
        if (showBeatVisualIndicator && visualBeatIndicatorActive)
        {
            // Afficher un simple point ou texte au centre de l'écran
            float boxSize = 20;
             GUI.Box(new Rect(Screen.width / 2 - boxSize/2, Screen.height / 2 - boxSize/2, boxSize, boxSize), "", debugStyle);
        }
    }


    private void OnDestroy()
    {
        if (rhythmBank != null && AkUnitySoundEngine.IsInitialized())
        {
            rhythmBank.Unload(); // Décharger la SoundBank.
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetBPM(float newBPM)
    {
        if (newBPM <= 0) return;
        bpm = newBPM;
        interval = 60f / bpm;
        // timer est maintenant basé sur unscaledDeltaTime
        float currentProgressRatio = (timer % interval) / interval;
        nextBeatTime = Time.time + (interval * (1 - currentProgressRatio)); // Time.time est unscaled
        // timer = interval * currentProgressRatio; // Cette ligne pourrait être redondante si le timer est recalculé dans Update

        if(debugLogBeats) Debug.Log($"[RhythmManager] BPM set to {newBPM}. Interval: {interval:F3}s. Next beat in: {(nextBeatTime - Time.time):F3}s");
    }

    /// <summary>
    /// Méthode pour retourner la durée d'un battement en secondes.
    /// </summary>
    /// <returns>La durée d'un battement.</returns>
    public float GetBeatDuration()
    {
        return interval;
    }

    public float GetTimeUntilNextBeat()
    {
        if (!this.enabled || !Application.isPlaying) return float.MaxValue;
        return Mathf.Max(0, nextBeatTime - Time.time);
    }
}