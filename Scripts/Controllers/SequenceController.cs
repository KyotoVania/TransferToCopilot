using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using ScriptableObjects;

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
    public float perfectTolerance = 0.1f;
    public float goodTolerance = 0.25f;

    private bool isSequenceActive = false;
    private float lastBeatTime;
    private float lastInputTime;

    // --- Événements ---
    public static event Action<string, Color> OnSequenceKeyPressed;
    public static event Action OnSequenceDisplayCleared;
    public static event Action<CharacterData_SO, int> OnCharacterInvocationSequenceComplete;
    public static event Action<GlobalSpellData_SO, int> OnGlobalSpellSequenceComplete;
    public static event Action OnSequenceSuccess;
    public static event Action OnSequenceFail;

    private bool isResponding = false;
    
    // --- MODIFICATION : Référence à MusicManager ---
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
        // --- MODIFICATION : Utilisation de MusicManager ---
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += HandleBeat;
        }
    }

    private void OnDisable()
    {
        // --- MODIFICATION : Utilisation de MusicManager ---
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleBeat;
        }
    }

    private void Start()
    {
        // --- MODIFICATION : Recherche de MusicManager ---
        musicManager = FindFirstObjectByType<MusicManager>();
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

    private void Update()
    {
        if (isResponding) return;

        if (Input.GetKeyDown(KeyCode.X))
        {
            ProcessInput(KeyCode.X, inputSounds, playSwitchContainerEvent);
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            ProcessInput(KeyCode.C, inputSounds, playSwitchContainerEvent);
        }
        else if (Input.GetKeyDown(KeyCode.V))
        {
            ProcessInput(KeyCode.V, inputSounds, playSwitchContainerEvent);
        }
    }

    private AK.Wwise.Switch GetKeySwitch(KeyCode key)
    {
        if (key == KeyCode.X) return inputSoundsKey.XSwitch;
        if (key == KeyCode.C) return inputSoundsKey.CSwitch;
        if (key == KeyCode.V) return inputSoundsKey.VSwitch;
        Debug.LogWarning("[SequenceController] No Wwise key switch assigned for this key input!");
        return null;
    }
    
    private void ProcessInput(KeyCode key, InputSoundVariants soundVariants, AK.Wwise.Event playEvent)
    {
        // --- MODIFICATION : Utilisation de musicManager ---
        if (musicManager == null) {
             Debug.LogError("[SequenceController] MusicManager is null in ProcessInput. Cannot evaluate timing.");
             SetSwitchAndPlay(soundVariants.failedSwitch, "Failed (No MusicManager)", playEvent, GetKeySwitch(key));
             OnSequenceFail?.Invoke();
             ResetSequence();
             return;
        }
        
        float currentTime = Time.time;
        float timeDifference = Mathf.Abs(currentTime - lastBeatTime);

        AK.Wwise.Switch keySwitch = GetKeySwitch(key);

        if (timeDifference <= perfectTolerance)
        {
            isSequenceActive = true;
            perfectCount++;
            SetSwitchAndPlay(inputSounds.perfectSwitch, "Perfect", playEvent, keySwitch);
            currentSequence.Add(key);
            lastInputTime = currentTime;
            OnSequenceKeyPressed?.Invoke(key.ToString(), Color.green);
        }
        else if (timeDifference <= goodTolerance)
        {
            isSequenceActive = true;
            SetSwitchAndPlay(inputSounds.goodSwitch, "Good", playEvent, keySwitch);
            currentSequence.Add(key);
            lastInputTime = currentTime;
            OnSequenceKeyPressed?.Invoke(key.ToString(), Color.yellow);
        }
        else
        {
            SetSwitchAndPlay(inputSounds.failedSwitch, "Failed", playEvent, keySwitch);
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
        if (inputSequence.Count != targetSequence.Count) return false;
        for (int i = 0; i < inputSequence.Count; i++)
        {
            if (inputSequence[i] != targetSequence[i]) return false;
        }
        return true;
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

        // --- MODIFICATION : Utilisation de musicManager ---
        if (musicManager != null)
        {
            float responseDuration = musicManager.GetBeatDuration() * sequenceLength;
            if (responseDuration < 0.1f) responseDuration = 0.5f;
            yield return new WaitForSeconds(responseDuration);
        } else {
            yield return new WaitForSeconds(0.5f);
        }

        isResponding = false;
        ResetSequence();
    }
    
    private void ResetSequence()
    {
        isSequenceActive = false;
        perfectCount = 0; // Réinitialiser le compteur de perfects
        currentSequence.Clear();
        OnSequenceDisplayCleared?.Invoke();
    }

    private void HandleBeat(float beatDuration)
    {
        lastBeatTime = Time.time;
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