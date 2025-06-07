using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Pour OrderBy
using Unity.Properties;

/// <summary>
/// Action Node qui lit une Wave_SO depuis le Blackboard et exécute les requêtes de spawn
/// en respectant les délais internes de la vague.
/// </summary>
[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Start Commanded Wave",
    story: "Spawns units from a Wave_SO asset found on the Blackboard.",
    category: "Building Actions",
    id: "BuildingAction_StartCommandedWave_v1"
)]
public partial class StartWaveNode : Unity.Behavior.Action
{
    // --- CLÉS BLACKBOARD ---
    private const string BB_SELF_BUILDING = "SelfBuilding";
    private const string BB_COMMANDED_WAVE = "CommandedWave";

    // --- CACHE DES VARIABLES ---
    private BlackboardVariable<Building> bbSelfBuilding;
    private BlackboardVariable<Wave_SO> bbCommandedWave;

    private bool blackboardVariablesCached = false;
    private Building selfBuildingInstance;
    private Coroutine waveCoroutine;

    /// <summary>
    /// Appelé une fois lorsque le nœud commence à s'exécuter.
    /// </summary>
    protected override Status OnStart()
    {
        if (!CacheBlackboardVariables())
        {
            Debug.LogError($"[{GameObject?.name}] StartWaveNode: Échec du cache des variables Blackboard.", GameObject);
            return Status.Failure;
        }

        selfBuildingInstance = bbSelfBuilding.Value;
        Wave_SO commandedWave = bbCommandedWave.Value;

        if (selfBuildingInstance == null)
        {
            Debug.LogError($"[{GameObject?.name}] StartWaveNode: SelfBuilding est null.", GameObject);
            return Status.Failure;
        }

        if (commandedWave == null)
        {
            // Ce n'est pas une erreur. Si la variable est nulle, la condition "Is Not Null" du BT a échoué.
            // Ce nœud ne devrait même pas être atteint. Mais par sécurité, on retourne Failure.
            return Status.Failure;
        }

        // Démarrer la coroutine qui va gérer le déroulement de la vague
        waveCoroutine = selfBuildingInstance.StartCoroutine(ExecuteWave(commandedWave));

        // Le nœud reste en cours d'exécution pendant que la coroutine tourne
        return Status.Running;
    }

    /// <summary>
    /// Coroutine principale qui gère le spawn des unités de la vague.
    /// </summary>
    private IEnumerator ExecuteWave(Wave_SO wave)
    {
        Debug.Log($"[{selfBuildingInstance.name}] StartWaveNode: Exécution de la vague '{wave.waveName}'. {wave.spawnRequests.Count} requête(s).");

        // Trier les requêtes par délai pour les traiter dans l'ordre
        var sortedRequests = wave.spawnRequests.OrderBy(req => req.spawnDelay).ToList();
        float timeElapsedInWave = 0f;

        foreach (var request in sortedRequests)
        {
            // Attendre le délai nécessaire pour cette requête
            float waitTime = request.spawnDelay - timeElapsedInWave;
            if (waitTime > 0)
            {
                yield return new WaitForSeconds(waitTime);
                timeElapsedInWave += waitTime;
            }

            Debug.Log($"[{selfBuildingInstance.name}] StartWaveNode: Traitement de la requête '{request.requestName}' (Prefab: {request.unitPrefab.name}, Count: {request.count}).");

            // Spawner le nombre d'unités requis pour cette requête
            for (int i = 0; i < request.count; i++)
            {
                SpawnUnit(request.unitPrefab);
                // Optionnel : ajouter un micro-délai entre chaque spawn pour éviter qu'ils n'apparaissent tous sur la même frame
                if (request.count > 1) yield return new WaitForSeconds(0.1f);
            }
        }

        // La coroutine est terminée, le nœud peut maintenant réussir.
        // OnUpdate détectera que la coroutine est finie.
        waveCoroutine = null;
    }

    /// <summary>
    /// Gère l'instanciation d'une seule unité sur une tuile adjacente.
    /// </summary>
    private void SpawnUnit(GameObject unitPrefab)
    {
        if (unitPrefab == null) return;

        Tile spawnTile = FindAvailableAdjacentTile(selfBuildingInstance);
        if (spawnTile != null)
        {
            Vector3 spawnPosition = spawnTile.transform.position + Vector3.up * 0.1f;
            UnityEngine.Object.Instantiate(unitPrefab, spawnPosition, Quaternion.identity);
            Debug.Log($"[{selfBuildingInstance.name}] StartWaveNode: Unité '{unitPrefab.name}' générée sur la tuile ({spawnTile.column},{spawnTile.row}).", selfBuildingInstance);
        }
        else
        {
            Debug.LogWarning($"[{selfBuildingInstance.name}] StartWaveNode: Aucune tuile adjacente valide trouvée pour le spawn. L'unité n'a pas pu être générée.", selfBuildingInstance);
        }
    }

    /// <summary>
    /// Appelé à chaque frame tant que le nœud est en statut "Running".
    /// </summary>
    protected override Status OnUpdate()
    {
        // Si la coroutine est terminée (remise à null), le nœud a fini son travail.
        if (waveCoroutine == null)
        {
            return Status.Success;
        }
        // Sinon, on continue d'attendre.
        return Status.Running;
    }

    /// <summary>
    /// Appelé si le nœud est interrompu par une branche de plus haute priorité.
    /// </summary>
    protected override void OnEnd()
    {
        // Si le nœud est interrompu, il faut stopper la coroutine pour ne pas
        // continuer à spawner des unités alors que le BT fait autre chose.
        if (waveCoroutine != null && selfBuildingInstance != null)
        {
            selfBuildingInstance.StopCoroutine(waveCoroutine);
            waveCoroutine = null;
            Debug.Log($"[{selfBuildingInstance.name}] StartWaveNode: Vague interrompue.", selfBuildingInstance);
        }
        // Le nettoyage de la variable Blackboard "CommandedWave" doit être fait
        // par un nœud suivant dans le BT (typiquement un "Set Blackboard Variable").
        blackboardVariablesCached = false;
    }

    /// <summary>
    /// Logique pour trouver une tuile adjacente libre.
    /// </summary>
    private Tile FindAvailableAdjacentTile(Building building)
    {
        Tile buildingTile = building.GetOccupiedTile();
        if (buildingTile == null || HexGridManager.Instance == null) return null;

        List<Tile> neighbors = HexGridManager.Instance.GetAdjacentTiles(buildingTile);
        
        List<Tile> availableTiles = neighbors.Where(t => 
            t != null && !t.IsOccupied && !t.IsReserved && t.tileType == TileType.Ground
        ).ToList();

        if (availableTiles.Count > 0)
        {
            // Retourne une des tuiles disponibles au hasard
            return availableTiles[UnityEngine.Random.Range(0, availableTiles.Count)];
        }

        return null;
    }

    /// <summary>
    /// Met en cache les références aux variables du Blackboard.
    /// </summary>
    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;
        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null) return false;

        var blackboard = agent.BlackboardReference;
        bool success = true;
        
        if (!blackboard.GetVariable(BB_SELF_BUILDING, out bbSelfBuilding))
        {
             Debug.LogError($"[StartWaveNode] Variable Blackboard '{BB_SELF_BUILDING}' introuvable.");
             success = false;
        }

        if (!blackboard.GetVariable(BB_COMMANDED_WAVE, out bbCommandedWave))
        {
            Debug.LogError($"[StartWaveNode] Variable Blackboard '{BB_COMMANDED_WAVE}' introuvable.");
            success = false;
        }
        
        blackboardVariablesCached = success;
        return success;
    }
}