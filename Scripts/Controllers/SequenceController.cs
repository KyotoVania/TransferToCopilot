using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using ScriptableObjects;
using UnityEngine.InputSystem; // Ajout important pour le nouveau système d'input

// Ces classes sérialisées restent identiques
[System.Serializable]
public class InputSoundVariants
{
    public AK.Wwise.Switch goodSwitch;
    public AK.Wwise.Switch perfectSwitch;
    public AK.Wwise.Switch failedSwitch;
}

[System.Serializable]
public class InputSoundVariantsKey
{
    public AK.Wwise.Switch XSwitch;
    public AK.Wwise.Switch CSwitch;
    public AK.Wwise.Switch VSwitch;
}

public class SequenceController : MonoBehaviour
{
    // --- Variables de Séquence et de Timing (inchangées) ---
    private List<CharacterData_SO> availablePlayerCharactersInTeam = new List<CharacterData_SO>();
    private List<GlobalSpellData_SO> availableGlobalSpells = new List<GlobalSpellData_SO>();

    private List<KeyCode> currentSequence = new List<KeyCode>();
    public IReadOnlyList<KeyCode> CurrentSequence => currentSequence.AsReadOnly();

    private int perfectCount = 0;

    [Tooltip("Tolérance en % de la durée du beat pour un input 'Perfect'. Ex: 0.2 = 20% de la durée du beat.")]
    [Range(0, 1)]
    public float perfectTolerancePercent = 0.2f;

    [Tooltip("Tolérance en % de la durée du beat pour un input 'Good'. Ex: 0.4 = 40% de la durée du beat.")]
    [Range(0, 1)]
    public float goodTolerancePercent = 0.4f;

    private bool _hasInputForCurrentBeat = false;
    private float lastBeatTime;
    private float _currentBeatDuration = 1f;
    private bool isResponding = false;

    // --- Dépendances et Événements (inchangés) ---
    private MusicManager musicManager;
    public InputSoundVariants inputSounds;
    public InputSoundVariantsKey inputSoundsKey;
    public AK.Wwise.Event playSwitchContainerEvent;
    [SerializeField] private TextMeshProUGUI sequenceDisplay;

    public static event Action<string, Color> OnSequenceKeyPressed;
    public static event Action OnSequenceDisplayCleared;
    public static event Action<CharacterData_SO, int> OnCharacterInvocationSequenceComplete;
    public static event Action<GlobalSpellData_SO, int> OnGlobalSpellSequenceComplete;
    public static event Action OnSequenceSuccess;
    public static event Action OnSequenceFail;

    #region Initialisation et Cycle de Vie

    private void Awake()
    {
        availablePlayerCharactersInTeam = new List<CharacterData_SO>();
        availableGlobalSpells = new List<GlobalSpellData_SO>();
    }

