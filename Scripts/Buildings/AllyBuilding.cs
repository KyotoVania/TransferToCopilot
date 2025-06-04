using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;

public class PlayerBuilding : Building
{
    [System.Serializable]
    public class UnitSequence
    {
        [ListDrawerSettings(ShowFoldout = true)]
        public List<KeyCode> sequence;
        public GameObject unitPrefab;
    }

    [SerializeField, ListDrawerSettings(ShowFoldout = true)]
    private List<UnitSequence> unitSequences;

    private int beatCounter = 0;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private float healthBarOffset = 1.5f;
    [SerializeField] private bool showHealthBar = true;

    // Health bar components
    private GameObject healthBarInstance;
    private Slider healthBarSlider;
    private TextMeshProUGUI healthText;

    // Effects for damage and repair
    [Header("Effects")]
    [SerializeField] private GameObject damageVFXPrefab;
    [SerializeField] private GameObject repairVFXPrefab;
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip repairSound;

    // Audio source for SFX
    private AudioSource audioSource;

    // NOUVEAU: Système de cases de réserves
    [Header("Reserve System")]
    [Tooltip("Tiles linked to this building where units can be positioned in defensive mode")]
    [SerializeField] private List<Tile> reserveTiles = new List<Tile>();
    
    [Tooltip("Visual indicator for reserve tiles in scene view")]
    [SerializeField] private bool showReserveTilesGizmos = true;
    
    [SerializeField] private Color reserveTileGizmoColor = Color.cyan;

    // Dictionnaire pour tracker quelles cases de réserves sont occupées
    private Dictionary<Tile, Unit> occupiedReserveTiles = new Dictionary<Tile, Unit>();

    private void OnEnable()
    {
        RhythmManager.OnBeat += HandleBeat;
        //SequenceController.OnSequenceExecuted += OnSequenceExecuted;
    }

    private void OnDisable()
    {
        RhythmManager.OnBeat -= HandleBeat;
       // SequenceController.OnSequenceExecuted -= OnSequenceExecuted;
    }

    protected override IEnumerator Start()
    {
        // Set the team to Player right at the start
        SetTeam(TeamType.Player);

        // Initialize audio
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f; // 3D sound
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = 20f;

        // Setup health bar if needed
        if (showHealthBar && healthBarPrefab != null)
        {
            CreateHealthBar();
        }

        // Subscribe to the damage event
        Building.OnBuildingDamaged += OnAnyBuildingDamaged;

        // Call base implementation to handle tile attachment, etc.
        yield return StartCoroutine(base.Start());

        // Initialize reserve tiles system
        InitializeReserveTiles();

        Debug.Log($"[ALLY BUILDING] {gameObject.name} initialized as {Team} team with {reserveTiles.Count} reserve tiles!");
    }

#region Reserve Tiles System

    private void InitializeReserveTiles()
    {
        occupiedReserveTiles.Clear();
        foreach (Tile tile in reserveTiles)
        {
            if (tile != null)
            {
                occupiedReserveTiles[tile] = null;
            }
        }
    }

    /// <summary>
    /// Trouve une case de réserve libre pour une unité spécifique.
    /// Si l'unité occupe déjà une case de réserve de ce bâtiment, cette case est retournée.
    /// </summary>
    public Tile GetAvailableReserveTileForUnit(Unit unitSeekingReserve)
    {
        if (unitSeekingReserve == null) return null;

        // 1. Vérifier si l'unité occupe déjà une des cases de réserve de CE bâtiment
        foreach (var kvp in occupiedReserveTiles)
        {
            if (kvp.Value == unitSeekingReserve && kvp.Key != null && reserveTiles.Contains(kvp.Key))
            {
                // L'unité est déjà sur une de nos cases de réserve, elle est "disponible" pour elle-même.
                return kvp.Key;
            }
        }

        // 2. Sinon, chercher une case de réserve réellement libre.
        foreach (Tile tile in reserveTiles)
        {
            // IsReserveTileAvailable vérifie si la tuile est dans reserveTiles,
            // n'est pas occupée physiquement (par une autre unité sur Tile.currentUnit),
            // et est marquée comme libre dans notre dictionnaire occupiedReserveTiles.
            if (tile != null && IsReserveTileAvailable(tile))
            {
                return tile;
            }
        }
        return null; // Aucune case de réserve disponible pour cette unité.
    }

