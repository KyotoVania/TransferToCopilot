using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using System.Linq;
using ScriptableObjects;
using System;
using Random = UnityEngine.Random;

/// <summary>
/// Enumeration representing the different states a unit can be in.
/// </summary>
public enum UnitState
{
    /// <summary>Unit is idle and waiting for commands.</summary>
    Idle,
    /// <summary>Unit is currently moving to a target location.</summary>
    Moving,
    /// <summary>Unit is currently attacking a target.</summary>
    Attacking,
    /// <summary>Unit is currently capturing a building.</summary>
    Capturing,
}

/// <summary>
/// Abstract base class for all units in the game.
/// Provides core functionality for movement, combat, state management, and rhythm-based gameplay.
/// Implements tile reservation and targeting systems.
/// </summary>
public abstract class Unit : MonoBehaviour, ITileReservationObserver, ITargetable
{
    // --- FEATURE DU FICHIER 1 : Événement de destruction ---
    /// <summary>
    /// Event triggered just before the unit is destroyed.
    /// Banners or other systems can subscribe to this event.
    /// </summary>
    public event Action OnUnitDestroyed;

    /// <summary>
    /// Gets the target position for this unit's movement. Must be implemented by derived classes.
    /// </summary>
    protected abstract Vector2Int? TargetPosition { get; }

    /// <summary>
    /// Structure containing information about the last damage event.
    /// </summary>
    public struct LastDamageEvent
    {
        /// <summary>The unit that attacked this unit.</summary>
        public Unit Attacker;
        /// <summary>The time when the attack occurred.</summary>
        public float Time;
    }

    /// <summary>
    /// Information about the last unit that attacked this unit.
    /// </summary>
    public LastDamageEvent? LastAttackerInfo { get; private set; } = null;

    [Header("State & Core Mechanics")]
    /// <summary>
    /// Whether the unit is currently moving.
    /// </summary>
    public bool IsMoving;
    
    /// <summary>
    /// The tile this unit is currently occupying.
    /// </summary>
    protected Tile occupiedTile;
    
    /// <summary>
    /// Y offset for positioning the unit above the tile.
    /// </summary>
    [SerializeField] protected float yOffset = 0f;
    
    /// <summary>
    /// Whether the unit is attached to a tile and ready for gameplay.
    /// </summary>
    public bool isAttached = false;
    
    /// <summary>
    /// The current level of this unit.
    /// </summary>
    public int Level;

    /// <summary>
    /// The tile this unit has reserved for movement.
    /// </summary>
    private Tile _reservedTile;

    [Header("Debugging")]
    /// <summary>
    /// Enable debug logging for unit movement.
    /// </summary>
    [SerializeField] private bool debugUnitMovement = true;
    
    /// <summary>
    /// Enable debug logging for unit combat.
    /// </summary>
    [SerializeField] private bool debugUnitCombat = false;

    [Header("Stats & Systems")]
    /// <summary>
    /// The current runtime stats for this unit.
    /// </summary>
    public RuntimeStats CurrentStats { get; private set; }
    
    /// <summary>
    /// The base stats before any modifications.
    /// </summary>
    private RuntimeStats baseStats;
    
    /// <summary>
    /// The fever mode buffs configuration for this unit.
    /// </summary>
    private FeverBuffs _feverBuffs;
    
    /// <summary>
    /// Gets the active fever buffs for this unit.
    /// </summary>
    public FeverBuffs ActiveFeverBuffs => _feverBuffs;
    
    /// <summary>
    /// Whether this unit can receive fever buffs.
    /// </summary>
    private bool _canReceiveFeverBuffs = false;
    
    /// <summary>
    /// Whether fever mode is currently active for this unit.
    /// </summary>
    public bool IsFeverActive { get; private set; } = false;

    // --- FEATURE DU FICHIER 2 : VFX pour le mode Fever ---
    [Header("Fever Aura VFX")]
    /// <summary>
    /// Prefab for the fever aura to instantiate under the unit when fever mode is active.
    /// </summary>
    [Tooltip("Prefab de l'aura Fever à instancier sous l'unité quand le mode Fever est actif.")]
    [SerializeField] private GameObject feverAuraPrefab;
    
    /// <summary>
    /// The currently active fever aura instance.
    /// </summary>
    private GameObject _activeFeverAuraInstance;

    /// <summary>
    /// The character stat sheets containing base statistics.
    /// </summary>
    public StatSheet_SO CharacterStatSheets;
    
    /// <summary>
    /// The current health of this unit.
    /// </summary>
    public int Health { get; protected set; }

    /// <summary>
    /// Gets the attack power of this unit.
    /// </summary>
    public virtual int Attack => CurrentStats != null ? CurrentStats.Attack : 0;
    
    /// <summary>
    /// Gets the defense value of this unit.
    /// </summary>
    public virtual int Defense => CurrentStats != null ? CurrentStats.Defense : 0;
    
    /// <summary>
    /// Gets the attack range of this unit in tiles.
    /// </summary>
    public virtual int AttackRange => CurrentStats != null ? CurrentStats.AttackRange : 0;
    
    /// <summary>
    /// Gets the number of beats this unit must wait between attacks.
    /// </summary>
    public virtual int AttackDelay => CurrentStats != null ? CurrentStats.AttackDelay : 1;
    
    /// <summary>
    /// Gets the number of beats this unit must wait between movements.
    /// </summary>
    public virtual int MovementDelay => CurrentStats != null ? CurrentStats.MovementDelay : 1;
    
    /// <summary>
    /// Gets the detection range of this unit in tiles.
    /// </summary>
    public virtual int DetectionRange => CurrentStats != null ? CurrentStats.DetectionRange : 0;


    /// <summary>
    /// The movement system component for this unit.
    /// </summary>
    [SerializeField] private MonoBehaviour movementSystemComponent;
    
    /// <summary>
    /// The attack system component for this unit.
    /// </summary>
    [SerializeField] private MonoBehaviour attackSystemComponent;

    /// <summary>
    /// The movement system interface for this unit.
    /// </summary>
    public IMovement MovementSystem { get; private set; }
    
    /// <summary>
    /// The attack system interface for this unit.
    /// </summary>
    public IAttack AttackSystem { get; private set; }
    /// <summary>
    /// Represents an active buff applied to the unit.
    /// </summary>
    protected class ActiveBuff
    {
        /// <summary>The stat that is being buffed.</summary>
        public StatToBuff Stat;
        /// <summary>The multiplier applied to the stat.</summary>
        public float Multiplier;
        /// <summary>The remaining duration of the buff in seconds.</summary>
        public float DurationRemaining;
        /// <summary>The coroutine handling the buff expiry.</summary>
        public Coroutine ExpiryCoroutine;
    }
    
    /// <summary>
    /// Whether the unit is currently in spawning state.
    /// </summary>
    public bool IsSpawning { get; private set; } = true;
    
    /// <summary>
    /// Sets the spawning state of the unit.
    /// </summary>
    /// <param name="isCurrentlySpawning">Whether the unit is currently spawning.</param>
    public void SetSpawningState(bool isCurrentlySpawning)
    {
        IsSpawning = isCurrentlySpawning;
    }


    /// <summary>
    /// Counter for tracking beats for movement timing.
    /// </summary>
    protected int _beatCounter = 0;
    
    /// <summary>
    /// Counter for tracking beats for attack timing.
    /// </summary>
    private int _attackBeatCounter = 0;
    
    /// <summary>
    /// Counter for tracking how many times the unit has been stuck.
    /// </summary>
    private int _stuckCount = 0;
    
    /// <summary>
    /// Whether the unit is currently performing an attack.
    /// </summary>
    protected bool _isAttacking = false;

    /// <summary>
    /// Delegate for unit attacked events.
    /// </summary>
    /// <param name="attacker">The attacking unit.</param>
    /// <param name="target">The target unit.</param>
    /// <param name="damage">The damage dealt.</param>
    public delegate void UnitAttackedHandler(Unit attacker, Unit target, int damage);
    
    /// <summary>
    /// Event triggered when a unit attacks another unit.
    /// </summary>
    public static event UnitAttackedHandler OnUnitAttacked;
    
