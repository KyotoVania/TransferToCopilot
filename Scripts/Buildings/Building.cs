using UnityEngine;
using System.Collections;
using Sirenix.OdinInspector;

public abstract class Building : MonoBehaviour, ITargetable
{
    [Header("Team Settings")]
    [SerializeField] private TeamType _team = TeamType.Neutral;
    protected Tile occupiedTile;
    [SerializeField] private float yOffset = 0f; // Adjustable vertical offset in the editor
    private bool isAttached = false;

    [Header("Combat")]
    [SerializeField] protected bool debugBuildingCombat = true;
    public virtual bool IsTargetable => true; // Most buildings should be targetable by default

    [Header("Effects")]
    [SerializeField] protected GameObject destructionVFXPrefab; // Prefab for destruction visual effect
    [SerializeField] protected float destructionVFXDuration = 3f; // Duration in seconds before the VFX is destroyed

    // Reference to the BuildingStats asset.
    [InlineEditor(InlineEditorModes.FullEditor)]
    [SerializeField] private BuildingStats buildingStats;

    // Public getters and private health tracking
    private int _currentHealth;
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => buildingStats != null ? buildingStats.health : 0;
    public int Defense => buildingStats != null ? buildingStats.defense : 0;
    public int GoldGeneration => buildingStats != null ? buildingStats.goldGeneration : 0;
    public int GoldGenerationDelay => buildingStats != null ? buildingStats.goldGenerationDelay : 1;
    public int Garrison => buildingStats != null ? buildingStats.Garrison : 0;

    public TeamType Team => _team;

    // Event for building damage and destruction
    public delegate void BuildingHealthChangedHandler(Building building, int newHealth, int damage);
    public static event BuildingHealthChangedHandler OnBuildingDamaged;
    public static event System.Action<Building, Unit> OnBuildingAttackedByUnit; // Building attaqué, par quelle Unit

    public delegate void BuildingDestroyedHandler(Building building);
    public static event BuildingDestroyedHandler OnBuildingDestroyed;

    public delegate void BuildingTeamChangedHandler(Building building, TeamType oldTeam, TeamType newTeam);
    public static event BuildingTeamChangedHandler OnBuildingTeamChangedGlobal; // Événement global

    // Protected accessor for use in derived classes.
    protected BuildingStats Stats => buildingStats;

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

    // Method to handle incoming damage
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

    public virtual void SetTeam(TeamType newTeam)
    {
        if (_team != newTeam)
        {
            TeamType oldTeam = _team; // Stocker l'ancienne équipe
            _team = newTeam;
            OnTeamChanged(newTeam); // Appel à la méthode virtuelle pour la logique spécifique à la classe dérivée

            // Déclencher l'événement global de changement d'équipe
            OnBuildingTeamChangedGlobal?.Invoke(this, oldTeam, newTeam);
            if(debugBuildingCombat) Debug.Log($"[{gameObject.name}] Global Team Change Event Invoked: {oldTeam} -> {newTeam}");
        }
    }

    // Virtual method for team change events
    protected virtual void OnTeamChanged(TeamType newTeam)
    {
        // Override in derived classes if you need special behavior
        if (debugBuildingCombat)
        {
            Debug.Log($"[BUILDING] {gameObject.name} changed to team: {newTeam}");
        }
    }

    // Method for destruction logic
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

    public virtual void OnDestroy()
    {
        if (occupiedTile != null)
        {
            occupiedTile.RemoveBuilding();
        }
    }

    // Helper method to get the tile this building occupies
    public Tile GetOccupiedTile()
    {
        return occupiedTile;
    }

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

// Method to heal the building
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

    // ITargetable implementation
    public Transform TargetPoint => transform;
    public GameObject GameObject => gameObject;
}
