using UnityEngine;

/// <summary>
/// Observe le FeverManager et déclenche une étape de tutoriel 
/// lorsque le mode Fever est activé.
/// </summary>
public class TutorialFeverObserver : MonoBehaviour
{
    private FeverManager feverManager;
    private TutorialManager tutorialManager;

    void Start()
    {
        // Récupérer les instances
        feverManager = FeverManager.Instance;
        tutorialManager = TutorialManager.Instance;

        if (feverManager == null || tutorialManager == null)
        {
            Debug.LogError("[TutorialFeverObserver] Un des managers requis (FeverManager, TutorialManager) n'a pas été trouvé.", this);
            enabled = false;
            return;
        }

        // S'abonner à l'événement de changement d'état du mode Fever
        feverManager.OnFeverStateChanged += HandleFeverStateChanged;
    }

    private void OnDestroy()
    {
        // Se désabonner pour éviter les fuites de mémoire
        if (FeverManager.Instance != null)
        {
            feverManager.OnFeverStateChanged -= HandleFeverStateChanged;
        }
    }

    private void HandleFeverStateChanged(bool isFeverActive)
    {
        // On ne s'intéresse qu'à l'activation du mode Fever
        if (!isFeverActive) return;

        if (tutorialManager.CurrentStep != null &&
            tutorialManager.CurrentStep.triggerType == TutorialTriggerType.FeverLevelReached)
        {
            Debug.Log("[TutorialFeverObserver] Mode Fever activé. Avancement du tutoriel.");
            tutorialManager.AdvanceToNextStep();
        }
    }
}