// Fichier: Scripts/Tutorials/TutorialSequence_SO.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewTutorialSequence", menuName = "Tutoriel/Tutorial Sequence")]
public class TutorialSequence_SO : ScriptableObject
{
    [Tooltip("La liste ordonnée des étapes de ce tutoriel.")]
    public List<TutorialStep> steps = new List<TutorialStep>();
}