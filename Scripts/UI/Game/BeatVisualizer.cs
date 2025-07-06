// Contenu à mettre dans votre fichier : Scripts2/UI/Game/BeatVisualizer.cs

using UnityEngine;
using System.Collections; // NÉCESSAIRE pour utiliser les Coroutines
using System.Collections.Generic;
using System.Linq;

public class BeatVisualizer : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameObject notePrefab;
    [SerializeField] private RectTransform laneStartPoint;
    [SerializeField] private RectTransform hitZone;

    [Header("Configuration du Timing")]
    [SerializeField] private float travelTimeInBeats = 2f;

    // NOUVEAU : Paramètres pour l'effet de pulsation
    [Header("Pulse Effect")]
    [Tooltip("La durée de l'animation de pulsation en secondes.")]
    [SerializeField] private float pulseDuration = 0.15f;
    [Tooltip("L'ampleur de la pulsation (ex: 1.2 = 120% de la taille originale).")]
    [SerializeField] private float pulseMagnitude = 1.2f;


    private MusicManager musicManager;
    private readonly List<BeatNote> activeNotes = new List<BeatNote>();

    void Start()
    {
        musicManager = MusicManager.Instance;
        if (musicManager == null)
        {
            Debug.LogError("[BeatVisualizer] MusicManager.Instance non trouvé !");
            enabled = false;
            return;
        }
        musicManager.OnBeat += SpawnNoteOnBeat;
        SequenceFlagDisplay.OnFlagStateChanged += HandleFlagStateChanged;
    }

    private void OnDestroy()
    {
        if (musicManager != null) musicManager.OnBeat -= SpawnNoteOnBeat;
        SequenceFlagDisplay.OnFlagStateChanged -= HandleFlagStateChanged;
    }

    private void SpawnNoteOnBeat(float beatDuration)
    {
        // NOUVEAU : On déclenche la pulsation sur le marqueur de départ
        if (laneStartPoint != null)
        {
            StartCoroutine(Pulse(laneStartPoint));
        }

        // Le reste de la fonction est inchangé
        GameObject noteInstance = Instantiate(notePrefab, laneStartPoint.position, Quaternion.identity, transform);
        BeatNote noteScript = noteInstance.GetComponent<BeatNote>();
        if (noteScript != null)
        {
            float spawnBeat = musicManager.PublicBeatCount;
            noteScript.Initialize(laneStartPoint.position, hitZone.position, travelTimeInBeats, spawnBeat, () => activeNotes.Remove(noteScript));
            activeNotes.Add(noteScript);
        }
    }

    private void HandleFlagStateChanged(Color timingColor)
    {
        // On ne fait rien si le coup est raté
        if (timingColor == Color.red) return;

        // NOUVEAU : On déclenche la pulsation sur la zone de validation
        if (hitZone != null)
        {
            StartCoroutine(Pulse(hitZone));
        }

        // Le reste de la fonction est inchangé
        if (activeNotes.Count == 0) return;

        BeatNote bestNoteToHit = null;
        float minDistance = float.MaxValue;

        foreach (var note in activeNotes)
        {
            if (note == null) continue;
            float distance = Mathf.Abs(1.0f - note.GetCurrentProgress());
            if (distance < minDistance)
            {
                minDistance = distance;
                bestNoteToHit = note;
            }
        }

        if (bestNoteToHit != null)
        {
            bestNoteToHit.ProcessHit(timingColor);
            activeNotes.Remove(bestNoteToHit);
        }
    }

    // NOUVEAU : La Coroutine qui gère l'animation de pulsation
    /// <summary>
    /// Anime la taille d'un élément UI pour créer un effet de "pulsation".
    /// </summary>
    private IEnumerator Pulse(RectTransform rectTransform)
    {
        Vector3 originalScale = Vector3.one; // On part du principe que l'échelle de base est (1,1,1)
        Vector3 targetScale = originalScale * pulseMagnitude;
        float halfDuration = pulseDuration / 2;

        // Étape 1 : Agrandissement
        float timer = 0f;
        while (timer < halfDuration)
        {
            rectTransform.localScale = Vector3.Lerp(originalScale, targetScale, timer / halfDuration);
            timer += Time.deltaTime;
            yield return null; // Attend la prochaine frame
        }

        // Étape 2 : Rétrecissement
        timer = 0f;
        while (timer < halfDuration)
        {
            rectTransform.localScale = Vector3.Lerp(targetScale, originalScale, timer / halfDuration);
            timer += Time.deltaTime;
            yield return null; // Attend la prochaine frame
        }

        // Assure que l'échelle revient exactement à sa valeur d'origine
        rectTransform.localScale = originalScale;
    }
}