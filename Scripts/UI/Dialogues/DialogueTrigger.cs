using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DialogueTrigger : MonoBehaviour
{
    [Header("Configuration du Dialogue")]
    [Tooltip("La séquence de dialogue à afficher lorsque les conditions sont remplies.")]
    [SerializeField] private DialogueSequence dialogueSequenceToShow;

    [Header("Conditions de Déclenchement (Bâtiments)")]
    [Tooltip("Liste des bâtiments qui doivent être détruits ou capturés par le joueur. Laisser vide pour déclencher au démarrage.")]
    public List<Building> prerequisiteBuildings = new List<Building>();

    [Tooltip("Si vrai et qu'il n'y a pas de bâtiments prérequis, le dialogue se déclenche au démarrage du niveau.")]
    [SerializeField] private bool triggerAtStartIfNoPrerequisites = true;

    [Tooltip("Si vrai, ce dialogue ne se déclenchera qu'une seule fois.")]
    [SerializeField] private bool onlyTriggerOnce = true;

    private bool _hasBeenTriggered = false;
    private List<Building> _buildingsToMonitor = new List<Building>();

    private RhythmGameCameraController _cameraController;

    void Start()
    {
        if (dialogueSequenceToShow == null)
        {
            Debug.LogWarning($"[{gameObject.name}/DialogueTrigger] Aucun DialogueSequence assigné.", this);
            enabled = false;
            return;
        }

        if (DialogueUIManager.Instance == null)
        {
            Debug.LogError($"[{gameObject.name}/DialogueTrigger] DialogueUIManager.Instance non trouvé. Impossible de déclencher des dialogues.", this);
            enabled = false;
            return;
        }

        // Initialiser la surveillance des bâtiments
        _buildingsToMonitor = prerequisiteBuildings.Where(b => b != null).ToList();

        if (_buildingsToMonitor.Count > 0)
        {
            Building.OnBuildingDestroyed += HandleBuildingEvent;
            Building.OnBuildingTeamChangedGlobal += HandleBuildingTeamChangeEvent;
            Debug.Log($"[{gameObject.name}/DialogueTrigger] Abonné aux événements des bâtiments. Surveillance de {_buildingsToMonitor.Count} bâtiments pour la séquence '{dialogueSequenceToShow.name}'.");
            CheckAllConditionsMet(); // Vérifier une première fois
        }
        else if (triggerAtStartIfNoPrerequisites)
        {
            Debug.Log($"[{gameObject.name}/DialogueTrigger] Aucune condition de bâtiment, déclenchement au démarrage pour la séquence '{dialogueSequenceToShow.name}'.");
            AttemptToTriggerDialogue();
        }
    }

    void OnDestroy()
    {
        UnsubscribeFromBuildingEvents();
    }

    void UnsubscribeFromBuildingEvents()
    {
        Building.OnBuildingDestroyed -= HandleBuildingEvent;
        Building.OnBuildingTeamChangedGlobal -= HandleBuildingTeamChangeEvent;
    }

    private void HandleBuildingEvent(Building building)
    {
        if ((onlyTriggerOnce && _hasBeenTriggered)) return; // Ne plus vérifier si déjà déclenché et onlyTriggerOnce
        if (_buildingsToMonitor.Contains(building))
        {
            CheckAllConditionsMet();
        }
    }

    private void HandleBuildingTeamChangeEvent(Building building, TeamType oldTeam, TeamType newTeam)
    {
        if ((onlyTriggerOnce && _hasBeenTriggered)) return;
        if (_buildingsToMonitor.Contains(building))
        {
            CheckAllConditionsMet();
        }
    }

    void CheckAllConditionsMet()
    {
        if (onlyTriggerOnce && _hasBeenTriggered) return;
        if (DialogueUIManager.Instance.IsDialogueActive()) return; // Ne pas vérifier si un dialogue est déjà en cours

        bool allConditionsSatisfied = true;
        if (_buildingsToMonitor.Count == 0 && !triggerAtStartIfNoPrerequisites) // Si liste vide et pas de trigger au start
        {
            allConditionsSatisfied = false; // Il n'y a pas de condition à remplir si la liste est vide intentionnellement
        }

        foreach (Building building in _buildingsToMonitor)
        {
            if (building == null) continue; // Ignorer les bâtiments déjà détruits/nullifiés

            bool currentBuildingConditionMet = false;
            if (building.CurrentHealth <= 0)
            {
                currentBuildingConditionMet = true;
            }
            else if (building is NeutralBuilding neutralBuilding)
            {
                if (neutralBuilding.Team == TeamType.Player) // Capturé par le joueur
                {
                    currentBuildingConditionMet = true;
                }
            }

            if (!currentBuildingConditionMet)
            {
                allConditionsSatisfied = false;
                break;
            }
        }

        if (allConditionsSatisfied)
        {
            AttemptToTriggerDialogue();
        }
    }

    private void AttemptToTriggerDialogue()
    {
        if (onlyTriggerOnce && _hasBeenTriggered) return;
        if (DialogueUIManager.Instance.IsDialogueActive())
        {
            Debug.LogWarning($"[{gameObject.name}/DialogueTrigger] Tentative de déclencher '{dialogueSequenceToShow.name}', mais un dialogue est déjà actif.");
            return;
        }


        Debug.Log($"[{gameObject.name}/DialogueTrigger] Toutes les conditions remplies OU déclenchement au démarrage pour '{dialogueSequenceToShow.name}'. Lancement du dialogue.");
        DialogueUIManager.Instance.StartDialogue(dialogueSequenceToShow);
        _hasBeenTriggered = true;

        if (onlyTriggerOnce)
        {
            UnsubscribeFromBuildingEvents(); // Plus besoin de surveiller
            Debug.Log($"[{gameObject.name}/DialogueTrigger] Dialogue déclenché une fois, désabonnement des événements pour '{dialogueSequenceToShow.name}'.");
        }
    }

    [ContextMenu("Debug: Force Trigger Dialogue")]
    public void Debug_ForceTriggerDialogue()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug_ForceTriggerDialogue ne peut être appelé qu'en mode Play.");
            return;
        }
        if (DialogueUIManager.Instance == null)
        {
             Debug.LogError($"[{gameObject.name}/DialogueTrigger] DEBUG: DialogueUIManager non trouvé.");
            return;
        }
         if (dialogueSequenceToShow == null)
        {
             Debug.LogError($"[{gameObject.name}/DialogueTrigger] DEBUG: dialogueSequenceToShow non assigné.");
            return;
        }
        if (DialogueUIManager.Instance.IsDialogueActive())
        {
            Debug.LogWarning($"[{gameObject.name}/DialogueTrigger] DEBUG: Un dialogue est déjà actif.");
            return;
        }

        Debug.Log($"[{gameObject.name}/DialogueTrigger] DEBUG: Forçage du déclenchement du dialogue '{dialogueSequenceToShow.name}' !");
        DialogueUIManager.Instance.StartDialogue(dialogueSequenceToShow);
        _hasBeenTriggered = true; // Marquer comme déclenché
        if (onlyTriggerOnce)
        {
            UnsubscribeFromBuildingEvents();
        }
    }
}