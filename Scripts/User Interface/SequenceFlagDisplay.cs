// Contenu à mettre dans votre fichier : Scripts2/User Interface/SequenceFlagDisplay.cs

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System; // NOUVEAU : Ajouté pour pouvoir utiliser 'Action'

public class SequenceFlagDisplay : MonoBehaviour
{
    // NOUVEAU : L'événement qui va notifier le BeatVisualizer.
    // Il enverra la couleur du timing (vert, jaune ou rouge).
    public static event Action<Color> OnFlagStateChanged;

    [Header("X Flag Sprites")]
    [Tooltip("Sprite for a perfect timing flag when key X is pressed.")]
    public Sprite perfectXFlagSprite;
    [Tooltip("Sprite for a good timing flag when key X is pressed.")]
    public Sprite goodXFlagSprite;

    [Header("C Flag Sprites")]
    [Tooltip("Sprite for a perfect timing flag when key C is pressed.")]
    public Sprite perfectCFlagSprite;
    [Tooltip("Sprite for a good timing flag when key C is pressed.")]
    public Sprite goodCFlagSprite;

    [Header("V Flag Sprites")]
    [Tooltip("Sprite for a perfect timing flag when key V is pressed.")]
    public Sprite perfectVFlagSprite;
    [Tooltip("Sprite for a good timing flag when key V is pressed.")]
    public Sprite goodVFlagSprite;

    [Header("Display Settings")]
    [Tooltip("Distance in pixels between consecutive flags.")]
    public float flagSpacing = 50f;
    [Tooltip("Size of each flag (width, height).")]
    public Vector2 flagSize = new Vector2(100f, 100f);

    // List to track all spawned flag objects.
    private List<GameObject> spawnedFlags = new List<GameObject>();

    private void OnEnable()
    {
        SequenceController.OnSequenceKeyPressed += SpawnFlag;
        // MODIFIÉ : On s'assure d'écouter les bons événements pour effacer les drapeaux
        SequenceController.OnSequenceFail += ClearFlags;
        SequenceController.OnSequenceSuccess += ClearFlags;
        SequenceController.OnSequenceDisplayCleared += ClearFlags;
    }

    private void OnDisable()
    {
        SequenceController.OnSequenceKeyPressed -= SpawnFlag;
        SequenceController.OnSequenceFail -= ClearFlags;
        SequenceController.OnSequenceSuccess -= ClearFlags;
        SequenceController.OnSequenceDisplayCleared -= ClearFlags;
    }

    /// <summary>
    /// Spawns a flag based on the key pressed and its timing color.
    /// </summary>
    private void SpawnFlag(string key, Color timingColor)
    {
        // Si le coup est raté (rouge), on ne crée pas de drapeau.
        // On notifie juste le BeatVisualizer de l'échec.
        if (timingColor == Color.red)
        {
            OnFlagStateChanged?.Invoke(Color.red);
            return;
        }

        Sprite selectedSprite = null;

        // Détermine quel sprite utiliser (logique originale conservée).
        if (key.Equals("X", System.StringComparison.OrdinalIgnoreCase))
        {
            selectedSprite = (timingColor == Color.green) ? perfectXFlagSprite : goodXFlagSprite;
        }
        else if (key.Equals("C", System.StringComparison.OrdinalIgnoreCase))
        {
            selectedSprite = (timingColor == Color.green) ? perfectCFlagSprite : goodCFlagSprite;
        }
        else if (key.Equals("V", System.StringComparison.OrdinalIgnoreCase))
        {
            selectedSprite = (timingColor == Color.green) ? perfectVFlagSprite : goodVFlagSprite;
        }
        else
        {
            Debug.LogWarning($"Received an unknown key '{key}'.");
            return; // On ne fait rien pour une touche inconnue
        }

        if (selectedSprite == null)
        {
            Debug.LogError("The selected flag sprite is null. Check that all sprites are assigned in the Inspector.");
            return;
        }

        // Crée un nouvel objet pour le drapeau (logique originale conservée).
        GameObject flagObject = new GameObject("Flag_" + key);
        flagObject.transform.SetParent(transform, false);

        Image flagImage = flagObject.AddComponent<Image>();
        flagImage.sprite = selectedSprite;

        RectTransform flagRect = flagObject.GetComponent<RectTransform>();
        if (flagRect != null)
        {
            flagRect.sizeDelta = flagSize;
            float posX = spawnedFlags.Count * flagSpacing;
            flagRect.anchoredPosition = new Vector2(posX, 0);
        }

        flagObject.transform.SetAsFirstSibling();
        spawnedFlags.Add(flagObject);

        // NOUVEAU : On déclenche l'événement pour notifier le BeatVisualizer.
        OnFlagStateChanged?.Invoke(timingColor);
    }

    /// <summary>
    /// Clears all spawned flag objects.
    /// </summary>
    private void ClearFlags()
    {
        foreach (GameObject flag in spawnedFlags)
        {
            Destroy(flag);
        }
        spawnedFlags.Clear();

        // NOUVEAU : On notifie aussi le BeatVisualizer quand on efface les drapeaux,
        // pour qu'il puisse considérer cela comme un échec.
        OnFlagStateChanged?.Invoke(Color.red);
    }
}