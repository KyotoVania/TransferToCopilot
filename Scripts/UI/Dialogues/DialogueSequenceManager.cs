using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using ScriptableObjects;
using Gameplay;


[System.Serializable]
public class DialogueStep
{
    [Tooltip("Juste pour l'organisation dans l'inspecteur.")]
    public string stepName;
    [Tooltip("La caméra virtuelle Cinemachine à activer pour cette étape.")]
    public CinemachineCamera cameraToShow;
    [Tooltip("La séquence de dialogue à afficher.")]
    public DialogueSequence dialogueToShow;
    [Tooltip("Nombre de BATTEMENTS à attendre après la fin du dialogue.")]
    public int beatsToWaitAfterDialogue = 1;
}

public class DialogueSequenceManager : MonoBehaviour
{
    [Header("Étapes de la Séquence")]
    [SerializeField] private List<DialogueStep> dialogueSteps = new List<DialogueStep>();

    [Header("Composants Essentiels")]
    [SerializeField] private CinemachineBrain cinemachineBrain;
    [Tooltip("Le GameObject parent qui contient toute l'interface du jeu (barre d'invocation, etc.) à cacher.")]
    [SerializeField] private GameObject inGameUiContainer;

    [Header("Effet de Transition")]
    [Tooltip("Assignez l'Image UI qui couvre tout l'écran pour le fondu au noir.")]
    [SerializeField] private Image fadeToBlackImage;

    private RhythmGameCameraController playerCameraController;
    private bool isSequenceRunning = false;
    private List<GameObject> _deactivatedAllyUnits = new List<GameObject>();
    private List<GameObject> _deactivatedEnemyUnits = new List<GameObject>();

    void Awake()
    {
        playerCameraController = FindFirstObjectByType<RhythmGameCameraController>();
        if (cinemachineBrain == null && Camera.main != null)
        {
            cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
        }
    }

    void Start()
    {
        StartSequence();
    }

    public void StartSequence()
    {
        if (isSequenceRunning) return;
        StartCoroutine(PlayFullSequence());
    }

    private void SetMainUIChildrenActive(bool isActive)
    {
        if (inGameUiContainer == null) return;
        inGameUiContainer.SetActive(true);
        foreach (Transform child in inGameUiContainer.transform)
        {
            child.gameObject.SetActive(isActive);
        }
        Debug.Log($"[DialogueSequenceManager] Tous les enfants de '{inGameUiContainer.name}' ont été mis à : {isActive}");
    }

    private IEnumerator FadeTransition(bool fadeIn, int totalBeats)
    {
        if (fadeToBlackImage == null || MusicManager.Instance == null || totalBeats <= 0)
        {
            yield break;
        }

        fadeToBlackImage.gameObject.SetActive(true);
        int beatsWaited = 0;

        System.Action<float> onBeatFade = (_) => {
            beatsWaited++;
            float targetAlpha = (float)beatsWaited / totalBeats;
            float newAlpha = fadeIn ? 1.0f - targetAlpha : targetAlpha;
            fadeToBlackImage.color = new Color(0, 0, 0, newAlpha);
        };

        fadeToBlackImage.color = new Color(0, 0, 0, fadeIn ? 1f : 0f);

        MusicManager.Instance.OnBeat += onBeatFade;
        yield return new WaitUntil(() => beatsWaited >= totalBeats);
        MusicManager.Instance.OnBeat -= onBeatFade;

        fadeToBlackImage.color = new Color(0, 0, 0, fadeIn ? 0f : 1f);

        if (fadeIn)
        {
            fadeToBlackImage.gameObject.SetActive(false);
        }
    }

