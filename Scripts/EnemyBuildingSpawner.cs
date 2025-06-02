using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Ajouté pour List<Tile>

/// <summary>
/// Permet à un bâtiment ennemi de faire apparaître un prefab (généralement une unité)
/// toutes les X pulsations musicales sur une tuile adjacente disponible.
/// </summary>
public class EnemyBuildingSpawner : MonoBehaviour
{
    [Header("Spawning Settings")]
    [Tooltip("Le prefab à faire apparaître (doit avoir un composant Unit si c'est une unité).")]
    [SerializeField] private GameObject prefabToSpawn;

    [Tooltip("Nombre de battements musicaux entre chaque tentative de spawn.")]
    [SerializeField] private int beatsPerSpawn = 8;

    [Tooltip("Faut-il que le bâtiment appartienne à l'équipe ennemie pour spawner ?")]
    [SerializeField] private bool requireEnemyTeam = true;

    [Header("Spawn Point & Offset")]
    [Tooltip("Point de spawn optionnel. Si non défini, cherche une tuile adjacente.")]
    [SerializeField] private Transform specificSpawnPoint;
    [Tooltip("Décalage en Y appliqué à la position de spawn sur la tuile.")]
    [SerializeField] private float spawnYOffset = 0.5f; // Pour que l'unité apparaisse légèrement au-dessus du sol de la tuile

    [Header("Debugging")]
    [SerializeField] private bool enableDebugLogs = false;

    private Building building; // Référence au composant Building sur cet objet
    private int beatCounter = 0;
    private bool subscribedToBeat = false;

