using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using System.Linq;

public enum UnitState
{
    Idle,
    Moving,
    Attacking,
    Capturing,
}


public abstract class Unit : MonoBehaviour, ITileReservationObserver
{
    protected abstract Vector2Int? TargetPosition { get; }

    [Header("State & Core Mechanics")]
    public bool IsMoving;
    protected Tile occupiedTile;
    [SerializeField] private float yOffset = 0f;
    public bool isAttached = false;
    private Tile _reservedTile;

    [Header("Debugging")]
    [SerializeField] private bool debugUnitMovement = true;
    [SerializeField] private bool debugUnitCombat = false;

    [Header("Stats & Systems")]
    [InlineEditor(InlineEditorModes.FullEditor)]
    [SerializeField] private UnitStats_SO unitStats;
    [SerializeField] private MonoBehaviour movementSystemComponent;
    [SerializeField] private MonoBehaviour attackSystemComponent;

    public IMovement MovementSystem { get; private set; }
    public IAttack AttackSystem { get; private set; }
    protected class ActiveBuff
    {
        public StatToBuff Stat;
        public float Multiplier;
        public float DurationRemaining;
        public Coroutine ExpiryCoroutine;
    }
    public bool IsSpawning { get; private set; } = true;
    public void SetSpawningState(bool isCurrentlySpawning)
    {
        IsSpawning = isCurrentlySpawning;
      
    }

    public int Health { get; protected set; }
    public int MovementDelay => unitStats?.MovementDelay ?? 1;
    public virtual int Attack
    {
        get
        {
            float baseAttack = unitStats?.Attack ?? 0; //
            float multiplier = 1f;
            foreach (var buff in activeBuffs.Where(b => b.Stat == StatToBuff.Attack))
            {
                multiplier *= buff.Multiplier;
            }
            return Mathf.Max(0, Mathf.RoundToInt(baseAttack * multiplier));
        }
    }

    public virtual int Defense
    {
        get
        {
            float baseDefense = unitStats?.Defense ?? 0; //
            float multiplier = 1f;
            foreach (var buff in activeBuffs.Where(b => b.Stat == StatToBuff.Defense))
            {
                multiplier *= buff.Multiplier;
            }
            return Mathf.Max(0, Mathf.RoundToInt(baseDefense * multiplier)); 
        }
    }
    public int AttackDelay => unitStats?.AttackDelay ?? 1;
    public float AttackRange => unitStats?.AttackRange ?? 1f;
    public int DetectionRange => unitStats?.DetectionRange ?? 3;
    protected UnitStats_SO Stats => unitStats;

    protected int _beatCounter = 0;
    private int _attackBeatCounter = 0;
    private int _stuckCount = 0;
    protected bool _isAttacking = false;

    public delegate void UnitAttackedHandler(Unit attacker, Unit target, int damage);
    public static event UnitAttackedHandler OnUnitAttacked;
    public delegate void UnitAttackedBuildingHandler(Unit attacker, Building target, int damage);
    public static event UnitAttackedBuildingHandler OnUnitAttackedBuilding;

    [Header("Animation")]
    [SerializeField] protected Animator animator;
    [SerializeField] protected bool useAnimations = true;

    public readonly int IdleParamId = Animator.StringToHash("IsIdle");
    public readonly int MovingParamId = Animator.StringToHash("IsMoving");
    public readonly int AttackTriggerId = Animator.StringToHash("Attack");
    public readonly int CaptureTriggerId = Animator.StringToHash("Capture");
    public readonly int DieTriggerId = Animator.StringToHash("Die");
    public readonly int CheerTriggerId = Animator.StringToHash("Cheer");

    [SerializeField] protected UnitState currentState = UnitState.Idle;

    public Unit targetUnit = null;
    public Building targetBuilding = null;
    protected bool isInteractingWithBuilding = false;
    protected NeutralBuilding buildingBeingCaptured = null;
    protected int beatsSpentCapturing = 0;

    
    protected List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    public Tile GetOccupiedTile() => occupiedTile;

    protected List<Tile> GetTilesInAttackRange()
    {
        if (occupiedTile == null || HexGridManager.Instance == null) return new List<Tile>();
        return HexGridManager.Instance.GetTilesWithinRange(occupiedTile.column, occupiedTile.row, Mathf.CeilToInt(AttackRange));
    }


