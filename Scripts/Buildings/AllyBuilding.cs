using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;

/// <summary>
/// Represents a building controlled by the player.
/// Features unit production, gold generation, health visualization, and reserve tile management.
/// Supports defensive positioning system for allied units.
/// </summary>
public class PlayerBuilding : Building
{
    /// <summary>
    /// Serializable class representing a unit production sequence.
    /// </summary>
    [System.Serializable]
    public class UnitSequence
    {
        /// <summary>
        /// The key sequence required to produce this unit.
        /// </summary>
        [ListDrawerSettings(ShowFoldout = true)]
        public List<KeyCode> sequence;
        /// <summary>
        /// The unit prefab to instantiate when sequence is completed.
        /// </summary>
        public GameObject unitPrefab;
    }

    /// <summary>
    /// List of unit sequences this building can produce.
    /// </summary>
    [SerializeField, ListDrawerSettings(ShowFoldout = true)]
    private List<UnitSequence> unitSequences;

    /// <summary>
    /// Counter for tracking beats for gold generation timing.
    /// </summary>
    private int beatCounter = 0;

    /// <summary>
    /// Whether gold generation is currently active (disabled during tutorial).
    /// </summary>
    private bool isGoldGenerationActive = false;

    [Header("Visual Feedback")]
    /// <summary>
    /// Prefab for the health bar UI element.
    /// </summary>
    [SerializeField] private GameObject healthBarPrefab;
    /// <summary>
    /// Vertical offset for health bar positioning.
    /// </summary>
    [SerializeField] private float healthBarOffset = 1.5f;
    /// <summary>
    /// Whether to display the health bar.
    /// </summary>
    [SerializeField] private bool showHealthBar = true;

    /// <summary>
    /// Instance of the health bar GameObject.
    /// </summary>
    private GameObject healthBarInstance;
    /// <summary>
    /// Slider component for health bar.
    /// </summary>
    private Slider healthBarSlider;
    /// <summary>
    /// Text component for health display.
    /// </summary>
    private TextMeshProUGUI healthText;

    [Header("Effects")]
    /// <summary>
    /// Visual effect prefab for damage events.
    /// </summary>
    [SerializeField] private GameObject damageVFXPrefab;
    /// <summary>
    /// Visual effect prefab for repair events.
    /// </summary>
    [SerializeField] private GameObject repairVFXPrefab;
    /// <summary>
    /// Audio clip for damage sound effects.
    /// </summary>
    [SerializeField] private AudioClip damageSound;
    /// <summary>
    /// Audio clip for repair sound effects.
    /// </summary>
    [SerializeField] private AudioClip repairSound;

    /// <summary>
    /// Audio source component for playing sound effects.
    /// </summary>
    private AudioSource audioSource;

    [Header("Reserve System")]
    /// <summary>
    /// Tiles linked to this building where units can be positioned in defensive mode.
    /// </summary>
    [Tooltip("Tiles linked to this building where units can be positioned in defensive mode")]
    [SerializeField] private List<Tile> reserveTiles = new List<Tile>();
    
    /// <summary>
    /// Whether to show visual indicators for reserve tiles in scene view.
    /// </summary>
    [Tooltip("Visual indicator for reserve tiles in scene view")]
    [SerializeField] private bool showReserveTilesGizmos = true;
    
    /// <summary>
    /// Color for reserve tile gizmos in scene view.
    /// </summary>
    [SerializeField] private Color reserveTileGizmoColor = Color.cyan;

    /// <summary>
    /// Dictionary to track which reserve tiles are occupied by which units.
    /// </summary>
    private Dictionary<Tile, Unit> occupiedReserveTiles = new Dictionary<Tile, Unit>();

