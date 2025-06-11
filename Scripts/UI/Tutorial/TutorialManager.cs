using UnityEngine;
using System.Collections.Generic;
using ScriptableObjects;

public class TutorialManager : SingletonPersistent<TutorialManager>
{
    [Header("Configuration du Tuto")]
    [SerializeField] private TutorialUIManager uiManager;
    [SerializeField] private TutorialSequence_SO sequenceToPlay;

    [Header("Références au HUD du Tutoriel (Listes)")]
    [SerializeField] private List<GameObject> invocationUIObjects = new List<GameObject>();
    [SerializeField] private List<GameObject> comboUIObjects = new List<GameObject>();
    [SerializeField] private List<GameObject> goldUIObjects = new List<GameObject>();
    [SerializeField] private List<GameObject> unitsAndSpellsUIObjects = new List<GameObject>();

    private Queue<TutorialStep> tutorialQueue;
    private TutorialStep currentStep;

    private int inputCounter = 0;
    private int beatCounter = 0;

    void Start()
    {
        if (uiManager == null || sequenceToPlay == null)
        {
            Debug.LogError("TutorialManager n'est pas configuré correctement !", this);
            enabled = false;
            return;
        }
        HideAllTutorialHUD();
    }

    public void StartTutorial()
    {
        if (tutorialQueue != null && tutorialQueue.Count > 0) return;
        if (TutorialBannerObserver.Instance != null) TutorialBannerObserver.Instance.ResetStatus();

        Debug.Log("Début de la séquence de tutoriel !");
        tutorialQueue = new Queue<TutorialStep>(sequenceToPlay.steps);
        AdvanceToNextStep();
    }

    private void AdvanceToNextStep()
    {
        UnsubscribeFromCurrentTrigger();

        if (tutorialQueue.Count > 0)
        {
            currentStep = tutorialQueue.Dequeue();
            uiManager.ShowStep(currentStep);
            TriggerStepStartAction(currentStep);
            SubscribeToCurrentTrigger();
        }
        else
        {
            Debug.Log("Fin de la séquence de tutoriel.");
            currentStep = null;
            uiManager.HidePanel();
        }
    }

    private void SubscribeToCurrentTrigger()
    {
        if (currentStep == null) return;

        inputCounter = 0;
        beatCounter = 0;

        switch (currentStep.triggerType)
        {
            case TutorialTriggerType.BeatCount:
                // --- MODIFICATION : On s'abonne à MusicManager.Instance ---
                if (MusicManager.Instance != null) MusicManager.Instance.OnBeat += HandleBeat;
                break;
            case TutorialTriggerType.PlayerInputs:
                SequenceController.OnSequenceKeyPressed += HandlePlayerInput;
                break;
            case TutorialTriggerType.BannerPlacedOnBuilding:
                if (BannerController.Exists && TutorialBannerObserver.Instance != null)
                {
                    TutorialBannerObserver.Instance.OnBannerPlacedOnBuilding += HandleBannerPlacement;
                    BannerController.Instance.AddObserver(TutorialBannerObserver.Instance);
                }
                break;
            case TutorialTriggerType.UnitSummoned:
                SequenceController.OnCharacterInvocationSequenceComplete += HandleUnitSummoned;
                break;
        }
    }

    private void UnsubscribeFromCurrentTrigger()
    {
        if (currentStep == null) return;

        switch (currentStep.triggerType)
        {
            case TutorialTriggerType.BeatCount:
                // --- MODIFICATION : On se désabonne de MusicManager.Instance ---
                if (MusicManager.Instance != null) MusicManager.Instance.OnBeat -= HandleBeat;
                break;
            case TutorialTriggerType.PlayerInputs:
                SequenceController.OnSequenceKeyPressed -= HandlePlayerInput;
                break;
            case TutorialTriggerType.BannerPlacedOnBuilding:
                 if (TutorialBannerObserver.Instance != null)
                 {
                    TutorialBannerObserver.Instance.OnBannerPlacedOnBuilding -= HandleBannerPlacement;
                 }
                break;
            case TutorialTriggerType.UnitSummoned:
                SequenceController.OnCharacterInvocationSequenceComplete -= HandleUnitSummoned;
                break;
        }
    }

    // --- Handlers ---
    // --- MODIFICATION : Signature de la méthode mise à jour ---
    private void HandleBeat(float beatDuration) { beatCounter++; if (beatCounter >= currentStep.triggerParameter) AdvanceToNextStep(); }
    private void HandlePlayerInput(string key, Color timingColor) { inputCounter++; if (inputCounter >= currentStep.triggerParameter) AdvanceToNextStep(); }
    private void HandleBannerPlacement() { AdvanceToNextStep(); }
    
    private void HandleUnitSummoned(CharacterData_SO characterData, int perfectCount)
    {
        Debug.Log($"[TutorialManager] Séquence d'invocation pour '{characterData.DisplayName}' détectée, le tutoriel avance.");
        AdvanceToNextStep();
    }

    // --- Reste du script (inchangé) ---
    private void OnDestroy() { UnsubscribeFromCurrentTrigger(); }
    private void TriggerStepStartAction(TutorialStep step) { switch (step.groupToShowOnStart)
        {
            case HUDGroup.Invocation:
                ShowInvocationUI();
                break;
            case HUDGroup.Combo:
                ShowComboUI();
                break;
            case HUDGroup.Gold:
                ShowGoldUI();
                break;
            case HUDGroup.UnitsAndSpells:
                ShowUnitsAndSpellsUI();
                break;
            default:
                // Par défaut ou si None, on ne cache plus tout, on ne fait rien.
                // HideAllTutorialHUD(); // Commenté pour éviter de cacher des UI inutilement
                break;
        }
    }
    public void HideAllTutorialHUD() { 
        invocationUIObjects.ForEach(obj => { if (obj != null) obj.SetActive(false); });
        comboUIObjects.ForEach(obj => { if (obj != null) obj.SetActive(false); });
        goldUIObjects.ForEach(obj => { if (obj != null) obj.SetActive(false); });
        unitsAndSpellsUIObjects.ForEach(obj => { if (obj != null) obj.SetActive(false); });
    }
    public void ShowInvocationUI() { HideAllTutorialHUD(); invocationUIObjects.ForEach(obj => { if (obj != null) obj.SetActive(true); });}
    public void ShowComboUI() { HideAllTutorialHUD(); comboUIObjects.ForEach(obj => { if (obj != null) obj.SetActive(true); }); }
    public void ShowGoldUI() { HideAllTutorialHUD(); goldUIObjects.ForEach(obj => { if (obj != null) obj.SetActive(true); });}
    public void ShowUnitsAndSpellsUI() { HideAllTutorialHUD(); unitsAndSpellsUIObjects.ForEach(obj => { if (obj != null) obj.SetActive(true); });}
}