using UnityEngine;

/// <summary>
/// Data component for projectiles that should follow an arched (lob) trajectory.
/// This is dynamically added to projectiles fired by towers.
/// </summary>
public class LobProjectileData : MonoBehaviour
{
    /// <summary>
    /// The maximum height of the arc (higher value = more pronounced arc).
    /// </summary>
    [Tooltip("Maximum height of the arc (higher = more pronounced arc).")]
    public float lobHeight = 5f;
    
    /// <summary>
    /// Indicates if this projectile should use a lob trajectory.
    /// </summary>
    [Tooltip("Indicates if this projectile should use a lob trajectory.")]
    public bool useLobTrajectory = true;
    
    // Internal variables for calculating the trajectory
    [HideInInspector] public Vector3 startPosition;
    [HideInInspector] public Vector3 targetPosition;
    [HideInInspector] public float totalDistance;
    [HideInInspector] public bool isInitialized = false;
    
    /// <summary>
    /// Initializes the data for the lob trajectory.
    /// </summary>
    /// <param name="start">The starting position.</param>
    /// <param name="target">The target position.</param>
    public void Initialize(Vector3 start, Vector3 target)
    {
        startPosition = start;
        targetPosition = target;
        totalDistance = Vector3.Distance(start, target);
        isInitialized = true;
    }
    
    /// <summary>
    /// Calculates the position on the lob trajectory based on the progress percentage.
    /// </summary>
    /// <param name="progress">Progress from 0 to 1.</param>
    /// <returns>The calculated position with the arc.</returns>
    public Vector3 GetLobPosition(float progress)
    {
        if (!isInitialized) return Vector3.zero;
        
        // Linear position between start and target
        Vector3 linearPosition = Vector3.Lerp(startPosition, targetPosition, progress);
        
        // Calculate the height of the arc (parabola)
        // Maximum at 50% of the progress
        float arcHeight = lobHeight * 4 * progress * (1 - progress);
        
        // Add the arc height
        linearPosition.y += arcHeight;
        
        return linearPosition;
    }
    
    /// <summary>
    /// Calculates the direction of the projectile at a given point on the trajectory.
    /// </summary>
    /// <param name="progress">The current progress.</param>
    /// <param name="deltaProgress">A small increment to calculate the direction.</param>
    /// <returns>The normalized direction.</returns>
    public Vector3 GetLobDirection(float progress, float deltaProgress = 0.01f)
    {
        if (!isInitialized) return Vector3.forward;
        
        Vector3 currentPos = GetLobPosition(progress);
        Vector3 nextPos = GetLobPosition(Mathf.Clamp01(progress + deltaProgress));
        
        Vector3 direction = (nextPos - currentPos).normalized;
        return direction != Vector3.zero ? direction : Vector3.forward;
    }
}
