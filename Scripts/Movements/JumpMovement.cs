using UnityEngine;
using System.Collections;

public class JumpMovement : MonoBehaviour, IMovement
{
    [SerializeField] private float jumpHeight = 1f;
    [Header("Debug")]
    [SerializeField] private bool showJumpLogs = true;

    // Fixed implementation to match the interface signature
    public IEnumerator MoveToTile(Transform unitTransform, Vector3 startPos, Vector3 targetPos, float duration)
    {
        float elapsed = 0f;

        if (showJumpLogs)
        {
            Debug.Log($"[JUMP] Starting jump from {startPos} to {targetPos} " +
                      $"(Distance: {Vector3.Distance(startPos, targetPos):F2})");
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Calculate jump arc using sine function
            float yOffset = Mathf.Sin(t * Mathf.PI) * jumpHeight;

            // Interpolate position
            unitTransform.position = Vector3.Lerp(startPos, targetPos, t) + Vector3.up * yOffset;

            yield return null;
        }

        // Ensure exact final position
        unitTransform.position = targetPos;

        if (showJumpLogs)
        {
            Debug.Log($"[JUMP] Landed at {targetPos} " +
                      $"(Final distance: {Vector3.Distance(startPos, targetPos):F2})");
        }
    }

    public void Initialize(Unit unit)
    {
        // Initialization if needed
    }
}