    // --- MISE À JOUR MAJEURE : Abonnement au nouveau Input System ---
    private void OnEnable()
    {
        // On s'abonne directement aux actions du InputManager
        if (InputManager.Instance != null)
        {
            InputManager.Instance.GameplayActions.RhythmInput_Left.performed += OnRhythmInput_Left;
            InputManager.Instance.GameplayActions.RhythmInput_Down.performed += OnRhythmInput_Down;
            InputManager.Instance.GameplayActions.RhythmInput_Right.performed += OnRhythmInput_Right;
        }
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += HandleBeat;
        }
    }

    // --- MISE À JOUR MAJEURE : Se désabonner proprement ---
    private void OnDisable()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleBeat;
        }
        if (InputManager.Instance != null)
        {
            InputManager.Instance.GameplayActions.RhythmInput_Left.performed -= OnRhythmInput_Left;
            InputManager.Instance.GameplayActions.RhythmInput_Down.performed -= OnRhythmInput_Down;
            InputManager.Instance.GameplayActions.RhythmInput_Right.performed -= OnRhythmInput_Right;
        }
    }

    private void Start()
    {
        // Utilisation du singleton pour plus de robustesse
        musicManager = MusicManager.Instance;
        if (musicManager == null)
        {
            Debug.LogError("No MusicManager found in the scene!");
        }
        OnSequenceDisplayCleared?.Invoke();
    }

    public void InitializeWithPlayerTeamAndSpells(List<CharacterData_SO> activeTeam, List<GlobalSpellData_SO> globalSpells)
    {
        availablePlayerCharactersInTeam = activeTeam ?? new List<CharacterData_SO>();
        availableGlobalSpells = globalSpells ?? new List<GlobalSpellData_SO>();
        Debug.Log($"[SequenceController] Initialized with {availablePlayerCharactersInTeam.Count} character(s) in team and {availableGlobalSpells.Count} global spell(s).");
    }

    // SUPPRIMÉ : La méthode Update() n'est plus utilisée pour les inputs.

    #endregion

    #region Gestion des Inputs

    // --- NOUVEAU : Méthodes de rappel pour le nouveau Input System ---
    private void OnRhythmInput_Left(InputAction.CallbackContext context)
    {
        ProcessInput(KeyCode.X);
    }

    private void OnRhythmInput_Down(InputAction.CallbackContext context)
    {
        ProcessInput(KeyCode.C);
    }

    private void OnRhythmInput_Right(InputAction.CallbackContext context)
    {
        ProcessInput(KeyCode.V);
    }

    // --- AMÉLIORATION : ProcessInput intègre maintenant la logique audio Wwise ---
    private void ProcessInput(KeyCode key)
    {
        if (isResponding) return;

        // Vérification de robustesse
        if (musicManager == null) {
             SetSwitchAndPlay(inputSounds.failedSwitch, "Failed (No MusicManager)", playSwitchContainerEvent, GetKeySwitch(key));
             OnSequenceFail?.Invoke();
             ResetSequence();
             return;
        }

        // Logique anti-spam
        if (_hasInputForCurrentBeat)
        {
            Debug.Log("[SequenceController] Input registered before next beat window. Combo Reset!");
            SetSwitchAndPlay(inputSounds.failedSwitch, "Failed (Spam)", playSwitchContainerEvent, GetKeySwitch(key));
            OnSequenceFail?.Invoke();
            ResetSequence();
            return;
        }

        // Calcul du timing (inchangé, mais maintenant utilisé pour le son)
        float currentTime = Time.time;
        float timeSinceLastBeat = Mathf.Abs(currentTime - lastBeatTime);
        float timeUntilNextBeat = musicManager.GetTimeUntilNextBeat();
        float timeToClosestBeat = Mathf.Min(timeSinceLastBeat, timeUntilNextBeat);

        AK.Wwise.Switch keySwitch = GetKeySwitch(key);
        float perfectToleranceInSeconds = _currentBeatDuration * perfectTolerancePercent;
        float goodToleranceInSeconds = _currentBeatDuration * goodTolerancePercent;

        if (timeToClosestBeat <= perfectToleranceInSeconds)
        {
            _hasInputForCurrentBeat = true;
            perfectCount++;
            SetSwitchAndPlay(inputSounds.perfectSwitch, "Perfect", playSwitchContainerEvent, keySwitch); // Appel audio
            currentSequence.Add(key);
            OnSequenceKeyPressed?.Invoke(key.ToString(), Color.green); // Feedback visuel
        }
        else if (timeToClosestBeat <= goodToleranceInSeconds)
        {
            _hasInputForCurrentBeat = true;
            SetSwitchAndPlay(inputSounds.goodSwitch, "Good", playSwitchContainerEvent, keySwitch); // Appel audio
            currentSequence.Add(key);
            OnSequenceKeyPressed?.Invoke(key.ToString(), Color.yellow); // Feedback visuel
        }
        else
        {
            SetSwitchAndPlay(inputSounds.failedSwitch, "Failed (Off-beat)", playSwitchContainerEvent, keySwitch); // Appel audio
            OnSequenceFail?.Invoke();
            ResetSequence();
            return;
        }

        ValidateSequence();
    }

    #endregion

    #region Logique de Séquence et Audio (avec améliorations)

    private void ValidateSequence()
    {
        // La longueur de la séquence peut varier, donc on vérifie après chaque touche
        // si une séquence valide est complétée.
        bool sequenceMatched = false;

        foreach (CharacterData_SO characterData in availablePlayerCharactersInTeam)
        {
            if (characterData?.InvocationSequence != null &&
                CompareKeySequence(currentSequence, characterData.InvocationSequence.Select(input => ConvertInputTypeToKeyCode(input)).ToList()))
            {
                Debug.Log($"[SequenceController] Character Invocation Sequence Matched: {characterData.DisplayName}");
                StartCoroutine(HandleSuccessfulSequence(() => OnCharacterInvocationSequenceComplete?.Invoke(characterData, perfectCount), currentSequence.Count));
                sequenceMatched = true;
                break; // Sortir de la boucle dès qu'une correspondance est trouvée
            }
        }

        if (!sequenceMatched)
        {
            foreach (GlobalSpellData_SO spellData in availableGlobalSpells)
            {
                if (spellData?.SpellSequence != null &&
                    CompareKeySequence(currentSequence, spellData.SpellSequence.Select(input => ConvertInputTypeToKeyCode(input)).ToList()))
                {
                    Debug.Log($"[SequenceController] Global Spell Sequence Matched: {spellData.DisplayName}");
                    StartCoroutine(HandleSuccessfulSequence(() => OnGlobalSpellSequenceComplete?.Invoke(spellData, perfectCount), currentSequence.Count));
                    sequenceMatched = true;
                    break;
                }
            }
        }

        // Si la séquence atteint 4 et ne correspond à rien, c'est un échec.
        if (!sequenceMatched && currentSequence.Count >= 4)
        {
            Debug.LogWarning($"[SequenceController] Sequence of 4+ inputs did not match any known sequence: {string.Join("-", currentSequence)}");
            OnSequenceFail?.Invoke();
            ResetSequence();
        }
    }

    private bool CompareKeySequence(List<KeyCode> inputSequence, List<KeyCode> targetSequence)
    {
        // Cette comparaison simple fonctionne pour les séquences de même longueur.
        return inputSequence.SequenceEqual(targetSequence);
    }

    private KeyCode ConvertInputTypeToKeyCode(InputType inputType)
    {
        switch (inputType)
        {
            case InputType.X: return KeyCode.X;
            case InputType.C: return KeyCode.C;
            case InputType.V: return KeyCode.V;
            default: return KeyCode.None;
        }
    }

    private IEnumerator HandleSuccessfulSequence(Action specificEventCallback, int sequenceLength)
    {
        isResponding = true;
        OnSequenceSuccess?.Invoke();
        specificEventCallback?.Invoke();

        float responseDuration = musicManager != null ? musicManager.GetBeatDuration() * sequenceLength : 0.5f;
        yield return new WaitForSeconds(Mathf.Max(responseDuration, 0.1f));

        isResponding = false;
        ResetSequence();
    }

    private void ResetSequence()
    {
        perfectCount = 0;
        currentSequence.Clear();
        OnSequenceDisplayCleared?.Invoke();
    }

    private void HandleBeat(float beatDuration)
    {
        lastBeatTime = Time.time;
        _hasInputForCurrentBeat = false;
        _currentBeatDuration = beatDuration;
    }

    private AK.Wwise.Switch GetKeySwitch(KeyCode key)
    {
        if (key == KeyCode.X) return inputSoundsKey.XSwitch;
        if (key == KeyCode.C) return inputSoundsKey.CSwitch;
        if (key == KeyCode.V) return inputSoundsKey.VSwitch;
        return null;
    }

    // --- AMÉLIORATION : Logique audio complète et robuste ---
    private void SetSwitchAndPlay(AK.Wwise.Switch switchState, string switchName, AK.Wwise.Event playEvent, AK.Wwise.Switch keySwitch)
    {
        if (switchState != null && switchState.IsValid() && keySwitch != null && keySwitch.IsValid())
        {
            switchState.SetValue(gameObject);
            keySwitch.SetValue(gameObject);
            if (playEvent != null && playEvent.IsValid())
            {
                playEvent.Post(gameObject);
            }
        }
        else
        {
            // Logs de débogage pour aider à configurer Wwise dans Unity
            if (switchState == null) Debug.LogWarning($"[SequenceController] Wwise timing switch '{switchName}' not assigned in Inspector.");
            if (keySwitch == null) Debug.LogWarning($"[SequenceController] Wwise key switch for the pressed key not assigned in Inspector.");
        }
    }

    #endregion
}