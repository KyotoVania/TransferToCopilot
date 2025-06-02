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
    // Choose from which side the UI element should come in
    public EntranceDirection entranceDirection = EntranceDirection.Left;
    // How far (in pixels) the element will overshoot the final position
    public float overshootDistance = 20f;
    // Duration (in seconds) of each animation phase (entrance and correction)
    public float animationDuration = 0.5f;

    private RectTransform rectTransform;
    private Vector2 finalPosition;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        // The final anchored position is set in the Editor
        finalPosition = rectTransform.anchoredPosition;
    }

    void Start()
    {
        // Set the starting X position based on the desired entrance direction.
        float initialX = finalPosition.x;
        if (entranceDirection == EntranceDirection.Left)
        {
            // Coming from left: position it off-screen to the left.
            initialX = finalPosition.x - Mathf.Abs(rectTransform.rect.width);
        }
        else if (entranceDirection == EntranceDirection.Right)
        {
            // Coming from right: position it off-screen to the right.
            initialX = finalPosition.x + Mathf.Abs(rectTransform.rect.width);
        }
        // Update the anchored position to the starting position.
        rectTransform.anchoredPosition = new Vector2(initialX, finalPosition.y);

        // Start the animation coroutine.
        StartCoroutine(AnimateEntrance());
    }

    IEnumerator AnimateEntrance()
    {
        // Determine the overshoot target based on the entrance direction.
        float overshootTargetX = finalPosition.x;
        if (entranceDirection == EntranceDirection.Left)
        {
            // For left entrance, overshoot to the right.
            overshootTargetX = finalPosition.x + overshootDistance;
        }
        else if (entranceDirection == EntranceDirection.Right)
        {
            // For right entrance, overshoot to the left.
            overshootTargetX = finalPosition.x - overshootDistance;
        }

        // Phase 1: Animate from the starting off-screen position to the overshoot position.
        float elapsedTime = 0f;
        float startX = rectTransform.anchoredPosition.x;
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / animationDuration);
            float newX = Mathf.Lerp(startX, overshootTargetX, t);
            rectTransform.anchoredPosition = new Vector2(newX, finalPosition.y);
            yield return null;
        }
        // Ensure overshoot target is reached.
        rectTransform.anchoredPosition = new Vector2(overshootTargetX, finalPosition.y);

        // Phase 2: Animate back from the overshoot position to the final anchored position.
        elapsedTime = 0f;
        float overshootX = overshootTargetX;
        float finalX = finalPosition.x;
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / animationDuration);
            float newX = Mathf.Lerp(overshootX, finalX, t);
            rectTransform.anchoredPosition = new Vector2(newX, finalPosition.y);
            yield return null;
        }
        // Finalize the position.
        rectTransform.anchoredPosition = new Vector2(finalX, finalPosition.y);
    }
}
