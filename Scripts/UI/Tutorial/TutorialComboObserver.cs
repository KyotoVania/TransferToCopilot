using UnityEngine;

/// <summary>
/// Observe le ComboController et déclenche une étape de tutoriel 
/// lorsque le combo du joueur atteint le seuil requis.
/// </summary>
public class TutorialComboObserver : MonoBehaviour
{
    private ComboController comboController;
    private TutorialManager tutorialManager;

    void Start()
    {
        // Récupérer les instances des managers
        comboController = ComboController.Instance;
        tutorialManager = TutorialManager.Instance;

        if (comboController == null || tutorialManager == null)
        {
            Debug.LogError("[TutorialComboObserver] Un des managers requis (ComboController, TutorialManager) n'a pas été trouvé.", this);
            enabled = false;
            return;
        }

        // S'abonner à l'événement de changement de combo.
        // On s'abonne via un adaptateur pour correspondre à la logique d'IComboObserver.
        comboController.AddObserver(new ComboObserverAdapter(this));
    }

    private void HandleComboChanged(int newComboValue)
    {
        if (tutorialManager.CurrentStep != null && 
            tutorialManager.CurrentStep.triggerType == TutorialTriggerType.ComboCountReached)
        {
            if (newComboValue >= tutorialManager.CurrentStep.triggerParameter)
            {
                Debug.Log($"[TutorialComboObserver] Combo de {newComboValue} atteint, le seuil de {tutorialManager.CurrentStep.triggerParameter} est dépassé. Avancement du tutoriel.");
                tutorialManager.AdvanceToNextStep();
            }
        }
    }

    // Adaptateur pour utiliser l'interface IComboObserver du ComboController
    private class ComboObserverAdapter : Game.Observers.IComboObserver
    {
        private readonly TutorialComboObserver _owner;

        public ComboObserverAdapter(TutorialComboObserver owner)
        {
            _owner = owner;
        }

        public void OnComboUpdated(int newCombo)
        {
            _owner.HandleComboChanged(newCombo);
        }

        public void OnComboReset()
        {
            // Le reset du combo est géré par OnComboUpdated(0)
        }
    }
}