    /// <summary>
    /// Delegate for unit attacked building events.
    /// </summary>
    /// <param name="attacker">The attacking unit.</param>
    /// <param name="target">The target building.</param>
    /// <param name="damage">The damage dealt.</param>
    public delegate void UnitAttackedBuildingHandler(Unit attacker, Building target, int damage);
    
    /// <summary>
    /// Event triggered when a unit attacks a building.
    /// </summary>
    public static event UnitAttackedBuildingHandler OnUnitAttackedBuilding;

    /// <summary>
    /// VFX to play during the 'cheer and despawn' animation.
    /// </summary>
    [Tooltip("VFX à jouer lors de l'animation de 'cheer and despawn'.")]
    [SerializeField] private GameObject cheerAndDespawnVFX; 

    /// <summary>
    /// Event triggered when a unit is killed by another unit.
    /// </summary>
    /// <param name="attacker">The attacking unit.</param>
    /// <param name="victim">The unit that was killed.</param>
    public static event Action<Unit, Unit> OnUnitKilled;

[Header("Animation")]
    /// <summary>
    /// The animator component for this unit.
    /// </summary>
    [SerializeField] protected Animator animator;
    
    /// <summary>
    /// Whether to use animations for this unit.
    /// </summary>
    [SerializeField] protected bool useAnimations = true;

    /// <summary>
    /// Animator parameter ID for idle state.
    /// </summary>
    public readonly int IdleParamId = Animator.StringToHash("IsIdle");
    
    /// <summary>
    /// Animator parameter ID for moving state.
    /// </summary>
    public readonly int MovingParamId = Animator.StringToHash("IsMoving");
    
    /// <summary>
    /// Animator trigger ID for attack animation.
    /// </summary>
    public readonly int AttackTriggerId = Animator.StringToHash("Attack");
    
    /// <summary>
    /// Animator trigger ID for capture animation.
    /// </summary>
    public readonly int CaptureTriggerId = Animator.StringToHash("Capture");
    
    /// <summary>
    /// Animator trigger ID for death animation.
    /// </summary>
    public readonly int DieTriggerId = Animator.StringToHash("Die");
    
    /// <summary>
    /// Animator trigger ID for cheer animation.
    /// </summary>
    public readonly int CheerTriggerId = Animator.StringToHash("Cheer");

    /// <summary>
    /// The current state of this unit.
    /// </summary>
    [SerializeField] protected UnitState currentState = UnitState.Idle;

    /// <summary>
    /// The unit this unit is currently targeting.
    /// </summary>
    public Unit targetUnit = null;
    
    /// <summary>
    /// The building this unit is currently targeting.
    /// </summary>
    public Building targetBuilding = null;
    
    /// <summary>
    /// Whether the unit is currently interacting with a building.
    /// </summary>
    protected bool isInteractingWithBuilding = false;
    
    /// <summary>
    /// The neutral building currently being captured by this unit.
    /// </summary>
    protected NeutralBuilding buildingBeingCaptured = null;
    
    /// <summary>
    /// Number of beats spent capturing the current building.
    /// </summary>
    protected int beatsSpentCapturing = 0;

    /// <summary>
    /// List of active buffs applied to this unit.
    /// </summary>
    protected List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    /// <summary>
    /// Gets the tile this unit is currently occupying.
    /// </summary>
    /// <returns>The occupied tile, or null if not on any tile.</returns>
    public Tile GetOccupiedTile() => occupiedTile;

    // --- FEATURE DU FICHIER 1 : Gestion des tuiles multiples ---
    /// <summary>
    /// Gets all tiles occupied by this unit. For base units, this is just the main tile.
    /// Override in derived classes for units that occupy multiple tiles.
    /// </summary>
    /// <returns>List of tiles occupied by this unit.</returns>
    public virtual List<Tile> GetOccupiedTiles()
    {
        // For a base unit, return a list containing only its main tile.
        if (occupiedTile != null)
        {
            return new List<Tile> { occupiedTile };
        }
        return new List<Tile>(); // Return empty list if unit is not on any tile.
    }

    /// <summary>
    /// Gets all tiles within the attack range of this unit.
    /// </summary>
    /// <returns>List of tiles within attack range.</returns>
    protected List<Tile> GetTilesInAttackRange()
    {
        if (occupiedTile == null || HexGridManager.Instance == null) return new List<Tile>();
        return HexGridManager.Instance.GetTilesWithinRange(occupiedTile.column, occupiedTile.row, Mathf.CeilToInt(AttackRange));
    }

    /// <summary>
    /// Initializes the unit with systems, stats, and fever mode integration.
    /// </summary>
    /// <returns>Coroutine for initialization process.</returns>
    protected virtual IEnumerator Start()
    {
        if (animator == null) { animator = GetComponent<Animator>(); }
        if (useAnimations && animator == null)
        {
             Debug.LogWarning($"[{name}] Animator not found/assigned, but useAnimations is true. Animations disabled.");
             useAnimations = false;
        }

        if (movementSystemComponent == null) { Debug.LogError("No movement system assigned!", this); yield break; }
        MovementSystem = movementSystemComponent as IMovement;
        if (MovementSystem == null) { Debug.LogError($"Movement component {movementSystemComponent.name} doesn't implement IMovement!", this); yield break; }

        if (attackSystemComponent != null)
        {
            AttackSystem = attackSystemComponent as IAttack;
            if (AttackSystem == null) { Debug.LogError($"Attack component {attackSystemComponent.name} doesn't implement IAttack!", this); }
        }
        else if (debugUnitCombat) Debug.Log($"[{name}] No attack system assigned. This unit cannot attack.");

        SetState(UnitState.Idle);

        if (FeverManager.Instance != null)
        {
            // If fever mode is already at maximum level when unit spawns,
            // apply effects immediately
            if (FeverManager.Instance.CurrentFeverLevel > 0 && FeverManager.Instance.CurrentFeverLevel == FeverManager.Instance.MaxFeverLevel)
            {
                ApplyFeverEffects();
            }

            // Subscribe to level changes for future tiers or end of fever mode
            FeverManager.Instance.OnFeverLevelChanged += HandleFeverLevelChanged;
        }

        yield return StartCoroutine(AttachToNearestTile());

        if (TileReservationController.Instance != null)
        {
            TileReservationController.Instance.AddObserver(this);
        }
        else if(debugUnitMovement)
        {
            Debug.LogWarning($"[{name}] TileReservationController not found. Tile reservation features might not work as expected.");
        }
    }
    /// <summary>
    /// Virtual Awake method that allows derived classes to override.
    /// </summary>
    protected virtual void Awake()
    {
        // Virtual Awake method to allow derived classes to override
    }
    /// <summary>
    /// Subscribes to rhythm beat events when the unit is enabled.
    /// </summary>
    protected virtual void OnEnable()
    {
       if (isAttached && MusicManager.Instance != null)
       {
            MusicManager.Instance.OnBeat -= OnRhythmBeatInternal;
            MusicManager.Instance.OnBeat += OnRhythmBeatInternal;
       }
    }

    /// <summary>
    /// Unsubscribes from rhythm beat events and resets state when disabled.
    /// </summary>
    protected virtual void OnDisable()
    {
       if (MusicManager.Instance != null)
       {
            if (isAttached)
                MusicManager.Instance.OnBeat -= OnRhythmBeatInternal;
       }
        IsMoving = false;
        _isAttacking = false;
        if (currentState == UnitState.Capturing) StopCapturing();
    }

    /// <summary>
    /// Gets the type of this unit from its character stat sheets.
    /// </summary>
    /// <returns>The unit type, or Null if no stat sheets are assigned.</returns>
    public UnitType GetUnitType()
    {
        return CharacterStatSheets?.Type ?? UnitType.Null;
    }
#if UNITY_EDITOR
    [Button("Log Current Stats", ButtonSizes.Medium), GUIColor(0.4f, 0.8f, 1f)]
    [PropertyOrder(-10)]
    [ShowIf("@UnityEngine.Application.isPlaying")] // Version robuste de la condition
    private void LogCurrentStatsForInspector()
    {
        if (this.CurrentStats == null)
        {
            Debug.Log($"<color=orange>[{name}] CurrentStats est null. Les stats n'ont pas encore été calculées ou assignées.</color>");
            return;
        }
        Debug.Log($"--- Stats Log for <color=cyan>{name}</color> (Level {this.Level}) HP: {this.Health} at frame {Time.frameCount} ---");
        this.CurrentStats.LogStats();
    }
#endif

