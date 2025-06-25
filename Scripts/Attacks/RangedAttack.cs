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
    [SerializeField] private Vector3 spawnOffset = new Vector3(0, 1f, 0.5f); // Ex: un peu au-dessus et devant
    [Tooltip("Délai dans l'animation de l'attaquant avant que le projectile ne soit réellement lancé (en secondes).")]
    [SerializeField] private float fireAnimationDelay = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showAttackLogs = true;

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

        if (showAttackLogs)
        {
            Debug.Log($"[{attacker.name}] RangedAttack: Début de PerformAttack sur {target.name} pour {damage} dégâts. Durée anim: {duration}s, Délai de tir: {fireAnimationDelay}s.");
        }

        // Orienter l'attaquant vers la cible
        Vector3 directionToTarget = target.position - attacker.position;
        directionToTarget.y = 0; // Garder l'orientation sur le plan horizontal
        if (directionToTarget != Vector3.zero)
        {
            attacker.rotation = Quaternion.LookRotation(directionToTarget);
        }

        // Attendre la fin de l'animation de préparation au tir (si spécifié)
        // La 'duration' passée peut être la durée totale de l'animation de l'attaquant.
        // Le 'fireAnimationDelay' est le moment spécifique DANS cette animation où le projectile part.
        if (fireAnimationDelay > 0)
        {
            if (showAttackLogs) Debug.Log($"[{attacker.name}] RangedAttack: Attente du délai d'animation de tir ({fireAnimationDelay}s).");
            yield return new WaitForSeconds(fireAnimationDelay);
        }

        // Vérifier à nouveau si la cible est toujours valide après le délai d'animation
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            if (showAttackLogs) Debug.LogWarning($"[{attacker.name}] RangedAttack: Cible devenue invalide pendant l'animation de tir.");
            yield break;
        }

        // Déterminer le point de spawn
        Vector3 spawnPosition = projectileSpawnPoint != null ? projectileSpawnPoint.position : attacker.position + attacker.TransformDirection(spawnOffset);
        Quaternion spawnRotation = projectileSpawnPoint != null ? projectileSpawnPoint.rotation : attacker.rotation;


        if (showAttackLogs) Debug.Log($"[{attacker.name}] RangedAttack: Instanciation du projectile à {spawnPosition} vers {target.name}.");

        // Instancier le projectile
        GameObject projectileGO = Instantiate(projectilePrefab, spawnPosition, spawnRotation);
        Projectile projectileScript = projectileGO.GetComponent<Projectile>();

        if (projectileScript == null)
        {
            Debug.LogError($"[{attacker.name}] RangedAttack: Le prefab du projectile '{projectilePrefab.name}' ne contient pas de script Projectile ! Destruction du projectile instancié.");
            Destroy(projectileGO);
            yield break;
        }

        // Initialiser le projectile
        projectileScript.Initialize(target, damage, projectileSpeed, impactVFXPrefab, attacker.GetComponent<Unit>());

        // Le reste de la 'duration' de l'animation de l'attaquant peut continuer après le lancement du projectile.
        float remainingDuration = duration - fireAnimationDelay;
        if (remainingDuration > 0)
        {
            if (showAttackLogs) Debug.Log($"[{attacker.name}] RangedAttack: Attente de la fin de l'animation de l'attaquant ({remainingDuration}s).");
            yield return new WaitForSeconds(remainingDuration);
        }

        if (showAttackLogs)
        {
            Debug.Log($"[{attacker.name}] RangedAttack: Animation d'attaque terminée pour {target.name}.");
        }
    }

    public bool CanAttack(Transform attacker, Transform target, float attackRange)
    {
        // Comme pour MeleeAttack, nous nous fions à la détection de portée de la classe Unit.
        // On pourrait ajouter une vérification de ligne de mire ici si nécessaire.
        // float distanceToTarget = Vector3.Distance(attacker.position, target.position);
        // bool inRange = distanceToTarget <= attackRange;
        // if (showAttackLogs)
        // {
        //     Debug.Log($"[{attacker.name}] RangedAttack: CanAttack {target.name}? Distance: {distanceToTarget:F2}, Range: {attackRange}. Result: {inRange}");
        // }
        // return inRange; // Si vous voulez une vérification de distance brute.

        // Pour l'instant, on se fie à la logique de Unit.cs qui appelle cette méthode APRES avoir vérifié la portée via les tuiles.
        if (showAttackLogs)
        {
             Debug.Log($"[{attacker.name}] RangedAttack: CanAttack {target.name} - On se fie à la détection de portée par tuile de l'unité.");
        }
        return true;
    }
}