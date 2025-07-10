using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossAttackSystem : MonoBehaviour, IAttack
{
    [Tooltip("Effet visuel à jouer lors de l'attaque (ex: une onde de choc). Optionnel.")]
    [SerializeField] private GameObject attackVFX;
    [Tooltip("La distance de repoussement en nombre de cases.")]
    [SerializeField] private int knockbackDistance = 2;
    [Tooltip("Rotation fixe pour le VFX (pour corriger l'orientation du boss)")]
    [SerializeField] private Vector3 vfxRotation = Vector3.zero;

    public bool CanAttack(Transform attacker, Transform target, float attackRange)
    {
        // La décision d'attaquer est gérée par le boss, pas par la portée.
        return true;
    }

    public IEnumerator PerformAttack(Transform attacker, Transform target, int damage, float animationDuration)
    {
        Unit bossUnit = attacker.GetComponent<Unit>();
        if (bossUnit == null) yield break;

        if (attackVFX != null)
        {
            // Utiliser une rotation fixe au lieu de Quaternion.identity pour corriger l'orientation
            Quaternion vfxQuat = Quaternion.Euler(vfxRotation);
			Debug.Log($"[{bossUnit.name}] attaque avec l'effet visuel. Rotation: {vfxQuat.eulerAngles}");
            Instantiate(attackVFX, attacker.position, vfxQuat);
        }

        // On attend un peu pour la synchronisation visuelle
        yield return new WaitForSeconds(animationDuration * 0.3f);

        Tile centralTile = bossUnit.GetOccupiedTile();
        if (centralTile == null) yield break;

        // On récupère les cases adjacentes au corps du boss (rayon 2)
        List<Tile> tilesInAoERange = HexGridManager.Instance.GetTilesWithinRange(centralTile.column, centralTile.row, 2);

        List<Coroutine> knockbackCoroutines = new List<Coroutine>();

        foreach (var tile in tilesInAoERange)
        {
            if (tile.currentUnit != null && tile.currentUnit is AllyUnit)
            {
                AllyUnit allyToPush = tile.currentUnit as AllyUnit;

                // 1. Appliquer les dégâts
                Debug.Log($"[{bossUnit.name}] attaque [{allyToPush.name}].");
                allyToPush.TakeDamage(damage, bossUnit);

                // 2. Tenter de repousser l'unité
                if (allyToPush != null && allyToPush.Health > 0) // On ne repousse pas une unité morte
                {
                    knockbackCoroutines.Add(StartCoroutine(KnockbackUnit(bossUnit, allyToPush)));
                }
            }
        }

        foreach (var coroutine in knockbackCoroutines)
        {
            yield return coroutine;
        }

        yield return new WaitForSeconds(animationDuration * 0.7f);
    }

    /// <summary>
    /// Gère la logique de repoussement pour une unité spécifique.
    /// </summary>
    private IEnumerator KnockbackUnit(Unit boss, AllyUnit unitToPush)
    {
        Tile startTile = unitToPush.GetOccupiedTile();
        Tile currentTileForPathfinding = startTile;
        Tile destinationTile = startTile;

        if (startTile == null) yield break;

        // On cherche une case de destination valide en s'éloignant du boss, case par case.
        for (int i = 0; i < knockbackDistance; i++)
        {
            // Trouve le voisin le plus éloigné du boss
            Tile nextTileAway = HexGridManager.Instance.GetNeighborAwayFromTarget(
                currentTileForPathfinding.column, currentTileForPathfinding.row,
                boss.GetOccupiedTile().column, boss.GetOccupiedTile().row
            );

            if (nextTileAway != null && !nextTileAway.IsOccupied &&
                !TileReservationController.Instance.IsTileReservedByOtherUnit(new Vector2Int(nextTileAway.column, nextTileAway.row), unitToPush))
            {
                // Si la case est valide, elle devient notre nouvelle destination potentielle
                destinationTile = nextTileAway;
                currentTileForPathfinding = nextTileAway;
            }
            else
            {
                // Si on rencontre un obstacle, on arrête de chercher plus loin.
                break;
            }
        }

        // Si on a trouvé une nouvelle destination (différente de celle de départ)
        if (destinationTile != startTile)
        {
            Debug.Log($"[{boss.name}] repousse [{unitToPush.name}] vers la case ({destinationTile.column}, {destinationTile.row}).");
            // On lance la coroutine de mouvement de l'unité elle-même.
            // C'est la manière la plus propre de la faire bouger.
            yield return unitToPush.StartCoroutine(unitToPush.MoveToTile(destinationTile));
        }
    }
}