    // --- FEVER MODE LOGIC ---
    /// <summary>
    /// Handles fever level changes and applies or removes fever effects.
    /// </summary>
    /// <param name="newFeverLevel">The new fever level.</param>
    private void HandleFeverLevelChanged(int newFeverLevel)
    {
        // If fever mode activates (any level > 0) AND this unit doesn't have buffs yet
        if (newFeverLevel > 0 && !IsFeverActive)
        {
            ApplyFeverEffects();
        }
        // Si le mode Fever se désactive (niveau 0) ET que cette unité avait ses buffs
        else if (newFeverLevel == 0 && IsFeverActive)
        {
            RemoveFeverEffects();
        }
    }

    /// <summary>
    /// Applies fever mode effects to the unit including stat buffs and VFX.
    /// </summary>
    private void ApplyFeverEffects()
    {
        if (!_canReceiveFeverBuffs || baseStats == null)
        {
            if (debugUnitCombat) Debug.LogWarning($"[{name}] Cannot apply Fever effects: unit is not eligible or baseStats are null.");
            return;
        }

        IsFeverActive = true;
        if(debugUnitCombat) Debug.Log($"[{name}] Applying Fever effects.");

        // Apply stat buffs
        CurrentStats.AttackDelay = Mathf.Max(1, (int)(baseStats.AttackDelay / _feverBuffs.AttackSpeedMultiplier));
        CurrentStats.Defense = (int)(baseStats.Defense * _feverBuffs.DefenseMultiplier);

        // Activate VFX
        if (_activeFeverAuraInstance == null && feverAuraPrefab != null)
        {
            _activeFeverAuraInstance = Instantiate(feverAuraPrefab, transform);
            _activeFeverAuraInstance.transform.localPosition = Vector3.zero;
        }
    }

    /// <summary>
    /// Removes fever mode effects from the unit and restores base stats.
    /// </summary>
    private void RemoveFeverEffects()
    {
        if (baseStats == null) return; // Safety check

        IsFeverActive = false;
        if(debugUnitCombat) Debug.Log($"[{name}] Removing Fever effects.");

        // Restore base stats
        CurrentStats.AttackDelay = baseStats.AttackDelay;
        CurrentStats.Defense = baseStats.Defense;

        // Destroy VFX
        if (_activeFeverAuraInstance != null)
        {
            Destroy(_activeFeverAuraInstance);
            _activeFeverAuraInstance = null;
        }
    }

