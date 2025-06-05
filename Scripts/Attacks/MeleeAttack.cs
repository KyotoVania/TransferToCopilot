using UnityEngine;
using System.Collections;

public class MeleeAttack : MonoBehaviour, IAttack
{
    [Header("Visual Effects")]
    [SerializeField] private GameObject attackVFXPrefab;
    [SerializeField] private float vfxDuration = 0.5f; // Durée pendant laquelle le VFX reste avant d'être détruit

    [Header("Beat Synchronization")]
    [SerializeField] private bool syncToBeats = true;
    [SerializeField] private bool visualizeAttackTiming = true;

    [Header("Debug")]
    [SerializeField] private bool showAttackLogs = true;

    private RhythmManager rhythmManager;

    private void Awake()
    {
        rhythmManager = FindObjectOfType<RhythmManager>(); //
        if (rhythmManager == null && syncToBeats)
        {
            Debug.LogWarning($"[{name}] RhythmManager not found but syncToBeats is enabled. Attack timing might be inconsistent.");
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

        // Rotate attacker to face target
        Vector3 directionToTarget = target.position - attacker.position;
        directionToTarget.y = 0;
        if (directionToTarget != Vector3.zero)
        {
            attacker.rotation = Quaternion.LookRotation(directionToTarget);
        }

        // Spawn VFX at the target's position (ou entre l'attaquant et la cible)
        GameObject vfxInstance = null;
        if (attackVFXPrefab != null)
        {
            // Positionner le VFX de manière appropriée pour une attaque de mêlée, ex: sur la cible ou au point de contact
            Vector3 vfxPosition = target.position + Vector3.up * 0.5f; // Ajuster si nécessaire
            vfxInstance = Instantiate(attackVFXPrefab, vfxPosition, Quaternion.identity);
        }

        // Calculate animation time, potentially synced to beat
        float animationTime = duration;
        if (syncToBeats && rhythmManager != null)
        {
            float timeUntilNextBeat = rhythmManager.GetTimeUntilNextBeat(); //
            animationTime = Mathf.Min(duration, timeUntilNextBeat * 0.95f); // Ne pas déborder sur le prochain battement
            animationTime = Mathf.Max(animationTime, 0.1f); // Durée minimale
            if (showAttackLogs && visualizeAttackTiming)
            {
                Debug.Log($"[{attacker.name}] MeleeAttack Timing: Adjusted anim time: {animationTime:F3}s / Beat Duration: {rhythmManager.BeatDuration:F3}s"); //
            }
        }

        // Attendre la durée de l'animation d'attaque (ou jusqu'au point d'impact)
        // Wait for the duration, but check if the target is dead during the wait
        float elapsed = 0f;
        while (elapsed < animationTime)
        {
            // Check if the target or the attacker is still valid, il faudrait check la vie
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                if (showAttackLogs) Debug.LogWarning($"[{attacker.name}] MeleeAttack: Target became null or inactive during attack animation. Cancelling attack.");
                if (vfxInstance != null) Destroy(vfxInstance);
                yield break; // Cible invalide, annuler l'attaque
            }
            if (attacker == null || !attacker.gameObject.activeInHierarchy)
            {
                if (showAttackLogs) Debug.LogWarning($"[{attacker.name}] MeleeAttack: Attacker became null or inactive during attack animation. Cancelling attack.");
                if (vfxInstance != null) Destroy(vfxInstance);
                yield break; // Attaquant invalide, annuler l'attaque
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // --- APPLICATION DES DÉGÂTS ---
        // Vérifier à nouveau si la cible est valide avant d'appliquer les dégâts
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

        // Détruire le VFX après sa durée
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
        // La logique de Unit.cs (via GetTilesInAttackRange) est prioritaire pour déterminer si l'attaque est possible.
        // Ce CanAttack est plus une confirmation ou pour des conditions spécifiques au type d'attaque.
        if (showAttackLogs)
        {
            // Note : 'attacker' et 'target' sont des Transforms. Il faut GetComponent<Unit>() pour accéder aux noms réels.
            // Debug.Log($"[MeleeAttack] CanAttack check from {attacker?.name} to {target?.name}. (Range check primarily handled by Unit class)");
        }
        return true; // On se fie à la vérification de portée par tuiles de Unit.cs
    }
}