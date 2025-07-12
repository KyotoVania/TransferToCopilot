using UnityEngine;
using System.Collections.Generic;
using ScriptableObjects;

public class TutorialManager : MonoBehaviour
{
    [Header("Debug/Dev")]
    [Tooltip("Active ce booléen pour bypasser complètement le tutoriel en mode debug.")]
    [SerializeField] private bool bypassTutorial = false;

    [Header("Configuration du Tuto")]
    [SerializeField] private TutorialUIManager uiManager;
    [SerializeField] private TutorialSequence_SO sequenceToPlay;

    [Header("Références au HUD du Tutoriel (Listes)")]
    [SerializeField] private List<GameObject> invocationUIObjects = new List<GameObject>();
    [SerializeField] private List<GameObject> comboUIObjects = new List<GameObject>();
    [SerializeField] private List<GameObject> goldUIObjects = new List<GameObject>();
    [SerializeField] private List<GameObject> unitsAndSpellsUIObjects = new List<GameObject>();
    public static TutorialManager Instance { get; private set; }

    /// <summary>
    /// Indique si une séquence de tutoriel est actuellement en cours.
    /// Peut être consulté par d'autres systèmes (comme PlayerBuilding).
    /// </summary>
    public static bool IsTutorialActive { get; private set; }

    /// <summary>
    /// Événement déclenché lorsque le tutoriel est entièrement terminé.
    /// </summary>
    public static event System.Action OnTutorialCompleted;

    /// <summary>
    /// Expose l'étape actuelle pour que les observers puissent y accéder
    /// </summary>
    public TutorialStep CurrentStep => currentStep;

    private Queue<TutorialStep> tutorialQueue;
    private TutorialStep currentStep;

    private int inputCounter = 0;
    private int beatCounter = 0;

    
    protected void Awake() 
    {
        // Logique du singleton de scène
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Note : NE PAS appeler DontDestroyOnLoad(gameObject);	
        
        // Le reste de votre logique Awake/Start originale peut rester ici.
    }
    
    void Start()
    {
        if (uiManager == null || sequenceToPlay == null)
        {
            Debug.LogError("TutorialManager n'est pas configuré correctement !", this);
            enabled = false;
            return;
        }
        HideAllTutorialHUD();
        // Le tutoriel n'est pas actif par défaut au chargement de la scène.
        IsTutorialActive = false;
    }

    public void StartTutorial()
    {
        // Si le mode bypass est activé, on skip complètement le tutoriel
        if (bypassTutorial)
        {
            Debug.Log("[TutorialManager] Mode bypass activé - Skip du tutoriel");
            
            // On s'assure que le panel de tutoriel est caché
            if (uiManager != null)
            {
                uiManager.HidePanel();
            }
            
            // On affiche tous les éléments d'UI du tutoriel immédiatement
            ShowInvocationUI();
            ShowComboUI();
            ShowGoldUI();
            ShowUnitsAndSpellsUI();
            
            // On s'assure que le tutoriel n'est pas marqué comme actif
            IsTutorialActive = false;
            
            // On déclenche l'événement de fin de tutoriel pour que les autres systèmes sachent qu'on a "terminé"
            OnTutorialCompleted?.Invoke();
            
            Debug.Log("Tutoriel bypassé - Tous les éléments UI sont maintenant visibles.");
            return;
        }

        if (IsTutorialActive || (tutorialQueue != null && tutorialQueue.Count > 0)) return;
        if (TutorialBannerObserver.Instance != null) TutorialBannerObserver.Instance.ResetStatus();

        // Active l'interrupteur
        IsTutorialActive = true;

        Debug.Log("Début de la séquence de tutoriel !");
        tutorialQueue = new Queue<TutorialStep>(sequenceToPlay.steps);
        AdvanceToNextStepInternal();
    }

    /// <summary>
    /// Méthode publique pour permettre aux observers d'avancer le tutoriel
    /// </summary>
    public void AdvanceToNextStep()
    {
        AdvanceToNextStepInternal();
    }

