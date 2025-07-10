// Fichier: Scripts/Buildings/BossDamageBuilding.cs
using UnityEngine;

public enum TowerMode
{
    Explosive,  // Mode original : dégâts instantanés à la capture
    Artillery   // Mode artillerie : attaque rythmique continue
}

public class BossDamageBuilding : NeutralBuilding
{
    [Header("Tower Mode")]
    [Tooltip("Mode de fonctionnement de la tour")]
    [SerializeField] private TowerMode towerMode = TowerMode.Explosive;

    [Header("Boss Targeting")]
    [Tooltip("Faites glisser ici le Boss que ce bâtiment doit attaquer.")]
    [SerializeField] private BossUnit targetBoss;

    [Header("Explosive Mode Settings")]
    [Tooltip("Le pourcentage de la vie maximale du boss à infliger comme dégâts lors de la capture.")]
    [Range(0f, 100f)]
    [SerializeField] private float explosiveDamagePercentage = 10f;

    [Header("Artillery Mode Settings")]
    [Tooltip("Dégâts par attaque rythmique (en pourcentage de la vie max du boss).")]
    [Range(0f, 10f)]
    [SerializeField] private float artilleryDamagePercentage = 2f;
    
    [Tooltip("Portée d'attaque en mode artillerie (en tuiles). 0 = portée illimitée.")]
    [SerializeField] private int artilleryRange = 5;
    
    [Tooltip("Si true, trouve automatiquement des boss à proximité en mode artillerie.")]
    [SerializeField] private bool autoTargetBosses = false;
    
    [Tooltip("Nombre de beats à attendre entre chaque tir (1 = tire à chaque beat, 2 = tire tous les 2 beats, etc.).")]
    [Range(1, 10)]
    [SerializeField] private int fireRateInBeats = 2;

    [Header("Projectile Settings")]
    [Tooltip("Le prefab du projectile à lancer (doit avoir un script Projectile).")]
    [SerializeField] private GameObject projectilePrefab;
    
    [Tooltip("Vitesse du projectile en unités par seconde.")]
    [SerializeField] private float projectileSpeed = 15f;
    
    [Tooltip("Point de lancement du projectile. Si non assigné, utilise la position de la tour avec un offset.")]
    [SerializeField] private Transform projectileSpawnPoint;
    
    [Tooltip("Offset par rapport à la position de la tour si projectileSpawnPoint n'est pas défini.")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0, 2f, 0);
    
    [Tooltip("Si true, le projectile aura une trajectoire en arc (lob), sinon trajectoire droite.")]
    [SerializeField] private bool useLobTrajectory = true;
    
    [Tooltip("Hauteur de l'arc pour la trajectoire lob (plus haut = arc plus prononcé).")]
    [SerializeField] private float lobHeight = 5f;

    [Header("Effects")]
    [Tooltip("Effet visuel à l'impact du projectile (optionnel).")]
    [SerializeField] private GameObject impactVFX;
    
    [Tooltip("Son de tir du projectile (optionnel).")]
    [SerializeField] private AudioClip fireSFX;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Variables privées pour le mode artillerie
    private bool isAttackingRhythmically = false;
    private BossUnit currentArtilleryTarget;
    private int currentBeatCounter = 0; // Compteur pour les beats

    private void OnDisable()
    {
        StopArtilleryMode();
    }

    /// <summary>
    /// Cette méthode est appelée automatiquement lorsque l'équipe du bâtiment change.
    /// </summary>
    protected override void OnTeamChanged(TeamType newTeam)
    {
        base.OnTeamChanged(newTeam);

        // Si le bâtiment est capturé par le joueur
        if (newTeam == TeamType.Player)
        {
            if (towerMode == TowerMode.Explosive)
            {
                ExecuteExplosiveMode();
            }
            else if (towerMode == TowerMode.Artillery)
            {
                StartArtilleryMode();
            }
        }
        else
        {
            // Si le bâtiment n'appartient plus au joueur, arrêter le mode artillerie
            StopArtilleryMode();
        }
    }

    /// <summary>
    /// Exécute le mode explosif (comportement original).
    /// </summary>
    private void ExecuteExplosiveMode()
    {
        // On vérifie si une cible a été assignée dans l'inspecteur
        if (targetBoss != null && targetBoss.Health > 0)
        {
            if (enableDebugLogs)
                Debug.Log($"[BossDamageBuilding] Bâtiment capturé ! Mode EXPLOSIF : Inflige {explosiveDamagePercentage}% de dégâts au boss '{targetBoss.name}'.");
            
            targetBoss.TakePercentageDamage(explosiveDamagePercentage);
        }
        else if (targetBoss == null)
        {
            Debug.LogError($"[BossDamageBuilding] Le bâtiment '{this.name}' en mode explosif a été capturé, mais aucun 'Target Boss' n'a été assigné dans l'inspecteur !");
        }
    }