    private IEnumerator PlayFullSequence()
    {
        isSequenceRunning = true;

        if (playerCameraController != null) playerCameraController.controlsLocked = true;
        //SetMainUIChildrenActive(false);

        if (fadeToBlackImage == null)
        {
            isSequenceRunning = false;
            yield break;
        }

        fadeToBlackImage.color = new Color(0, 0, 0, 1);
        fadeToBlackImage.gameObject.SetActive(true);

        SetUnitsGameObjectsActive(false);

        if (dialogueSteps.Count > 0 && dialogueSteps[0].cameraToShow != null)
        {
            ActivateCamera(dialogueSteps[0].cameraToShow);
        }

        yield return StartCoroutine(FadeTransition(true, 3));

        for (int i = 0; i < dialogueSteps.Count; i++)
        {
            var currentStep = dialogueSteps[i];

            if (currentStep.dialogueToShow != null)
            {
                bool dialogueFinished = false;
                System.Action onDialogueEndCallback = () => { dialogueFinished = true; };
                DialogueUIManager.OnDialogueSystemEnd += onDialogueEndCallback;
                DialogueUIManager.Instance.StartDialogue(currentStep.dialogueToShow);
                yield return new WaitUntil(() => dialogueFinished);
                DialogueUIManager.OnDialogueSystemEnd -= onDialogueEndCallback;
            }

            if (currentStep.beatsToWaitAfterDialogue > 0 && MusicManager.Instance != null)
            {
                int beatsWaitedForDelay = 0;
                System.Action<float> onBeatDelay = (_) => { beatsWaitedForDelay++; };
                MusicManager.Instance.OnBeat += onBeatDelay;
                yield return new WaitUntil(() => beatsWaitedForDelay >= currentStep.beatsToWaitAfterDialogue);
                MusicManager.Instance.OnBeat -= onBeatDelay;
            }

            if (i + 1 < dialogueSteps.Count)
            {
                var nextStep = dialogueSteps[i + 1];
                yield return StartCoroutine(FadeAndSwitchCamera(nextStep.cameraToShow));
            }
        }

        SetUnitsGameObjectsActive(true);
        if (cinemachineBrain != null) cinemachineBrain.enabled = false;
        if (playerCameraController != null) playerCameraController.controlsLocked = false;

        TutorialManager tutorialManager = FindFirstObjectByType<TutorialManager>();
        if (tutorialManager != null)
        {
            tutorialManager.StartTutorial();
        }

        isSequenceRunning = false;
        Debug.Log("Séquence de dialogue terminée.");
    }

    private IEnumerator FadeAndSwitchCamera(CinemachineCamera nextCamera)
    {
        yield return StartCoroutine(FadeTransition(false, 3));

        if (nextCamera != null)
        {
            if (cinemachineBrain != null) cinemachineBrain.enabled = false;
            ActivateCamera(nextCamera);
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.position = nextCamera.transform.position;
                mainCam.transform.rotation = nextCamera.transform.rotation;
            }
            yield return null;
            if (cinemachineBrain != null) cinemachineBrain.enabled = true;
        }

        yield return StartCoroutine(FadeTransition(true, 3));
    }

    private void ActivateCamera(CinemachineCamera camToActivate)
    {
        foreach (var step in dialogueSteps)
        {
            if (step.cameraToShow != null)
                step.cameraToShow.Priority = (step.cameraToShow == camToActivate) ? 15 : 10;
        }
    }

    private void SetUnitsGameObjectsActive(bool shouldBeActive)
{
    Debug.Log($"[DialogueSequenceManager] SetUnitsGameObjectsActive called with shouldBeActive: {shouldBeActive}");

    if (!shouldBeActive)
    {
        Debug.Log("[DialogueSequenceManager] Deactivating units...");
        _deactivatedAllyUnits.Clear();
        _deactivatedEnemyUnits.Clear();

        // La logique pour les alliés ne change pas
        if (AllyUnitRegistry.Instance != null)
        {
            foreach (AllyUnit allyUnit in AllyUnitRegistry.Instance.ActiveAllyUnits.ToList())
            {
                if (allyUnit != null && allyUnit.gameObject.activeSelf)
                {
                    _deactivatedAllyUnits.Add(allyUnit.gameObject);
                    allyUnit.gameObject.SetActive(false);
                }
            }
        }

        // On cherche tous les composants EnemyUnit, même sur les objets inactifs.
        EnemyUnit[] allEnemiesInScene = FindObjectsByType<EnemyUnit>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        Debug.Log($"[DialogueSequenceManager] Found {allEnemiesInScene.Length} total enemy units (active and inactive).");

        foreach (EnemyUnit enemy in allEnemiesInScene)
        {
            // On l'ajoute à la liste pour la réactivation future.
            _deactivatedEnemyUnits.Add(enemy.gameObject);
            
            // Et on s'assure qu'il est bien désactivé maintenant.
            if (enemy.gameObject.activeSelf)
            {
                enemy.gameObject.SetActive(false);
                Debug.Log($"[DialogueSequenceManager] Deactivated enemy unit: {enemy.gameObject.name}");
            }
        }
        Debug.Log($"[DialogueSequenceManager] Stored {_deactivatedEnemyUnits.Count} enemy units for reactivation.");
    }
    else
    {
        Debug.Log("[DialogueSequenceManager] Reactivating units...");
        // La réactivation ne change pas, mais elle fonctionnera maintenant pour tous les ennemis
        foreach (GameObject unitGO in _deactivatedAllyUnits)
        {
            if (unitGO != null) unitGO.SetActive(true);
        }

        foreach (GameObject unitGO in _deactivatedEnemyUnits)
        {
            if (unitGO != null)
            {
                unitGO.SetActive(true); // C'est ici que l'unité inactive de base sera activée pour la 1ère fois.
                Debug.Log($"[DialogueSequenceManager] Reactivated enemy unit: {unitGO.name}");
            }
        }

        _deactivatedAllyUnits.Clear();
        _deactivatedEnemyUnits.Clear();
        Debug.Log("[DialogueSequenceManager] Unit reactivation complete, lists cleared");
    }
}
}