    /// <summary>
    /// Coroutine that attaches the unit to the nearest available tile.
    /// </summary>
    /// <returns>Coroutine for tile attachment process.</returns>
    private IEnumerator AttachToNearestTile()
    {
        while (!isAttached)
        {
            if (HexGridManager.Instance != null)
            {
                Tile nearestTile = HexGridManager.Instance.GetClosestTile(transform.position);
                if (nearestTile != null && !nearestTile.IsOccupied)
                {
                    bool canAttach = false;
                    Vector2Int nearestTilePos = new Vector2Int(nearestTile.column, nearestTile.row);
                    if (TileReservationController.Instance != null)
                    {
                        if (!TileReservationController.Instance.IsTileReservedByOtherUnit(nearestTilePos, this))
                        {
                           if (TileReservationController.Instance.TryReserveTile(nearestTilePos, this))
                           {
                               canAttach = true;
                           } else {
                                if (debugUnitMovement) Debug.LogWarning($"[{name}] Could not reserve initial tile {nearestTilePos} even if not reserved by other.");
                           }
                        } else {
                             if (debugUnitMovement) Debug.LogWarning($"[{name}] Initial tile {nearestTilePos} is reserved by another unit.");
                        }
                    }
                    else
                    {
                        canAttach = true;
                    }

                    if (canAttach)
                    {
                        AttachToTile(nearestTile);
                        isAttached = true;

                        if (MusicManager.Instance != null)
                        {
                            MusicManager.Instance.OnBeat += OnRhythmBeatInternal;
                        }

                        if (debugUnitMovement) Debug.Log($"[{name}] Attached to tile ({nearestTile.column}, {nearestTile.row}) and subscribed to OnRhythmBeat.");
                        yield break;
                    }
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    /// <summary>
    /// Handles capture logic on each rhythm beat when in capturing state.
    /// </summary>
    public virtual void OnCaptureBeat()
    {
        if (currentState != UnitState.Capturing) return;

        beatsSpentCapturing++;
        if (useAnimations && animator != null)
        {
            animator.SetTrigger(CaptureTriggerId);
        }
    }

    /// <summary>
    /// Stops the current capturing process and resets to idle state.
    /// </summary>
    public virtual void StopCapturing()
    {
        if (currentState == UnitState.Capturing)
        {
            if (buildingBeingCaptured != null)
            {
                buildingBeingCaptured.StopCapturing(this);
                buildingBeingCaptured = null;
            }
            beatsSpentCapturing = 0;
            SetState(UnitState.Idle);
        }
    }

    /// <summary>
    /// Internal wrapper for rhythm beat events that calls the virtual OnRhythmBeat method.
    /// </summary>
    /// <param name="beatDuration">Duration of the current beat.</param>
    private void OnRhythmBeatInternal(float beatDuration)
    {
        OnRhythmBeat(beatDuration);
    }

    /// <summary>
    /// Handles rhythm beat events for this unit, managing movement, attack, and capture logic.
    /// </summary>
    /// <param name="beatDuration">Duration of the current beat.</param>
    protected virtual void OnRhythmBeat(float beatDuration)
    {
        HandleMovementOnBeat();
        if (!IsMoving)
        {
            //HandleAttackOnBeat();
        }
        else
        {
            _attackBeatCounter = 0;
        }
        if (currentState == UnitState.Capturing) { HandleCaptureOnBeat(); }
    }

    #region Movement and Combat Methods
    /// <summary>
    /// Handles movement logic on rhythm beats, including pathfinding and stuck detection.
    /// </summary>
    protected virtual void HandleMovementOnBeat()
    {
        if (IsMoving || currentState == UnitState.Capturing) return;
        if (!TargetPosition.HasValue) { SetState(UnitState.Idle); return; }
        if (IsAtTargetLocation()) { SetState(UnitState.Idle); return; }
        if (IsSpawning)
        {
            if (debugUnitMovement) Debug.Log($"[{name}] HandleMovementOnBeat: Skipped due to IsSpawning = true.");
            return;
        }
        _beatCounter++;
        if (_beatCounter < MovementDelay) return;
        _beatCounter = 0;

        Tile nextTile = GetNextTileTowardsDestination();
        if (nextTile != null)
        {
            _stuckCount = 0;
            StartCoroutine(MoveToTile(nextTile));
        }
        else
        {
            _stuckCount++;
            if (debugUnitMovement) Debug.LogWarning($"[{name}] Cannot find next tile towards {TargetPosition.Value}. Stuck count: {_stuckCount}");
            if (_stuckCount > 3)
            {
                Tile forcedNextTile = ForceGetAnyAvailableNeighbor();
                if (forcedNextTile != null)
                {
                    if (debugUnitMovement) Debug.Log($"[{name}] Forcing move to random neighbor: ({forcedNextTile.column},{forcedNextTile.row})");
                    StartCoroutine(MoveToTile(forcedNextTile));
                    _stuckCount = 0;
                }
                else
                {
                    if (debugUnitMovement) Debug.LogWarning($"[{name}] Stuck and no available neighbor to move to.");
                    SetState(UnitState.Idle);
                }
            } else {
                 SetState(UnitState.Idle);
            }
        }
    }

    /// <summary>
    /// Handles attack logic on rhythm beats, finding targets and initiating attacks.
    /// </summary>
    protected virtual void HandleAttackOnBeat()
    {
        if (_isAttacking || currentState == UnitState.Capturing || AttackSystem == null) return;

        _attackBeatCounter++;
        if (_attackBeatCounter >= AttackDelay)
        {
            _attackBeatCounter = 0;
            Debug.Log($"[{name}] HandleAttackOnBeat: Attempting to attack on beat {_attackBeatCounter}. :  AttackDelay: {AttackDelay}");
            Unit potentialUnitTarget = FindAttackableUnitTarget();
            Building potentialBuildingTarget = (potentialUnitTarget == null) ? FindAttackableBuildingTarget() : null;

            if (potentialUnitTarget != null)
            {
                targetUnit = potentialUnitTarget;
                targetBuilding = null;
                StartCoroutine(PerformAttackCoroutine(targetUnit));
            }
            else if (potentialBuildingTarget != null)
            {
                targetBuilding = potentialBuildingTarget;
                targetUnit = null;
                StartCoroutine(PerformAttackBuildingCoroutine(targetBuilding));
            }
             else
            {
                 if (currentState == UnitState.Attacking) SetState(UnitState.Idle);
            }
        }
    }

    /// <summary>
    /// Handles building capture logic on rhythm beats.
    /// </summary>
    protected virtual void HandleCaptureOnBeat()
    {
         if (buildingBeingCaptured == null || currentState != UnitState.Capturing) return;
         if (useAnimations && animator != null)
         {
             animator.SetTrigger(CaptureTriggerId);
         }
    }

    /// <summary>
    /// Coroutine that moves the unit to a target tile with reservation management.
    /// </summary>
    /// <param name="targetTile">The tile to move to.</param>
    /// <returns>Coroutine for movement process.</returns>
    public IEnumerator MoveToTile(Tile targetTile)
    {
        string context = $"[{name}/{GetInstanceID()}]";

        if (MovementSystem == null || targetTile == null)
        {
            if (debugUnitMovement) Debug.LogWarning($"{context} MoveToTile ABORT: Preconditions not met. MS: {MovementSystem != null}, target: {targetTile?.name ?? "NULL"}", this);
            IsMoving = false; SetState(UnitState.Idle);
            yield break;
        }
        if (occupiedTile == targetTile)
        {
            if (debugUnitMovement) Debug.LogWarning($"{context} MoveToTile ABORT: Target tile {targetTile.name} is current tile.", this);
            IsMoving = false; SetState(UnitState.Idle);
            yield break;
        }
        if (debugUnitMovement) Debug.Log($"{context} MoveToTile START for target: {targetTile.name}. Current _reservedTile: {(_reservedTile?.name ?? "null")}", this);

        Vector2Int targetTilePos = new Vector2Int(targetTile.column, targetTile.row);
        bool reservationSuccess = false;

        if (TileReservationController.Instance != null)
        {
            if (_reservedTile != null && _reservedTile != targetTile)
            {
                if (debugUnitMovement) Debug.Log($"{context} MoveToTile: _reservedTile ({_reservedTile.name}) is different from targetTile ({targetTile.name}). Releasing _reservedTile.", this);
                TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(_reservedTile.column, _reservedTile.row), this);
                _reservedTile = null;
            }
            reservationSuccess = TileReservationController.Instance.TryReserveTile(targetTilePos, this);
        }
        else
        {
            reservationSuccess = !targetTile.IsOccupied && targetTile.tileType == TileType.Ground;
        }

        if (!reservationSuccess)
        {
            if (debugUnitMovement) Debug.LogWarning($"{context} MoveToTile ABORT: Failed to reserve target tile {targetTile.name}.", this);
            IsMoving = false; SetState(UnitState.Idle);
            yield break;
        }
        _reservedTile = targetTile;
        if (debugUnitMovement) Debug.Log($"{context} MoveToTile: Successfully reserved targetTile {targetTile.name}. _reservedTile is now {targetTile.name}.", this);

        IsMoving = true;
        SetState(UnitState.Moving);
        Tile originalTile = occupiedTile;
        if (debugUnitMovement && originalTile != null) Debug.Log($"{context} MoveToTile: OriginalTile is {originalTile.name}.", this);

        try
        {
            if (debugUnitMovement) Debug.Log($"{context} MoveToTile: In try block. About to rotate.", this);
            yield return StartCoroutine(RotateToFaceTile(targetTile));

            if (originalTile != null)
            {
                if (TileReservationController.Instance != null)
                {
                    TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(originalTile.column, originalTile.row), this);
                    if (debugUnitMovement) Debug.Log($"{context} MoveToTile: Released reservation for originalTile {originalTile.name} via Controller.", this);
                }
                originalTile.RemoveUnit();
                if (debugUnitMovement) Debug.Log($"{context} MoveToTile: Called RemoveUnit on originalTile {originalTile.name}.", this);
            }
            transform.SetParent(null, true);

            Vector3 currentWorldPosition = transform.position;
            Vector3 targetWorldPosition = targetTile.transform.position + Vector3.up * yOffset;

            if (debugUnitMovement) Debug.Log($"{context} MoveToTile: Calling MovementSystem.MoveToTile from {currentWorldPosition} to {targetWorldPosition}.", this);

            yield return StartCoroutine(MovementSystem.MoveToTile(transform, currentWorldPosition, targetWorldPosition, 0.45f));

            if (debugUnitMovement) Debug.Log($"{context} MoveToTile: MovementSystem.MoveToTile FINISHED.", this);

            if (this == null || !this.gameObject.activeInHierarchy) {
                if (debugUnitMovement) Debug.LogWarning($"{context} MoveToTile: Unit became inactive/destroyed during MovementSystem.MoveToTile. Aborting Attach.", this);
                yield break;
            }
            if (targetTile == null || !targetTile.gameObject.activeInHierarchy) {
                 if (debugUnitMovement) Debug.LogWarning($"{context} MoveToTile: targetTile {targetTilePos} became inactive/destroyed. Aborting Attach.", this);
                 if (_reservedTile == targetTile && TileReservationController.Instance != null) {
                     TileReservationController.Instance.ReleaseTileReservation(targetTilePos, this);
                     _reservedTile = null;
                 }
                 yield break;
            }

            if (debugUnitMovement) Debug.Log($"{context} MoveToTile: About to call AttachToTile for {targetTile.name}.", this);
            AttachToTile(targetTile);
            if (debugUnitMovement) Debug.Log($"{context} MoveToTile: AttachToTile for {targetTile.name} COMPLETED. OccupiedTile: {(occupiedTile?.name ?? "null")}", this);
        }
        finally
        {
            IsMoving = false;
            if (debugUnitMovement) Debug.Log($"{context} MoveToTile: In FINALLY. IsMoving set to false. Occupied: {(occupiedTile?.name ?? "null")}, _reservedTile: {(_reservedTile?.name ?? "null")}, targetTile: {targetTile.name}", this);

            if (currentState == UnitState.Moving)
            {
                 SetState(UnitState.Idle);
            }

            if (occupiedTile != targetTile)
            {
                if (_reservedTile == targetTile && TileReservationController.Instance != null)
                {
                    TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(targetTile.column, targetTile.row), this);
                    if (debugUnitMovement) Debug.LogWarning($"{context} MoveToTile FINALLY: Movement FAILED/INTERRUPTED after reserving {targetTile.name} and before attaching. Reservation released.", this);
                    _reservedTile = null;
                }
            }
        }
    }

    /// <summary>
    /// Coroutine that performs an attack on a target unit.
    /// </summary>
    /// <param name="target">The unit to attack.</param>
    /// <returns>Coroutine for attack process.</returns>
    public IEnumerator PerformAttackCoroutine(Unit target)
    {
        if (AttackSystem == null || target == null || target.Health <= 0)
        {
            if (debugUnitCombat) Debug.LogWarning($"[{name}] PerformAttackCoroutine: Conditions not met (AttackSystem null, target null or dead). Target: {(target?.name ?? "NULL")}, Target HP: {target?.Health ?? -1}");
            _isAttacking = false;
            SetState(UnitState.Idle);
            yield break;
        }

        _isAttacking = true;
        SetState(UnitState.Attacking);
        FaceUnitTarget(target);

        if (useAnimations && animator != null)
        {
            if (debugUnitCombat) Debug.Log($"[{name} ({Time.frameCount})] PerformAttackCoroutine: Déclenchement de l'animation d'attaque (ID: {AttackTriggerId}) pour la cible {target.name}.", this);
            animator.SetTrigger(AttackTriggerId);
        }
        else
        {
            if (useAnimations && animator == null && debugUnitCombat) Debug.LogWarning($"[{name}] PerformAttackCoroutine: Animator non assigné mais useAnimations est true.", this);
        }

        int calculatedDamage = Mathf.Max(1, Attack - target.Defense);
        float attackerAnimationDuration = 0.5f;
        //TODO : check si on peut pas utiliser le temps de beat pour la durée de l'animation

        if (AttackSystem != null)
        {
             if (debugUnitCombat) Debug.Log($"[{name}] PerformAttackCoroutine: Appel de AttackSystem.PerformAttack sur {target.name} avec {calculatedDamage} dégâts potentiels et une durée d'animation de {attackerAnimationDuration}s.", this);
            yield return StartCoroutine(
                AttackSystem.PerformAttack(
                    transform,
                    target.transform,
                    calculatedDamage,
                    attackerAnimationDuration
                )
            );
        }
        if (target == null)
        {
            if (debugUnitCombat) Debug.Log($"[{name}] PerformAttackCoroutine: La cible a été détruite pendant l'animation. L'attaque est annulée.", this);
            _isAttacking = false;
            SetState(UnitState.Idle);
            yield break;
        }
        if (debugUnitCombat) Debug.Log($"[{name}] PerformAttackCoroutine: Action d'attaque (lancement/coup) terminée pour {target.name}. _isAttacking sera mis à false.", this);
        _isAttacking = false;
        SetState(UnitState.Idle);
    }

    public IEnumerator PerformAttackBuildingCoroutine(Building target)
    {
        if (AttackSystem == null || target == null || target.CurrentHealth <= 0)
        {
            if (debugUnitCombat) Debug.LogWarning($"[{name}] PerformAttackBuildingCoroutine: Conditions non remplies. Cible: {(target?.name ?? "NULL")}, Cible PV: {target?.CurrentHealth ?? -1}");
            _isAttacking = false;
            SetState(UnitState.Idle);
            yield break;
        }

        _isAttacking = true;
        SetState(UnitState.Attacking);
        FaceBuildingTarget(target);

        if (useAnimations && animator != null)
        {
            if (debugUnitCombat) Debug.Log($"[{name} ({Time.frameCount})] PerformAttackBuildingCoroutine: Déclenchement de l'animation d'attaque (ID: {AttackTriggerId}) pour la cible {target.name}.", this);
            animator.SetTrigger(AttackTriggerId);
        }
         else
        {
            if (useAnimations && animator == null && debugUnitCombat) Debug.LogWarning($"[{name}] PerformAttackBuildingCoroutine: Animator non assigné mais useAnimations est true.", this);
        }

        int attackDamage = Attack;
        float attackerAnimationDuration = 0.5f;

        if (AttackSystem != null)
        {
            if (debugUnitCombat) Debug.Log($"[{name}] PerformAttackBuildingCoroutine: Appel de AttackSystem.PerformAttack sur {target.name} avec {attackDamage} dégâts potentiels et une durée d'animation de {attackerAnimationDuration}s.", this);
            yield return StartCoroutine(
                AttackSystem.PerformAttack(
                    transform,
                    target.transform,
                    attackDamage,
                    attackerAnimationDuration
                )
            );
        }

        if (debugUnitCombat) Debug.Log($"[{name}] PerformAttackBuildingCoroutine: Action d'attaque (lancement/coup) terminée pour {target.name}. _isAttacking sera mis à false.", this);
        _isAttacking = false;
        SetState(UnitState.Idle);
    }

    /// <summary>
    /// Finds the closest attackable unit target within range.
    /// </summary>
    /// <returns>The closest valid unit target, or null if none found.</returns>
    protected virtual Unit FindAttackableUnitTarget()
    {
        if (occupiedTile == null || AttackSystem == null) return null;
        List<Tile> tilesInRange = GetTilesInAttackRange();
        Unit closestValidTarget = null;
        float minDistanceSq = float.MaxValue;

        foreach (Tile tile in tilesInRange)
        {
            if (tile.currentUnit != null && IsValidUnitTarget(tile.currentUnit))
            {
                if (AttackSystem.CanAttack(transform, tile.currentUnit.transform, AttackRange))
                {
                    float distSq = (tile.currentUnit.transform.position - transform.position).sqrMagnitude;
                    if (distSq < minDistanceSq)
                    {
                        minDistanceSq = distSq;
                        closestValidTarget = tile.currentUnit;
                    }
                }
            }
        }
        return closestValidTarget;
    }

    /// <summary>
    /// Finds the closest attackable building target within range.
    /// </summary>
    /// <returns>The closest valid building target, or null if none found.</returns>
    protected virtual Building FindAttackableBuildingTarget()
    {
        if (occupiedTile == null || AttackSystem == null) return null;
        List<Tile> tilesInRange = GetTilesInAttackRange();
        Building closestValidTarget = null;
        float minDistanceSq = float.MaxValue;

        foreach (Tile tile in tilesInRange)
        {
            if (tile.currentBuilding != null && IsValidBuildingTarget(tile.currentBuilding))
            {
                if (AttackSystem.CanAttack(transform, tile.currentBuilding.transform, AttackRange))
                {
                     float distSq = (tile.currentBuilding.transform.position - transform.position).sqrMagnitude;
                    if (distSq < minDistanceSq)
                    {
                        minDistanceSq = distSq;
                        closestValidTarget = tile.currentBuilding;
                    }
                }
            }
        }
        return closestValidTarget;
    }

    /// <summary>
    /// Handles damage taken by the unit, including defense calculations and death.
    /// </summary>
    /// <param name="damage">The amount of damage to take.</param>
    /// <param name="attacker">The unit that attacked this unit (optional).</param>
    public virtual void TakeDamage(int damage, Unit attacker = null)
    {
         if (debugUnitCombat) Debug.Log($"[{name}] TakeDamage called with {damage} damage from {attacker?.name ?? "unknown attacker"}.");
    if (damage <= 0) return;

    if (attacker != null)
    {
        OnUnitAttacked?.Invoke(attacker, this, damage);
        int actualDamage = Mathf.Max(1, damage - this.Defense);
        Health -= actualDamage;
        LastAttackerInfo = new LastDamageEvent { Attacker = attacker, Time = Time.time };
        if (debugUnitCombat)
        {
            Debug.Log($"[{name}] Dégâts finaux après défense ({this.Defense}): {actualDamage}. " +
                      $"PV restants: {Health}/{CurrentStats.MaxHealth}");
        }
    }
    else
    {
            int actualDamage = Mathf.Max(1, damage - this.Defense);
            Health -= actualDamage;
            if (debugUnitCombat) Debug.Log($"[{name}] a subi {actualDamage} dégâts environnementaux. PV restants: {Health}/{CurrentStats.MaxHealth}");
        }

        if (Health <= 0)
        {
          if (attacker != null)
            {
                OnUnitKilled?.Invoke(attacker, this);
            }
          Die();
        }
    }

    /// <summary>
    /// Handles unit death, including cleanup and destruction.
    /// </summary>
    protected virtual void Die()
    {
        if (debugUnitCombat) Debug.Log($"[{name}] Died.");

        // Déclenche l'événement pour notifier les observateurs (comme la bannière) avant toute autre chose
        OnUnitDestroyed?.Invoke();

        StopAllCoroutines();
        if (TileReservationController.Instance != null && occupiedTile != null)
        {
            TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(occupiedTile.column, occupiedTile.row), this);
        }
        if (occupiedTile != null) occupiedTile.RemoveUnit();

        if (useAnimations && animator != null)
        {
            SetState(UnitState.Idle);
            _isAttacking = false;
            Destroy(gameObject);
        }
        else Destroy(gameObject);
    }

