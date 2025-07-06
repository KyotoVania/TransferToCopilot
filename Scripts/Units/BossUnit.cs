// Fichier: Scripts2/Units/BossUnit.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ScriptableObjects;

[RequireComponent(typeof(AudioSource))]
public class BossUnit : EnemyUnit
{
    // --- L'INTERRUPTEUR ---
    protected override bool IsHardcoded => true;

    [Header("Boss Settings")]
    public Vector2Int FinalDestinationCoordinates;

    [Header("Stun Mechanic")]
    [Tooltip("Nombre de coups reçus avant que le boss ne soit étourdi.")]
    public int HitsToStun = 10;
    [Tooltip("Durée de l'étourdissement en nombre de battements (beats).")]
    public int StunDurationInBeats = 8;

    [Header("Stun Effects")]
    [Tooltip("Le préfabriqué de l'effet visuel à instancier lors de l'étourdissement.")]
    [SerializeField] private GameObject stunVFX;
    [Tooltip("Effet sonore à jouer lorsque le boss est étourdi (Optionnel).")]
    [SerializeField] private AudioClip stunSFX;
    [Tooltip("Le décalage en hauteur (axe Y) pour l'effet visuel d'étourdissement.")]
    [SerializeField] private float stunVFX_Y_Offset = 2.5f;

    // NOUVEAU: Effets pour la destruction du boss
    [Header("Destruction Effects")]
    [Tooltip("Le préfabriqué de l'effet visuel à instancier lors de la destruction du boss.")]
    [SerializeField] private GameObject destructionVFX;
    [Tooltip("Effet sonore à jouer lorsque le boss est détruit (Optionnel).")]
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

    // --- GESTION DES DÉGÂTS ET DE LA MORT ---

    /// <summary>
    /// Prend des dégâts basés sur un pourcentage de la vie maximale.
    /// </summary>
    public void TakePercentageDamage(float percentage)
    {
        if (percentage <= 0) return;
        int damageToTake = Mathf.RoundToInt(_maximumHealth * (percentage / 100f));
        Health -= damageToTake;

        if (Health <= 0)
        {
            Health = 0;
            Die(); // Le boss est vaincu !
        }
    }

    /// <summary>
    /// NOUVEAU: Gère la séquence de destruction du boss.
    /// </summary>
    protected override void Die()
    {
        if (enableVerboseLogging) Debug.Log($"[BossUnit] Le boss a été vaincu ! Lancement de la séquence de destruction.");

        // On libère les tuiles occupées pour éviter les problèmes
        UnreserveAllTiles();

        // On s'assure que le boss ne peut plus rien faire
        StopAllCoroutines();
        this.enabled = false;

        // On joue les effets de destruction
        if (destructionVFX != null)
        {
            Instantiate(destructionVFX, transform.position, Quaternion.identity);
        }
        if (audioSource != null && destructionSFX != null)
        {
            // On joue le son à la position du boss, pour qu'il ne soit pas coupé lors de la destruction de l'objet
            AudioSource.PlayClipAtPoint(destructionSFX, transform.position);
        }

        // On appelle la méthode de base qui s'occupe de détruire le GameObject
        base.Die();
    }

    /// <summary>
    /// Les attaques des unités ne font que contribuer à l'étourdissement.
    /// </summary>
    public override void TakeDamage(int damage, Unit attacker = null)
    {
        if (_isStunned) return;
        _currentHitCounter++;
        if (_currentHitCounter >= HitsToStun)
        {
            StopAllCoroutines();
            IsMoving = false;
            SetState(UnitState.Idle);
            StartCoroutine(StunSequence());
        }
    }

    private IEnumerator StunSequence()
    {
        if (enableVerboseLogging) Debug.Log($"[BossUnit] ÉTOURDI pour {StunDurationInBeats} battements !");

        _isStunned = true;
        _stunBeatCounter = StunDurationInBeats;
        _currentHitCounter = 0;

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

    // --- GESTION DU RYTHME ---
    protected override void OnRhythmBeat(float beatDuration)
    {
        if (_isStunned)
        {
            _stunBeatCounter--;
            if (_stunBeatCounter <= 0) _isStunned = false;
            return;
        }

        // MISE À JOUR: Logique pour détruire le bâtiment cible
        if (IsAtTargetLocation())
        {
            _beatCounter++;
            if (_beatCounter >= MovementDelay)
            {
                _beatCounter = 0;
                DestroyTargetBuilding();
            }
            return; // On ne fait rien d'autre si on a atteint la destination
        }

        if (currentState == UnitState.Idle) UpdateFacingDirectionSafe();
        if (IsMoving) return;
        int moveDelay = MovementDelay;
        if (moveDelay <= 0) return;

        _beatCounter++;
        if (_beatCounter == moveDelay - 2)
        {
            if (AttackSystem != null)
            {
                StopCoroutine(nameof(AttackSystem.PerformAttack));
                SetState(UnitState.Attacking);
                StartCoroutine(AttackSystem.PerformAttack(transform, null, Attack, beatDuration));
            }
        }
        else if (_beatCounter >= moveDelay)
        {
            _beatCounter = 0;
            StartCoroutine(AdvanceOneRow());
        }
    }

    // --- INITIALISATION ET CYCLES DE VIE ---
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

    protected override void OnEnable()
    {
        if (MusicManager.Instance != null) MusicManager.Instance.OnBeat += OnRhythmBeat;
    }

    protected override void OnDisable()
    {
        if (MusicManager.Instance != null) MusicManager.Instance.OnBeat -= OnRhythmBeat;
    }

    // --- MOUVEMENT ET ACTION FINALE ---

    /// <summary>
    /// NOUVEAU: Trouve et détruit le bâtiment sur la tuile de destination finale.
    /// </summary>
    private void DestroyTargetBuilding()
    {
        if (!TargetPosition.HasValue) return;

        Tile targetTile = HexGridManager.Instance.GetTileAt(TargetPosition.Value.x, TargetPosition.Value.y);
        if (targetTile != null)
        {
            // Note: On suppose que la classe Tile a un moyen de trouver le bâtiment qui l'occupe.
            // "GetOccupyingBuilding()" est une supposition, vous devrez peut-être l'adapter à votre code.
            Building targetBuilding = ((Unit)this).targetBuilding;

            if (targetBuilding != null)
            {
                if (enableVerboseLogging) Debug.Log($"[BossUnit] Destination atteinte ! Destruction du bâtiment : {targetBuilding.name}");

                // Ici, vous pourriez jouer un effet d'explosion sur le bâtiment avant de le détruire.
                // Pour l'instant, nous le détruisons simplement.
                Destroy(targetBuilding.gameObject);

                // Le boss a accompli son objectif, vous pourriez déclencher la défaite du joueur ici.
                // Par exemple: GameManager.Instance.EndGame(GameResult.Defeat);
            }
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

    // --- GESTION DES TUILES (inchangé) ---
    #region Tile Management
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
    #endregion
}