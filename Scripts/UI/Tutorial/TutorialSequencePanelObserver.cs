using UnityEngine;

/// <summary>
/// Observe l'InputManager et déclenche une étape de tutoriel 
/// lorsque l'action TriggerUi est performée (touche pour afficher le panneau de séquence).
/// </summary>
public class TutorialSequencePanelObserver : MonoBehaviour
{
    private InputManager inputManager;
    private TutorialManager tutorialManager;

    void Start()
    {
        // Récupérer les instances
        inputManager = InputManager.Instance;
        tutorialManager = TutorialManager.Instance;

        if (inputManager == null || tutorialManager == null)
        {
            Debug.LogError("[TutorialSequencePanelObserver] Un des managers requis (InputManager, TutorialManager) n'a pas été trouvé.", this);
            enabled = false;
            return;
        }

        // S'abonner à l'événement TriggerUi
        inputManager.GameplayActions.TriggerUi.performed += HandleTriggerUiPerformed;
    }

    private void OnDestroy()
    {
        // Se désabonner pour éviter les fuites de mémoire
        if (inputManager != null)
        {
            inputManager.GameplayActions.TriggerUi.performed -= HandleTriggerUiPerformed;
        }
    }

    private void HandleTriggerUiPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // Vérifier si l'étape actuelle du tutoriel attend ce trigger
        if (tutorialManager.CurrentStep != null &&
            tutorialManager.CurrentStep.triggerType == TutorialTriggerType.SequencePanelHUD)
        {
            Debug.Log("[TutorialSequencePanelObserver] Action TriggerUi détectée. Avancement du tutoriel.");
            tutorialManager.AdvanceToNextStep();
        }
    }
}