    protected void AttachToTile(Tile tile)
    {
        if (tile == null) { Debug.LogError($"[{name}] AttachToTile: tile is null!"); return; }

        if (_reservedTile != null && _reservedTile != tile && TileReservationController.Instance != null) {
             TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(_reservedTile.column, _reservedTile.row), this);
             if (debugUnitMovement) Debug.Log($"[{name}] AttachToTile: Released PREVIOUS _reservedTile ({_reservedTile.column},{_reservedTile.row}).");
             _reservedTile = null;
        }

        Quaternion currentRotation = transform.rotation;
        occupiedTile = tile;
        transform.SetParent(tile.transform, true);
        transform.position = tile.transform.position + Vector3.up * yOffset;
        transform.rotation = currentRotation;
        tile.AssignUnit(this);

        if (TileReservationController.Instance != null) {
            bool reservedSuccessfully = TileReservationController.Instance.TryReserveTile(new Vector2Int(tile.column, tile.row), this);
            if (reservedSuccessfully)
            {
                _reservedTile = tile;
                if (debugUnitMovement) Debug.Log($"[{name}] AttachToTile: Successfully reserved and attached to tile ({tile.column}, {tile.row}). _reservedTile updated.");
            }
            else
            {
                 if (debugUnitMovement) Debug.LogError($"[{name}] AttachToTile: FAILED to reserve tile ({tile.column}, {tile.row}) even though attaching to it. This indicates a logic flaw!");
                _reservedTile = null;
            }
        } else {
             _reservedTile = tile;
        }

