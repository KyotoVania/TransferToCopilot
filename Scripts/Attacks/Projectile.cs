using UnityEngine;
using System.Collections;

public class Projectile : MonoBehaviour
{
    private Transform targetTransform;
    private int damage;
    private float speed;
    private GameObject impactVFXPrefab;
    private Transform attackerTransform; // Pour éviter de se toucher soi-même si l'attaquant a un collider
    private Unit attacker; // Référence à l'attaquant

    private bool initialized = false;
    private Vector3 lastKnownTargetPosition;

    [Tooltip("Distance à laquelle le projectile est considéré comme ayant atteint la cible (pour les cibles mobiles).")]
    [SerializeField] private float hitThreshold = 0.5f;
    [Tooltip("Durée de vie maximale du projectile en secondes, pour éviter qu'il ne vole indéfiniment si la cible est détruite.")]
    [SerializeField] private float maxLifetime = 5f;
    [Tooltip("Le projectile doit-il suivre la cible (homing) ou aller en ligne droite vers la position initiale de la cible ?")]
    [SerializeField] private bool isHoming = true;
    [Tooltip("Que faire quand la cible disparaît : 0 = disparaître, 1 = continuer vers la dernière position connue")]
    [SerializeField] private int targetDestroyedBehavior = 1;

    public void Initialize(Transform target, int projectileDamage, float projectileSpeed, GameObject vfxPrefab, Unit attacker)
    {
        targetTransform = target;
        damage = projectileDamage;
        speed = projectileSpeed;
        impactVFXPrefab = vfxPrefab;
        this.attacker = attacker; // Update to store the Unit attacker

        if (targetTransform != null)
        {
            lastKnownTargetPosition = targetTransform.position;
        }
        else
        {
            Debug.LogWarning("[Projectile] Cible nulle à l'initialisation. Le projectile s'autodétruira.");
            Destroy(gameObject, 0.1f);
            return;
        }

        initialized = true;
        Destroy(gameObject, maxLifetime); // Autodestruction après un certain temps pour éviter les projectiles perdus
    }

    void Update()
    {
        if (!initialized) return;

        bool targetDestroyed = false;
        
        // Vérifier si la cible existe toujours
        if (targetTransform != null && targetTransform.gameObject.activeInHierarchy)
        {
            lastKnownTargetPosition = targetTransform.position; // Mettre à jour la dernière position connue
        }
        else
        {
            targetDestroyed = true;
            
            // Si la cible est détruite, agir en fonction du comportement configuré
            if (targetDestroyedBehavior == 0) // Disparaître
            {
                HandleImpact(null);
                return;
            }
            // Sinon (comportement == 1), continuer vers la dernière position connue
        }

        Vector3 targetPositionToChase;
        
        if (targetDestroyed)
        {
            // Si la cible est détruite, continuer vers la dernière position connue quelle que soit l'option homing
            targetPositionToChase = lastKnownTargetPosition;
        }
        else if (isHoming)
        {
            // Projectile téléguidé qui suit la cible
            targetPositionToChase = lastKnownTargetPosition;
        }
        else
        {
            // Projectile non téléguidé qui va en ligne droite vers la position initiale
            targetPositionToChase = lastKnownTargetPosition;
        }


        Vector3 direction = (targetPositionToChase - transform.position).normalized;

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        transform.position += direction * speed * Time.deltaTime;

        // Vérifier la distance par rapport à la dernière position connue de la cible
        if (Vector3.Distance(transform.position, lastKnownTargetPosition) < hitThreshold)
        {
            HandleImpact(targetTransform != null && targetTransform.gameObject.activeInHierarchy ? targetTransform.GetComponent<Collider>() : null);
        }
    }

    // Gérer la collision du projectile
    void OnTriggerEnter(Collider other)
    {
        if (!initialized) return;

        // Éviter la collision avec l'attaquant lui-même juste après le lancement
        if (attackerTransform != null && other.transform == attackerTransform)
        {
            return;
        }

        // Vérifier si la collision est avec la cible désignée OU si la cible n'existe plus et qu'on touche quelque chose
        if ((targetTransform != null && other.transform == targetTransform) || (targetTransform == null && other.gameObject.layer != gameObject.layer))
        {
            HandleImpact(other);
        }
        // Optionnel : gérer les collisions avec d'autres objets (par exemple, des obstacles)
        // else if (other.gameObject.layer != LayerMask.NameToLayer("IgnoreProjectile")) // Exemple de couche à ignorer
        // {
        //     Debug.Log($"[Projectile] Collision avec un objet inattendu: {other.name}");
        //     HandleImpact(null); // Détruire le projectile, instancier VFX d'impact générique
        // }
    }

    private void HandleImpact(Collider hitCollider)
    {
        if (!initialized) return; // S'assurer que le projectile n'est pas déjà en train d'être détruit

        initialized = false; // Empêcher les impacts multiples

        if (showAttackLogs) Debug.Log($"[Projectile] Impact avec {(hitCollider != null ? hitCollider.name : "cible détruite/position")}.");

        if (hitCollider != null) // Si on a touché un collider valide (pas juste atteint une position)
        {
            // Essayer d'appliquer des dégâts si la cible est une unité ou un bâtiment
            Unit unitTarget = hitCollider.GetComponent<Unit>();
            if (unitTarget == null) unitTarget = hitCollider.GetComponentInParent<Unit>();

            Building buildingTarget = hitCollider.GetComponent<Building>();
            if (buildingTarget == null) buildingTarget = hitCollider.GetComponentInParent<Building>();

            if (unitTarget != null)
            {
                if (showAttackLogs) Debug.Log($"[Projectile] Application de {damage} dégâts à l'unité {unitTarget.name}.");
                unitTarget.TakeDamage(damage, attacker); // Passer l'attaquant
            }
            else if (buildingTarget != null)
            {
                if (showAttackLogs) Debug.Log($"[Projectile] Application de {damage} dégâts au bâtiment {buildingTarget.name}.");
                buildingTarget.TakeDamage(damage, attacker); // Passer l'attaquant
            }
        }

        // Instancier l'effet visuel d'impact si défini
        if (impactVFXPrefab != null)
        {
            Instantiate(impactVFXPrefab, transform.position, Quaternion.LookRotation(-transform.forward)); // Tourné vers l'arrière pour l'explosion
        }

        // Détruire le projectile
        Destroy(gameObject);
    }

    private static bool showAttackLogs = true; // Pourrait être synchronisé avec RangedAttack, mais simple pour l'instant
}