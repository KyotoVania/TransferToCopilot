using UnityEngine;
using System.Collections;

public class RangedAttack : MonoBehaviour, IAttack
{
    [Header("Projectile Settings")]
    [Tooltip("Le prefab du projectile à lancer (doit avoir un script Projectile).")]
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("Le prefab de l'effet visuel à l'impact (optionnel).")]
    [SerializeField] private GameObject impactVFXPrefab;
    [Tooltip("Vitesse du projectile en unités par seconde.")]
    [SerializeField] private float projectileSpeed = 20f;
    [Tooltip("Point de lancement du projectile. Si non assigné, utilise la position de l'attaquant avec un offset.")]
    [SerializeField] private Transform projectileSpawnPoint;
    [Tooltip("Offset par rapport à la position de l'attaquant si projectileSpawnPoint n'est pas défini.")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0, 1f, 0.5f);
    [Tooltip("Délai dans l'animation de l'attaquant avant que le projectile ne soit réellement lancé (en secondes).")]
    [SerializeField] private float fireAnimationDelay = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showAttackLogs = true;

    [Header("Fever Mode")]
    [Tooltip("Angle de dispersion pour les projectiles supplémentaires en Mode Fever.")]
    [SerializeField] private float feverSpreadAngle = 10f;

    [Header("Fever Mode")]
    [Tooltip("L'effet visuel d'impact à utiliser quand le mode Fever est actif.")]
    [SerializeField] private GameObject feverImpactVFXPrefab;

    [Header("Fever Mode")]
    [Tooltip("Délai entre chaque projectile supplémentaire en secondes.")]
    [SerializeField] private float feverProjectileDelay = 0.1f;

    [Header("Lob Trajectory")]
    [Tooltip("Si true, les projectiles suivront une trajectoire en arc (lob) au lieu d'une ligne droite.")]
    [SerializeField] private bool useLobTrajectory = false;
    
    [Tooltip("Hauteur de l'arc pour la trajectoire lob (plus haut = arc plus prononcé).")]
    [SerializeField] private float lobHeight = 3f;
    public IEnumerator PerformAttack(Transform attacker, Transform target, int damage, float duration)
    {
        if (projectilePrefab == null)
        {
            if (showAttackLogs) Debug.LogError($"[{attacker.name}] RangedAttack: Projectile Prefab non assigné !");
            yield break;
        }

        if (target == null)
        {
            if (showAttackLogs) Debug.LogWarning($"[{attacker.name}] RangedAttack: Cible nulle, attaque annulée.");
            yield break;
        }

        Vector3 directionToTarget = target.position - attacker.position;
        directionToTarget.y = 0;
        if (directionToTarget != Vector3.zero)
        {
            attacker.rotation = Quaternion.LookRotation(directionToTarget);
        }

        if (fireAnimationDelay > 0)
        {
            yield return new WaitForSeconds(fireAnimationDelay);
        }

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            if (showAttackLogs) Debug.LogWarning($"[{attacker.name}] RangedAttack: Cible devenue invalide pendant l'animation de tir.");
            yield break;
        }
        
        Vector3 spawnPosition = projectileSpawnPoint != null ? projectileSpawnPoint.position : attacker.position + attacker.TransformDirection(spawnOffset);
        Unit attackerUnit = attacker.GetComponent<Unit>();

        // --- LOGIQUE PRINCIPALE MISE À JOUR ---
        // 1. Tirer le projectile principal
        GameObject vfxToUse = impactVFXPrefab;
        if (attackerUnit != null && attackerUnit.IsFeverActive && feverImpactVFXPrefab != null)
        {
            vfxToUse = feverImpactVFXPrefab;
        }
        FireProjectile(attacker, target, damage, spawnPosition, attacker.rotation, vfxToUse);
        
        // 2. Vérifier si on est en Mode Fever et tirer les projectiles supplémentaires
         if (attackerUnit != null && attackerUnit.IsFeverActive)
        {
            int extraProjectiles = attackerUnit.ActiveFeverBuffs.ExtraProjectiles;
            if (extraProjectiles > 0)
            {
                if (showAttackLogs) Debug.Log($"[{attacker.name}] Mode Fever: Tir de {extraProjectiles} projectiles en plus.");
                
                for (int i = 0; i < extraProjectiles; i++)
                {
                    // On attend AVANT de tirer le prochain projectile
                    if (feverProjectileDelay > 0)
                    {
                        yield return new WaitForSeconds(feverProjectileDelay);
                    }

                    // On ne tire que si la cible existe toujours
                    if (target != null && target.gameObject.activeInHierarchy)
                    {
                         // On tire simplement droit devant, sans calcul d'angle
                         FireProjectile(attacker, target, damage, spawnPosition, attacker.rotation, vfxToUse);
                    }
                    else
                    {
                        // Si la cible disparaît au milieu de la rafale, on arrête
                        break; 
                    }
                }
            }
        }


        float remainingDuration = duration - fireAnimationDelay;
        if (remainingDuration > 0)
        {
            yield return new WaitForSeconds(remainingDuration);
        }
    }
        private void FireProjectile(Transform attacker, Transform target, int damage, Vector3 spawnPosition, Quaternion spawnRotation, GameObject impactVfx)
    {
        if (showAttackLogs) Debug.Log($"[{attacker.name}] RangedAttack: Instanciation du projectile à {spawnPosition} vers {target.name}.");
        GameObject projectileGO = Instantiate(projectilePrefab, spawnPosition, spawnRotation);
        Projectile projectileScript = projectileGO.GetComponent<Projectile>();

        if (projectileScript == null)
        {
            Debug.LogError($"[{attacker.name}] RangedAttack: Le prefab du projectile '{projectilePrefab.name}' ne contient pas de script Projectile !");
            Destroy(projectileGO);
            return;
        }
        
        // Ajouter les données de trajectoire lob si nécessaire
        if (useLobTrajectory)
        {
            LobProjectileData lobData = projectileGO.AddComponent<LobProjectileData>();
            lobData.lobHeight = lobHeight;
            lobData.useLobTrajectory = true;
            
            if (showAttackLogs) 
                Debug.Log($"[{attacker.name}] RangedAttack: Projectile configuré avec trajectoire lob (hauteur: {lobHeight}).");
        }
        
        projectileScript.Initialize(target, damage, projectileSpeed, impactVfx, attacker.GetComponent<Unit>());
    }
    public bool CanAttack(Transform attacker, Transform target, float attackRange)
    {
        if (showAttackLogs)
        {
             Debug.Log($"[{attacker.name}] RangedAttack: CanAttack {target.name} - On se fie à la détection de portée par tuile de l'unité.");
        }
        return true;
    }
}