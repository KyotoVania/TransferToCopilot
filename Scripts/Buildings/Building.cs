using UnityEngine;
using System.Collections;
using Sirenix.OdinInspector;

/// <summary>
/// Abstract base class for all buildings in the game.
/// Provides core functionality for health, team affiliation, targeting, and tile attachment.
/// Implements ITargetable interface for selection and interaction systems.
/// </summary>
public abstract class Building : MonoBehaviour, ITargetable
{
    [Header("Team Settings")]
    /// <summary>
    /// The team this building belongs to.
    /// </summary>
    [SerializeField] private TeamType _team = TeamType.Neutral;
    /// <summary>
    /// The tile this building is occupying.
    /// </summary>
    protected Tile occupiedTile;
    /// <summary>
    /// Vertical offset for positioning the building above the tile.
    /// </summary>
    [SerializeField] private float yOffset = 0f;
    /// <summary>
    /// Whether this building has been successfully attached to a tile.
    /// </summary>
    private bool isAttached = false;

    [Header("Combat")]
    /// <summary>
    /// Enable debug logging for building combat events.
    /// </summary>
    [SerializeField] protected bool debugBuildingCombat = true;
    
    [Header("Targeting")]
    /// <summary>
    /// Whether this building can be targeted by default (mouse, controller, banner).
    /// </summary>
    [Tooltip("Si true, ce bâtiment peut être ciblé par défaut (souris, manette, bannière)")]
    [SerializeField] private bool isTargetableByDefault = true;
    
    /// <summary>
    /// Current targeting state (can change during gameplay).
    /// </summary>
    private bool _isCurrentlyTargetable;
    
    /// <summary>
    /// Gets whether this building can currently be targeted.
    /// </summary>
    public virtual bool IsTargetable => _isCurrentlyTargetable;

    [Header("Effects")]
    /// <summary>
    /// Prefab for destruction visual effect.
    /// </summary>
    [SerializeField] protected GameObject destructionVFXPrefab;
    /// <summary>
    /// Duration in seconds before the VFX is destroyed.
    /// </summary>
    [SerializeField] protected float destructionVFXDuration = 3f;

    /// <summary>
    /// Reference to the BuildingStats asset containing health, defense, and resource generation data.
    /// </summary>
    [InlineEditor(InlineEditorModes.FullEditor)]
    [SerializeField] private BuildingStats buildingStats;

    /// <summary>
    /// Current health of the building.
    /// </summary>
    private int _currentHealth;
    /// <summary>
    /// Gets the current health of the building.
    /// </summary>
    public int CurrentHealth => _currentHealth;
    /// <summary>
    /// Gets the maximum health of the building from stats.
    /// </summary>
    public int MaxHealth => buildingStats != null ? buildingStats.health : 0;
    /// <summary>
    /// Gets the defense value of the building from stats.
    /// </summary>
    public int Defense => buildingStats != null ? buildingStats.defense : 0;
    /// <summary>
    /// Gets the gold generation amount from stats.
    /// </summary>
    public int GoldGeneration => buildingStats != null ? buildingStats.goldGeneration : 0;
    /// <summary>
    /// Gets the gold generation delay from stats.
    /// </summary>
    public int GoldGenerationDelay => buildingStats != null ? buildingStats.goldGenerationDelay : 1;
    /// <summary>
    /// Gets the garrison capacity from stats.
    /// </summary>
    public int Garrison => buildingStats != null ? buildingStats.Garrison : 0;

    /// <summary>
    /// Gets the team this building belongs to.
    /// </summary>
    public TeamType Team => _team;

    /// <summary>
    /// Delegate for building health change events.
    /// </summary>
    /// <param name="building">The building that took damage.</param>
    /// <param name="newHealth">The new health value.</param>
    /// <param name="damage">The amount of damage taken.</param>
    public delegate void BuildingHealthChangedHandler(Building building, int newHealth, int damage);
    /// <summary>
    /// Event triggered when a building takes damage.
    /// </summary>
    public static event BuildingHealthChangedHandler OnBuildingDamaged;
    /// <summary>
    /// Event triggered when a building is attacked by a unit.
    /// </summary>
    public static event System.Action<Building, Unit> OnBuildingAttackedByUnit;

