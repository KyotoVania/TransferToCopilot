using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BossMovementSystem : MonoBehaviour, IMovement
{
    public IEnumerator MoveToTile(Transform unitTransform, Vector3 startPos, Vector3 endPos, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            unitTransform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        unitTransform.position = endPos;
    }

    /// <summary>
    /// Vérifie si la zone de destination (7 cases) est entièrement libre pour le boss.
    /// </summary>
    public bool IsDestinationAreaClear(Tile nextCentralTile, Unit bossUnit)
    {
        if (nextCentralTile == null) return false;

        List<Tile> destinationTiles = HexGridManager.Instance.GetTilesWithinRange(nextCentralTile.column, nextCentralTile.row, 1);

        if (destinationTiles.Count < 7)
        {
            Debug.LogWarning("La zone de destination du boss est en bord de carte et n'est pas complète. Mouvement interdit.");
            return false;
        }

        foreach (var tile in destinationTiles)
        {
            Vector2Int tilePos = new Vector2Int(tile.column, tile.row);

      
            bool isOccupiedByOther = tile.currentUnit != null && tile.currentUnit != bossUnit;
            bool isReservedByOther = TileReservationController.Instance.IsTileReservedByOtherUnit(tilePos, bossUnit);

            if (isOccupiedByOther || isReservedByOther)
            {
                Debug.Log($"Mouvement du boss bloqué par la case {tilePos} qui est occupée ou réservée par un autre.");
                return false;
            }
        }

        return true; // Toutes les cases sont libres !
    }
}