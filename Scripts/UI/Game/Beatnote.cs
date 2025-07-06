// Contenu à mettre dans votre fichier : Scripts2/UI/Game/Beatnote.cs

using UnityEngine;
using UnityEngine.UI;
using System;

[RequireComponent(typeof(Image))]
public class BeatNote : MonoBehaviour
{
    [Header("Références Visuelles")]
    [SerializeField] private Image noteImage;
    [SerializeField] private Sprite perfectHitSprite;
    [SerializeField] private Sprite goodHitSprite;
    [SerializeField] private Sprite failedHitSprite;

    [Header("Configuration")]
    [SerializeField] private float hitWindow = 0.15f;
    [SerializeField] private float fadeOutDuration = 0.3f;

    // --- Variables internes ---
    private Vector3 startPosition;
    private Vector3 endPosition;
    private float travelTimeInBeats;
    private float startBeat;
    private MusicManager musicManager;
    private Action onCleanup;

    private bool hasBeenProcessed = false;
    private float timeSinceProcessed = 0f;

    void Awake()
    {
        if (noteImage == null) noteImage = GetComponent<Image>();
    }

    public void Initialize(Vector3 startPos, Vector3 endPos, float travelBeats, float spawnBeat, Action onCleanupCallback)
    {
        this.startPosition = startPos;
        this.endPosition = endPos;
        this.travelTimeInBeats = travelBeats;
        this.startBeat = spawnBeat;
        this.onCleanup = onCleanupCallback;
        this.musicManager = MusicManager.Instance;
    }

    void Update()
    {
        if (musicManager == null) { DestroyNote(); return; }

        float progress = GetCurrentProgress();

        // --- LA CORRECTION EST ICI ---
        // On utilise LerpUnclamped pour permettre à la note de dépasser sa destination.
        transform.position = Vector3.LerpUnclamped(startPosition, endPosition, progress);

        if (hasBeenProcessed)
        {
            timeSinceProcessed += Time.deltaTime;
            float alpha = Mathf.Max(0, 1.0f - (timeSinceProcessed / fadeOutDuration));
            noteImage.color = new Color(noteImage.color.r, noteImage.color.g, noteImage.color.b, alpha);

            if (alpha <= 0)
            {
                DestroyNote();
            }
        }
        else
        {
            if (progress > 1.0f + hitWindow)
            {
                ProcessHit(Color.red);
            }
        }
    }

    public void ProcessHit(Color timingColor)
    {
        if (hasBeenProcessed) return;
        hasBeenProcessed = true;

        if (timingColor == Color.green)
        {
            if (perfectHitSprite != null) noteImage.sprite = perfectHitSprite;
        }
        else if (timingColor == Color.yellow)
        {
            if (goodHitSprite != null) noteImage.sprite = goodHitSprite;
        }
        else
        {
            if (failedHitSprite != null) noteImage.sprite = failedHitSprite;
        }
    }

    public float GetCurrentProgress()
    {
        if (musicManager == null) return -1f;
        float currentBeatPosition = musicManager.PublicBeatCount + musicManager.GetBeatProgress();
        return (currentBeatPosition - startBeat) / travelTimeInBeats;
    }

    private void DestroyNote()
    {
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        onCleanup?.Invoke();
    }
}