    /// <summary>
    /// Delegate for building destruction events.
    /// </summary>
    /// <param name="building">The building that was destroyed.</param>
    public delegate void BuildingDestroyedHandler(Building building);
    /// <summary>
    /// Event triggered when a building is destroyed.
    /// </summary>
    public static event BuildingDestroyedHandler OnBuildingDestroyed;

    /// <summary>
    /// Delegate for building team change events.
    /// </summary>
    /// <param name="building">The building that changed teams.</param>
    /// <param name="oldTeam">The previous team.</param>
    /// <param name="newTeam">The new team.</param>
    public delegate void BuildingTeamChangedHandler(Building building, TeamType oldTeam, TeamType newTeam);
    /// <summary>
    /// Global event triggered when any building changes teams.
    /// </summary>
    public static event BuildingTeamChangedHandler OnBuildingTeamChangedGlobal;

    /// <summary>
    /// Protected accessor for building stats, for use in derived classes.
    /// </summary>
    protected BuildingStats Stats => buildingStats;

    protected virtual void Awake()
    {
        // Initialiser l'état de ciblage avec la valeur par défaut
        _isCurrentlyTargetable = isTargetableByDefault;
        
        if (debugBuildingCombat)
        {
            Debug.Log($"[BUILDING] {gameObject.name} initialized with IsTargetable = {_isCurrentlyTargetable}");
        }
    }

