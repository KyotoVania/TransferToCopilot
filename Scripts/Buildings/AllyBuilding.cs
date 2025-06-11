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

    private GameObject healthBarInstance;
    private Slider healthBarSlider;
    private TextMeshProUGUI healthText;

    [Header("Effects")]
    [SerializeField] private GameObject damageVFXPrefab;
    [SerializeField] private GameObject repairVFXPrefab;
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip repairSound;

    private AudioSource audioSource;

    [Header("Reserve System")]
    [Tooltip("Tiles linked to this building where units can be positioned in defensive mode")]
    [SerializeField] private List<Tile> reserveTiles = new List<Tile>();
    
    [Tooltip("Visual indicator for reserve tiles in scene view")]
    [SerializeField] private bool showReserveTilesGizmos = true;
    
    [SerializeField] private Color reserveTileGizmoColor = Color.cyan;

    private Dictionary<Tile, Unit> occupiedReserveTiles = new Dictionary<Tile, Unit>();

    private void OnEnable()
    {
        // --- MODIFICATION : Utilisation de MusicManager ---
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += HandleBeat;
        }
    }

    private void OnDisable()
    {
        // --- MODIFICATION : Utilisation de MusicManager ---
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleBeat;
        }
    }

    protected override IEnumerator Start()
    {
        SetTeam(TeamType.Player);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = 20f;

        if (showHealthBar && healthBarPrefab != null)
        {
            CreateHealthBar();
        }

        Building.OnBuildingDamaged += OnAnyBuildingDamaged;
        yield return StartCoroutine(base.Start());
        InitializeReserveTiles();
        Debug.Log($"[ALLY BUILDING] {gameObject.name} initialized as {Team} team with {reserveTiles.Count} reserve tiles!");
    }

    // --- Le reste du code pour le système de réserves reste inchangé ---
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

    public Tile GetAvailableReserveTileForUnit(Unit unitSeekingReserve)
    {
        if (unitSeekingReserve == null) return null;
        foreach (var kvp in occupiedReserveTiles)
        {
            if (kvp.Value == unitSeekingReserve && kvp.Key != null && reserveTiles.Contains(kvp.Key))
            {
                return kvp.Key;
            }
        }
        foreach (Tile tile in reserveTiles)
        {
            if (tile != null && IsReserveTileAvailable(tile))
            {
                return tile;
            }
        }
        return null;
    }

    public bool IsReserveTileAvailable(Tile tile)
    {
        if (tile == null || !reserveTiles.Contains(tile))
            return false;

        if (tile.currentUnit != null && (!occupiedReserveTiles.ContainsKey(tile) || occupiedReserveTiles[tile] != tile.currentUnit) )
        {
            return false;
        }
        if (tile.currentBuilding != null && tile.currentBuilding != this)
        {
            return false;
        }
        if (tile.currentEnvironment != null && tile.currentEnvironment.IsBlocking)
        {
            return false;
        }
        
        return occupiedReserveTiles.ContainsKey(tile) && occupiedReserveTiles[tile] == null;
    }
    
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
                occupiedReserveTiles[newReserveTile] = unit;
                Debug.Log($"[PlayerBuilding:{name}] Unit {unit.name} assignée à la case de réserve ({newReserveTile.column},{newReserveTile.row}).", this);
                return true;
            }
            else
            {
                Debug.LogWarning($"[PlayerBuilding:{name}] AssignUnitToReserveTile: La nouvelle case {newReserveTile.name} est déjà occupée par {occupiedReserveTiles[newReserveTile].name}. Impossible d'assigner {unit.name}.", this);
                return false;
            }
        }
        else
        {
            Debug.LogError($"[PlayerBuilding:{name}] AssignUnitToReserveTile: La case {newReserveTile.name} est dans reserveTiles mais pas dans occupiedReserveTiles. Problème d'initialisation probable.", this);
            return false;
        }
    }
    
    public void ReleaseReserveTile(Tile reserveTile, Unit unit)
    {
        if (unit == null || reserveTile == null) return;

        if (occupiedReserveTiles.ContainsKey(reserveTile) && occupiedReserveTiles[reserveTile] == unit)
        {
            occupiedReserveTiles[reserveTile] = null;
            Debug.Log($"[PlayerBuilding:{name}] Case de réserve ({reserveTile.column},{reserveTile.row}) explicitement libérée par/pour {unit.name}.", this);
        }
    }
    
    public bool HasAvailableReserveTiles()
    {
        foreach (Tile tile in reserveTiles)
        {
            if (tile != null && IsReserveTileAvailable(tile))
            {
                return true;
            }
        }
        return false;
    }

    public List<Tile> GetReserveTiles()
    {
        return new List<Tile>(reserveTiles);
    }
    #endregion
    
    // --- MODIFICATION : Signature de la méthode mise à jour ---
    private void HandleBeat(float beatDuration)
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

    // --- Le reste du script reste inchangé ---
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
        healthBarInstance = Instantiate(healthBarPrefab, transform);
        healthBarInstance.transform.localPosition = Vector3.up * healthBarOffset;
        healthBarInstance.transform.rotation = Quaternion.Euler(30, 0, 0);
        healthBarSlider = healthBarInstance.GetComponentInChildren<Slider>();
        healthText = healthBarInstance.GetComponentInChildren<TextMeshProUGUI>();
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

    private void OnAnyBuildingDamaged(Building building, int newHealth, int damage)
    {
        if (building != this) return;
        UpdateHealthBar();
        PlayDamageEffects(damage);
    }

    private void PlayDamageEffects(int damage)
    {
        if (damageSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(damageSound);
        }

        if (damageVFXPrefab != null)
        {
            GameObject vfx = Instantiate(damageVFXPrefab, transform.position + Vector3.up, Quaternion.identity);
            Destroy(vfx, 2.0f);
        }
    }

    public void Repair(int amount)
    {
        if (CurrentHealth >= MaxHealth) return;
        int newHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
        int actualRepair = newHealth - CurrentHealth;
        if (actualRepair <= 0) return;
        SetCurrentHealth(newHealth);
        if (debugBuildingCombat)
        {
            Debug.Log($"[ALLY BUILDING] {gameObject.name} repaired for {actualRepair}. Health: {CurrentHealth}/{MaxHealth}");
        }
        PlayRepairEffects(actualRepair);
        UpdateHealthBar();
    }

    private void PlayRepairEffects(int amount)
    {
        if (repairSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(repairSound);
        }
        if (repairVFXPrefab != null)
        {
            GameObject vfx = Instantiate(repairVFXPrefab, transform.position + Vector3.up, Quaternion.identity);
            Destroy(vfx, 2.0f);
        }
    }

    protected override void OnTeamChanged(TeamType newTeam)
    {
        base.OnTeamChanged(newTeam);
        if (newTeam == TeamType.Player)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.materials)
                {
                    Color originalColor = material.color;
                    Color tintedColor = Color.Lerp(originalColor, Color.blue, 0.3f);
                    material.color = tintedColor;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showReserveTilesGizmos || reserveTiles == null) return;
        Gizmos.color = reserveTileGizmoColor;
        foreach (Tile tile in reserveTiles)
        {
            if (tile != null)
            {
                Gizmos.DrawWireSphere(tile.transform.position + Vector3.up * 0.5f, 0.3f);
                Gizmos.DrawLine(transform.position + Vector3.up, tile.transform.position + Vector3.up * 0.5f);
            }
        }
    }

    public override void OnDestroy()
    {
        Building.OnBuildingDamaged -= OnAnyBuildingDamaged;
        // La désinscription de OnBeat est déjà gérée dans OnDisable
        base.OnDestroy();
    }
    
    public override void TakeDamage(int damage, Unit attacker = null)
    {
        base.TakeDamage(damage, attacker);
        if (attacker != null)
        {
            AlertReserveUnitsOfAttack(attacker);
        }
    }
    
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