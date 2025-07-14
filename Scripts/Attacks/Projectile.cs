using UnityEngine;
using System.Collections;

/// <summary>
/// Represents a projectile fired by a unit or building.
/// </summary>
public class Projectile : MonoBehaviour
{
    private Transform targetTransform;
    private int damage;
    private float speed;
    private GameObject impactVFXPrefab;
    private Transform attackerTransform;
    private Unit attacker;

    private bool initialized = false;
    private Vector3 lastKnownTargetPosition;
    
    private LobProjectileData lobData;
    private float lobProgress = 0f;
    private Vector3 lobStartPosition;

    /// <summary>
    /// The distance at which the projectile is considered to have hit the target.
    /// </summary>
    [Tooltip("Distance at which the projectile is considered to have hit the target (for moving targets).")]
    [SerializeField] private float hitThreshold = 0.5f;
    /// <summary>
    /// The maximum lifetime of the projectile in seconds.
    /// </summary>
    [Tooltip("Maximum lifetime of the projectile in seconds, to prevent it from flying indefinitely if the target is destroyed.")]
    [SerializeField] private float maxLifetime = 5f;
    /// <summary>
    /// Whether the projectile should home in on the target or fly in a straight line.
    /// </summary>
    [Tooltip("Should the projectile follow the target (homing) or go in a straight line to the initial target position?")]
    [SerializeField] private bool isHoming = true;
    /// <summary>
    /// What to do when the target disappears: 0 = disappear, 1 = continue to last known position.
    /// </summary>
    [Tooltip("What to do when the target disappears: 0 = disappear, 1 = continue to last known position")]
    [SerializeField] private int targetDestroyedBehavior = 1;

    /// <summary>
    /// Initializes the projectile with its target, damage, speed, and visual effects.
    /// </summary>
    /// <param name="target">The target transform.</param>
    /// <param name="projectileDamage">The damage the projectile will inflict.</param>
    /// <param name="projectileSpeed">The speed of the projectile.</param>
    /// <param name="vfxPrefab">The prefab for the impact visual effect.</param>
    /// <param name="attacker">The unit that fired the projectile.</param>
    public void Initialize(Transform target, int projectileDamage, float projectileSpeed, GameObject vfxPrefab, Unit attacker)
    {
        targetTransform = target;
        damage = projectileDamage;
        speed = projectileSpeed;
        impactVFXPrefab = vfxPrefab;
        this.attacker = attacker;
        
        if (showAttackLogs) Debug.Log($"[Projectile] Initialize: VFX Prefab = {(vfxPrefab != null ? vfxPrefab.name : "NULL")}");

        if (targetTransform != null)
        {
            lastKnownTargetPosition = targetTransform.position;
        }
        else
        {
            Debug.LogWarning("[Projectile] Target is null on initialization. Projectile will self-destruct.");
            Destroy(gameObject, 0.1f);
            return;
        }

        initialized = true;
        
        lobData = GetComponent<LobProjectileData>();
        if (lobData != null && lobData.useLobTrajectory)
        {
            lobStartPosition = transform.position;
            lobData.Initialize(lobStartPosition, lastKnownTargetPosition);
            lobProgress = 0f;
        }
        
        Destroy(gameObject, maxLifetime);
    }