    protected virtual IEnumerator Start()
    {
        // Initialize health
        if (buildingStats != null)
        {
            _currentHealth = buildingStats.health;
            Debug.Log($"[BUILDING] {gameObject.name} initialized with {_currentHealth} health");
        }
        else
        {
            Debug.LogError($"[BUILDING] {gameObject.name} has no building stats assigned!");
        }

        Debug.Log($"[BUILDING] {gameObject.name} starting attachment process at position: {transform.position}");

        // Wait for the HexGridManager to initialize if needed
        while (HexGridManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Try to find and attach to the nearest tile
        while (!isAttached)
        {
            Tile nearestTile = HexGridManager.Instance.GetClosestTile(transform.position);
            if (nearestTile != null)
            {
                if (!nearestTile.IsOccupied)
                {
                    AttachToTile(nearestTile);
                    isAttached = true;
                    break;
                }
            }
            yield return new WaitForSeconds(0.2f);
        }
    }

    /// <summary>
    /// Enables or disables the ability to target this building.
    /// </summary>
    /// <param name="targetable">Whether the building should be targetable.</param>
    public void SetTargetable(bool targetable)
    {
        if (_isCurrentlyTargetable != targetable)
        {
            _isCurrentlyTargetable = targetable;
            
            if (debugBuildingCombat)
            {
                Debug.Log($"[BUILDING] {gameObject.name} targetable state changed to: {targetable}");
            }
        }
    }

    /// <summary>
    /// Toggles the current targeting state.
    /// </summary>
    public void ToggleTargetable()
    {
        SetTargetable(!_isCurrentlyTargetable);
    }

    /// <summary>
    /// Handles incoming damage to the building.
    /// </summary>
    /// <param name="damage">The amount of damage to apply.</param>
    /// <param name="attacker">The unit that attacked this building (optional).</param>
    public virtual void TakeDamage(int damage, Unit attacker = null)
    {
        // Calculate damage after defense
        int actualDamage = Mathf.Max(1, damage - Defense);
        _currentHealth -= actualDamage;

        if (debugBuildingCombat)
        {
            Debug.Log($"[BUILDING] {gameObject.name} took {actualDamage} damage (after {Defense} defense). Health: {_currentHealth}/{MaxHealth}");
            Debug.Log($"[BUILDING] Attacker: {attacker?.name ?? "None"}");  
        }

        // Trigger the damage event
        OnBuildingDamaged?.Invoke(this, _currentHealth, actualDamage);
        if (attacker != null)
        {
            OnBuildingAttackedByUnit?.Invoke(this, attacker);
        }
        // Check if building is destroyed
        if (_currentHealth <= 0)
        {
            Die();
        }
        
    }

    /// <summary>
    /// Changes the team affiliation of this building.
    /// </summary>
    /// <param name="newTeam">The new team to assign to this building.</param>
    public virtual void SetTeam(TeamType newTeam)
    {
        if (_team != newTeam)
        {
            TeamType oldTeam = _team; // Store the old team
            _team = newTeam;
            OnTeamChanged(newTeam); // Call virtual method for derived class-specific logic

            // Trigger global team change event
            OnBuildingTeamChangedGlobal?.Invoke(this, oldTeam, newTeam);
            if(debugBuildingCombat) Debug.Log($"[{gameObject.name}] Global Team Change Event Invoked: {oldTeam} -> {newTeam}");
        }
    }

    /// <summary>
    /// Virtual method called when the building's team changes.
    /// Override in derived classes for special behavior.
    /// </summary>
    /// <param name="newTeam">The new team this building belongs to.</param>
    protected virtual void OnTeamChanged(TeamType newTeam)
    {
        // Override in derived classes if you need special behavior
        if (debugBuildingCombat)
        {
            Debug.Log($"[BUILDING] {gameObject.name} changed to team: {newTeam}");
        }
    }

    /// <summary>
    /// Handles the destruction of the building.
    /// </summary>
    protected virtual void Die()
    {
        if (debugBuildingCombat)
        {
            Debug.Log($"[BUILDING] {gameObject.name} has been destroyed!");
        }

        // Trigger the destroyed event
        OnBuildingDestroyed?.Invoke(this);

        // Play destruction VFX if assigned
        if (destructionVFXPrefab != null)
        {
            GameObject vfx = Instantiate(destructionVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, destructionVFXDuration); // Destroy the VFX object after the specified duration
        }

        // Destroy the gameObject
        Destroy(gameObject);
    }
    /// <summary>
    /// Public method to trigger building destruction from external sources.
    /// </summary>
    public void CallDie()
    {
        Die();
    }

    /// <summary>
    /// Attaches the building to a specific tile.
    /// </summary>
    /// <param name="tile">The tile to attach to.</param>
    protected void AttachToTile(Tile tile)
    {
        occupiedTile = tile;

        // Teleport to the exact center of the tile first
        transform.position = tile.transform.position + new Vector3(0f, yOffset, 0f);

        // Then make it a child with zero local position (except for y offset)
        transform.SetParent(tile.transform, false);
        transform.localPosition = new Vector3(0f, yOffset, 0f);

        Debug.Log($"[BUILDING] {gameObject.name} final position after attachment: {transform.position}");

        // Assign this building to the tile
        tile.AssignBuilding(this);
    }

    /// <summary>
    /// Called when the building is destroyed. Cleans up tile occupation.
    /// </summary>
    public virtual void OnDestroy()
    {
        if (occupiedTile != null)
        {
            occupiedTile.RemoveBuilding();
        }
    }

    /// <summary>
    /// Gets the tile this building is occupying.
    /// </summary>
    /// <returns>The occupied tile, or null if not attached.</returns>
    public Tile GetOccupiedTile()
    {
        return occupiedTile;
    }

    /// <summary>
    /// Sets the current health of the building and triggers appropriate events.
    /// </summary>
    /// <param name="newHealth">The new health value to set.</param>
    protected void SetCurrentHealth(int newHealth)
    {
        int oldHealth = _currentHealth;
        _currentHealth = Mathf.Clamp(newHealth, 0, MaxHealth);

        // If health changed, invoke the damage event
        if (oldHealth != _currentHealth)
        {
            int difference = oldHealth - _currentHealth;
            if (difference > 0)
            {
                // Damage was taken
                OnBuildingDamaged?.Invoke(this, _currentHealth, difference);
            }

            // Check if building is destroyed
            if (_currentHealth <= 0)
            {
                Die();
            }
        }
    }

    /// <summary>
    /// Heals the building by the specified amount.
    /// </summary>
    /// <param name="amount">The amount of health to restore.</param>
    public virtual void Heal(int amount)
    {
        if (amount <= 0 || _currentHealth >= MaxHealth)
            return;

        int newHealth = Mathf.Min(_currentHealth + amount, MaxHealth);
        int actualHeal = newHealth - _currentHealth;

        if (debugBuildingCombat)
        {
            Debug.Log($"[BUILDING] {gameObject.name} healed for {actualHeal}. Health: {newHealth}/{MaxHealth}");
        }

        // Set the new health
        SetCurrentHealth(newHealth);
    }

    /// <summary>
    /// Gets the target point for this building (ITargetable implementation).
    /// </summary>
    public Transform TargetPoint => transform;
    /// <summary>
    /// Gets the GameObject for this building (ITargetable implementation).
    /// </summary>
    public GameObject GameObject => gameObject;
}