        if (debugUnitMovement && occupiedTile != null) Debug.Log($"[{name}] Attached to tile ({occupiedTile.column}, {occupiedTile.row}). Position: {transform.position}");
        else if (debugUnitMovement) Debug.LogWarning($"[{name}] Attached, but occupiedTile is unexpectedly null.");
    }

    /// <summary>
    /// Called when the unit completes a movement to a new tile.
    /// </summary>
    public virtual void OnMovementComplete()
    {
        if (debugUnitMovement) Debug.Log($"[{name}] Movement complete. Now at ({occupiedTile?.column ?? -1}, {occupiedTile?.row ?? -1}).");
        _attackBeatCounter = 0;
    }

    /// <summary>
    /// Gets the next tile towards the unit's destination, considering tile reservations.
    /// </summary>
    /// <returns>The next tile to move to, or null if no valid tile is available.</returns>
    protected Tile GetNextTileTowardsDestination()
    {
        if (!TargetPosition.HasValue || occupiedTile == null || HexGridManager.Instance == null) return null;

        Tile nextTile = HexGridManager.Instance.GetNextNeighborTowardsTarget(
            occupiedTile.column, occupiedTile.row, TargetPosition.Value.x, TargetPosition.Value.y, this
        );

        if (nextTile != null) {
             Vector2Int nextPos = new Vector2Int(nextTile.column, nextTile.row);
             if (TileReservationController.Instance != null &&
                 TileReservationController.Instance.IsTileReservedByOtherUnit(nextPos, this)) {
                 if (debugUnitMovement) Debug.LogWarning($"[{name}] Next preferred tile {nextPos} is reserved by another unit. Finding alternative.");
                 return FindAlternativeNeighbor(nextPos);
             }
             return nextTile;
        }
        return null;
    }

    /// <summary>
    /// Gets the next tile towards a specific destination for Behavior Graph usage.
    /// </summary>
    /// <param name="finalDestination">The target destination coordinates.</param>
    /// <returns>The next tile to move to, or null if no valid tile is available.</returns>
    public Tile GetNextTileTowardsDestinationForBG(Vector2Int finalDestination)
    {
        if (occupiedTile == null || HexGridManager.Instance == null)
        {
            if (debugUnitMovement) Debug.LogError($"[{name}] GetNextTileForBG: OccupiedTile or HexGridManager is null.");
            return null;
        }

        Tile nextTile = HexGridManager.Instance.GetNextNeighborTowardsTarget(
            occupiedTile.column,
            occupiedTile.row,
            finalDestination.x,
            finalDestination.y,
            this
        );

        if (nextTile != null)
        {
            if (debugUnitMovement) Debug.Log($"[{name}] GetNextTileForBG: Next tile towards ({finalDestination.x},{finalDestination.y}) is ({nextTile.column},{nextTile.row}).");
        }
        else
        {
            if (debugUnitMovement) Debug.LogWarning($"[{name}] GetNextTileForBG: No valid next tile found towards ({finalDestination.x},{finalDestination.y}).");
        }
        return nextTile;
    }

     /// <summary>
     /// Finds an alternative neighbor tile when the preferred tile is not available.
     /// </summary>
     /// <param name="originallyPreferredTilePos">The position of the originally preferred tile.</param>
     /// <returns>The best alternative neighbor tile, or null if none found.</returns>
     private Tile FindAlternativeNeighbor(Vector2Int originallyPreferredTilePos)
     {
        if (occupiedTile?.Neighbors == null) return null;
        Tile bestAlternative = null;
        float minDistanceToFinalTarget = float.MaxValue;

        List<Tile> shuffledNeighbors = new List<Tile>(occupiedTile.Neighbors);
        for(int i=0; i<shuffledNeighbors.Count; ++i) {
            int r = Random.Range(i, shuffledNeighbors.Count);
            (shuffledNeighbors[i], shuffledNeighbors[r]) = (shuffledNeighbors[r], shuffledNeighbors[i]);
        }

        foreach (Tile neighbor in shuffledNeighbors)
        {
            if (neighbor == null || neighbor.IsOccupied) continue;
            Vector2Int neighborPos = new Vector2Int(neighbor.column, neighbor.row);
            if (neighborPos == originallyPreferredTilePos) continue;
            if (TileReservationController.Instance != null &&
                TileReservationController.Instance.IsTileReservedByOtherUnit(neighborPos, this)) continue;

            if(TargetPosition.HasValue) {
                float dist = HexGridManager.Instance.HexDistance(neighbor.column, neighbor.row, TargetPosition.Value.x, TargetPosition.Value.y);
                if (dist < minDistanceToFinalTarget) {
                    minDistanceToFinalTarget = dist;
                    bestAlternative = neighbor;
                }
            } else if(bestAlternative == null) {
                 bestAlternative = neighbor;
            }
        }
        if (bestAlternative != null && debugUnitMovement) Debug.Log($"[{name}] Found alternative neighbor: ({bestAlternative.column},{bestAlternative.row})");
        else if(debugUnitMovement && TargetPosition.HasValue) Debug.LogWarning($"[{name}] No unreserved alternative neighbor found that is closer to target {TargetPosition.Value}.");
        else if(debugUnitMovement) Debug.LogWarning($"[{name}] No unreserved alternative neighbor found.");
        return bestAlternative;
     }

    /// <summary>
    /// Forces finding any available neighbor tile when the unit is stuck.
    /// Shuffles neighbors to avoid predictable movement patterns.
    /// </summary>
    /// <returns>Any available neighbor tile, or null if all are occupied.</returns>
    private Tile ForceGetAnyAvailableNeighbor()
    {
        if (occupiedTile?.Neighbors == null || occupiedTile.Neighbors.Count == 0) return null;
        List<Tile> neighbors = new List<Tile>(occupiedTile.Neighbors);
        for (int i = 0; i < neighbors.Count; i++) { int r = Random.Range(i, neighbors.Count); var tmp = neighbors[i]; neighbors[i] = neighbors[r]; neighbors[r] = tmp; }
        foreach (var neighbor in neighbors)
        {
            if (neighbor != null && !neighbor.IsOccupied)
            {
                if (TileReservationController.Instance == null || !TileReservationController.Instance.IsTileReservedByOtherUnit(new Vector2Int(neighbor.column, neighbor.row), this))
                {
                    return neighbor;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Calculates the rotation needed to face a target tile.
    /// </summary>
    /// <param name="targetTile">The tile to face.</param>
    /// <returns>The rotation quaternion to face the target tile.</returns>
    protected Quaternion CalculateRotationToFaceTile(Tile targetTile)
    {
        if (targetTile == null || targetTile == occupiedTile) return transform.rotation;
        Vector3 direction = targetTile.transform.position - transform.position;
        direction.y = 0;
        return (direction != Vector3.zero) ? Quaternion.LookRotation(direction) : transform.rotation;
    }

    /// <summary>
    /// Smoothly rotates the unit to face a target tile over time.
    /// </summary>
    /// <param name="targetTile">The tile to face.</param>
    /// <param name="duration">The duration of the rotation in seconds.</param>
    /// <returns>Coroutine for rotation process.</returns>
    protected IEnumerator RotateToFaceTile(Tile targetTile, float duration = 0.15f)
    {
        if (targetTile == null || targetTile == occupiedTile) yield break;
        Quaternion targetRotation = CalculateRotationToFaceTile(targetTile);
        if (Quaternion.Angle(transform.rotation, targetRotation) < 1f) yield break;

        if (duration <= 0) { transform.rotation = targetRotation; yield break; }
        float elapsed = 0;
        Quaternion startRotation = transform.rotation;
        while (elapsed < duration)
        {
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.rotation = targetRotation;
    }

    /// <summary>
    /// Immediately faces the unit towards a target transform.
    /// </summary>
    /// <param name="targetTransform">The transform to face.</param>
    protected void FaceTarget(Transform targetTransform)
    {
        if (targetTransform == null) return;
        Vector3 direction = targetTransform.position - transform.position;
        direction.y = 0;
        if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);
    }
    protected void FaceUnitTarget(Unit target) { FaceTarget(target?.transform); }
    protected void FaceBuildingTarget(Building target) { FaceTarget(target?.transform); }

    protected void UpdateFacingDirection()
    {
        if (!TargetPosition.HasValue || occupiedTile == null || HexGridManager.Instance == null) return;
        Tile finalTargetTile = HexGridManager.Instance.GetTileAt(TargetPosition.Value.x, TargetPosition.Value.y);
        if (finalTargetTile == null || IsAtTargetLocation()) return;
        Tile nextStepTile = GetNextTileTowardsDestination();
        Tile tileToFace = nextStepTile ?? finalTargetTile;
        if (tileToFace != null && tileToFace != occupiedTile) StartCoroutine(RotateToFaceTile(tileToFace));
    }
     protected void UpdateFacingDirectionSafe() { if (!_isAttacking && currentState != UnitState.Capturing && !IsMoving) UpdateFacingDirection(); }

    protected virtual void SetState(UnitState newState)
    {
        if (currentState == newState && !(newState == UnitState.Attacking || newState == UnitState.Capturing))
        {
            return;
        }

        if (debugUnitMovement || debugUnitCombat)
        {
            Debug.Log($"[{name} ({Time.frameCount})] SetState: Changing C# State from '{currentState}' to '{newState}'.", this);
        }

        UnitState previousCSharpState = currentState;
        currentState = newState;

        if (useAnimations && animator != null)
        {
            bool setAnimIdle = (newState == UnitState.Idle);
            bool setAnimMoving = (newState == UnitState.Moving);

            if (newState == UnitState.Attacking || newState == UnitState.Capturing)
            {
                setAnimIdle = false;
                setAnimMoving = false;
            }

            animator.SetBool(IdleParamId, setAnimIdle);
            animator.SetBool(MovingParamId, setAnimMoving);

            if (debugUnitMovement || debugUnitCombat)
            {
                Debug.Log($"[{name} ({Time.frameCount})] SetState: Animator Booleans Set -> IsIdle: {setAnimIdle}, IsMoving: {setAnimMoving}", this);
            }

            if (newState != UnitState.Attacking && previousCSharpState == UnitState.Attacking)
            {
                animator.ResetTrigger(AttackTriggerId);
                if (debugUnitCombat) Debug.Log($"[{name} ({Time.frameCount})] SetState: Resetting AttackTriggerId because newState is no longer Attacking.", this);
            }

            if (newState != UnitState.Capturing && previousCSharpState == UnitState.Capturing)
            {
                animator.ResetTrigger(CaptureTriggerId);
                if (debugUnitCombat) Debug.Log($"[{name} ({Time.frameCount})] SetState: Resetting CaptureTriggerId because newState is no longer Capturing.", this);
            }
        }
        else if (useAnimations && animator == null)
        {
            Debug.LogError($"[{name} ({Time.frameCount})] SetState: Animator is NULL for state {newState}.", this);
        }

        switch (newState)
        {
            case UnitState.Idle:
                isInteractingWithBuilding = false;
                if (buildingBeingCaptured != null) { buildingBeingCaptured.StopCapturing(this); buildingBeingCaptured = null; }
                beatsSpentCapturing = 0;
                if (!(this is AllyUnit)) { targetUnit = null; targetBuilding = null; }
                break;

            case UnitState.Attacking:
                if (buildingBeingCaptured != null) { buildingBeingCaptured.StopCapturing(this); buildingBeingCaptured = null; }
                beatsSpentCapturing = 0;
                break;

            case UnitState.Capturing:
                beatsSpentCapturing = 0;
                targetUnit = null;
                isInteractingWithBuilding = true;
                break;

            case UnitState.Moving:
                if (buildingBeingCaptured != null) { buildingBeingCaptured.StopCapturing(this); buildingBeingCaptured = null; }
                isInteractingWithBuilding = false;
                beatsSpentCapturing = 0;
                break;
        }
    }

    protected bool IsAtTargetLocation() => TargetPosition.HasValue && occupiedTile != null && occupiedTile.column == TargetPosition.Value.x && occupiedTile.row == TargetPosition.Value.y;

    public virtual bool IsValidUnitTarget(Unit otherUnit) => false;
    public virtual bool IsValidBuildingTarget(Building building) => building != null && building.IsTargetable;


    public bool IsBuildingInRange(Building building)
    {
         if (building == null || occupiedTile == null || HexGridManager.Instance == null) return false;
         Tile buildingTile = building.GetOccupiedTile();
         if (buildingTile == null) return false;
         var tilesInRange = HexGridManager.Instance.GetTilesWithinRange(occupiedTile.column, occupiedTile.row, Mathf.CeilToInt(AttackRange));
         return tilesInRange.Contains(buildingTile);
    }

    public bool IsUnitInRange(Unit unit)
    {
        if (unit == null || occupiedTile == null || HexGridManager.Instance == null) return false;
        Tile unitTile = unit.GetOccupiedTile();
        if (unitTile == null) return false;
        var tilesInRange = HexGridManager.Instance.GetTilesWithinRange(occupiedTile.column, occupiedTile.row, Mathf.CeilToInt(AttackRange));
        return tilesInRange.Contains(unitTile);
    }

    public bool IsBuildingInCaptureRange(Building building)
    {
        if (building == null || occupiedTile == null || HexGridManager.Instance == null) return false;
        Tile buildingTile = building.GetOccupiedTile();
        if (buildingTile == null) return false;
        var tilesInRange = HexGridManager.Instance.GetTilesWithinRange(occupiedTile.column, occupiedTile.row, 1);
        return tilesInRange.Contains(buildingTile);
    }

    public virtual bool PerformCapture(Building buildingToCapture)
    {
        NeutralBuilding neutralBuilding = buildingToCapture as NeutralBuilding;
        if (neutralBuilding == null || !neutralBuilding.IsRecapturable || (neutralBuilding.Team != TeamType.Neutral && neutralBuilding.Team != TeamType.Enemy))
        {
            if (debugUnitCombat) Debug.LogWarning($"[{name}] Cannot capture '{buildingToCapture.name}'.");
            return false;
        }
        if (!IsBuildingInCaptureRange(neutralBuilding))
        {
            if (debugUnitCombat) Debug.LogWarning($"[{name}] Cannot capture '{neutralBuilding.name}': out of range.");
            return false;
        }

        FaceBuildingTarget(neutralBuilding);
        TeamType teamToCaptureFor = (this is AllyUnit) ? TeamType.Player : TeamType.Enemy;
        bool captureStarted = neutralBuilding.StartCapture(teamToCaptureFor, this);

        if (captureStarted)
        {
            buildingBeingCaptured = neutralBuilding;
            SetState(UnitState.Capturing);
            if (useAnimations && animator != null) animator.SetTrigger(CaptureTriggerId);
            beatsSpentCapturing = 0;
            return true;
        }
        if(currentState == UnitState.Capturing) SetState(UnitState.Idle);
        return false;
    }

    public virtual void OnCaptureComplete()
    {
        if (debugUnitCombat) Debug.Log($"[{name}] Capture of '{buildingBeingCaptured?.name}' complete/ended.");
        buildingBeingCaptured = null;
        beatsSpentCapturing = 0;
        SetState(UnitState.Idle);
        targetBuilding = null;
        isInteractingWithBuilding = false;
        UpdateFacingDirectionSafe();
    }

     public void ReleaseCurrentReservation()
    {
        if (_reservedTile != null && TileReservationController.Instance != null)
        {
            TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(_reservedTile.column, _reservedTile.row), this);
            if (debugUnitMovement) Debug.Log($"[{name}] Reservation on tile ({_reservedTile.column},{_reservedTile.row}) released by Unit.ReleaseCurrentReservation().");
            _reservedTile = null;
        } else if (_reservedTile != null && debugUnitMovement) {
            Debug.LogWarning($"[{name}] TileReservationController not found, cannot formally release reservation for tile ({_reservedTile.column},{_reservedTile.row}). Clearing local _reservedTile.");
            _reservedTile = null;
        }
    }

    public void OnTileReservationChanged(Vector2Int tilePos, Unit reservingUnit, bool isReserved)
    {
        if (reservingUnit != this && !isReserved && !IsMoving && TargetPosition.HasValue)
        {
            if (TargetPosition.Value.x == tilePos.x && TargetPosition.Value.y == tilePos.y)
            {
                if (debugUnitMovement) Debug.Log($"[{name}] Target tile {tilePos} became free. Triggering movement check.");
                _beatCounter = MovementDelay;
            }
        }
    }
    public virtual void ApplyBuff(StatToBuff stat, float multiplier, float duration)
    {
        if (duration <= 0) return;

        ActiveBuff newBuff = new ActiveBuff
        {
            Stat = stat,
            Multiplier = multiplier,
            DurationRemaining = duration
        };
        newBuff.ExpiryCoroutine = StartCoroutine(BuffExpiryCoroutine(newBuff));
        activeBuffs.Add(newBuff);

        Debug.Log($"[{name}] Buff appliqué: {stat} x{multiplier} pour {duration}s. Nouvelle Att: {Attack}, Nouvelle Def: {Defense}");
    }

    private IEnumerator BuffExpiryCoroutine(ActiveBuff buff)
    {
        yield return new WaitForSeconds(buff.DurationRemaining);
        RemoveBuff(buff);
    }

    protected virtual void RemoveBuff(ActiveBuff buff)
    {
        if (activeBuffs.Contains(buff))
        {
            activeBuffs.Remove(buff);
            Debug.Log($"[{name}] Buff expiré/retiré: {buff.Stat}. Att actuelle: {Attack}, Def actuelle: {Defense}");
        }
    }

    public virtual void OnDestroy()
    {
        // --- LOGIQUE FUSIONNÉE : Nettoyage complet ---
        if (FeverManager.Instance != null)
        {
            FeverManager.Instance.OnFeverLevelChanged -= HandleFeverLevelChanged;
        }
        if (_activeFeverAuraInstance != null)
        {
            Destroy(_activeFeverAuraInstance);
            _activeFeverAuraInstance = null;
        }
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= OnRhythmBeatInternal;
        }

        if (TileReservationController.Instance != null)
        {
            if (occupiedTile != null)
            {
                TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(occupiedTile.column, occupiedTile.row), this);
                if (debugUnitMovement) Debug.Log($"[{name}] OnDestroy: Released reservation for occupiedTile ({occupiedTile.column},{occupiedTile.row}).");
            }
            if (_reservedTile != null && _reservedTile != occupiedTile)
            {
                TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(_reservedTile.column, _reservedTile.row), this);
                if (debugUnitMovement) Debug.Log($"[{name}] OnDestroy: Released reservation for _reservedTile ({_reservedTile.column},{_reservedTile.row}).");
            }
            TileReservationController.Instance.RemoveObserver(this);
        }

        if (occupiedTile != null)
        {
            occupiedTile.RemoveUnit();
        }
        _reservedTile = null;
        occupiedTile = null;

        foreach (var buff in activeBuffs)
        {
            if (buff.ExpiryCoroutine != null)
            {
                StopCoroutine(buff.ExpiryCoroutine);
            }
        }
        activeBuffs.Clear();
    }

    public IEnumerator PerformCheerAndDespawnCoroutine()
    {
        if (useAnimations && animator != null)
        {
            animator.SetBool(IdleParamId, false);
            animator.SetBool(MovingParamId, false);
            animator.SetTrigger(CheerTriggerId);
        }

        float cheerAnimationDuration = 2.0f;
        if (useAnimations && animator != null)
        {
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            foreach (AnimationClip clip in clips)
            {
                if (clip.name.ToLower().Contains("cheer"))
                {
                    cheerAnimationDuration = clip.length;
                    break;
                }
            }
        }
        yield return new WaitForSeconds(cheerAnimationDuration);
        if (this is AllyUnit allyUnit)
        {
            if (MomentumManager.Instance != null && allyUnit.MomentumGainOnObjectiveComplete > 0)
            {
                MomentumManager.Instance.AddMomentum(allyUnit.MomentumGainOnObjectiveComplete);
                Debug.Log($"[Unit] L'unité offensive {allyUnit.name} a complété son objectif et a rapporté {allyUnit.MomentumGainOnObjectiveComplete} de momentum.");
            }
        }
        if (cheerAndDespawnVFX != null)
        {
            Instantiate(cheerAndDespawnVFX, transform.position, Quaternion.identity);
        }
        
        Die();
    }

    public virtual void InitializeFromCharacterStatsSheets(StatSheet_SO characterStatsReceived)
    {
        if (characterStatsReceived == null)
        {
            Debug.LogError("InitializeFromCharacterStatsSheets: characterStatsReceived is null. Cannot initialize unit.");
            return;
        }

        this.CharacterStatSheets = characterStatsReceived;
        this.CurrentStats = StatsCalculator.GetFinalStats(this.CharacterStatSheets, this.Level);
        this.Health = this.CurrentStats.MaxHealth;
    }

    public virtual void InitializeFromCharacterData(CharacterData_SO characterData)
    {
        if (characterData == null)
        {
            Debug.LogError("InitializeFromCharacterData: characterData est null !", this);
            return;
        }

        this.CharacterStatSheets = characterData.Stats;
        _feverBuffs = characterData.feverBuffs;
        _canReceiveFeverBuffs = true;

        int level = 1;
        List<EquipmentData_SO> equipment = new List<EquipmentData_SO>();
        Debug.Log($"[{name}] InitializeFromCharacterData: Initializing unit with CharacterData_SO");
        if (this is AllyUnit && PlayerDataManager.Instance != null)
        {
            if (PlayerDataManager.Instance.Data.CharacterProgressData.TryGetValue(characterData.CharacterID, out var progress))
            {
                level = progress.CurrentLevel;
                if (PlayerDataManager.Instance.Data.EquippedItems.TryGetValue(characterData.CharacterID, out var equippedItemIDs))
                {
                    foreach (var itemID in equippedItemIDs)
                    {
                        EquipmentData_SO equipmentData = Resources.Load<EquipmentData_SO>($"Data/Equipment/{itemID}");
                        if (equipmentData != null)
                        {
                            equipment.Add(equipmentData);
                        }
                    }
                }
            }
        }

        this.baseStats = StatsCalculator.GetFinalStats(characterData, level, equipment);
        this.CurrentStats = new RuntimeStats
        {
            MaxHealth = baseStats.MaxHealth,
            Attack = baseStats.Attack,
            Defense = baseStats.Defense,
            AttackRange = baseStats.AttackRange,
            AttackDelay = baseStats.AttackDelay,
            MovementDelay = baseStats.MovementDelay,
            DetectionRange = baseStats.DetectionRange
        };

       if (this is AllyUnit allyUnit)
       {
           allyUnit.MomentumGainOnObjectiveComplete = characterData.MomentumGainOnObjectiveComplete;
       }

        this.Health = this.CurrentStats.MaxHealth;
        
    }

    // ITargetable implementation
    public Transform TargetPoint => transform;
    public GameObject GameObject => gameObject;
    #endregion
    // Override dans les classes dérivées pour spécifier si l'unité est ciblable
    public virtual bool IsTargetable => false; // Par défaut, les unités ne sont pas ciblables
}

