using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using ScriptableObjects;
using UnityEngine.InputSystem;

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
    private bool isSequenceActive = false;
    private float lastBeatTime;
    
    private float _currentBeatDuration = 1f; // Initialisé à 1 seconde par défaut

    // --- Événements ---
    public static event Action<string, Color> OnSequenceKeyPressed;
    public static event Action OnSequenceDisplayCleared;
    public static event Action<CharacterData_SO, int> OnCharacterInvocationSequenceComplete;
    public static event Action<GlobalSpellData_SO, int> OnGlobalSpellSequenceComplete;
    public static event Action OnSequenceSuccess;
    public static event Action OnSequenceFail;

    private bool isResponding = false;
    
    private MusicManager musicManager;

    public InputSoundVariants inputSounds;
    public InputSoundVariantsKey inputSoundsKey;
    public AK.Wwise.Event playSwitchContainerEvent;
    
    [SerializeField] private TextMeshProUGUI sequenceDisplay;

    private void Awake()
    {
        availablePlayerCharactersInTeam = new List<CharacterData_SO>();
        availableGlobalSpells = new List<GlobalSpellData_SO>();
    }

    private void OnEnable()
    {
        if (InputManager.Instance != null)
        {
            // On s'abonne directement aux actions exposées par le manager.
            // La syntaxe est un peu plus longue mais beaucoup plus claire.
            InputManager.Instance.GameplayActions.RhythmInput_Left.performed += OnRhythmInput_Left;
            InputManager.Instance.GameplayActions.RhythmInput_Down.performed += OnRhythmInput_Down;
            InputManager.Instance.GameplayActions.RhythmInput_Right.performed += OnRhythmInput_Right;
        }
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += HandleBeat;
        }
    }

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
        musicManager = MusicManager.Instance; // Utilisation du Singleton pour plus de robustesse
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
    private AK.Wwise.Switch GetKeySwitch(KeyCode key)
    {
        if (key == KeyCode.X) return inputSoundsKey.XSwitch;
        if (key == KeyCode.C) return inputSoundsKey.CSwitch;
        if (key == KeyCode.V) return inputSoundsKey.VSwitch;
        Debug.LogWarning("[SequenceController] No Wwise key switch assigned for this key input!");
        return null;
    }
    
    private void ProcessInput(KeyCode key)
    {
         if (isResponding) return;

        if (musicManager == null) {
             SetSwitchAndPlay(inputSounds.failedSwitch, "Failed (No MusicManager)", playSwitchContainerEvent, GetKeySwitch(key));
             OnSequenceFail?.Invoke();
             ResetSequence();
             return;
        }
        if (_hasInputForCurrentBeat)
        {
            Debug.Log("[SequenceController] Input registered before next beat window. Combo Reset!");
            SetSwitchAndPlay(inputSounds.failedSwitch, "Failed (Spam)", playSwitchContainerEvent, GetKeySwitch(key));
            OnSequenceFail?.Invoke();
            ResetSequence();
            return;
        }

        float currentTime = Time.time;
        float timeSinceLastBeat = Mathf.Abs(currentTime - lastBeatTime);
        float timeUntilNextBeat = musicManager.GetTimeUntilNextBeat();
        float timeToClosestBeat = Mathf.Min(timeSinceLastBeat, timeUntilNextBeat);

        AK.Wwise.Switch keySwitch = GetKeySwitch(key);

        float perfectToleranceInSeconds = _currentBeatDuration * perfectTolerancePercent;
        float goodToleranceInSeconds = _currentBeatDuration * goodTolerancePercent;

        if (timeToClosestBeat <= perfectToleranceInSeconds)
        {
            isSequenceActive = true;
            _hasInputForCurrentBeat = true;
            perfectCount++;
            SetSwitchAndPlay(inputSounds.perfectSwitch, "Perfect", playSwitchContainerEvent, keySwitch);
            currentSequence.Add(key);
            OnSequenceKeyPressed?.Invoke(key.ToString(), Color.green);
        }
        else if (timeToClosestBeat <= goodToleranceInSeconds)
        {
            isSequenceActive = true;
            _hasInputForCurrentBeat = true;
            SetSwitchAndPlay(inputSounds.goodSwitch, "Good", playSwitchContainerEvent, keySwitch);
            currentSequence.Add(key);
            OnSequenceKeyPressed?.Invoke(key.ToString(), Color.yellow);
        }
        else
        {
            SetSwitchAndPlay(inputSounds.failedSwitch, "Failed (Off-beat)", playSwitchContainerEvent, keySwitch);
            OnSequenceFail?.Invoke();
            ResetSequence();
            return;
        }

        ValidateSequence();
    }


    private void ValidateSequence()
    {
        if (currentSequence.Count < 4)
        {
            return;
        }

        foreach (CharacterData_SO characterData in availablePlayerCharactersInTeam)
        {
            if (characterData == null)
            {
                Debug.LogWarning("[SequenceController] CharacterData_SO is null in availablePlayerCharactersInTeam.");
                continue;
            }
            
            if (characterData.InvocationSequence != null &&
                CompareKeySequence(currentSequence, characterData.InvocationSequence.Select(input => ConvertInputTypeToKeyCode(input)).ToList()))
            {
                Debug.Log($"[SequenceController] Character Invocation Sequence Matched: {characterData.DisplayName}");
                StartCoroutine(HandleSuccessfulSequence(() => {
                    OnCharacterInvocationSequenceComplete?.Invoke(characterData, perfectCount);
                }, currentSequence.Count));
                return;
            }
        }

        foreach (GlobalSpellData_SO spellData in availableGlobalSpells)
        {
            if (spellData.SpellSequence != null &&
                CompareKeySequence(currentSequence, spellData.SpellSequence.Select(input => ConvertInputTypeToKeyCode(input)).ToList()))
            {
                Debug.Log($"[SequenceController] Global Spell Sequence Matched: {spellData.DisplayName}");
                StartCoroutine(HandleSuccessfulSequence(() => {
                    OnGlobalSpellSequenceComplete?.Invoke(spellData, perfectCount);
                }, currentSequence.Count));
                return;
            }
        }

        Debug.LogWarning($"[SequenceController] Sequence of 4 inputs completed but did not match any known character invocation or global spell: {string.Join("-", currentSequence)}");
        OnSequenceFail?.Invoke();
        ResetSequence();
    }

   
    private bool CompareKeySequence(List<KeyCode> inputSequence, List<KeyCode> targetSequence)
    {
        return inputSequence.SequenceEqual(targetSequence);
    }
    
    private KeyCode ConvertInputTypeToKeyCode(InputType inputType)
    {
        switch (inputType)
        {
            case InputType.X: return KeyCode.X;
            case InputType.C: return KeyCode.C;
            case InputType.V: return KeyCode.V;
            default:
                Debug.LogError($"[SequenceController] InputType inconnu: {inputType}");
                return KeyCode.None;
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
        isSequenceActive = false;
        perfectCount = 0;
        currentSequence.Clear();
        OnSequenceDisplayCleared?.Invoke();
    }

    private void HandleBeat(float beatDuration)
    {
        lastBeatTime = Time.time;
        _hasInputForCurrentBeat = false;
        _currentBeatDuration = beatDuration;

        if (isSequenceActive && currentSequence.Count > 0 && !_hasInputForCurrentBeat)
        {
            // Cette condition est un peu complexe. Il faut décider si "sauter" un beat doit reset.
            // La logique actuelle avec la vérification anti-spam gère déjà le reset si on joue trop tôt.
            // Laissons la logique de "saut de beat" pour une itération future si nécessaire.
        }
    }

    private void SetSwitchAndPlay(AK.Wwise.Switch switchState, string switchName, AK.Wwise.Event playEvent, AK.Wwise.Switch keySwitch)
    {
        if (switchState != null && switchState.IsValid() && keySwitch != null && keySwitch.IsValid())
        {
            switchState.SetValue(gameObject);
            keySwitch.SetValue(gameObject);
            if (playEvent != null && playEvent.IsValid())
            {
                playEvent.Post(gameObject);
            } else if (playEvent == null) {
                Debug.LogWarning($"[SequenceController] Wwise playEvent is null for SetSwitchAndPlay (Switch: {switchName}).");
            } else if (!playEvent.IsValid()) {
                Debug.LogWarning($"[SequenceController] Wwise playEvent '{playEvent.Name}' is not valid for SetSwitchAndPlay (Switch: {switchName}).");
            }
        }
        else
        {
            if (switchState == null) Debug.LogWarning($"[SequenceController] Wwise timing switch '{switchName}' not assigned.");
            else if (!switchState.IsValid()) Debug.LogWarning($"[SequenceController] Wwise timing switch '{switchState.Name}' (for {switchName}) is not valid.");
            if (keySwitch == null) Debug.LogWarning($"[SequenceController] Wwise key switch for the pressed key not assigned.");
            else if (!keySwitch.IsValid()) Debug.LogWarning($"[SequenceController] Wwise key switch '{keySwitch.Name}' is not valid.");
        }
    }
}