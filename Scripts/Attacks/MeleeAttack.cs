using UnityEngine;
using System.Collections;

public class MeleeAttack : MonoBehaviour, IAttack
{
    [Header("Visual Effects")]
    [SerializeField] private GameObject attackVFXPrefab;
    [SerializeField] private float vfxDuration = 0.5f;

    [Header("Beat Synchronization")]
    [SerializeField] private bool syncToBeats = true;
    [SerializeField] private bool visualizeAttackTiming = true;

    [Header("Debug")]
    [SerializeField] private bool showAttackLogs = true;

    private MusicManager musicManager;

    private void Awake()
    {
        musicManager = FindObjectOfType<MusicManager>();
        if (musicManager == null && syncToBeats)
        {
            Debug.LogWarning($"[{name}] MusicManager not found but syncToBeats is enabled. Attack timing might be inconsistent.");
        }
    }

    public IEnumerator PerformAttack(Transform attacker, Transform target, int damage, float duration)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            if (showAttackLogs) Debug.LogWarning($"[{attacker.name}] MeleeAttack: Target is null or inactive. Attack cancelled.");
            yield break;
        }

        if (showAttackLogs)
        {
            Debug.Log($"[{attacker.name}] MeleeAttack: Attacking {target.name} for {damage} damage. Animation duration: {duration}s.");
        }

        Vector3 directionToTarget = target.position - attacker.position;
        directionToTarget.y = 0;
        if (directionToTarget != Vector3.zero)
        {
            attacker.rotation = Quaternion.LookRotation(directionToTarget);
        }

        GameObject vfxInstance = null;
        if (attackVFXPrefab != null)
        {
            Vector3 vfxPosition = target.position + Vector3.up * 0.5f;
            vfxInstance = Instantiate(attackVFXPrefab, vfxPosition, Quaternion.identity);
        }

        float animationTime = duration;
        if (syncToBeats && musicManager != null)
        {
            float timeUntilNextBeat = musicManager.GetTimeUntilNextBeat();
            animationTime = Mathf.Min(duration, timeUntilNextBeat * 0.95f);
            animationTime = Mathf.Max(animationTime, 0.1f);
            if (showAttackLogs && visualizeAttackTiming)
            {
                Debug.Log($"[{attacker.name}] MeleeAttack Timing: Adjusted anim time: {animationTime:F3}s / Beat Duration: {musicManager.BeatDuration:F3}s");
            }
        }

        float elapsed = 0f;
        while (elapsed < animationTime)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                if (showAttackLogs) Debug.LogWarning($"[{attacker.name}] MeleeAttack: Target became null or inactive during attack animation. Cancelling attack.");
                if (vfxInstance != null) Destroy(vfxInstance);
                yield break;
            }
            if (attacker == null || !attacker.gameObject.activeInHierarchy)
            {
                if (showAttackLogs) Debug.LogWarning($"[{attacker.name}] MeleeAttack: Attacker became null or inactive during attack animation. Cancelling attack.");
                if (vfxInstance != null) Destroy(vfxInstance);
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (target != null && target.gameObject.activeInHierarchy)
        {
            Unit targetUnit = target.GetComponent<Unit>(); 
            if (targetUnit == null) targetUnit = target.GetComponentInParent<Unit>(); 

            Building targetBuilding = target.GetComponent<Building>(); 
            if (targetBuilding == null) targetBuilding = target.GetComponentInParent<Building>(); 
            
            if ((targetUnit != null && targetUnit.Health <= 0) || 
                (targetBuilding != null && targetBuilding.CurrentHealth <= 0))
            {
                if (showAttackLogs) Debug.LogWarning($"[{attacker.name}] MeleeAttack: Target {target.name} is dead, cancelling damage application.");
                yield break; 
            }
            if (targetUnit != null)
            {
                if (showAttackLogs) Debug.Log($"[{attacker.name}] MeleeAttack: Applying {damage} damage to Unit {targetUnit.name}.");
                targetUnit.TakeDamage(damage, attacker.GetComponent<Unit>()); 
                
            }
            else if (targetBuilding != null)
            {
                if (showAttackLogs) Debug.Log($"[{attacker.name}] MeleeAttack: Applying {damage} damage to Building {targetBuilding.name}.");
                targetBuilding.TakeDamage(damage, attacker.GetComponent<Unit>()); 
               
            }
            else
            {
                if (showAttackLogs) Debug.LogWarning($"[{attacker.name}] MeleeAttack: Target {target.name} has no Unit or Building component to apply damage to.");
            }
        }
        else
        {
            if (showAttackLogs) Debug.LogWarning($"[{attacker.name}] MeleeAttack: Target became null or inactive before damage application.");
        }

        if (vfxInstance != null)
        {
            Destroy(vfxInstance, Mathf.Max(0, vfxDuration - animationTime));
        }

        if (showAttackLogs)
        {
            Debug.Log($"[{attacker.name}] MeleeAttack: Attack animation and damage phase completed for target {target?.name ?? "UNKNOWN"}.");
        }
    }

    public bool CanAttack(Transform attacker, Transform target, float attackRange)
    {
        if (showAttackLogs)
        {
             Debug.Log($"[{attacker.name}] MeleeAttack: CanAttack {target.name} - On se fie à la détection de portée par tuile de l'unité.");
        }
        return true;
    }
}