    private void AdvanceToNextStepInternal()
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
            EndTutorial();
        }
    }

    private void EndTutorial()
    {
        Debug.Log("Fin de la séquence de tutoriel.");
        currentStep = null;
        if (uiManager != null) uiManager.HidePanel();

        // On désactive l'interrupteur et on notifie que c'est terminé.
        IsTutorialActive = false;
        OnTutorialCompleted?.Invoke(); 
        
        
    }

    private void SubscribeToCurrentTrigger()
    {
        if (currentStep == null) return;

        inputCounter = 0;
        beatCounter = 0;

        switch (currentStep.triggerType)
        {
            case TutorialTriggerType.BeatCount:
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
            case TutorialTriggerType.MomentumSpend:
                // On s'abonne à l'événement du TutorialMomentumManager
                if (TutorialMomentumManager.Instance != null)
                {
                    TutorialMomentumManager.Instance.OnMomentumSpent += HandleMomentumSpent;
                }
                break;
            case TutorialTriggerType.FeverLevelReached:
                // Les TutorialFeverObserver se charge automatiquement via son Start()
                // Pas besoin de souscription manuelle ici
                break;
            case TutorialTriggerType.ComboCountReached:
                // Les TutorialComboObserver se charge automatiquement via son Start()
                // Pas besoin de souscription manuelle ici
                break;
            case TutorialTriggerType.SequencePanelHUD:
                // Les TutorialSequencePanelObserver se charge automatiquement via son Start()
                // Pas besoin de souscription manuelle ici
                break;
        }
    }

    private void UnsubscribeFromCurrentTrigger()
    {
        if (currentStep == null) return;

        switch (currentStep.triggerType)
        {
            case TutorialTriggerType.BeatCount:
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
            case TutorialTriggerType.MomentumSpend:
                // On se désabonne proprement
                if (TutorialMomentumManager.Instance != null)
                {
                    TutorialMomentumManager.Instance.OnMomentumSpent -= HandleMomentumSpent;
                }
                break;
            case TutorialTriggerType.FeverLevelReached:
                // Les TutorialFeverObserver se décharge automatiquement
                // Pas besoin de désouscription manuelle ici
                break;
            case TutorialTriggerType.ComboCountReached:
                // Les TutorialComboObserver se décharge automatiquement
                // Pas besoin de désouscription manuelle ici
                break;
			
        }
    }

    /// <summary>
    /// Gère l'événement de dépense de momentum et fait avancer le tutoriel.
    /// </summary>
    private void HandleMomentumSpent()
    {
        // On n'a pas besoin de vérifier de paramètre ici, la simple dépense suffit.
        AdvanceToNextStep();
    }

    private void HandleBeat(float beatDuration) { beatCounter++; if (beatCounter >= currentStep.triggerParameter) AdvanceToNextStep(); }
    private void HandlePlayerInput(string key, Color timingColor) { inputCounter++; if (inputCounter >= currentStep.triggerParameter) AdvanceToNextStep(); }
    private void HandleBannerPlacement() { AdvanceToNextStep(); }
    private void HandleUnitSummoned(CharacterData_SO characterData, int perfectCount)
    {
        Debug.Log($"[TutorialManager] Séquence d'invocation pour '{characterData.DisplayName}' détectée, le tutoriel avance.");
        AdvanceToNextStep();
    }

    private void OnDestroy()
    {
        UnsubscribeFromCurrentTrigger();
        if (Instance == this)
        {
            Instance = null;
        }
    }
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

    private void SetGroupVisibility(List<GameObject> uiObjects, bool visible)
    {
        foreach (var obj in uiObjects)
        {
            if (obj != null)
            {
                CanvasGroup canvasGroup = obj.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = obj.AddComponent<CanvasGroup>();
                }
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
        }
    }

    public void HideAllTutorialHUD()
    {
        SetGroupVisibility(invocationUIObjects, false);
        SetGroupVisibility(comboUIObjects, false);
        SetGroupVisibility(goldUIObjects, false);
        SetGroupVisibility(unitsAndSpellsUIObjects, false);
    }

    public void ShowInvocationUI() { SetGroupVisibility(invocationUIObjects, true); }
    public void ShowComboUI() { SetGroupVisibility(comboUIObjects, true); }
    public void ShowGoldUI() { SetGroupVisibility(goldUIObjects, true); }
    public void ShowUnitsAndSpellsUI() { SetGroupVisibility(unitsAndSpellsUIObjects, true); }
}