    /// <summary>
    /// Subscribes to beat events for gold generation and tutorial completion events.
    /// </summary>
    private void OnEnable()
    {
        // Subscribe to music beat events
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += HandleBeat;
        }
        // Subscribe to tutorial completion event
        TutorialManager.OnTutorialCompleted += EnableGoldGeneration;
    }

    /// <summary>
    /// Unsubscribes from beat events and tutorial completion events.
    /// </summary>
    private void OnDisable()
    {
        // Unsubscribe from music beat events
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleBeat;
        }
        if (TutorialManager.Instance != null)
        {
            TutorialManager.OnTutorialCompleted -= EnableGoldGeneration;
        }
    }

    /// <summary>
    /// Initializes the player building with team assignment, audio, health bar, and reserve tiles.
    /// </summary>
    /// <returns>Coroutine for initialization process.</returns>
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

        // --- NOUVEAU ---
        // Vérifier l'état du tutoriel au démarrage. Si le manager n'existe pas ou que le tuto est déjà fini, on active l'or.
        if (TutorialManager.Instance == null || !TutorialManager.IsTutorialActive)
        {
            isGoldGenerationActive = true;
        }
        // --- FIN NOUVEAU ---

        yield return StartCoroutine(base.Start());

        // Initialize reserve tiles system
        InitializeReserveTiles();

        Debug.Log($"[ALLY BUILDING] {gameObject.name} initialized as {Team} team with {reserveTiles.Count} reserve tiles!");
    }

    /// <summary>
    /// Enables gold generation when the tutorial is completed.
    /// Called by the TutorialManager's OnTutorialCompleted event.
    /// </summary>
    private void EnableGoldGeneration()
    {
        Debug.Log($"[{gameObject.name}] Tutorial completed. Gold generation is now active.");
        isGoldGenerationActive = true;

        // Reset counter so first generation happens after normal delay following tutorial end
        beatCounter = 0;
    }
    /// <summary>
    /// Handles beat events for gold generation timing.
    /// </summary>
    /// <param name="beatDuration">Duration of the current beat.</param>
    private void HandleBeat(float beatDuration)
    {
        if (!isGoldGenerationActive)
        {
            return;
        }

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

    #region Reserve Tiles System

    /// <summary>
    /// Initializes the reserve tiles system by clearing and setting up the occupied tiles dictionary.
    /// </summary>
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
    /// Finds a free reserve tile for a specific unit.
    /// If the unit already occupies a reserve tile of this building, that tile is returned.
    /// </summary>
    /// <param name="unitSeekingReserve">The unit seeking a reserve position.</param>
    /// <returns>An available reserve tile, or null if none available.</returns>
    public Tile GetAvailableReserveTileForUnit(Unit unitSeekingReserve)
    {
        if (unitSeekingReserve == null) return null;

        // 1. Check if the unit already occupies one of THIS building's reserve tiles
        foreach (var kvp in occupiedReserveTiles)
        {
            if (kvp.Value == unitSeekingReserve && kvp.Key != null && reserveTiles.Contains(kvp.Key))
            {
                // The unit is already on one of our reserve tiles, it's "available" for itself
                return kvp.Key;
            }
        }

        // 2. Otherwise, look for a truly free reserve tile
        foreach (Tile tile in reserveTiles)
        {
            // IsReserveTileAvailable checks if the tile is in reserveTiles,
            // is not physically occupied (by another unit on Tile.currentUnit),
            // and is marked as free in our occupiedReserveTiles dictionary
            if (tile != null && IsReserveTileAvailable(tile))
            {
                return tile;
            }
        }
        return null; // No reserve tile available for this unit
    }

    /// <summary>
    /// Checks if a specific reserve tile is generally available (not assigned and not physically occupied).
    /// </summary>
    /// <param name="tile">The tile to check availability for.</param>
    /// <returns>True if the tile is available for reservation.</returns>
    public bool IsReserveTileAvailable(Tile tile)
    {
        if (tile == null || !reserveTiles.Contains(tile)) // Must be one of our known reserve tiles
            return false;

        // Check physical occupation on the tile itself (by a *different* unit than the one that might be in our dictionary)
        if (tile.currentUnit != null && (!occupiedReserveTiles.ContainsKey(tile) || occupiedReserveTiles[tile] != tile.currentUnit) )
        {
            return false; // Physically occupied by an unregistered unit or another unit
        }
        if (tile.currentBuilding != null && tile.currentBuilding != this) // Occupied by another building
        {
            return false;
        }
        if (tile.currentEnvironment != null && tile.currentEnvironment.IsBlocking) // Blocking environment
        {
            return false;
        }

        return occupiedReserveTiles.ContainsKey(tile) && occupiedReserveTiles[tile] == null;
    }

    /// <summary>
    /// Assigns a unit to a specific reserve tile.
    /// </summary>
    /// <param name="unit">The unit to assign.</param>
    /// <param name="newReserveTile">The reserve tile to assign the unit to.</param>
    /// <returns>True if assignment was successful.</returns>
    public bool AssignUnitToReserveTile(Unit unit, Tile newReserveTile)
    {
        if (unit == null || newReserveTile == null || !reserveTiles.Contains(newReserveTile))
        {
            Debug.LogWarning($"[PlayerBuilding:{name}] AssignUnitToReserveTile: Conditions non remplies (Unit, newReserveTile ou tile pas dans la liste). Unit: {unit?.name}, Tile: {newReserveTile?.name}", this);
            return false;
        }

        Tile previousTileForThisUnit = null;
        foreach(var kvp in occupiedReserveTiles)
        {
            if (kvp.Value == unit && kvp.Key != newReserveTile)
            {
                previousTileForThisUnit = kvp.Key;
                break;
            }
        }

        if (previousTileForThisUnit != null)
        {
            occupiedReserveTiles[previousTileForThisUnit] = null;
            Debug.Log($"[PlayerBuilding:{name}] Unit {unit.name} a libéré son ancienne case de réserve {previousTileForThisUnit.name} sur ce bâtiment.", this);
        }

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
    /// Releases a reserve tile from a specific unit.
    /// </summary>
    /// <param name="reserveTile">The tile to release.</param>
    /// <param name="unit">The unit releasing the tile.</param>
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
    /// Checks if this building has any available reserve tiles.
    /// </summary>
    /// <returns>True if there are available reserve tiles.</returns>
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

    /// <summary>
    /// Gets a copy of the reserve tiles list.
    /// </summary>
    /// <returns>A copy of the reserve tiles list.</returns>
    public List<Tile> GetReserveTiles()
    {
        return new List<Tile>(reserveTiles); // Returns a copy
    }
    #endregion
    /// <summary>
    /// Compares two key sequences for equality.
    /// </summary>
    /// <param name="seq1">First sequence to compare.</param>
    /// <param name="seq2">Second sequence to compare.</param>
    /// <returns>True if sequences are equal.</returns>
    private bool AreSequencesEqual(List<KeyCode> seq1, List<KeyCode> seq2)
    {
        if (seq1.Count != seq2.Count) return false;
        for (int i = 0; i < seq1.Count; i++)
        {
            if (seq1[i] != seq2[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// Produces a unit by instantiating the unit prefab on an adjacent tile.
    /// </summary>
    /// <param name="unitPrefab">The unit prefab to instantiate.</param>
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

    /// <summary>
    /// Creates and initializes the health bar UI element.
    /// </summary>
    private void CreateHealthBar()
    {
        healthBarInstance = Instantiate(healthBarPrefab, transform);
        healthBarInstance.transform.localPosition = Vector3.up * healthBarOffset;
        healthBarInstance.transform.rotation = Quaternion.Euler(30, 0, 0);
        healthBarSlider = healthBarInstance.GetComponentInChildren<Slider>();
        healthText = healthBarInstance.GetComponentInChildren<TextMeshProUGUI>();
        UpdateHealthBar();
    }

    /// <summary>
    /// Updates the health bar display with current health values.
    /// </summary>
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

    /// <summary>
    /// Handles damage events for this building specifically.
    /// </summary>
    /// <param name="building">The building that was damaged.</param>
    /// <param name="newHealth">The new health value.</param>
    /// <param name="damage">The amount of damage taken.</param>
    private void OnAnyBuildingDamaged(Building building, int newHealth, int damage)
    {
        if (building != this) return;
        UpdateHealthBar();
        PlayDamageEffects(damage);
    }

    /// <summary>
    /// Plays visual and audio effects for damage events.
    /// </summary>
    /// <param name="damage">The amount of damage taken.</param>
    private void PlayDamageEffects(int damage)
    {
        if (damageSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(damageSound);
        }
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

    /// <summary>
    /// Repairs the building by the specified amount.
    /// </summary>
    /// <param name="amount">The amount of health to restore.</param>
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

    /// <summary>
    /// Plays visual and audio effects for repair events.
    /// </summary>
    /// <param name="amount">The amount of health restored.</param>
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

    /// <summary>
    /// Handles visual changes when the building's team changes.
    /// </summary>
    /// <param name="newTeam">The new team this building belongs to.</param>
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

    /// <summary>
    /// Draws gizmos for reserve tiles when the building is selected in the editor.
    /// </summary>
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

    /// <summary>
    /// Cleans up event subscriptions when the building is destroyed.
    /// </summary>
    public override void OnDestroy()
    {
        // Unsubscribe from events
        Building.OnBuildingDamaged -= OnAnyBuildingDamaged;

        if (TutorialManager.Instance != null)
        {
            TutorialManager.OnTutorialCompleted -= EnableGoldGeneration;
        }

        // Appeler la méthode de la classe de base est une bonne pratique.
        base.OnDestroy();
    }

    /// <summary>
    /// Handles damage to the building and alerts reserve units.
    /// </summary>
    /// <param name="damage">The amount of damage to apply.</param>
    /// <param name="attacker">The unit that attacked this building.</param>
    public override void TakeDamage(int damage, Unit attacker = null)
    {
        base.TakeDamage(damage, attacker);

        if (attacker != null)
        {
            AlertReserveUnitsOfAttack(attacker);
        }
    }

    /// <summary>
    /// Alerts all units in reserve positions that this building is under attack.
    /// </summary>
    /// <param name="attacker">The unit attacking this building.</param>
    private void AlertReserveUnitsOfAttack(Unit attacker)
    {
        foreach (var kvp in occupiedReserveTiles)
        {
            Unit unit = kvp.Value;
            if (unit != null)
            {
                if (unit is AllyUnit ally)
                {
                    ally.OnDefendedBuildingAttacked(this, attacker);
                }
            }
        }
    }
}
