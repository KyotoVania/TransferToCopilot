namespace ScriptableObjects
{
    using System.Collections.Generic;
    using UnityEngine;
    
    /// <summary>
    /// ScriptableObject defining enemy wave configurations for level progression.
    /// Contains spawn requests with timing, quantities, and spawner assignments for strategic enemy deployment.
    /// Used by wave management systems to orchestrate enemy encounters during gameplay.
    /// </summary>
    [CreateAssetMenu(fileName = "New Wave", menuName = "Game/Wave")]
    public class Wave_SO : ScriptableObject
    {
        /// <summary>
        /// Display name for this wave configuration.
        /// Used for identification in editor and debugging purposes.
        /// </summary>
        [Header("Wave Configuration")]
        public string waveName;
        
        /// <summary>
        /// Collection of unit spawn requests that compose this wave.
        /// Each request defines what units to spawn, when, and from which spawner.
        /// </summary>
        [Header("Spawn Requests")]
        public List<UnitSpawnRequest> spawnRequests = new List<UnitSpawnRequest>();
        
        /// <summary>
        /// Data structure defining a single unit spawn request within a wave.
        /// Specifies the unit type, quantity, timing, and spawn location for enemy deployment.
        /// </summary>
        [System.Serializable]
        public class UnitSpawnRequest
        {
            /// <summary>
            /// Prefab of the unit to instantiate during this spawn request.
            /// Must be a valid enemy unit prefab with appropriate components.
            /// </summary>
            [Tooltip("Prefab de l'unité à générer")]
            [Header("Unit Configuration")]
            public GameObject unitPrefab;
            
            /// <summary>
            /// Number of units to spawn for this specific request.
            /// Multiple units can be spawned simultaneously or in sequence.
            /// </summary>
            [Tooltip("Nombre d'unités à générer")]
            public int count = 1;
            
            /// <summary>
            /// Delay in seconds before executing this spawn request after wave initiation.
            /// Allows for staggered enemy deployment and tactical timing.
            /// </summary>
            [Tooltip("Délai en secondes avant ce spawn spécifique, après le début de la vague")]
            [Header("Timing")]
            public float spawnDelay = 0f;
            
            /// <summary>
            /// Optional tag identifier for the specific spawner building to use.
            /// If empty, the system will use default spawner selection logic.
            /// </summary>
            [Tooltip("Tag du bâtiment spawner à utiliser (optionnel)")]
            [Header("Spawn Behavior")]
            public string spawnerBuildingTag;
            
            /// <summary>
            /// Human-readable name for this spawn request for debugging and identification.
            /// Helps with wave design and troubleshooting in the editor.
            /// </summary>
            [Header("Debug")]
            [Tooltip("Nom pour identifier cette demande de spawn")]
            public string requestName;
        }
        
        /// <summary>
        /// Calculates the total duration of this wave based on the longest spawn delay.
        /// Used by wave management systems to determine when the wave is complete.
        /// </summary>
        /// <returns>The duration in seconds from wave start to the last spawn request.</returns>
        public float GetWaveDuration()
        {
            float maxDelay = 0f;
            foreach (var request in spawnRequests)
            {
                if (request.spawnDelay > maxDelay)
                    maxDelay = request.spawnDelay;
            }
            return maxDelay;
        }
    }
}
