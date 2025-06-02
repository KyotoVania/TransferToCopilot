using UnityEngine;

public static class DetectionSystem
{
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
