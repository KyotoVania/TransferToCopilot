using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ScriptableObjects;

/// <summary>
/// Represents a boss enemy unit with hardcoded AI logic and unique mechanics.
/// Handles stun, movement, and destruction behaviors, and overrides standard AI behavior tree usage.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BossUnit : EnemyUnit
{
    /// <summary>
    /// Defines the possible action states for the boss.
    /// </summary>
    private enum BossActionState { Waiting, Preparing, Impacting, Moving }
    /// <summary>
    /// Current action state of the boss.
    /// </summary>
    private BossActionState _currentActionState = BossActionState.Waiting;

    /// <summary>
    /// Indicates that this unit uses hardcoded logic instead of behavior trees.
    /// </summary>
    protected override bool IsHardcoded => true;

    [Header("Boss Settings")]
    /// <summary>
    /// Final destination coordinates for the boss unit.
    /// </summary>
    public Vector2Int FinalDestinationCoordinates;
    /// <summary>
    /// The main building the boss must destroy at the end of its sequence.
    /// </summary>
    [Tooltip("Assign the main building for the boss to destroy.")]
    public Building TargetBuildingToDestroy;
    
    [Header("Stun Mechanic")]
    /// <summary>
    /// Number of hits required to stun the boss.
    /// </summary>
    [Tooltip("Number of hits before the boss is stunned.")]
    public int HitsToStun = 10;
    /// <summary>
    /// Duration of the stun in beats.
    /// </summary>
    [Tooltip("Stun duration in beats.")]
    public int StunDurationInBeats = 8;

    [Header("Stun Effects")]
    /// <summary>
    /// Visual effect prefab for stun.
    /// </summary>
    [SerializeField] private GameObject stunVFX;
    /// <summary>
    /// Audio clip played when stunned.
    /// </summary>
    [SerializeField] private AudioClip stunSFX;
    /// <summary>
    /// Vertical offset for stun VFX placement.
    /// </summary>
    [SerializeField] private float stunVFX_Y_Offset = 2.5f;

    [Header("Destruction Effects")]
    /// <summary>
    /// Visual effect prefab for destruction.
    /// </summary>
    [SerializeField] private GameObject destructionVFX;
    /// <summary>
    /// Audio clip played on destruction.
    /// </summary>
    [SerializeField] private AudioClip destructionSFX;
    
    // --- Private Variables ---
    /// <summary>
    /// Tracks the current number of hits received.
    /// </summary>
    private int _currentHitCounter = 0;
    /// <summary>
    /// Tracks the number of beats remaining while stunned.
    /// </summary>
    private int _stunBeatCounter = 0;
    /// <summary>
    /// Indicates whether the boss is currently stunned.
    /// </summary>
    private bool _isStunned = false;
    /// <summary>
    /// Maximum health value for the boss.
    /// </summary>
    private int _maximumHealth = 0;

    // --- Component References ---
    /// <summary>
    /// Reference to the boss movement system.
    /// </summary>
    private BossMovementSystem bossMovementSystem;
    /// <summary>
    /// Reference to the audio source component.
    /// </summary>
    private AudioSource audioSource;
    /// <summary>
    /// Gets the target position for the boss.
    /// </summary>
    protected override Vector2Int? TargetPosition => FinalDestinationCoordinates;

    /// <summary>
    /// Handles rhythm beat events and manages boss state transitions.
    /// </summary>
    /// <param name="beatDuration">Duration of the current beat.</param>
    protected override void OnRhythmBeat(float beatDuration)
    {
        // On gère d'abord les états qui interrompent le cycle normal (stun, destination atteinte).
        if (HandleInterruptStates()) return;
        
        // Si l'unité est déjà en mouvement ou en attaque, on laisse la coroutine finir.
        if (IsMoving || _isAttacking) return;
        
        _beatCounter++;
        Debug.Log($"[BossUnit] Beat {_beatCounter} sur {MovementDelay}.");

        // --- CYCLE D'ATTENTE ---
        // Si le boss est en attente, on vérifie s'il est temps de commencer l'attaque.
	    UpdateFacingDirection();

        if (_currentActionState == BossActionState.Waiting)
        {
            if (_beatCounter >= MovementDelay - 1)
            {
                // Il est temps de commencer la séquence d'attaque. On passe à l'état de préparation.
                _currentActionState = BossActionState.Preparing;
            }
            else
            {
                // Sinon, on continue d'attendre.
                return;
            }
        }

        // --- MACHINE A ÉTATS POUR LA SÉQUENCE D'ACTION ---
        // Cette machine s'exécute une fois par beat une fois que le cycle d'attente est terminé.
        switch (_currentActionState)
        {
            case BossActionState.Preparing:
                // BEAT 1: Lancer l'animation de préparation (saut).
                StartCoroutine(ExecutePreparationPhase(beatDuration));
                _currentActionState = BossActionState.Impacting; // Préparer l'état pour le prochain beat.
                break;

            case BossActionState.Impacting:
                // BEAT 2: Lancer l'animation d'impact (atterrissage) et les dégâts.
                StartCoroutine(ExecuteImpactPhase(beatDuration));
                _currentActionState = BossActionState.Moving; // Préparer l'état pour le prochain beat.
                break;

            case BossActionState.Moving:
                // BEAT 3: Exécuter le mouvement.
                StartCoroutine(ExecuteMovePhase());
                _currentActionState = BossActionState.Waiting; // La séquence est finie.
                _beatCounter = 0; // Réinitialiser le compteur pour le prochain cycle.
                break;
        }
    }
    
    /// <summary>
    /// Executes the preparation phase of the boss's attack sequence.
    /// </summary>
    /// <param name="beatDuration">Duration of the current beat.</param>
    /// <returns>Coroutine for preparation animation.</returns>
    private IEnumerator ExecutePreparationPhase(float beatDuration)
    {
        _isAttacking = true;
        SetState(UnitState.Attacking);
        if (AttackSystem is BossAttackSystem bossAttackSystem)
        {
            yield return StartCoroutine(bossAttackSystem.PerformPreparationAnimation(transform, beatDuration));
        }
        _isAttacking = false;
    }

    /// <summary>
    /// Executes the impact phase of the boss's attack sequence.
    /// </summary>
    /// <param name="beatDuration">Duration of the current beat.</param>
    /// <returns>Coroutine for impact animation and damage.</returns>
    private IEnumerator ExecuteImpactPhase(float beatDuration)
    {
        _isAttacking = true;
        if (AttackSystem is BossAttackSystem bossAttackSystem)
        {
            yield return StartCoroutine(bossAttackSystem.PerformImpactAnimation(transform, Attack, this, beatDuration));
        }
        _isAttacking = false;
    }

    /// <summary>
    /// Executes the movement phase of the boss's action sequence.
    /// </summary>
    /// <returns>Coroutine for movement logic.</returns>
    private IEnumerator ExecuteMovePhase()
    {
        Tile nextCentralTile = GetNextTileTowardsDestination();
        if (nextCentralTile != null && bossMovementSystem != null && bossMovementSystem.IsDestinationAreaClear(nextCentralTile, this))
        {
            yield return StartCoroutine(AdvanceOneRow());
        }
        else
        {
            if (enableVerboseLogging) Debug.Log("[BossUnit] Mouvement bloqué, le boss attend le prochain cycle.");
        }
        SetState(UnitState.Idle);
    }

    /// <summary>
    /// Handles interrupt states such as stun or reaching the target location.
    /// </summary>
    /// <returns>True if an interrupt state is active; otherwise, false.</returns>
    private bool HandleInterruptStates()
    {
        if (_isStunned)
        {
            _stunBeatCounter--;
            if (_stunBeatCounter <= 0) _isStunned = false;
            return true;
        }

        if (IsAtTargetLocation())
        {
            _beatCounter++;
			Debug.Log($"[BossUnit] Le boss a atteint sa destination finale : {FinalDestinationCoordinates}.");

            if (_beatCounter >= MovementDelay)
            {
                _beatCounter = 0;
                DestroyTargetBuilding();
				Debug.Log("[BossUnit] Le bâtiment cible a été détruit.");
            }
            return true;
        }

        return false;
    }
    
    #region Unchanged Methods
    public void TakePercentageDamage(float percentage)
    {
        if (percentage <= 0) return;
        int damageToTake = Mathf.RoundToInt(_maximumHealth * (percentage / 100f));
        Health -= damageToTake;

        if (Health <= 0)
        {
            Health = 0;
            Die();
        }
    }

    protected override void Die()
    {
        if (enableVerboseLogging) Debug.Log($"[BossUnit] Le boss a été vaincu ! Lancement de la séquence de destruction.");
        UnreserveAllTiles();
        StopAllCoroutines();
        this.enabled = false;

        if (destructionVFX != null)
        {
            Instantiate(destructionVFX, transform.position, Quaternion.identity);
        }
        if (audioSource != null && destructionSFX != null)
        {
            AudioSource.PlayClipAtPoint(destructionSFX, transform.position);
        }
        base.Die();
    }

    public override void TakeDamage(int damage, Unit attacker = null)
    {
        if (_isStunned) return;
        _currentHitCounter++;
        if (_currentHitCounter >= HitsToStun)
        {
            _currentActionState = BossActionState.Waiting; // Interrompt l'attaque en cours si stun
            StopAllCoroutines();
            IsMoving = false;
            _isAttacking = false;
            SetState(UnitState.Idle);
            LeanTween.cancel(gameObject); // Arrête les animations LeanTween
            StartCoroutine(StunSequence());
        }
    }

    private IEnumerator StunSequence()
    {
        if (enableVerboseLogging) Debug.Log($"[BossUnit] ÉTOURDI pour {StunDurationInBeats} battements !");
        _isStunned = true;
        _stunBeatCounter = StunDurationInBeats;
        _currentHitCounter = 0;
        transform.position = occupiedTile.transform.position + Vector3.up * yOffset; // Repositionne le boss au sol

        GameObject stunEffectInstance = null;
        if (stunVFX != null)
        {
            Vector3 spawnPosition = transform.position + new Vector3(0, stunVFX_Y_Offset, 0);
            stunEffectInstance = Instantiate(stunVFX, spawnPosition, Quaternion.identity);
        }
        if (audioSource != null && stunSFX != null)
        {
            audioSource.PlayOneShot(stunSFX);
        }

        yield return new WaitUntil(() => !_isStunned);
        if (enableVerboseLogging) Debug.Log("[BossUnit] L'étourdissement est terminé.");
        if (stunEffectInstance != null) Destroy(stunEffectInstance);
    }
    
    protected override IEnumerator Start()
    {
        yield return base.Start();
        bossMovementSystem = GetComponent<BossMovementSystem>();
        audioSource = GetComponent<AudioSource>();
        _maximumHealth = Health;
        if (bossMovementSystem == null) Debug.LogError("Le composant BossMovementSystem est manquant !");
        yield return new WaitForEndOfFrame();
        OccupyAdjacentTiles();
    }
    
    private void DestroyTargetBuilding()
    {
        if (TargetBuildingToDestroy == null)
        {
            TargetBuildingToDestroy = FindClosestAllyBuilding();
        }

        if (TargetBuildingToDestroy != null)
        {
            if (enableVerboseLogging) Debug.Log($"[BossUnit] Destination atteinte ! Destruction de la cible principale : {TargetBuildingToDestroy.name}");
        
            // On détruit directement l'objet référencé.
            TargetBuildingToDestroy.CallDie();
        }
        else
        {
            // Message d'erreur si aucune cible n'est trouvée, même après recherche automatique
            Debug.LogError("[BossUnit] Le boss a atteint sa destination, mais aucun bâtiment allié n'a pu être trouvé à proximité pour être détruit !");
        }
    }
	
    private IEnumerator AdvanceOneRow()
    {
        if (IsMoving || IsAtTargetLocation() || _isStunned) yield break;
        Tile nextCentralTile = GetNextTileTowardsDestination();
        if (nextCentralTile == null) yield break;
        if (bossMovementSystem == null || !bossMovementSystem.IsDestinationAreaClear(nextCentralTile, this))
        {
            SetState(UnitState.Idle);
            yield break;
        }
        IsMoving = true;
        SetState(UnitState.Moving);
        UnreserveAllTiles();
        Vector3 startPos = transform.position;
        Vector3 endPos = nextCentralTile.transform.position + Vector3.up * yOffset;
        yield return StartCoroutine(MovementSystem.MoveToTile(transform, startPos, endPos, 0.45f));
        AttachToTile(nextCentralTile);
        OccupyAdjacentTiles();
        IsMoving = false;
        SetState(UnitState.Idle);
    }
    
    public override List<Tile> GetOccupiedTiles()
    {
        if (occupiedTile == null || HexGridManager.Instance == null) return new List<Tile>();
        return HexGridManager.Instance.GetTilesWithinRange(occupiedTile.column, occupiedTile.row, 1);
    }
    private void OccupyAdjacentTiles()
    {
        List<Tile> tilesToOccupy = GetOccupiedTiles();
        if (TileReservationController.Instance == null) return;
        foreach (Tile tile in tilesToOccupy)
        {
            if (tile != null) TileReservationController.Instance.TryReserveTile(new Vector2Int(tile.column, tile.row), this);
        }
    }
    private void UnreserveAllTiles()
    {
        List<Tile> tilesToUnreserve = GetOccupiedTiles();
        if (TileReservationController.Instance == null) return;
        foreach (var tile in tilesToUnreserve)
        {
            if (tile != null) TileReservationController.Instance.ReleaseTileReservation(new Vector2Int(tile.column, tile.row), this);
        }
    }
    protected new void UpdateFacingDirectionSafe()
    {
        if (!_isAttacking && currentState != UnitState.Capturing && !IsMoving && !_isStunned) UpdateFacingDirection();
    }
    protected new void UpdateFacingDirection()
    {
        if (!TargetPosition.HasValue || occupiedTile == null || HexGridManager.Instance == null) return;
        Tile finalTargetTile = HexGridManager.Instance.GetTileAt(TargetPosition.Value.x, TargetPosition.Value.y);
        if (finalTargetTile == null || IsAtTargetLocation()) return;
        Tile nextStepTile = GetNextTileTowardsDestination();
        Tile tileToFace = nextStepTile ?? finalTargetTile;
        if (tileToFace != null && tileToFace != occupiedTile)
        {
            Vector3 direction = tileToFace.transform.position - transform.position;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion yRotation = Quaternion.LookRotation(direction);
                Quaternion finalRotation = Quaternion.Euler(270, yRotation.eulerAngles.y, 0);
                transform.rotation = finalRotation;
            }
        }
    }
    public List<Tile> GetAttackablePerimeterTiles()
    {
        if (HexGridManager.Instance == null || TileReservationController.Instance == null) return new List<Tile>();
        List<Tile> occupied = GetOccupiedTiles();
        HashSet<Tile> perimeterTiles = new HashSet<Tile>();
        foreach (Tile occupiedTile in occupied)
        {
            List<Tile> neighbors = HexGridManager.Instance.GetAdjacentTiles(occupiedTile);
            foreach (Tile neighbor in neighbors)
            {
                if (neighbor != null) perimeterTiles.Add(neighbor);
            }
        }
        foreach (Tile occupiedTile in occupied)
        {
            if (perimeterTiles.Contains(occupiedTile)) perimeterTiles.Remove(occupiedTile);
        }
        List<Tile> finalAttackableTiles = new List<Tile>();
        foreach (Tile perimeterTile in perimeterTiles)
        {
            if (!TileReservationController.Instance.IsTileReserved(new Vector2Int(perimeterTile.column, perimeterTile.row)))
            {
                finalAttackableTiles.Add(perimeterTile);
            }
        }
        return finalAttackableTiles;
    }
    