    /// <summary>
    /// Démarre le mode artillerie (attaque rythmique continue).
    /// </summary>
    private void StartArtilleryMode()
    {
        if (isAttackingRhythmically) return;

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += ExecuteArtilleryAttack;
            isAttackingRhythmically = true;

            // Initialiser la cible pour le mode artillerie
            currentArtilleryTarget = targetBoss;
            currentBeatCounter = 0; // Réinitialiser le compteur

            if (enableDebugLogs)
                Debug.Log($"[BossDamageBuilding] {name} : Mode ARTILLERIE démarré.", this);
        }
        else
        {
            Debug.LogWarning("[BossDamageBuilding] MusicManager.Instance est null. Impossible de démarrer le mode artillerie.", this);
        }
    }

    /// <summary>
    /// Arrête le mode artillerie.
    /// </summary>
    private void StopArtilleryMode()
    {
        if (!isAttackingRhythmically) return;

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= ExecuteArtilleryAttack;
        }

        isAttackingRhythmically = false;
        currentArtilleryTarget = null;

        if (enableDebugLogs)
            Debug.Log($"[BossDamageBuilding] {name} : Mode artillerie arrêté.", this);
    }

    /// <summary>
    /// Exécute une attaque d'artillerie selon la cadence de tir définie.
    /// </summary>
    /// <param name="beatDuration">Durée du battement.</param>
    private void ExecuteArtilleryAttack(float beatDuration)
    {
        // Vérifier si le bâtiment appartient toujours au joueur
        if (Team != TeamType.Player) return;

        // Incrémenter le compteur de beats
        currentBeatCounter++;

        // Ne tirer que tous les X beats selon fireRateInBeats
        if (currentBeatCounter < fireRateInBeats)
        {
            return; // Pas encore le moment de tirer
        }

        // Réinitialiser le compteur pour le prochain tir
        currentBeatCounter = 0;

        // Vérifier si on a un prefab de projectile
        if (projectilePrefab == null)
        {
            if (enableDebugLogs)
                Debug.LogError($"[BossDamageBuilding] {name} : Aucun projectile assigné pour l'attaque d'artillerie !", this);
            return;
        }

        // Trouver une cible valide
        BossUnit target = FindValidArtilleryTarget();
        
        if (target != null)
        {
            // Vérifier la portée si nécessaire
            if (artilleryRange > 0 && !IsTargetInRange(target))
            {
                return;
            }

            // Tirer un projectile au lieu d'infliger des dégâts instantanés
            FireProjectileAtTarget(target);

            if (enableDebugLogs)
                Debug.Log($"[BossDamageBuilding] {name} tire un projectile sur {target.name} (tous les {fireRateInBeats} beats).", this);
        }
    }

    /// <summary>
    /// Trouve une cible valide pour l'attaque d'artillerie.
    /// </summary>
    /// <returns>La cible trouvée ou null.</returns>
    private BossUnit FindValidArtilleryTarget()
    {
        // Priorité 1: Utiliser la cible assignée si elle est encore valide
        if (targetBoss != null && IsValidTarget(targetBoss))
        {
            currentArtilleryTarget = targetBoss;
            return targetBoss;
        }

        // Priorité 2: Si auto-ciblage activé, chercher d'autres boss
        if (autoTargetBosses)
        {
            BossUnit[] allBosses = FindObjectsByType<BossUnit>(FindObjectsSortMode.None);
            BossUnit closestBoss = null;
            float closestDistance = float.MaxValue;

            foreach (BossUnit boss in allBosses)
            {
                if (IsValidTarget(boss))
                {
                    float distance = GetDistanceToTarget(boss);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestBoss = boss;
                    }
                }
            }

            currentArtilleryTarget = closestBoss;
            return closestBoss;
        }

        return null;
    }

    /// <summary>
    /// Vérifie si une cible est valide.
    /// </summary>
    /// <param name="boss">Le boss à vérifier.</param>
    /// <returns>True si la cible est valide.</returns>
    private bool IsValidTarget(BossUnit boss)
    {
        return boss != null && 
               boss.gameObject.activeInHierarchy && 
               boss.Health > 0 &&
               boss.IsTargetable; // Respect du principe ITargetable
    }

    /// <summary>
    /// Vérifie si une cible est dans la portée.
    /// </summary>
    /// <param name="target">La cible à vérifier.</param>
    /// <returns>True si la cible est dans la portée.</returns>
    private bool IsTargetInRange(BossUnit target)
    {
        if (artilleryRange <= 0) return true; // Portée illimitée

        float distance = GetDistanceToTarget(target);
        return distance <= artilleryRange;
    }

    /// <summary>
    /// Calcule la distance entre ce bâtiment et une cible.
    /// </summary>
    /// <param name="target">La cible.</param>
    /// <returns>La distance en tuiles.</returns>
    private float GetDistanceToTarget(BossUnit target)
    {
        if (target == null || HexGridManager.Instance == null) return float.MaxValue;

        Tile buildingTile = GetOccupiedTile();
        Tile targetTile = target.GetOccupiedTile();

        if (buildingTile == null || targetTile == null) return float.MaxValue;

        return HexGridManager.Instance.HexDistance(
            buildingTile.column, buildingTile.row,
            targetTile.column, targetTile.row
        );
    }

    /// <summary>
    /// Tire un projectile vers la cible spécifiée.
    /// </summary>
    /// <param name="target">La cible à viser.</param>
    private void FireProjectileAtTarget(BossUnit target)
    {
        // Calculer la position de spawn du projectile
        Vector3 spawnPosition = projectileSpawnPoint != null 
            ? projectileSpawnPoint.position 
            : transform.position + transform.TransformDirection(spawnOffset);

        // Calculer la direction vers la cible
        Vector3 directionToTarget = target.transform.position - spawnPosition;
        directionToTarget.y = 0; // Garder le projectile horizontal
        
        Quaternion spawnRotation = Quaternion.identity;
        if (directionToTarget != Vector3.zero)
        {
            spawnRotation = Quaternion.LookRotation(directionToTarget);
        }

        // Instancier le projectile
        GameObject projectileGO = Instantiate(projectilePrefab, spawnPosition, spawnRotation);
        Projectile projectileScript = projectileGO.GetComponent<Projectile>();

        if (projectileScript == null)
        {
            Debug.LogError($"[BossDamageBuilding] {name} : Le prefab du projectile '{projectilePrefab.name}' ne contient pas de script Projectile !", this);
            Destroy(projectileGO);
            return;
        }

        // Pour les boss, on utilise un système de dégâts spécial
        // On va passer les dégâts via un composant spécial sur le projectile
        BossProjectileData bossData = projectileGO.AddComponent<BossProjectileData>();
        bossData.damagePercentage = artilleryDamagePercentage;
        bossData.isFromTower = true;
        
        // Ajouter les données de trajectoire lob si nécessaire
        if (useLobTrajectory)
        {
            LobProjectileData lobData = projectileGO.AddComponent<LobProjectileData>();
            lobData.lobHeight = lobHeight;
            lobData.useLobTrajectory = true;
        }
        
        // Utiliser des dégâts temporaires (1) car le vrai calcul se fera à l'impact
        int tempDamage = 1;
        
        // Initialiser le projectile
        if (enableDebugLogs)
            Debug.Log($"[BossDamageBuilding] Initialisation du projectile avec VFX: {(impactVFX != null ? impactVFX.name : "NULL")}", this);
        
        projectileScript.Initialize(target.transform, tempDamage, projectileSpeed, impactVFX, null);

        // Jouer le son de tir
        PlayFireSound();
    }

    /// <summary>
    /// Joue le son de tir de projectile.
    /// </summary>
    private void PlayFireSound()
    {
        if (fireSFX != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(fireSFX);
            }
            else
            {
                AudioSource.PlayClipAtPoint(fireSFX, transform.position);
            }
        }
    }

    /// <summary>
    /// Méthode publique pour changer le mode de la tour.
    /// </summary>
    /// <param name="newMode">Le nouveau mode.</param>
    public void SetTowerMode(TowerMode newMode)
    {
        if (towerMode != newMode)
        {
            StopArtilleryMode(); // Arrêter l'ancien mode
            towerMode = newMode;

            // Si déjà capturé par le joueur, appliquer le nouveau mode
            if (Team == TeamType.Player)
            {
                if (towerMode == TowerMode.Artillery)
                {
                    StartArtilleryMode();
                }
            }

            if (enableDebugLogs)
                Debug.Log($"[BossDamageBuilding] {name} : Mode changé vers {newMode}.", this);
        }
    }

    /// <summary>
    /// Propriété pour obtenir le mode actuel.
    /// </summary>
    public TowerMode CurrentMode => towerMode;

    /// <summary>
    /// Propriété pour savoir si la tour attaque en mode artillerie.
    /// </summary>
    public bool IsInArtilleryMode => towerMode == TowerMode.Artillery && isAttackingRhythmically;
}