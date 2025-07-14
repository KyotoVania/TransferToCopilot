using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ScriptableObjects;

[RequireComponent(typeof(AudioSource))]
public class BossUnit : EnemyUnit
{
    private enum BossActionState { Waiting, Preparing, Impacting, Moving }
    private BossActionState _currentActionState = BossActionState.Waiting;

    // --- L'INTERRUPTEUR ---
    protected override bool IsHardcoded => true;

    [Header("Boss Settings")]
    public Vector2Int FinalDestinationCoordinates;
	[Tooltip("Faites glisser ici le bâtiment principal que le boss doit détruire à la fin.")]
    public Building TargetBuildingToDestroy;
    
	[Header("Stun Mechanic")]
    [Tooltip("Nombre de coups reçus avant que le boss ne soit étourdi.")]
    public int HitsToStun = 10;
    [Tooltip("Durée de l'étourdissement en nombre de battements (beats).")]
    public int StunDurationInBeats = 8;

    [Header("Stun Effects")]
    [SerializeField] private GameObject stunVFX;
    [SerializeField] private AudioClip stunSFX;
    [SerializeField] private float stunVFX_Y_Offset = 2.5f;

    [Header("Destruction Effects")]
    [SerializeField] private GameObject destructionVFX;
    [SerializeField] private AudioClip destructionSFX;
    
    // --- Variables privées ---
    private int _currentHitCounter = 0;
    private int _stunBeatCounter = 0;
    private bool _isStunned = false;
    private int _maximumHealth = 0;

    // --- Références aux composants ---
    private BossMovementSystem bossMovementSystem;
    private AudioSource audioSource;
    protected override Vector2Int? TargetPosition => FinalDestinationCoordinates;

    // --- GESTION DU RYTHME (LOGIQUE PRINCIPALE REFACTORISÉE) ---
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
    
    // --- PHASES D'ACTION EN COROUTINES ---

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

    private IEnumerator ExecuteImpactPhase(float beatDuration)
    {
        _isAttacking = true;
        if (AttackSystem is BossAttackSystem bossAttackSystem)
        {
            yield return StartCoroutine(bossAttackSystem.PerformImpactAnimation(transform, Attack, this, beatDuration));
        }
        _isAttacking = false;
    }

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

    // --- GESTION DES ÉTATS ET AUTRES MÉTHODES (peu de changements ici) ---

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
    
    // Le reste du script (Die, TakeDamage, StunSequence, etc.) reste inchangé.
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
/// Trouve le bâtiment allié (PlayerBuilding) le plus proche du boss.
/// Fix sauvage pour les cas où TargetBuildingToDestroy n'est pas assigné.
/// </summary>
private PlayerBuilding FindClosestAllyBuilding()
{
    if (enableVerboseLogging) Debug.Log("[BossUnit] Recherche automatique du bâtiment allié le plus proche...");
    
    // Trouver tous les PlayerBuilding dans la scène
    PlayerBuilding[] allPlayerBuildings = FindObjectsByType<PlayerBuilding>(FindObjectsSortMode.None);
    
    if (allPlayerBuildings.Length == 0)
    {
        Debug.LogWarning("[BossUnit] Aucun PlayerBuilding trouvé dans la scène !");
        return null;
    }

    PlayerBuilding closestBuilding = null;
    float closestDistance = float.MaxValue;
    Vector3 bossPosition = transform.position;

    foreach (PlayerBuilding building in allPlayerBuildings)
    {
        if (building == null || building.gameObject == null) continue;
        
        // Vérifier que le bâtiment est bien de l'équipe Player
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
        if (enableVerboseLogging) Debug.Log($"[BossUnit] Bâtiment allié le plus proche trouvé : {closestBuilding.name} à {closestDistance:F2} unités de distance.");
    }
    else
    {
        Debug.LogWarning("[BossUnit] Aucun bâtiment allié valide trouvé !");
    }

    return closestBuilding;
}
    #endregion
}