/// <summary>
/// Finds the closest allied building (PlayerBuilding) to the boss.
/// Used as a fallback if TargetBuildingToDestroy is not assigned.
/// </summary>
/// <returns>The closest PlayerBuilding if found; otherwise, null.</returns>
private PlayerBuilding FindClosestAllyBuilding()
{
    if (enableVerboseLogging) Debug.Log("[BossUnit] Automatically searching for the closest allied building...");
    // Finds all PlayerBuilding objects in the scene
    PlayerBuilding[] allPlayerBuildings = FindObjectsByType<PlayerBuilding>(FindObjectsSortMode.None);
    if (allPlayerBuildings.Length == 0)
    {
        Debug.LogWarning("[BossUnit] No PlayerBuilding found in the scene!");
        return null;
    }
    PlayerBuilding closestBuilding = null;
    float closestDistance = float.MaxValue;
    Vector3 bossPosition = transform.position;
    foreach (PlayerBuilding building in allPlayerBuildings)
    {
        if (building == null || building.gameObject == null) continue;
        // Ensure the building belongs to the Player team
        if (building.Team != TeamType.Player) continue;
        float distance = Vector3.Distance(bossPosition, building.transform.position);
        if (distance < closestDistance)
        {
            closestDistance = distance;
            closestBuilding = building;
        }
    }
    if (closestBuilding != null)
    {
        if (enableVerboseLogging) Debug.Log($"[BossUnit] Closest allied building found: {closestBuilding.name} at {closestDistance:F2} units distance.");
    }
    else
    {
        Debug.LogWarning("[BossUnit] No valid allied building found!");
    }

    return closestBuilding;
}
    #endregion
}