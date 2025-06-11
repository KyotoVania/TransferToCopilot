// Fichier: Scripts/Tutorials/TutorialStep.cs (Version Finale)
using UnityEngine;

// Cet enum définit les groupes d'UI que le tutoriel peut contrôler.
public enum HUDGroup
{
    None,
    Invocation,
    Combo,
    Gold,
    UnitsAndSpells,
    All // Pour tout afficher si besoin
}

[System.Serializable]
public class TutorialStep
{
    [Header("Contenu de l'Étape")]
    [Tooltip("Le texte du tutoriel à afficher.")]
    [TextArea(3, 5)]
    public string tutorialText;

    [Header("Condition de Progression")]
    [Tooltip("Le type d'événement qui fera avancer le tutoriel.")]
    public TutorialTriggerType triggerType;

    [Tooltip("Paramètre pour le trigger. Ex: '4' pour PlayerInputs, '8' pour BeatCount.")]
    public int triggerParameter = 1;

    [Header("Action au Démarrage")]
    [Tooltip("Quel groupe d'éléments de l'interface doit être affiché au début de cette étape ?")]
    public HUDGroup groupToShowOnStart; // Remplacement du UnityEvent
}