    /// <summary>
    /// Unity's Update method. Moves the projectile towards its target.
    /// </summary>
    void Update()
    {
        if (!initialized) return;

        bool targetDestroyed = false;
        
        if (targetTransform != null && targetTransform.gameObject.activeInHierarchy)
        {
            lastKnownTargetPosition = targetTransform.position;
        }
        else
        {
            targetDestroyed = true;
            
            if (targetDestroyedBehavior == 0)
            {
                HandleImpact(null);
                return;
            }
        }

        Vector3 targetPositionToChase;
        
        if (targetDestroyed)
        {
            targetPositionToChase = lastKnownTargetPosition;
        }
        else if (isHoming)
        {
            targetPositionToChase = lastKnownTargetPosition;
        }
        else
        {
            targetPositionToChase = lastKnownTargetPosition;
        }

        if (lobData != null && lobData.useLobTrajectory && lobData.isInitialized)
        {
            float distanceToTarget = Vector3.Distance(lobStartPosition, lastKnownTargetPosition);
            float progressIncrement = (speed * Time.deltaTime) / distanceToTarget;
            lobProgress += progressIncrement;
            
            if (lobProgress >= 1f)
            {
                transform.position = lastKnownTargetPosition;
                HandleImpact(targetTransform != null && targetTransform.gameObject.activeInHierarchy ? targetTransform.GetComponent<Collider>() : null);
                return;
            }
            
            Vector3 newPosition = lobData.GetLobPosition(lobProgress);
            
            Vector3 direction = lobData.GetLobDirection(lobProgress);
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
            
            transform.position = newPosition;
        }
        else
        {
            Vector3 direction = (targetPositionToChase - transform.position).normalized;

            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            transform.position += direction * speed * Time.deltaTime;

            if (Vector3.Distance(transform.position, lastKnownTargetPosition) < hitThreshold)
            {
                HandleImpact(targetTransform != null && targetTransform.gameObject.activeInHierarchy ? targetTransform.GetComponent<Collider>() : null);
            }
        }
    }

    /// <summary>
    /// Handles the collision of the projectile.
    /// </summary>
    /// <param name="other">The collider the projectile collided with.</param>
    void OnTriggerEnter(Collider other)
    {
        if (!initialized) return;

        if (attackerTransform != null && other.transform == attackerTransform)
        {
            return;
        }

        if ((targetTransform != null && other.transform == targetTransform) || (targetTransform == null && other.gameObject.layer != gameObject.layer))
        {
            HandleImpact(other);
        }
    }

    /// <summary>
    /// Handles the impact of the projectile, applying damage and visual effects.
    /// </summary>
    /// <param name="hitCollider">The collider that was hit.</param>
    private void HandleImpact(Collider hitCollider)
    {
        if (!initialized) return;

        initialized = false;

        if (showAttackLogs) Debug.Log($"[Projectile] Impact with {(hitCollider != null ? hitCollider.name : "destroyed target/position")}.");

        if (hitCollider != null)
        {
            Unit unitTarget = hitCollider.GetComponent<Unit>();
            if (unitTarget == null) unitTarget = hitCollider.GetComponentInParent<Unit>();

            Building buildingTarget = hitCollider.GetComponent<Building>();
            if (buildingTarget == null) buildingTarget = hitCollider.GetComponentInParent<Building>();

            if (unitTarget != null)
            {
                BossProjectileData bossData = GetComponent<BossProjectileData>();
                if (bossData != null && unitTarget is BossUnit bossTarget)
                {
                    if (showAttackLogs) 
                        Debug.Log($"[Projectile] Applying {bossData.damagePercentage}% damage to boss {bossTarget.name} {(bossData.isFromTower ? "(from a tower)" : "")}.");
                    
                    bossTarget.TakePercentageDamage(bossData.damagePercentage);
                }
                else
                {
                    if (showAttackLogs) Debug.Log($"[Projectile] Applying {damage} damage to unit {unitTarget.name}.");
                    unitTarget.TakeDamage(damage, attacker);
                }
            }
            else if (buildingTarget != null)
            {
                if (showAttackLogs) Debug.Log($"[Projectile] Applying {damage} damage to building {buildingTarget.name}.");
                buildingTarget.TakeDamage(damage, attacker);
            }
        }

        if (impactVFXPrefab != null)
        {
            if (showAttackLogs) Debug.Log($"[Projectile] Instantiating impact VFX: {impactVFXPrefab.name} at position {transform.position}");
            Instantiate(impactVFXPrefab, transform.position, Quaternion.LookRotation(-transform.forward));
        }
        else
        {
            if (showAttackLogs) Debug.Log($"[Projectile] No impact VFX defined (impactVFXPrefab is null)");
        }

        Destroy(gameObject);
    }

    private static bool showAttackLogs = true;
}
