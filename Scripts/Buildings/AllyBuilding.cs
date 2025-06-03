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
        // Nettoyer le dictionnaire au démarrage
        occupiedReserveTiles.Clear();
        
        // Initialiser le dictionnaire avec toutes les cases de réserves comme libres
        foreach (Tile tile in reserveTiles)
        {
            if (tile != null)
            {
                occupiedReserveTiles[tile] = null;
            }
        }
    }

    /// <summary>
    /// Trouve une case de réserve libre pour une unité
    /// </summary>
    /// <returns>Une case de réserve libre, ou null si aucune n'est disponible</returns>
    public Tile GetAvailableReserveTile()
    {
        foreach (Tile tile in reserveTiles)
        {
            if (tile != null && IsReserveTileAvailable(tile))
            {
                return tile;
            }
        }
        return null;
    }

    /// <summary>
    /// Vérifie si une case de réserve est disponible
    /// </summary>
    public bool IsReserveTileAvailable(Tile tile)
    {
        if (tile == null || !reserveTiles.Contains(tile))
            return false;

        // Vérifier si la tile n'est pas occupée par une unité et n'est pas dans notre dictionnaire comme occupée
        return !tile.IsOccupied && (occupiedReserveTiles.ContainsKey(tile) && occupiedReserveTiles[tile] == null);
    }

    /// <summary>
    /// Assigne une unité à une case de réserve
    /// </summary>
    public bool AssignUnitToReserveTile(Unit unit, Tile reserveTile)
    {
        if (unit == null || reserveTile == null || !reserveTiles.Contains(reserveTile))
            return false;

        if (!IsReserveTileAvailable(reserveTile))
            return false;

        occupiedReserveTiles[reserveTile] = unit;
        return true;
    }

    /// <summary>
    /// Libère une case de réserve
    /// </summary>
    public void ReleaseReserveTile(Tile reserveTile, Unit unit)
    {
        if (reserveTile != null && occupiedReserveTiles.ContainsKey(reserveTile) && occupiedReserveTiles[reserveTile] == unit)
        {
            occupiedReserveTiles[reserveTile] = null;
        }
    }

    /// <summary>
    /// Vérifie s'il y a des cases de réserves disponibles
    /// </summary>
    public bool HasAvailableReserveTiles()
    {
        return GetAvailableReserveTile() != null;
    }

    /// <summary>
    /// Retourne toutes les cases de réserves
    /// </summary>
    public List<Tile> GetReserveTiles()
    {
        return new List<Tile>(reserveTiles);
    }

    /// <summary>
    /// Ajoute une case de réserve via script
    /// </summary>
    public void AddReserveTile(Tile tile)
    {
        if (tile != null && !reserveTiles.Contains(tile))
        {
            reserveTiles.Add(tile);
            occupiedReserveTiles[tile] = null;
        }
    }

    /// <summary>
    /// Supprime une case de réserve
    /// </summary>
    public void RemoveReserveTile(Tile tile)
    {
        if (tile != null && reserveTiles.Contains(tile))
        {
            reserveTiles.Remove(tile);
            if (occupiedReserveTiles.ContainsKey(tile))
            {
                occupiedReserveTiles.Remove(tile);
            }
        }
    }

    #endregion

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
}