namespace Gameplay
{
   using UnityEngine;
    using System.Collections.Generic;
    using System.Linq;
    using ScriptableObjects;

    /// <summary>
    /// Gère la logique spécifique de l'invocation d'unités,
    /// incluant la vérification des coûts, des cooldowns et la recherche de tuiles de spawn.
    /// </summary>
    public class UnitSpawner : MonoBehaviour
    {
        [Header("Configuration des Points de Spawn")]
        [Tooltip("Point de spawn par défaut si aucun bâtiment joueur n'est trouvé.")]
        [SerializeField] private Transform defaultPlayerUnitSpawnPoint;

        // Références aux contrôleurs nécessaires
        private GoldController _goldController;
        private MusicManager _musicManager;

        // Dictionnaires pour gérer les cooldowns
        private Dictionary<string, float> _unitCooldowns = new Dictionary<string, float>();
        public IReadOnlyDictionary<string, float> UnitCooldowns => _unitCooldowns;

        void Awake()
        {
            // Initialisation des références
            _goldController = GoldController.Instance;
            _musicManager = MusicManager.Instance;

            if (_goldController == null)
                Debug.LogError("[UnitSpawner] GoldController.Instance est introuvable !", this);
            if (_musicManager == null)
                Debug.LogError("[UnitSpawner] MusicManager.Instance est introuvable !", this);
        }

        /// <summary>
        /// Tente d'invoquer un personnage. Gère toutes les vérifications nécessaires.
        /// </summary>
        /// <param name="characterData">Les données du personnage à invoquer.</param>
        /// <param name="perfectCount">Le nombre d'inputs parfaits pour d'éventuels bonus.</param>
        public void TrySpawnUnit(CharacterData_SO characterData, int perfectCount)
        {
            
            // Vérification des données du personnage
            if (characterData == null)
            {
                Debug.LogWarning("[UnitSpawner] Tentative d'invocation avec un CharacterData_SO nul.");
                return;
            }

            // 1. Vérification du Cooldown
            if (_unitCooldowns.ContainsKey(characterData.CharacterID) && Time.time < _unitCooldowns[characterData.CharacterID])
            {
                Debug.Log($"[UnitSpawner] L'unité '{characterData.CharacterID}' est en cooldown.");
                // On pourrait ajouter un feedback sonore "négatif" ici.
                return;
            }

            if (!MomentumManager.Instance.TrySpendMomentum(characterData.MomentumCost))
            {
                Debug.Log($"Invocation de '{characterData.DisplayName}' échouée : Momentum insuffisant.");
                return; // Échec de l'invocation
            }

            // 2. Vérification de l'or
            if (_goldController.GetCurrentGold() < characterData.GoldCost)
            {
                Debug.LogWarning($"[UnitSpawner] Pas assez d'or pour invoquer {characterData.DisplayName}. Requis : {characterData.GoldCost}, Actuel : {_goldController.GetCurrentGold()}");
                return;
            }

            // 3. Trouver une tuile de spawn valide
            Tile spawnTile = FindValidSpawnTile();
            if (spawnTile == null)
            {
                Debug.LogError($"[UnitSpawner] Impossible d'invoquer {characterData.DisplayName}: Aucune tuile de spawn valide n'a été trouvée.");
                return;
            }
            
            // Si toutes les conditions sont remplies :
            // 4. Dépenser l'or
            _goldController.RemoveGold(characterData.GoldCost);
            Debug.Log($"[UnitSpawner] Or dépensé : {characterData.GoldCost}. Or restant : {_goldController.GetCurrentGold()}");
            
            // 5. Instancier et initialiser l'unité
            Vector3 spawnPosition = spawnTile.transform.position + Vector3.up * 0.1f; // Léger offset Y
            GameObject unitGO = Instantiate(characterData.GameplayUnitPrefab, spawnPosition, Quaternion.identity);
            
            Unit newUnit = unitGO.GetComponentInChildren<Unit>(true);
            if (newUnit != null)
            {
                newUnit.InitializeFromCharacterData(characterData);
                if (characterData.MomentumGainOnInvoke > 0)
                {
                    MomentumManager.Instance.AddMomentum(characterData.MomentumGainOnInvoke);
                }
                else {
                    Debug.Log($"[UnitSpawner] Pas de gain de Momentum à l'invocation pour {characterData.DisplayName}.");
                }

                Debug.Log($"[UnitSpawner] Unité {characterData.DisplayName} invoquée sur la tuile ({spawnTile.column},{spawnTile.row}).");
            }

            // 6. Mettre l'unité en cooldown
            float cooldownInSeconds = characterData.InvocationCooldown * (_musicManager?.GetBeatDuration() ?? 1.0f);
            _unitCooldowns[characterData.CharacterID] = Time.time + cooldownInSeconds;
            Debug.Log($"[UnitSpawner] Cooldown pour {characterData.DisplayName} défini à {cooldownInSeconds:F2} secondes.");
        }

        /// <summary>
        /// Cherche une tuile valide pour faire apparaître une unité alliée.
        /// Priorise les tuiles adjacentes au premier bâtiment joueur trouvé.
        /// Si aucun n'est trouvé, utilise le point de spawn par défaut.
        /// </summary>
        /// <returns>Une tuile valide ou null si aucune n'est trouvée.</returns>
        private Tile FindValidSpawnTile()
        {
            PlayerBuilding spawnerBuilding = FindFirstObjectByType<PlayerBuilding>();

            if (spawnerBuilding != null)
            {
                List<Tile> adjacentTiles = HexGridManager.Instance.GetAdjacentTiles(spawnerBuilding.GetOccupiedTile());
                foreach (Tile tile in adjacentTiles.OrderBy(t => Random.value)) // Randomise la recherche
                {
                    if (IsTileValidForSpawn(tile))
                    {
                        return tile;
                    }
                }
            }
            
            // Fallback si aucun bâtiment joueur ou aucune tuile adjacente n'est libre
            if (defaultPlayerUnitSpawnPoint != null)
            {
                Debug.LogWarning("[UnitSpawner] Aucun PlayerBuilding trouvé ou aucune tuile adjacente libre. Tentative de spawn au point par défaut.");
                Tile spawnTile = HexGridManager.Instance.GetClosestTile(defaultPlayerUnitSpawnPoint.position);
                if (IsTileValidForSpawn(spawnTile))
                {
                    return spawnTile;
                }
            }

            return null;
        }

        /// <summary>
        /// Vérifie si une tuile est valide pour le spawn (ni occupée, ni réservée, et de type Ground).
        /// </summary>
        private bool IsTileValidForSpawn(Tile tile)
        {
            return tile != null && !tile.IsOccupied && !tile.IsReserved && tile.tileType == TileType.Ground;
        }
    }
}