    /// <summary>
    /// Vérifie si une case de réserve spécifique est disponible de manière générale (non assignée et non occupée physiquement).
    /// </summary>
    public bool IsReserveTileAvailable(Tile tile)
    {
        if (tile == null || !reserveTiles.Contains(tile)) // Doit être une de nos cases de réserve connues
            return false;

        // Vérifier l'occupation physique sur la tuile elle-même (par une *autre* unité que celle qui pourrait être dans notre dictionnaire)
        if (tile.currentUnit != null && (!occupiedReserveTiles.ContainsKey(tile) || occupiedReserveTiles[tile] != tile.currentUnit) )
        {
            return false; // Occupée physiquement par une unité non enregistrée ici ou une autre unité
        }
        if (tile.currentBuilding != null && tile.currentBuilding != this) // Occupée par un autre bâtiment
        {
            return false;
        }
        if (tile.currentEnvironment != null && tile.currentEnvironment.IsBlocking) // Environnement bloquant
        {
            return false;
        }


        // Vérifier notre suivi interne : la case doit exister dans le dictionnaire et être marquée comme non occupée (null)
        return occupiedReserveTiles.ContainsKey(tile) && occupiedReserveTiles[tile] == null;
    }

    /// <summary>
    /// Assigne une unité à une case de réserve. S'assure de libérer l'ancienne case de l'unité si elle en avait une sur CE bâtiment.
    /// </summary>
    public bool AssignUnitToReserveTile(Unit unit, Tile newReserveTile)
    {
        if (unit == null || newReserveTile == null || !reserveTiles.Contains(newReserveTile))
        {
            Debug.LogWarning($"[PlayerBuilding:{name}] AssignUnitToReserveTile: Conditions non remplies (Unit, newReserveTile ou tile pas dans la liste). Unit: {unit?.name}, Tile: {newReserveTile?.name}", this);
            return false;
        }

        // Vérifier si l'unité était déjà assignée à une *autre* case de réserve de CE bâtiment.
        Tile previousTileForThisUnit = null;
        foreach(var kvp in occupiedReserveTiles)
        {
            if (kvp.Value == unit && kvp.Key != newReserveTile) // Trouvé l'unité sur une autre case
            {
                previousTileForThisUnit = kvp.Key;
                break;
            }
        }

        // Si elle était sur une autre case de ce bâtiment, la libérer.
        if (previousTileForThisUnit != null)
        {
            occupiedReserveTiles[previousTileForThisUnit] = null;
            Debug.Log($"[PlayerBuilding:{name}] Unit {unit.name} a libéré son ancienne case de réserve {previousTileForThisUnit.name} sur ce bâtiment.", this);
        }

        // Maintenant, vérifier si la nouvelle case est disponible ou déjà assignée à cette même unité.
        if (occupiedReserveTiles.ContainsKey(newReserveTile))
        {
            if (occupiedReserveTiles[newReserveTile] == null || occupiedReserveTiles[newReserveTile] == unit)
            {
                // La case est libre OU déjà assignée à cette unité (parfait, on confirme/réassigne).
                occupiedReserveTiles[newReserveTile] = unit;
                Debug.Log($"[PlayerBuilding:{name}] Unit {unit.name} assignée à la case de réserve ({newReserveTile.column},{newReserveTile.row}).", this);
                return true;
            }
            else
            {
                // La case est occupée par une AUTRE unité.
                Debug.LogWarning($"[PlayerBuilding:{name}] AssignUnitToReserveTile: La nouvelle case {newReserveTile.name} est déjà occupée par {occupiedReserveTiles[newReserveTile].name}. Impossible d'assigner {unit.name}.", this);
                return false;
            }
        }
        else
        {
            // La tuile de réserve n'était même pas dans notre dictionnaire, ce qui est étrange si reserveTiles.Contains(newReserveTile) est vrai.
            // Cela peut arriver si InitializeReserveTiles n'a pas inclus toutes les tuiles de la liste reserveTiles.
            Debug.LogError($"[PlayerBuilding:{name}] AssignUnitToReserveTile: La case {newReserveTile.name} est dans reserveTiles mais pas dans occupiedReserveTiles. Problème d'initialisation probable.", this);
            return false;
        }
    }

