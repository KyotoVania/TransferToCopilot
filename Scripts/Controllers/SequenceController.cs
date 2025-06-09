using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

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

    private int perfectCount = 0; // Count of perfect inputs for the current sequence
    public float perfectTolerance = 0.1f;
    public float goodTolerance = 0.25f;

    private bool isSequenceActive = false; // Is a sequence currently active?
    private float lastBeatTime;
    private float lastInputTime;


    // --- Événements ---
    // Events for UI decoupling:
    public static event Action<string, Color> OnSequenceKeyPressed;
    public static event Action OnSequenceDisplayCleared;
    // New event for the sequence using SO
    public static event Action<CharacterData_SO, int> OnCharacterInvocationSequenceComplete;
    public static event Action<GlobalSpellData_SO, int> OnGlobalSpellSequenceComplete;
    
    public static event Action OnSequenceSuccess;
    // Fired when the player presses a key outside tolerances (failed switch).
    public static event Action OnSequenceFail;

    private bool isResponding = false;
    private RhythmManager rhythmManager;

    // Wwise events and input sound settings
    public InputSoundVariants inputSounds;
    public InputSoundVariantsKey inputSoundsKey;
    public AK.Wwise.Event playSwitchContainerEvent;
    // Reference to the UI text element for displaying the sequence
    [SerializeField] private TextMeshProUGUI sequenceDisplay;


    private void Awake()
    {
        // Initialiser les listes ici garantit qu'elles ne sont jamais nulles,
        // même si InitializeWithPlayerTeamAndSpells n'est pas appelée (ce qui serait un problème de logique ailleurs).
        availablePlayerCharactersInTeam = new List<CharacterData_SO>();
        availableGlobalSpells = new List<GlobalSpellData_SO>();
    }

    private void OnEnable()
    {
        RhythmManager.OnBeat += HandleBeat;
    }

     private void OnDisable() //
    {
        if (RhythmManager.Instance != null) // Bonne pratique de vérifier si l'instance existe encore
        {
            RhythmManager.OnBeat -= HandleBeat; //
        }
    }

    private void Start()
    {
        rhythmManager = FindFirstObjectByType<RhythmManager>();
        if (rhythmManager == null)
        {
            Debug.LogError("No RhythmManager found in the scene!");
        }
        //Debug.Log("Sequences initialized. Total sequences: " + sequences.Count);
        OnSequenceDisplayCleared?.Invoke();
    }
    
    
    /// <summary>
    /// Initialise le SequenceController avec l'équipe active du joueur et les sorts globaux disponibles.
    /// Doit être appelée par le GameplayManager après le chargement des données.
    /// </summary>
    public void InitializeWithPlayerTeamAndSpells(List<CharacterData_SO> activeTeam, List<GlobalSpellData_SO> globalSpells)
    {
        availablePlayerCharactersInTeam = activeTeam ?? new List<CharacterData_SO>();
        availableGlobalSpells = globalSpells ?? new List<GlobalSpellData_SO>();

        Debug.Log($"[SequenceController] Initialized with {availablePlayerCharactersInTeam.Count} character(s) in team and {availableGlobalSpells.Count} global spell(s).");
    }
    private void Update()
    {
        if (isResponding) return;

        // Listen for input keys X, C, and V
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
        if (key == KeyCode.X)
            return inputSoundsKey.XSwitch;
        if (key == KeyCode.C)
            return inputSoundsKey.CSwitch;
        if (key == KeyCode.V)
            return inputSoundsKey.VSwitch;

        Debug.LogWarning("[SequenceController] No Wwise key switch assigned for this key input!");
        return null;
    }
    //TODO ; check if soundVariants.xxxSwitch is better
    private void ProcessInput(KeyCode key, InputSoundVariants soundVariants, AK.Wwise.Event playEvent)
    {
    
        if (rhythmManager == null) { //
             Debug.LogError("[SequenceController] RhythmManager is null in ProcessInput. Cannot evaluate timing."); //
             SetSwitchAndPlay(soundVariants.failedSwitch, "Failed (No RhythmManager)", playEvent, GetKeySwitch(key)); //
             OnSequenceFail?.Invoke(); //
             ResetSequence(); //
             return; //
        }
        
        float currentTime = Time.time;
        float timeDifference = Mathf.Abs(currentTime - lastBeatTime);

        // Determine which switch to use for the pressed key
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
            // Fire the fail event if a failed switch is used.
            OnSequenceFail?.Invoke();
            ResetSequence();
            return;
        }

        ValidateSequence();
    }

    private void ValidateSequence()
    {
        // Wait until 4 keys have been pressed
        if (currentSequence.Count < 4)
        {
            return;
        }

        foreach (CharacterData_SO characterData in availablePlayerCharactersInTeam)
        {
            if (characterData == null)
            {
                Debug.LogWarning("[SequenceController] CharacterData_SO is null in availablePlayerCharactersInTeam.");
                continue; // Passe au prochain personnage dans la liste si celui-ci est null
            }

            // Le reste de la logique pour ce personnage
            if (characterData.InvocationSequence != null &&
                CompareKeySequence(currentSequence, characterData.InvocationSequence.Select(input => ConvertInputTypeToKeyCode(input)).ToList()))
            {
                Debug.Log($"[SequenceController] Character Invocation Sequence Matched: {characterData.DisplayName}");
                StartCoroutine(HandleSuccessfulSequence(() => {
                    OnCharacterInvocationSequenceComplete?.Invoke(characterData, perfectCount);
                }, currentSequence.Count));
                return; // Séquence trouvée et gérée, on sort de ValidateSequence
            }
        }

        // 2. Vérifier les sorts globaux
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
        // Create sequence data even if no match found
        Debug.LogWarning($"[SequenceController] Sequence of 4 inputs completed but did not match any known character invocation or global spell: {string.Join("-", currentSequence)}");
        OnSequenceFail?.Invoke(); // Indique qu'une séquence de 4 a été faite mais n'a rien déclenché.
        // Pourrait aussi déclencher OnSequenceExecuted avec une "Sequence" générique si besoin.
        // Sequence customSequence = new Sequence(new List<KeyCode>(currentSequence), "Unrecognized Sequence");
        // OnSequenceExecuted?.Invoke(customSequence, perfectCount);
        ResetSequence(); // Réinitialiser immédiatement après un échec de reconnaissance
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
                return KeyCode.None; // Ou une autre gestion d'erreur
        }
    }
    
    /// <summary>
    /// Coroutine pour gérer la réponse à une séquence réussie (invocation ou sort).
    /// </summary>
    private IEnumerator HandleSuccessfulSequence(Action specificEventCallback, int sequenceLength)
    {
        isResponding = true; //

        OnSequenceSuccess?.Invoke(); // Indique qu'une séquence de 4 a été validée et correspond à une action.

        specificEventCallback?.Invoke(); // Déclenche l'événement spécifique (invocation ou sort)

        if (rhythmManager != null)
        {
            // Attendre un nombre de battements égal à la longueur de la séquence pour le "cooldown" visuel/sonore.
            float responseDuration = rhythmManager.GetBeatDuration() * sequenceLength; //
            if (responseDuration < 0.1f) responseDuration = 0.5f; // Sécurité pour éviter une attente nulle ou négative.
            yield return new WaitForSeconds(responseDuration); //
        } else {
            yield return new WaitForSeconds(0.5f); // Fallback si pas de rhythmManager
        }

        // perfectCount est réinitialisé dans ResetSequence, qui est appelé après cette coroutine
        isResponding = false; //
        // isSequenceActive = false; // Sera fait par ResetSequence
        ResetSequence(); //
        // Debug.Log("[SequenceController] HandleSuccessfulSequence complete, sequence reset.");
    }

    private bool IsPrefix(List<KeyCode> input, List<KeyCode> target)
    {
        if (input.Count > target.Count)
            return false;

        for (int i = 0; i < input.Count; i++)
        {
            if (input[i] != target[i])
                return false;
        }
        return true;
    }

    private void ResetSequence()
    {
        isSequenceActive = false;
        currentSequence.Clear();
        OnSequenceDisplayCleared?.Invoke();
    }


    private void HandleBeat()
    {
        lastBeatTime = Time.time;
    }

    private void SetSwitchAndPlay(AK.Wwise.Switch switchState, string switchName, AK.Wwise.Event playEvent, AK.Wwise.Switch keySwitch) //
    {
        if (switchState != null && switchState.IsValid() && keySwitch != null && keySwitch.IsValid()) //
        {
            switchState.SetValue(gameObject); //
            keySwitch.SetValue(gameObject); //
            if (playEvent != null && playEvent.IsValid()) //
            {
                playEvent.Post(gameObject); //
            } else if (playEvent == null) {
                Debug.LogWarning($"[SequenceController] Wwise playEvent is null for SetSwitchAndPlay (Switch: {switchName}).");
            } else if (!playEvent.IsValid()) {
                Debug.LogWarning($"[SequenceController] Wwise playEvent '{playEvent.Name}' is not valid for SetSwitchAndPlay (Switch: {switchName}).");
            }
        }
        else
        {
            if (switchState == null) Debug.LogWarning($"[SequenceController] Wwise timing switch '{switchName}' not assigned."); //
            else if (!switchState.IsValid()) Debug.LogWarning($"[SequenceController] Wwise timing switch '{switchState.Name}' (for {switchName}) is not valid.");
            if (keySwitch == null) Debug.LogWarning($"[SequenceController] Wwise key switch for the pressed key not assigned.");
            else if (!keySwitch.IsValid()) Debug.LogWarning($"[SequenceController] Wwise key switch '{keySwitch.Name}' is not valid.");
        }
    }
}
