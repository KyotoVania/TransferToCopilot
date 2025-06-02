using UnityEngine;
using System.Collections;
using Sirenix.OdinInspector; // Si vous utilisez Odin Inspector

public class Environment : MonoBehaviour
{
    [Header("Environment Settings")]
    [SerializeField] private bool _isBlocking = false;
    [SerializeField] private float yOffset = 0f; // Décalage vertical pour le positionnement
    [SerializeField] private string environmentName = "Generic Environment";
    [Tooltip("Description of this environment piece")]
    [TextArea(3, 5)]
    [SerializeField] private string description;

    [Header("Visual Settings")]
    [SerializeField] private bool useRandomRotation = false;
    [ShowIf("useRandomRotation")] // Attribut Odin Inspector, optionnel
    [SerializeField] private Vector3 randomRotationRange = new Vector3(0, 360, 0);

    // Référence à la tuile occupée
    protected Tile occupiedTile;
    private bool isAttached = false;

    // Référence aux statistiques (si l'environnement a des propriétés spéciales)
    [InlineEditor(InlineEditorModes.FullEditor)] // Attribut Odin Inspector
    [SerializeField] private EnvironmentStats environmentStats;

    // --- Propriétés Publiques ---
    public bool IsBlocking => _isBlocking;
    public string EnvironmentName => environmentName;
    public string Description => description;
    public EnvironmentStats Stats => environmentStats;

    protected virtual IEnumerator Start()
    {
        // Attendre que HexGridManager soit prêt
        while (HexGridManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Tenter de s'attacher à la tuile la plus proche
        while (!isAttached)
        {
            Tile nearestTile = HexGridManager.Instance.GetClosestTile(transform.position);
            if (nearestTile != null)
            {
                bool tileAvailableForEnvironment = true;
                Vector2Int nearestTilePos = new Vector2Int(nearestTile.column, nearestTile.row);

                // 1. Vérifier si la tuile est physiquement occupée par un bâtiment ou une unité
                if (nearestTile.currentBuilding != null)
                {
                    Debug.LogWarning($"[ENVIRONMENT:{name}] Cannot attach to tile ({nearestTilePos.x},{nearestTilePos.y}), it already has building: {nearestTile.currentBuilding.name}");
                    tileAvailableForEnvironment = false;
                }
                else if (nearestTile.currentUnit != null)
                {
                    Debug.LogWarning($"[ENVIRONMENT:{name}] Cannot attach to tile ({nearestTilePos.x},{nearestTilePos.y}), it already has unit: {nearestTile.currentUnit.name}");
                    tileAvailableForEnvironment = false;
                }
                // 2. Vérifier si la tuile est réservée par une unité via le TileReservationController
                //    (On ne veut pas placer un environnement, surtout bloquant, où une unité prévoit d'aller)
                else if (TileReservationController.Instance != null && TileReservationController.Instance.IsTileReserved(nearestTilePos))
                {
                    Unit reservingUnit = TileReservationController.Instance.GetReservingUnit(nearestTilePos);
                    Debug.LogWarning($"[ENVIRONMENT:{name}] Cannot attach to tile ({nearestTilePos.x},{nearestTilePos.y}), tile is reserved by unit: {reservingUnit?.name ?? "Unknown Unit"}");
                    tileAvailableForEnvironment = false;
                }
                // 3. (Optionnel) Ajouter d'autres conditions, par ex. si l'environnement ne peut être placé que sur certains TileType
                // else if (nearestTile.tileType != TileType.Ground && _isBlocking)
                // {
                //     Debug.LogWarning($"[ENVIRONMENT:{name}] Blocking environment cannot be placed on non-Ground tile ({nearestTilePos.x},{nearestTilePos.y}).");
                //     tileAvailableForEnvironment = false;
                // }


                if (tileAvailableForEnvironment)
                {
                    AttachToTile(nearestTile); // S'attache et notifie la tuile
                    isAttached = true;
                    // Si cet environnement est _isBlocking, la propriété Tile.IsOccupied deviendra true
                    // via Tile.currentEnvironment.IsBlocking. Cela empêchera les unités de la réserver/occuper.
                    break;
                }
                else
                {
                    // La tuile la plus proche n'est pas disponible.
                    // Selon la logique de votre jeu, vous pourriez :
                    // - Détruire cet environnement.
                    // - Le marquer comme "non placé" et le cacher.
                    // - Essayer une autre tuile (nécessiterait une logique de recherche plus complexe).
                    Debug.LogError($"[ENVIRONMENT:{name}] Failed to find a suitable initial tile for attachment near {transform.position}. Environment will not be placed correctly.");
                    // Pour éviter une boucle infinie si mal placé dans l'éditeur :
                    yield return new WaitForSeconds(5f); // Attendre plus longtemps avant de réessayer ou de logguer à nouveau
                }
            }
            yield return new WaitForSeconds(0.2f); // Attendre avant de réessayer si nearestTile était null
        }

        // Appliquer une rotation aléatoire si configuré
        if (useRandomRotation && isAttached)
        {
            ApplyRandomRotation();
        }
    }

    protected void AttachToTile(Tile tile)
    {
        occupiedTile = tile;

        // Se positionner correctement sur la tuile
        transform.position = tile.transform.position + new Vector3(0f, yOffset, 0f);
        transform.SetParent(tile.transform, false); // 'false' pour position locale relative au parent
        transform.localPosition = new Vector3(0f, yOffset, 0f); // Assurer la position locale correcte

        // Notifier la tuile qu'elle a maintenant cet environnement
        AssignToTile(tile);
    }

    // Méthode virtuelle pour que les classes dérivées puissent surcharger si besoin,
    // mais la logique principale est dans Tile.AssignEnvironment
    protected virtual void AssignToTile(Tile tile)
    {
        tile.AssignEnvironment(this);
    }

    protected void ApplyRandomRotation()
    {
        Vector3 randomRot = new Vector3(
            Random.Range(-randomRotationRange.x, randomRotationRange.x),
            Random.Range(-randomRotationRange.y, randomRotationRange.y),
            Random.Range(-randomRotationRange.z, randomRotationRange.z)
        );
        transform.localRotation = Quaternion.Euler(randomRot);
    }

    public virtual void OnDestroy()
    {
        // Lorsque l'environnement est détruit, notifier la tuile pour qu'elle se libère
        if (occupiedTile != null)
        {
            occupiedTile.RemoveEnvironment(); // La tuile mettra son currentEnvironment à null
        }
    }

    public Tile GetOccupiedTile()
    {
        return occupiedTile;
    }

    // Permet de changer dynamiquement si l'environnement bloque le passage
    public void SetBlocking(bool isBlocking)
    {
        if (_isBlocking != isBlocking)
        {
            _isBlocking = isBlocking;
            // Si la tuile existe, notifier qu'elle a peut-être changé d'état d'occupation
            // (Tile.IsOccupied dépendra de _isBlocking)
            occupiedTile?.NotifyManagerOfStateChange();
        }
    }
}