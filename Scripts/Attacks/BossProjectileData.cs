using UnityEngine;

/// <summary>
/// Data component for projectiles that should inflict percentage-based damage to bosses.
/// This is dynamically added to projectiles fired by towers.
/// </summary>
public class BossProjectileData : MonoBehaviour
{
    /// <summary>
    /// The percentage of damage to inflict on the boss (0-100).
    /// </summary>
    [Tooltip("Percentage of damage to inflict on the boss (0-100).")]
    public float damagePercentage = 1f;
    
    /// <summary>
    /// Indicates if this projectile comes from a tower (for logging purposes).
    /// </summary>
    [Tooltip("Indicates if this projectile comes from a tower (for logs).")]
    public bool isFromTower = false;
}
