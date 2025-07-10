using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossAttackSystem : MonoBehaviour, IAttack
{
    [Header("Attack Settings")]
    [Tooltip("Effet visuel à jouer lors de l'attaque (ex: une onde de choc). Optionnel.")]
    [SerializeField] private GameObject attackVFX;
    [Tooltip("La distance de repoussement en nombre de cases.")]
    [SerializeField] private int knockbackDistance = 2;
    [Tooltip("Rotation fixe pour le VFX (pour corriger l'orientation du boss)")]
    [SerializeField] private Vector3 vfxRotation = Vector3.zero;

    [Header("Animation Settings")]
    [Tooltip("Hauteur du saut pendant la préparation.")]
    [SerializeField] private float jumpHeight = 1.5f;
    [Tooltip("Multiplicateur d'échelle pour la compression (X, Y, Z).")]
    [SerializeField] private Vector3 squashMultiplier = new Vector3(1.2f, 0.8f, 1.2f);
    [Tooltip("Définit la vitesse de l'animation de chute. 1 = 100% de la durée du beat, 0.2 = 20% (très rapide).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float impactAnimationSpeed = 0.25f;
    [Tooltip("Type d'assouplissement (easing) pour la préparation.")]
    [SerializeField] private LeanTweenType prepEase = LeanTweenType.easeOutQuad;
    [Tooltip("Type d'assouplissement (easing) pour l'impact.")]
    [SerializeField] private LeanTweenType impactEase = LeanTweenType.easeInCubic;

    private Vector3 _originalScale;
    private Vector3 _originalPosition;
    
    /// <summary>
    /// MODIFIÉ : Accepte maintenant la durée d'un beat pour une synchronisation parfaite.
    /// </summary>
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

        // L'animation totale dure maintenant exactement un beat.
        float halfBeat = beatDuration / 2f;
        sequence.append(LeanTween.scale(attacker.gameObject, targetSquashScale, halfBeat).setEase(prepEase));
        sequence.append(LeanTween.moveY(attacker.gameObject, _originalPosition.y + jumpHeight, halfBeat).setEase(prepEase));
        sequence.append(LeanTween.scale(attacker.gameObject, _originalScale, halfBeat).setEase(prepEase));
        
        yield return new WaitForSeconds(beatDuration);
    }
    
    /// <summary>
    /// MODIFIÉ : Accepte maintenant la durée d'un beat.
    /// </summary>
    public IEnumerator PerformImpactAnimation(Transform attacker, int damage, Unit bossUnit, float beatDuration)
    {
        if (bossUnit == null) yield break;

        LeanTween.cancel(attacker.gameObject);
        
        // --- MODIFIÉ : L'animation de chute est maintenant plus rapide ---
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
        
        // Appliquer les dégâts au début de l'impact
        ApplyAoeDamage(damage, bossUnit);
        
        // La coroutine dure toujours exactement un beat pour maintenir le rythme global.
        yield return new WaitForSeconds(beatDuration);
    }
    
    // Logique de dégâts et knockback extraite dans sa propre méthode pour plus de clarté
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
    
    // Le reste du script (KnockbackUnit, PerformAttack, CanAttack) reste inchangé...
    #region Unchanged Methods
    public IEnumerator PerformAttack(Transform attacker, Transform target, int damage, float animationDuration)
    {
        yield return StartCoroutine(PerformPreparationAnimation(attacker, animationDuration/2));
        yield return new WaitForSeconds(animationDuration * 0.5f);
        yield return StartCoroutine(PerformImpactAnimation(attacker, damage, attacker.GetComponent<Unit>(), animationDuration/2));
    }
    
    public bool CanAttack(Transform attacker, Transform target, float attackRange) => true;

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