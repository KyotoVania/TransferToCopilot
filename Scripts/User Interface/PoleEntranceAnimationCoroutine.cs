using System.Collections;
using UnityEngine;

public enum EntranceDirection
{
    Left,
    Right
}

public class PoleEntranceAnimationGeneric : MonoBehaviour
{
    [Header("Animation Settings")]
    public EntranceDirection entranceDirection = EntranceDirection.Left;
    public float overshootDistance = 20f;
    public float animationDuration = 0.5f;

    private RectTransform rectTransform;
    private Vector2 finalPosition; // La position à l'écran, définie dans l'éditeur
    private Coroutine animationCoroutine;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        // On sauvegarde la position finale une seule fois
        finalPosition = rectTransform.anchoredPosition;
    }

    void OnEnable()
    {
        // S'assurer qu'aucune ancienne animation ne tourne en arrière-plan
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        // Réinitialiser la position à son état hors-écran
        ResetToOffScreenPosition();

        // Lancer la nouvelle animation
        animationCoroutine = StartCoroutine(AnimateEntrance());
    }

    void OnDisable()
    {
        // Si l'objet est désactivé en cours d'animation, on l'arrête
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        // Réinitialiser immédiatement la position à son état hors-écran
        ResetToOffScreenPosition();
    }

    /// <summary>
    /// Place l'objet à sa position de départ, hors de l'écran.
    /// </summary>
    private void ResetToOffScreenPosition()
    {
        if (rectTransform == null) return;

        float initialX = finalPosition.x;
        // La largeur du RectTransform est utilisée pour le placer juste à l'extérieur
        float width = rectTransform.rect.width;

        if (entranceDirection == EntranceDirection.Left)
        {
            initialX = finalPosition.x - Mathf.Abs(width);
        }
        else if (entranceDirection == EntranceDirection.Right)
        {
            initialX = finalPosition.x + Mathf.Abs(width);
        }

        rectTransform.anchoredPosition = new Vector2(initialX, finalPosition.y);
    }

    IEnumerator AnimateEntrance()
    {
        // --- Phase 1: Entrée avec overshoot ---
        float overshootTargetX = finalPosition.x;
        if (entranceDirection == EntranceDirection.Left)
        {
            overshootTargetX = finalPosition.x + overshootDistance;
        }
        else if (entranceDirection == EntranceDirection.Right)
        {
            overshootTargetX = finalPosition.x - overshootDistance;
        }

        float elapsedTime = 0f;
        Vector2 startPos = rectTransform.anchoredPosition; // Part de la position hors-écran
        Vector2 overshootPos = new Vector2(overshootTargetX, finalPosition.y);

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / animationDuration);
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, overshootPos, t);
            yield return null;
        }
        rectTransform.anchoredPosition = overshootPos;

        // --- Phase 2: Retour à la position finale ---
        elapsedTime = 0f;
        startPos = rectTransform.anchoredPosition; // Part de la position d'overshoot

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / animationDuration);
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, finalPosition, t);
            yield return null;
        }
        rectTransform.anchoredPosition = finalPosition;

        animationCoroutine = null;
    }
}