    /// <summary>
    /// Libère une case de réserve si l'unité spécifiée l'occupait.
    /// </summary>
    public void ReleaseReserveTile(Tile reserveTile, Unit unit)
    {
        if (unit == null || reserveTile == null) return;

        if (occupiedReserveTiles.ContainsKey(reserveTile) && occupiedReserveTiles[reserveTile] == unit)
        {
            occupiedReserveTiles[reserveTile] = null;
            Debug.Log($"[PlayerBuilding:{name}] Case de réserve ({reserveTile.column},{reserveTile.row}) explicitement libérée par/pour {unit.name}.", this);
        }
    }

    /// <summary>
    /// Vérifie s'il y a au moins une case de réserve globalement disponible.
    /// C'EST ICI QU'ÉTAIT L'ERREUR CS7036.
    /// </summary>
    public bool HasAvailableReserveTiles()
    {
        foreach (Tile tile in reserveTiles)
        {
            // Utilise la vérification générale IsReserveTileAvailable
            if (tile != null && IsReserveTileAvailable(tile))
            {
                return true;
            }
        }
        return false;
    }

    public List<Tile> GetReserveTiles()
    {
        return new List<Tile>(reserveTiles); // Retourne une copie
    }
    private void HandleBeat()
    {
        beatCounter++;
        if (beatCounter >= Stats.goldGenerationDelay)
        {
            if (Stats.goldGeneration > 0)
            {
                GoldController.Instance.AddGold(Stats.goldGeneration);
            }
            beatCounter = 0;
        }
    }
    #endregion
    /*
    private void OnSequenceExecuted(Sequence executedSequence, int perfectCount)
    {
        foreach (var unitSequence in unitSequences)
        {
            if (AreSequencesEqual(executedSequence.targetSequence, unitSequence.sequence))
            {
                ProduceUnit(unitSequence.unitPrefab);
                break;
            }
        }
    }
    */
    private bool AreSequencesEqual(List<KeyCode> seq1, List<KeyCode> seq2)
    {
        if (seq1.Count != seq2.Count) return false;
        for (int i = 0; i < seq1.Count; i++)
        {
            if (seq1[i] != seq2[i]) return false;
        }
        return true;
    }

    private void ProduceUnit(GameObject unitPrefab)
    {
        if (unitPrefab == null)
        {
            Debug.LogError("Unit prefab is null!");
            return;
        }

        List<Tile> adjacentTiles = HexGridManager.Instance.GetAdjacentTiles(occupiedTile);
        foreach (Tile tile in adjacentTiles)
        {
            if (!tile.IsOccupied)
            {
                Instantiate(unitPrefab, tile.transform.position, Quaternion.identity);
                return;
            }
        }
        Debug.LogWarning("No available adjacent tiles to spawn unit!");
    }

    private void CreateHealthBar()
    {
        // Instantiate the health bar
        healthBarInstance = Instantiate(healthBarPrefab, transform);
        healthBarInstance.transform.localPosition = Vector3.up * healthBarOffset;

        // Make the health bar face the camera
        healthBarInstance.transform.rotation = Quaternion.Euler(30, 0, 0);

        // Get the slider and text components
        healthBarSlider = healthBarInstance.GetComponentInChildren<Slider>();
        healthText = healthBarInstance.GetComponentInChildren<TextMeshProUGUI>();

        // Initialize the health bar values
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (healthBarSlider != null)
        {
            healthBarSlider.maxValue = MaxHealth;
            healthBarSlider.value = CurrentHealth;
        }

        if (healthText != null)
        {
            healthText.text = $"{CurrentHealth}/{MaxHealth}";
        }
    }

    // Event handler for damage
    private void OnAnyBuildingDamaged(Building building, int newHealth, int damage)
    {
        // Only respond to events for this building
        if (building != this) return;

        // Update health bar
        UpdateHealthBar();

        // Play damage effects
        PlayDamageEffects(damage);
    }

