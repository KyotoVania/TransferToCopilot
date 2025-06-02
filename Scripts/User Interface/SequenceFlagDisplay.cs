using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SequenceFlagDisplay : MonoBehaviour
{
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
        SequenceController.OnSequenceDisplayCleared += ClearFlags;
    }

    private void OnDisable()
    {
        SequenceController.OnSequenceKeyPressed -= SpawnFlag;
        SequenceController.OnSequenceDisplayCleared -= ClearFlags;
    }

    /// <summary>
    /// Spawns a flag based on the key pressed and its timing color.
    /// Expected keys are "X", "C" and "V". TimingColor is green for perfect and yellow for good.
    /// </summary>
    /// <param name="key">The key pressed (e.g., "X", "C", or "V").</param>
    /// <param name="timingColor">Timing color (green for perfect, yellow for good).</param>
    private void SpawnFlag(string key, Color timingColor)
    {
        Sprite selectedSprite = null;

        // Determine which sprite to use based on key and timing.
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
            Debug.LogWarning($"Received an unknown key '{key}'. Defaulting to good X flag.");
            selectedSprite = goodXFlagSprite;
        }

        if (selectedSprite == null)
        {
            Debug.LogError("The selected flag sprite is null. Check that all sprites are assigned in the Inspector.");
            return;
        }

        // Create a new GameObject for the flag.
        GameObject flagObject = new GameObject("Flag_" + key);
        flagObject.transform.SetParent(transform, false);

        // Add an Image component and assign the selected sprite.
        Image flagImage = flagObject.AddComponent<Image>();
        flagImage.sprite = selectedSprite;

        // Set the flag's size.
        RectTransform flagRect = flagObject.GetComponent<RectTransform>();
        if (flagRect != null)
        {
            flagRect.sizeDelta = flagSize;
            // Position the flag horizontally based on the number of spawned flags.
            float posX = spawnedFlags.Count * flagSpacing;
            flagRect.anchoredPosition = new Vector2(posX, 0);
            Debug.Log($"Spawning flag for key {key} at anchored position: ({posX}, 0) with size {flagSize}");
        }
        else
        {
            flagObject.transform.localScale = Vector3.one;
            flagObject.transform.localPosition = new Vector3(spawnedFlags.Count * flagSpacing, 0, 0);
        }

        // Optionally, to keep flags behind other UI elements, set the flag as first sibling.
        flagObject.transform.SetAsFirstSibling();
        spawnedFlags.Add(flagObject);
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
    }
}