    protected virtual IEnumerator Start()
    {
        if (unitStats != null) { Health = unitStats.Health; }
        else { Debug.LogError($"[{name}] UnitStats non assigné !"); }

        if (animator == null) { animator = GetComponent<Animator>(); }
        if (useAnimations && animator == null)
        {
             Debug.LogWarning($"[{name}] Animator non trouvé/assigné, mais useAnimations est true. Animations désactivées.");
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

//public getter to get the unitStats.UnitType
    public UnitType GetUnitType()
    {
        return unitStats?.Type ?? UnitType.Null; 
    }

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
                        RhythmManager.OnBeat += OnRhythmBeatInternal;
                        if (debugUnitMovement) Debug.Log($"[{name}] Attached to tile ({nearestTile.column}, {nearestTile.row}) and subscribed to OnRhythmBeat.");
                        yield break;
                    }
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    public virtual void OnCaptureBeat()
    {
        if (currentState != UnitState.Capturing) return; // Sécurité

        beatsSpentCapturing++; // Compteur interne de l'unité pour info, la logique principale est dans NeutralBuilding
        if (useAnimations && animator != null)
        {
            animator.SetTrigger(CaptureTriggerId); // Assurez-vous que ce trigger est réinitialisé ailleurs ou est bien un trigger one-shot
        }
    }

    public virtual void StopCapturing()
    {
        if (currentState == UnitState.Capturing)
        {
            if (buildingBeingCaptured != null)
            {
                buildingBeingCaptured.StopCapturing(this); // Notifier le bâtiment
                buildingBeingCaptured = null;
            }
            beatsSpentCapturing = 0;
            SetState(UnitState.Idle);
        }
    }

    private void OnRhythmBeatInternal()
    {
        OnRhythmBeat();
    }

    protected virtual void OnRhythmBeat()
    {
        HandleMovementOnBeat();
        if (!IsMoving)
        {
            HandleAttackOnBeat();
        }
        else
        {
            _attackBeatCounter = 0;
        }
        if (currentState == UnitState.Capturing) { HandleCaptureOnBeat(); }
    }

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

    protected virtual void HandleAttackOnBeat()
    {
        if (_isAttacking || currentState == UnitState.Capturing || AttackSystem == null) return;

        _attackBeatCounter++;
        if (_attackBeatCounter >= AttackDelay)
        {
            _attackBeatCounter = 0;

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

    protected virtual void HandleCaptureOnBeat()
    {
         if (buildingBeingCaptured == null || currentState != UnitState.Capturing) return;
         if (useAnimations && animator != null)
         {
             animator.SetTrigger(CaptureTriggerId);
         }
    }

    public IEnumerator MoveToTile(Tile targetTile)
    {
        string context = $"[{name}/{GetInstanceID()}]"; // Pour les logs de cette instance de coroutine

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
            // Si on avait une _reservedTile (future cible) et que targetTile est différente, on la libère.
            if (_reservedTile != null && _reservedTile != targetTile)
            {
                if (debugUnitMovement) Debug.Log($"{context} MoveToTile: _reservedTile ({_reservedTile.name}) is different from targetTile ({targetTile.name}). Releasing _reservedTile.", this);
                TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(_reservedTile.column, _reservedTile.row), this);
                _reservedTile = null; // Important de la nullifier si on la relâche
            }
            reservationSuccess = TileReservationController.Instance.TryReserveTile(targetTilePos, this);
        }
        else
        {
            reservationSuccess = !targetTile.IsOccupied && targetTile.tileType == TileType.Ground; // Simplifié si pas de contrôleur
        }

        if (!reservationSuccess)
        {
            if (debugUnitMovement) Debug.LogWarning($"{context} MoveToTile ABORT: Failed to reserve target tile {targetTile.name}.", this);
            IsMoving = false; SetState(UnitState.Idle);
            yield break;
        }
        _reservedTile = targetTile; // La tuile cible est maintenant notre _reservedTile pour ce mouvement
        if (debugUnitMovement) Debug.Log($"{context} MoveToTile: Successfully reserved targetTile {targetTile.name}. _reservedTile is now {targetTile.name}.", this);

        // --- DÉBUT SECTION CRITIQUE DU MOUVEMENT ---
        IsMoving = true;
        SetState(UnitState.Moving);
        Tile originalTile = occupiedTile;
        if (debugUnitMovement && originalTile != null) Debug.Log($"{context} MoveToTile: OriginalTile is {originalTile.name}.", this);

        try
        {
            if (debugUnitMovement) Debug.Log($"{context} MoveToTile: In try block. About to rotate.", this);
            yield return StartCoroutine(RotateToFaceTile(targetTile));

            if (originalTile != null) // Si on était effectivement sur une tuile
            {
                if (TileReservationController.Instance != null)
                {
                    // Libérer la réservation de la tuile que l'on quitte PHYSIQUEMENT
                    TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(originalTile.column, originalTile.row), this);
                    if (debugUnitMovement) Debug.Log($"{context} MoveToTile: Released reservation for originalTile {originalTile.name} via Controller.", this);
                }
                originalTile.RemoveUnit(); // La tuile met son currentUnit à null
                if (debugUnitMovement) Debug.Log($"{context} MoveToTile: Called RemoveUnit on originalTile {originalTile.name}.", this);
            }
            transform.SetParent(null, true); // Se détacher pour le mouvement

            Vector3 currentWorldPosition = transform.position; // Position de départ du mouvement physique
            Vector3 targetWorldPosition = targetTile.transform.position + Vector3.up * yOffset;

            if (debugUnitMovement) Debug.Log($"{context} MoveToTile: Calling MovementSystem.MoveToTile from {currentWorldPosition} to {targetWorldPosition}.", this);

            yield return StartCoroutine(MovementSystem.MoveToTile(transform, currentWorldPosition, targetWorldPosition, 0.45f)); // Durée du mouvement

            if (debugUnitMovement) Debug.Log($"{context} MoveToTile: MovementSystem.MoveToTile FINISHED.", this);

            // Vérifications post-mouvement (si l'unité ou la tuile cible existent toujours)
            if (this == null || !this.gameObject.activeInHierarchy) {
                if (debugUnitMovement) Debug.LogWarning($"{context} MoveToTile: Unit became inactive/destroyed during MovementSystem.MoveToTile. Aborting Attach.", this);
                // Si l'unité est détruite, le OnDestroy devrait s'occuper de _reservedTile
                yield break;
            }
            if (targetTile == null || !targetTile.gameObject.activeInHierarchy) {
                 if (debugUnitMovement) Debug.LogWarning($"{context} MoveToTile: targetTile {targetTilePos} became inactive/destroyed. Aborting Attach.", this);
                 if (_reservedTile == targetTile && TileReservationController.Instance != null) { // targetTile était notre _reservedTile
                     TileReservationController.Instance.ReleaseTileReservation(targetTilePos, this);
                     _reservedTile = null;
                 }
                 yield break;
            }

            if (debugUnitMovement) Debug.Log($"{context} MoveToTile: About to call AttachToTile for {targetTile.name}.", this);
            AttachToTile(targetTile); // S'attache à la nouvelle tuile (et gère la réservation de cette tuile)
            if (debugUnitMovement) Debug.Log($"{context} MoveToTile: AttachToTile for {targetTile.name} COMPLETED. OccupiedTile: {(occupiedTile?.name ?? "null")}", this);
        }
        finally
        {
            IsMoving = false; // Mouvement terminé ou interrompu
            if (debugUnitMovement) Debug.Log($"{context} MoveToTile: In FINALLY. IsMoving set to false. Occupied: {(occupiedTile?.name ?? "null")}, _reservedTile: {(_reservedTile?.name ?? "null")}, targetTile: {targetTile.name}", this);

            if (currentState == UnitState.Moving) // Si on était en mouvement, on redevient Idle (ou autre si une action est enchaînée)
            {
                 SetState(UnitState.Idle);
            }

            // Vérification cruciale : si on n'a PAS réussi à s'attacher à targetTile
            if (occupiedTile != targetTile)
            {
                // Si _reservedTile pointait vers targetTile (ce qui devrait être le cas si la réservation initiale a réussi)
                if (_reservedTile == targetTile && TileReservationController.Instance != null)
                {
                    TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(targetTile.column, targetTile.row), this);
                    if (debugUnitMovement) Debug.LogWarning($"{context} MoveToTile FINALLY: Movement FAILED/INTERRUPTED after reserving {targetTile.name} and before attaching. Reservation released.", this);
                    // Si on a libéré targetTile, elle ne peut plus être notre _reservedTile
                    _reservedTile = null;
                }
            }
            // else: mouvement réussi, on est sur targetTile. AttachToTile a mis à jour _reservedTile pour être targetTile.
            // La réservation est donc correcte et maintenue.
        }
    }

    public IEnumerator PerformAttackCoroutine(Unit target)
    {
        if (AttackSystem == null || target == null || target.Health <= 0)
        {
            if (debugUnitCombat) Debug.LogWarning($"[{name}] PerformAttackCoroutine: Conditions non remplies (AttackSystem null, cible nulle ou cible morte). Cible: {(target?.name ?? "NULL")}, Cible PV: {target?.Health ?? -1}");
            _isAttacking = false; // Assurez-vous que le flag est bien réinitialisé
            SetState(UnitState.Idle); // Repasser en Idle si l'attaque ne peut pas commencer
            yield break;
        }

        _isAttacking = true; // L'unité commence son action d'attaque
        SetState(UnitState.Attacking); // Met à jour l'état logique de l'unité
        FaceUnitTarget(target); // S'orienter vers la cible

        if (useAnimations && animator != null)
        {
            if (debugUnitCombat) Debug.Log($"[{name} ({Time.frameCount})] PerformAttackCoroutine: Déclenchement de l'animation d'attaque (ID: {AttackTriggerId}) pour la cible {target.name}.", this);
            animator.SetTrigger(AttackTriggerId);
        }
        else
        {
            if (useAnimations && animator == null && debugUnitCombat) Debug.LogWarning($"[{name}] PerformAttackCoroutine: Animator non assigné mais useAnimations est true.", this);
        }

        // Calculer les dégâts ici car c'est une stat de l'attaquant
        int calculatedDamage = Mathf.Max(1, Attack - target.Defense); // Attack et Defense viennent des Stats de l'unité

        // La durée passée ici est pour l'animation de l'ATTAQUANT.
        // Pour RangedAttack, le projectile aura sa propre durée de vie/vol.
        // Pour MeleeAttack, le MeleeAttack.cs pourrait utiliser cette durée pour son timing d'impact.
        float attackerAnimationDuration = 0.5f; // Durée typique de l'animation d'attaque de l'unité elle-même

        if (AttackSystem != null)
        {
             if (debugUnitCombat) Debug.Log($"[{name}] PerformAttackCoroutine: Appel de AttackSystem.PerformAttack sur {target.name} avec {calculatedDamage} dégâts potentiels et une durée d'animation de {attackerAnimationDuration}s.", this);
            // Pour RangedAttack, cela va instancier et lancer le projectile.
            // Pour MeleeAttack, cela pourrait jouer un VFX et attendre avant d'appliquer les dégâts (si MeleeAttack le gère).
            yield return StartCoroutine(
                AttackSystem.PerformAttack(
                    transform,
                    target.transform,
                    calculatedDamage,       // Les dégâts sont passés au système d'attaque
                    attackerAnimationDuration
                )
            );
        }

        // IMPORTANT:
        // Pour RangedAttack: Les dégâts sont appliqués par le Projectile.cs à l'impact.
        // Pour MeleeAttack: MeleeAttack.cs DEVRAIT appliquer les dégâts dans son PerformAttack.
        // DONC, Unit.cs ne devrait PLUS appliquer les dégâts ici si c'est délégué.

        // Si vous voulez garder un événement générique ici :
        // OnUnitAttacked?.Invoke(this, target, calculatedDamage); // Mais cela pourrait être redondant si le projectile/melee le fait déjà.
        // Il est préférable que l'événement OnUnitAttacked soit déclenché par celui qui APPLIQUE réellement les dégâts.

        if (debugUnitCombat) Debug.Log($"[{name}] PerformAttackCoroutine: Action d'attaque (lancement/coup) terminée pour {target.name}. _isAttacking sera mis à false.", this);
        _isAttacking = false; // L'action spécifique de ce "coup" ou "tir" est terminée du point de vue de l'unité.
                            // Si c'est une attaque à distance, le projectile continue sa course.
        SetState(UnitState.Idle); // Retour à l'état Idle après l'action, le Behavior Graph décidera de la suite.
    }

    // COROUTINE POUR ATTAQUER UN BÂTIMENT (Version Corrigée)
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

        int attackDamage = Attack; // Les bâtiments peuvent avoir une défense différente ou pas de défense via les stats de l'attaquant.
                                   // Building.TakeDamage() appliquera la défense du bâtiment.

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

        // De même, les dégâts sont gérés par le projectile ou MeleeAttack.cs.
        // OnUnitAttackedBuilding?.Invoke(this, target, attackDamage); // Préférable dans le système qui applique les dégâts.

        if (debugUnitCombat) Debug.Log($"[{name}] PerformAttackBuildingCoroutine: Action d'attaque (lancement/coup) terminée pour {target.name}. _isAttacking sera mis à false.", this);
        _isAttacking = false;
        SetState(UnitState.Idle);
    }

    protected virtual Unit FindAttackableUnitTarget()
    {
        if (occupiedTile == null || AttackSystem == null) return null;
        List<Tile> tilesInRange = GetTilesInAttackRange(); // Utilise la méthode déjà définie
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

    protected virtual Building FindAttackableBuildingTarget()
    {
        if (occupiedTile == null || AttackSystem == null) return null;
        List<Tile> tilesInRange = GetTilesInAttackRange(); // Utilise la méthode déjà définie
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


    public virtual void TakeDamage(int damage, Unit attacker = null)
    {
        if (debugUnitCombat) Debug.Log($"[{name}] TakeDamage called with {damage} damage from {attacker?.name ?? "unknown attacker"}.");
        if (damage <= 0) return; // Pas de dégâts négatifs ou nuls

        if (attacker != null && OnUnitAttacked != null)
        {
            OnUnitAttacked(attacker, this, damage);
        }

        // Appliquer les dégâts en 
        int actualDamage = Mathf.Max(0, damage - Defense);
        Health -= actualDamage;
        if (debugUnitCombat) Debug.Log($"[{name}] took {actualDamage} damage. Health: {Health}/{Stats?.Health ?? 0}");
        if (Health <= 0) Die();
    }

    protected virtual void Die()
    {
        if (debugUnitCombat) Debug.Log($"[{name}] Died.");
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
        	/* 
		   animator.SetTrigger(DieTriggerId);
         
		   float destroyDelay = 2.0f;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name.ToLower().Contains("die")) { destroyDelay = clip.length; break; }
            }
            Destroy(gameObject, destroyDelay);

			*/
            Destroy(gameObject);

        }
        else Destroy(gameObject);
    }

    protected void AttachToTile(Tile tile)
    {
        if (tile == null) { Debug.LogError($"[{name}] AttachToTile: tile is null!"); return; }

        // Libérer une réservation précédente si elle est différente de la nouvelle tuile
        if (_reservedTile != null && _reservedTile != tile && TileReservationController.Instance != null) {
             TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(_reservedTile.column, _reservedTile.row), this);
             if (debugUnitMovement) Debug.Log($"[{name}] AttachToTile: Released PREVIOUS _reservedTile ({_reservedTile.column},{_reservedTile.row}).");
             _reservedTile = null; // Important
        }

        Quaternion currentRotation = transform.rotation;
        occupiedTile = tile;
        transform.SetParent(tile.transform, true); // Utilisez true pour conserver la position/rotation/échelle mondiale lors du reparentage initial
        transform.position = tile.transform.position + Vector3.up * yOffset; // Puis ajustez la position
        transform.rotation = currentRotation; // Réappliquez la rotation
        tile.AssignUnit(this);

        // S'assurer que la tuile que nous occupons maintenant est marquée comme réservée par nous
        if (TileReservationController.Instance != null) {
            bool reservedSuccessfully = TileReservationController.Instance.TryReserveTile(new Vector2Int(tile.column, tile.row), this);
            if (reservedSuccessfully)
            {
                _reservedTile = tile; // --- MODIFICATION IMPORTANTE ---
                if (debugUnitMovement) Debug.Log($"[{name}] AttachToTile: Successfully reserved and attached to tile ({tile.column}, {tile.row}). _reservedTile updated.");
            }
            else
            {
                // Cela ne devrait pas arriver si IsOccupied est bien géré et que la tuile était libre
                 if (debugUnitMovement) Debug.LogError($"[{name}] AttachToTile: FAILED to reserve tile ({tile.column}, {tile.row}) even though attaching to it. This indicates a logic flaw!");
                _reservedTile = null; // S'assurer que _reservedTile est null si la réservation échoue
            }
        } else {
             _reservedTile = tile; // Si pas de controller, on suppose que c'est bon
        }

        if (debugUnitMovement && occupiedTile != null) Debug.Log($"[{name}] Attached to tile ({occupiedTile.column}, {occupiedTile.row}). Position: {transform.position}");
        else if (debugUnitMovement) Debug.LogWarning($"[{name}] Attached, but occupiedTile is unexpectedly null.");
    }

    public virtual void OnMovementComplete()
    {
        if (debugUnitMovement) Debug.Log($"[{name}] Movement complete. Now at ({occupiedTile?.column ?? -1}, {occupiedTile?.row ?? -1}).");
        _attackBeatCounter = 0;
    }

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
            this // 'this' est l'unité qui demande
        );

        if (nextTile != null)
        {
            if (debugUnitMovement) Debug.Log($"[{name}] GetNextTileForBG: Next tile towards ({finalDestination.x},{finalDestination.y}) is ({nextTile.column},{nextTile.row}).");
            // La logique de réservation de la tuile `nextTile` sera gérée par MoveToTile.
        }
        else
        {
            if (debugUnitMovement) Debug.LogWarning($"[{name}] GetNextTileForBG: No valid next tile found towards ({finalDestination.x},{finalDestination.y}).");
        }
        return nextTile;
    }

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

    protected Quaternion CalculateRotationToFaceTile(Tile targetTile)
    {
        if (targetTile == null || targetTile == occupiedTile) return transform.rotation;
        Vector3 direction = targetTile.transform.position - transform.position;
        direction.y = 0;
        return (direction != Vector3.zero) ? Quaternion.LookRotation(direction) : transform.rotation;
    }

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
        // Optionnel: Log ou return si l'état C# ne change pas et que ce n'est pas un état "actif".
        // if (debugUnitMovement) Debug.Log($"[{name} ({Time.frameCount})] SetState: newState ({newState}) already current C# state.");
        // return; // Peut être activé si pas d'effets de bord désirés lors de la réaffirmation de l'état.
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

        // Si l'unité entre dans un état logique C# d'attaque ou de capture,
        // elle n'est généralement ni inactive (Idle) ni en mouvement (Moving) du point de vue de l'animation principale.
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

        // Gestion des Triggers
        // Si l'état N'EST PLUS Attacking (C#) et qu'il l'ÉTAIT avant, réinitialiser le trigger d'attaque.
        // Cela est utile si le trigger a été activé mais que l'état C# change avant que l'animation ne se termine
        // (par exemple, une interruption).
        if (newState != UnitState.Attacking && previousCSharpState == UnitState.Attacking)
        {
            // Si vous avez encore un trigger "Attack" et que vous voulez le nettoyer
            // quand l'état logique d'attaque se termine.
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

    // --- Logique métier pour les états C# ---
    switch (newState)
    {
        case UnitState.Idle:
            isInteractingWithBuilding = false;
            if (buildingBeingCaptured != null) { buildingBeingCaptured.StopCapturing(this); buildingBeingCaptured = null; }
            beatsSpentCapturing = 0;
            if (!(this is AllyUnit)) { targetUnit = null; targetBuilding = null; }
            break;

        case UnitState.Attacking: // État logique C# d'être en mode attaque
            if (buildingBeingCaptured != null) { buildingBeingCaptured.StopCapturing(this); buildingBeingCaptured = null; }
            beatsSpentCapturing = 0;
            // L'animation elle-même est déclenchée par AttackTriggerId dans PerformAttackCoroutine.
            // Cet état C# indique juste que l'unité est engagée dans un comportement d'attaque.
            break;

        case UnitState.Capturing:
            beatsSpentCapturing = 0;
            targetUnit = null;
            isInteractingWithBuilding = true; // La capture est une interaction avec un bâtiment
            break;

        case UnitState.Moving:
            if (buildingBeingCaptured != null) { buildingBeingCaptured.StopCapturing(this); buildingBeingCaptured = null; }
            isInteractingWithBuilding = false;
            beatsSpentCapturing = 0;
            // Laisser le Behavior Graph gérer la logique de conservation/annulation des cibles.
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

        // Optionnel : Vérifier si un buff similaire existe déjà et le cumuler/remplacer/ignorer
        // Pour cet exemple, on ajoute simplement le nouveau buff.
        // Si vous voulez un remplacement :
        // activeBuffs.RemoveAll(b => b.Stat == stat);

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

    // Assurez-vous de nettoyer les coroutines de buff si l'unité est détruite
    
    public virtual void OnDestroy()
    {
        if (RhythmManager.Instance != null) RhythmManager.OnBeat -= OnRhythmBeatInternal;

        if (TileReservationController.Instance != null)
        {
            // Libère la tuile actuellement occupée SI ELLE EXISTE
            if (occupiedTile != null)
            {
                TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(occupiedTile.column, occupiedTile.row), this);
                if (debugUnitMovement) Debug.Log($"[{name}] OnDestroy: Released reservation for occupiedTile ({occupiedTile.column},{occupiedTile.row}).");
            }
            // Libère la tuile _reservedTile si elle est différente de occupiedTile et non null
            // Cela peut arriver si l'unité est détruite pendant un mouvement vers _reservedTile
            if (_reservedTile != null && _reservedTile != occupiedTile)
            {
                TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(_reservedTile.column, _reservedTile.row), this);
                if (debugUnitMovement) Debug.Log($"[{name}] OnDestroy: Released reservation for _reservedTile ({_reservedTile.column},{_reservedTile.row}).");
            }
            TileReservationController.Instance.RemoveObserver(this); // Se désabonner
        }

        if (occupiedTile != null)
        {
            occupiedTile.RemoveUnit(); // Notifie la tuile elle-même
        }
        _reservedTile = null; // Nettoyage
        occupiedTile = null;  // Nettoyage
        
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
        // 1. Set state to prevent other actions (optional, graph might handle this)
        // SetState(UnitState.Cheering); // You'd need a Cheering state in your UnitState enum

        // 2. Trigger Cheer Animation
        if (useAnimations && animator != null)
        {
            animator.SetBool(IdleParamId, false); // Ensure not stuck in idle
            animator.SetBool(MovingParamId, false);
            animator.SetTrigger(CheerTriggerId); // Use the ID from Unit.cs
        }

        // 3. Wait for animation duration
        //    This is a bit tricky. Best way is to get actual clip length.
        //    For simplicity, using a fixed delay or assuming you have an event at the end of cheer animation.
        float cheerAnimationDuration = 2.0f; // Default if clip length not found
        if (useAnimations && animator != null)
        {
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            foreach (AnimationClip clip in clips)
            {
                // Make sure your cheer animation clip is uniquely named or identifiable
                if (clip.name.ToLower().Contains("cheer")) // Adjust "cheer" if your clip is named differently
                {
                    cheerAnimationDuration = clip.length;
                    break;
                }
            }
        }
        yield return new WaitForSeconds(cheerAnimationDuration);

        // 4. Despawn (Effectively Die, but with a "victory" context)
        // You can call Die() or a new method if you want different effects for despawning vs. combat death.
        // For simplicity, let's use Die() which should already handle particle effects/sound.
        Die(); // Die() will handle Destroy(gameObject)
    }
    public virtual void InitializeFromCharacterData(CharacterData_SO characterData)
    {
        if (characterData == null)
        {
            Debug.LogError($"[{name}] Attempted to initialize with null CharacterData_SO.", this);
            if (this.unitStats == null) // Si aucun stats n'est même assigné dans l'inspecteur du prefab
            {
                Debug.LogError($"[{name}] CRITICAL: No UnitStats_SO assigned from CharacterData and no fallback stats on prefab. Unit may not function.", this);
                // Pour l'instant, on logue l'erreur.
            }
            return;
        }

        if (characterData.BaseStats == null)
        {
            Debug.LogError($"[{name}] CharacterData_SO '{characterData.DisplayName}' does not have BaseStats (UnitStats_SO) assigned.", this);
            if (this.unitStats == null)
            {
                 Debug.LogError($"[{name}] CRITICAL: No UnitStats_SO from CharacterData and no fallback stats on prefab for {characterData.DisplayName}. Unit may not function.", this);
            }
            return;
        }

        this.unitStats = characterData.BaseStats; // Assigner les stats du CharacterData
        
        this.Health = this.unitStats.Health;
        
        if (debugUnitCombat || debugUnitMovement) // Utilisez un de vos flags de debug existants ou créez-en un nouveau
        {
            Debug.Log($"[{name}] Stats initialisées depuis CharacterData '{characterData.DisplayName}' (Stats Asset: '{this.unitStats.name}'). Santé: {this.Health}", this);
        }
    }
}