    private void PlayDamageEffects(int damage)
    {
        // Play damage sound
        if (damageSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(damageSound);
        }

        // Spawn damage VFX
        if (damageVFXPrefab != null)
        {
            GameObject vfx = Instantiate(
                damageVFXPrefab,
                transform.position + Vector3.up,
                Quaternion.identity
            );

            // Auto-destroy the VFX after 2 seconds
            Destroy(vfx, 2.0f);
        }
    }

    // Method to repair the building
    public void Repair(int amount)
    {
        if (CurrentHealth >= MaxHealth)
            return;

        // Calculate new health
        int newHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
        int actualRepair = newHealth - CurrentHealth;

        if (actualRepair <= 0)
            return;

        // Update health using the protected method from the base class
        SetCurrentHealth(newHealth);

        if (debugBuildingCombat)
        {
            Debug.Log($"[ALLY BUILDING] {gameObject.name} repaired for {actualRepair}. Health: {CurrentHealth}/{MaxHealth}");
        }

        // Play repair effects
        PlayRepairEffects(actualRepair);

        // Update health bar
        UpdateHealthBar();
    }

    private void PlayRepairEffects(int amount)
    {
        // Play repair sound
        if (repairSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(repairSound);
        }

        // Spawn repair VFX
        if (repairVFXPrefab != null)
        {
            GameObject vfx = Instantiate(
                repairVFXPrefab,
                transform.position + Vector3.up,
                Quaternion.identity
            );

            // Auto-destroy the VFX after 2 seconds
            Destroy(vfx, 2.0f);
        }
    }

    // Override team change visuals
    protected override void OnTeamChanged(TeamType newTeam)
    {
        base.OnTeamChanged(newTeam);

        // Visual feedback for team change
        if (newTeam == TeamType.Player)
        {
            // For example, if you want to tint the building blue for player team
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                // Apply a slight blue tint to materials
                foreach (Material material in renderer.materials)
                {
                    Color originalColor = material.color;
                    Color tintedColor = Color.Lerp(originalColor, Color.blue, 0.3f);
                    material.color = tintedColor;
                }
            }
        }
    }

    // Gizmos pour visualiser les cases de réserves dans l'éditeur
    private void OnDrawGizmosSelected()
    {
        if (!showReserveTilesGizmos || reserveTiles == null) return;

        Gizmos.color = reserveTileGizmoColor;
        
        foreach (Tile tile in reserveTiles)
        {
            if (tile != null)
            {
                // Dessiner une sphère wireframe pour chaque case de réserve
                Gizmos.DrawWireSphere(tile.transform.position + Vector3.up * 0.5f, 0.3f);
                
                // Dessiner une ligne entre le bâtiment et la case de réserve
                Gizmos.DrawLine(transform.position + Vector3.up, tile.transform.position + Vector3.up * 0.5f);
            }
        }
    }

    public override void OnDestroy()
    {
        // Unsubscribe from events
        Building.OnBuildingDamaged -= OnAnyBuildingDamaged;
        RhythmManager.OnBeat -= HandleBeat;
       // SequenceController.OnSequenceExecuted -= OnSequenceExecuted;

        // Call base implementation
        base.OnDestroy();
    }

    // Ajout : Notifier explicitement les unités en réserve lors d'une attaque
    public override void TakeDamage(int damage, Unit attacker = null)
    {
        base.TakeDamage(damage, attacker);

        if (attacker != null)
        {
            AlertReserveUnitsOfAttack(attacker);
        }
    }

    /// <summary>
    /// Notifie toutes les unités présentes dans les cases de réserve que ce bâtiment est attaqué.
    /// </summary>
    private void AlertReserveUnitsOfAttack(Unit attacker)
    {
        foreach (var kvp in occupiedReserveTiles)
        {
            Unit unit = kvp.Value;
            if (unit != null)
            {
                // Appel direct à la méthode d'alerte sur l'unité (si elle existe)
                if (unit is AllyUnit ally)
                {
                    ally.OnDefendedBuildingAttacked(this, attacker);
                }
            }
        }
    }
}
