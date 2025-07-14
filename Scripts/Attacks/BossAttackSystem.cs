using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implements the boss's area-of-effect (AoE) attack, including animations and damage logic.
/// </summary>
public class BossAttackSystem : MonoBehaviour, IAttack
{
    [Header("Attack Settings")]
    /// <summary>
    /// The visual effect to play during the attack (e.g., a shockwave). Optional.
    /// </summary>
    [Tooltip("Visual effect to play on attack (e.g., a shockwave). Optional.")]
    [SerializeField] private GameObject attackVFX;
    /// <summary>
    /// The knockback distance in number of tiles.
    /// </summary>
    [Tooltip("The knockback distance in number of tiles.")]
    [SerializeField] private int knockbackDistance = 2;
    /// <summary>
    /// Fixed rotation for the VFX (to correct the boss's orientation).
    /// </summary>
    [Tooltip("Fixed rotation for the VFX (to correct the boss's orientation)")]
    [SerializeField] private Vector3 vfxRotation = Vector3.zero;

    [Header("Animation Settings")]
    /// <summary>
    /// The height of the jump during the preparation animation.
    /// </summary>
    [Tooltip("The height of the jump during preparation.")]
    [SerializeField] private float jumpHeight = 1.5f;
    /// <summary>
    /// The scale multiplier for the squash effect (X, Y, Z).
    /// </summary>
    [Tooltip("Scale multiplier for the squash effect (X, Y, Z).")]
    [SerializeField] private Vector3 squashMultiplier = new Vector3(1.2f, 0.8f, 1.2f);
    /// <summary>
    /// Defines the speed of the impact animation. 1 = 100% of beat duration, 0.2 = 20% (very fast).
    /// </summary>
    [Tooltip("Defines the speed of the impact animation. 1 = 100% of beat duration, 0.2 = 20% (very fast).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float impactAnimationSpeed = 0.25f;
    /// <summary>
    /// The easing type for the preparation animation.
    /// </summary>
    [Tooltip("Easing type for the preparation animation.")]
    [SerializeField] private LeanTweenType prepEase = LeanTweenType.easeOutQuad;
    /// <summary>
    /// The easing type for the impact animation.
    /// </summary>
    [Tooltip("Easing type for the impact animation.")]
    [SerializeField] private LeanTweenType impactEase = LeanTweenType.easeInCubic;

    private Vector3 _originalScale;
    private Vector3 _originalPosition;
    
    /// <summary>
    /// Performs the preparation animation for the attack.
    /// </summary>
    /// <param name="attacker">The transform of the attacker.</param>
    /// <param name="beatDuration">The duration of a music beat for synchronization.</param>
    /// <returns>An IEnumerator for the coroutine.</returns>
    public IEnumerator PerformPreparationAnimation(Transform attacker, float beatDuration)
    {
        _originalScale = attacker.localScale;
        _originalPosition = attacker.position;
        LeanTween.cancel(attacker.gameObject);

        Vector3 targetSquashScale = new Vector3(
            _originalScale.x * squashMultiplier.x,
            _originalScale.y * squashMultiplier.y,
            _originalScale.z * squashMultiplier.z
        );

        LTSeq sequence = LeanTween.sequence();

        float halfBeat = beatDuration / 2f;
        sequence.append(LeanTween.scale(attacker.gameObject, targetSquashScale, halfBeat).setEase(prepEase));
        sequence.append(LeanTween.moveY(attacker.gameObject, _originalPosition.y + jumpHeight, halfBeat).setEase(prepEase));
        sequence.append(LeanTween.scale(attacker.gameObject, _originalScale, halfBeat).setEase(prepEase));
        
        yield return new WaitForSeconds(beatDuration);
    }
    
    /// <summary>
    /// Performs the impact animation and applies damage.
    /// </summary>
    /// <param name="attacker">The transform of the attacker.</param>
    /// <param name="damage">The amount of damage to apply.</param>
    /// <param name="bossUnit">The boss unit component.</param>
    /// <param name="beatDuration">The duration of a music beat for synchronization.</param>
    /// <returns>An IEnumerator for the coroutine.</returns>
    public IEnumerator PerformImpactAnimation(Transform attacker, int damage, Unit bossUnit, float beatDuration)
    {
        if (bossUnit == null) yield break;

        LeanTween.cancel(attacker.gameObject);
        
        float actualAnimationDuration = beatDuration * impactAnimationSpeed;
        
        LeanTween.moveY(attacker.gameObject, _originalPosition.y, actualAnimationDuration)
            .setEase(impactEase)
            .setOnComplete(() => {
                if (attackVFX != null)
                {
                    Instantiate(attackVFX, attacker.position, Quaternion.Euler(vfxRotation));
                }

                Vector3 targetImpactScale = new Vector3(
                    _originalScale.x * squashMultiplier.x,
                    _originalScale.y * squashMultiplier.y,
                    _originalScale.z * squashMultiplier.z
                );
                
                LeanTween.scale(attacker.gameObject, targetImpactScale, 0.15f)
                         .setEase(LeanTweenType.easeShake)
                         .setOnComplete(() => {
                             LeanTween.scale(attacker.gameObject, _originalScale, 0.15f);
                         });
            });
        
        ApplyAoeDamage(damage, bossUnit);
        
        yield return new WaitForSeconds(beatDuration);
    }
    
    /// <summary>
    /// Applies AoE damage and knockback to units in the attack range.
    /// </summary>
    /// <param name="damage">The amount of damage to apply.</param>
    /// <param name="bossUnit">The boss unit component.</param>
    private void ApplyAoeDamage(int damage, Unit bossUnit)
    {
        Tile centralTile = bossUnit.GetOccupiedTile();
        if (centralTile == null) return;

        List<Tile> tilesInAoERange = HexGridManager.Instance.GetTilesWithinRange(centralTile.column, centralTile.row, 2);

        foreach (var tile in tilesInAoERange)
        {
            if (tile.currentUnit != null && tile.currentUnit is AllyUnit allyToPush)
            {
                allyToPush.TakeDamage(damage, bossUnit);
                if (allyToPush.Health > 0)
                {
                    StartCoroutine(KnockbackUnit(bossUnit, allyToPush));
                }
            }
        }
    }
    
    #region Unchanged Methods
    /// <summary>
    /// Performs the full attack sequence, including preparation and impact.
    /// </summary>
    /// <param name="attacker">The transform of the attacker.</param>
    /// <param name="target">The transform of the target (not used in this AoE attack).</param>
    /// <param name="damage">The amount of damage to apply.</param>
    /// <param name="animationDuration">The total duration of the attack animation.</param>
    /// <returns>An IEnumerator for the coroutine.</returns>
    public IEnumerator PerformAttack(Transform attacker, Transform target, int damage, float animationDuration)
    {
        yield return StartCoroutine(PerformPreparationAnimation(attacker, animationDuration/2));
        yield return new WaitForSeconds(animationDuration * 0.5f);
        yield return StartCoroutine(PerformImpactAnimation(attacker, damage, attacker.GetComponent<Unit>(), animationDuration/2));
    }
    
    /// <summary>
    /// Checks if the attack can be performed.
    /// </summary>
    /// <param name="attacker">The transform of the attacker.</param>
    /// <param name="target">The transform of the target.</param>
    /// <param name="attackRange">The attack range.</param>
    /// <returns>True, as the boss can always perform this attack.</returns>
    public bool CanAttack(Transform attacker, Transform target, float attackRange) => true;

    /// <summary>
    /// Knocks back a unit away from the boss.
    /// </summary>
    /// <param name="boss">The boss unit.</param>
    /// <param name="unitToPush">The unit to knock back.</param>
    /// <returns>An IEnumerator for the coroutine.</returns>
    private IEnumerator KnockbackUnit(Unit boss, AllyUnit unitToPush)
    {
        Tile startTile = unitToPush.GetOccupiedTile();
        if (startTile == null) yield break;
        
        Tile currentTileForPathfinding = startTile;
        Tile destinationTile = startTile;

        for (int i = 0; i < knockbackDistance; i++)
        {
            Tile nextTileAway = HexGridManager.Instance.GetNeighborAwayFromTarget(
                currentTileForPathfinding.column, currentTileForPathfinding.row,
                boss.GetOccupiedTile().column, boss.GetOccupiedTile().row
            );

            if (nextTileAway != null && !nextTileAway.IsOccupied && 
                !TileReservationController.Instance.IsTileReservedByOtherUnit(new Vector2Int(nextTileAway.column, nextTileAway.row), unitToPush))
            {
                destinationTile = nextTileAway;
                currentTileForPathfinding = nextTileAway;
            }
            else
            {
                break;
            }
        }

        if (destinationTile != startTile)
        {
            yield return unitToPush.StartCoroutine(unitToPush.MoveToTile(destinationTile));
        }
    }
    #endregion
}