    void Start()
    {
        building = GetComponent<Building>();
        if (building == null)
        {
            Debug.LogError($"[{gameObject.name}] EnemyBuildingSpawner: Composant Building non trouvé ! Le spawner ne fonctionnera pas.", this);
            enabled = false; // Désactiver ce script
            return;
        }

        if (prefabToSpawn == null)
        {
            Debug.LogError($"[{gameObject.name}] EnemyBuildingSpawner: PrefabToSpawn non assigné ! Le spawner ne fonctionnera pas.", this);
            enabled = false;
            return;
        }

        if (RhythmManager.Instance != null)
        {
            RhythmManager.OnBeat += HandleBeat;
            subscribedToBeat = true;
            if (enableDebugLogs) Debug.Log($"[{gameObject.name}] EnemyBuildingSpawner initialisé et abonné à OnBeat. Spawnera toutes les {beatsPerSpawn} pulsations.", this);
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] EnemyBuildingSpawner: RhythmManager.Instance non trouvé ! Le spawn synchronisé aux pulsations ne fonctionnera pas.", this);
            // Optionnel : désactiver le script ou passer à un mode de spawn basé sur le temps ?
            // Pour l'instant, on le laisse actif mais il ne recevra pas d'événements OnBeat.
        }
    }

    void OnDestroy()
    {
        if (RhythmManager.Instance != null && subscribedToBeat)
        {
            RhythmManager.OnBeat -= HandleBeat;
            subscribedToBeat = false;
        }
    }

    private void HandleBeat()
    {
        if (!enabled || building == null) return; // S'assurer que le script est actif et le bâtiment valide

        // Vérifier si le bâtiment doit appartenir à l'ennemi et si c'est le cas
        if (requireEnemyTeam && building.Team != TeamType.Enemy)
        {
            if (enableDebugLogs) Debug.Log($"[{gameObject.name}] Le bâtiment n'est pas de l'équipe Ennemie (actuellement {building.Team}). Spawn annulé.", this);
            return;
        }

        beatCounter++;
        if (enableDebugLogs) Debug.Log($"[{gameObject.name}] Beat {beatCounter}/{beatsPerSpawn}", this);

        if (beatCounter >= beatsPerSpawn)
        {
            beatCounter = 0; // Réinitialiser le compteur
            AttemptSpawn();
        }
    }

    private void AttemptSpawn()
    {
        if (prefabToSpawn == null) return;

        Tile spawnTile = null;
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        if (specificSpawnPoint != null)
        {
            // Utiliser le point de spawn spécifique s'il est défini
            spawnPosition = specificSpawnPoint.position;
            spawnRotation = specificSpawnPoint.rotation;
            // Optionnel : vérifier si le point de spawn spécifique est sur une tuile valide/libre
            // Tile tileUnderSpecificSpawn = HexGridManager.Instance.GetClosestTile(spawnPosition);
            // if(tileUnderSpecificSpawn == null || tileUnderSpecificSpawn.IsOccupied) {
            // if(enableDebugLogs) Debug.LogWarning($"[{gameObject.name}] SpecificSpawnPoint est sur une tuile occupée ou invalide. Spawn risqué.", this);
            // }
            if (enableDebugLogs) Debug.Log($"[{gameObject.name}] Tentative de spawn au specificSpawnPoint: {spawnPosition}", this);
        }
        else
        {
            // Trouver une tuile adjacente disponible
            spawnTile = FindAvailableAdjacentTile();
            if (spawnTile == null)
            {
                if (enableDebugLogs) Debug.LogWarning($"[{gameObject.name}] Aucune tuile adjacente disponible pour le spawn.", this);
                return; // Pas de tuile disponible
            }
            spawnPosition = spawnTile.transform.position + Vector3.up * spawnYOffset;
            spawnRotation = Quaternion.identity; // Ou une rotation par défaut pour les unités
            if (enableDebugLogs) Debug.Log($"[{gameObject.name}] Tentative de spawn sur la tuile adjacente: ({spawnTile.column}, {spawnTile.row}) à la position {spawnPosition}", this);
        }

        // Instancier le prefab
        GameObject spawnedObject = Instantiate(prefabToSpawn, spawnPosition, spawnRotation);
        if (enableDebugLogs) Debug.Log($"[{gameObject.name}] Prefab '{prefabToSpawn.name}' instancié à {spawnPosition}.", this);

        // Logique supplémentaire si le prefab est une unité et doit s'attacher à la tuile
        Unit spawnedUnit = spawnedObject.GetComponent<Unit>();
        if (spawnedUnit != null && spawnTile != null)
        {
            // L'unité s'attachera elle-même à la tuile la plus proche dans son propre Start().
            // On peut forcer la position pour s'assurer qu'elle considère la bonne tuile.
            spawnedUnit.transform.position = spawnPosition; // Assurer la position avant que son Start() ne s'exécute.
            if (enableDebugLogs) Debug.Log($"[{gameObject.name}] Unité '{spawnedUnit.name}' positionnée sur la tuile de spawn. Son Start() gèrera l'attachement.", this);
        }
    }

    private Tile FindAvailableAdjacentTile()
    {
        if (building == null || building.GetOccupiedTile() == null || HexGridManager.Instance == null)
        {
            Debug.LogError($"[{gameObject.name}] Conditions manquantes pour FindAvailableAdjacentTile (building, occupiedTile ou HexGridManager).", this);
            return null;
        }

        List<Tile> neighbors = HexGridManager.Instance.GetAdjacentTiles(building.GetOccupiedTile());
        List<Tile> availableNeighbors = new List<Tile>();

        foreach (Tile neighbor in neighbors)
        {
            if (neighbor != null && !neighbor.IsOccupied && !neighbor.IsReserved) // Vérifier aussi IsReserved
            {
                // Pour un EnemyBuildingSpawner, on voudra spawner sur un TileType.Ground
                if (neighbor.tileType == TileType.Ground) {
                    availableNeighbors.Add(neighbor);
                }
            }
        }

        if (availableNeighbors.Count > 0)
        {
            // Choisir une tuile au hasard parmi celles disponibles
            return availableNeighbors[Random.Range(0, availableNeighbors.Count)];
        }

        return null; // Aucune tuile adjacente disponible
    }
}