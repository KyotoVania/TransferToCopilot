using UnityEngine;

/// <summary>
/// A static class providing utility methods for detecting objects in the game world.
/// </summary>
public static class DetectionSystem
{
    /// <summary>
    /// Finds the closest target with a specific tag within a given range and layer.
    /// </summary>
    /// <param name="origin">The position from which to start the search.</param>
    /// <param name="tag">The tag of the target to find.</param>
    /// <param name="range">The search range.</param>
    /// <param name="layer">The layer mask to use for the search.</param>
    /// <returns>The transform of the closest target, or null if no target is found.</returns>
    public static Transform FindClosestTarget(Vector3 origin, string tag, float range, LayerMask layer)
    {
        Collider[] hits = Physics.OverlapSphere(origin, range, layer);
        Transform closest = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag(tag)) continue;

            float distance = Vector3.Distance(origin, hit.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = hit.transform;
            }
        }
        return closest;
    }
}