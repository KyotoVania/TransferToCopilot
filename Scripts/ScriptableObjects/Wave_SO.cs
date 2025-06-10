namespace ScriptableObjects
{
    using System.Collections.Generic;
    using UnityEngine;
    
    [CreateAssetMenu(fileName = "New Wave", menuName = "Game/Wave")]
    public class Wave_SO : ScriptableObject
    {
        [Header("Wave Configuration")]
        public string waveName;
        
        [Header("Spawn Requests")]
        public List<UnitSpawnRequest> spawnRequests = new List<UnitSpawnRequest>();
        
        /// <summary>
        /// Définit une demande de spawn d'unité dans une vague
        /// </summary>
        [System.Serializable]
        public class UnitSpawnRequest
        {
            [Header("Unit Configuration")]
            [Tooltip("Prefab de l'unité à générer")]
            public GameObject unitPrefab;
            
            [Tooltip("Nombre d'unités à générer")]
            public int count = 1;
            
            [Header("Timing")]
            [Tooltip("Délai en secondes avant ce spawn spécifique, après le début de la vague")]
            public float spawnDelay = 0f;
            
            [Header("Spawn Behavior")]
            [Tooltip("Tag du bâtiment spawner à utiliser (optionnel)")]
            public string spawnerBuildingTag;
            
            [Header("Debug")]
            [Tooltip("Nom pour identifier cette demande de spawn")]
            public string requestName;
        }
        
        /// <summary>
        /// Retourne la durée totale de la vague basée sur le plus grand délai de spawn
        /// </summary>
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
