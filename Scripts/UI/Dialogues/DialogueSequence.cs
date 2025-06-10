namespace ScriptableObjects
{
    using UnityEngine;
    using System.Collections.Generic;
    
    [CreateAssetMenu(fileName = "NewDialogueSequence", menuName = "Narration/Dialogue Sequence")]
    public class DialogueSequence : ScriptableObject
    {
        [Tooltip("Liste des entrées de dialogue qui composent cette séquence.")]
        public List<DialogueEntry> entries = new List<DialogueEntry>();
    }
}
