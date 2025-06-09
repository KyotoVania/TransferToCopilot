using UnityEngine;
using System; // Nécessaire pour [Serializable]

[Serializable]
public class DialogueEntry
{
    public string speakerName;
    public Sprite speakerPortrait; // Assigne une image pour le portrait dans l'Inspecteur du DialogueSequence
    [TextArea(3, 10)]
    public string dialogueText;
}