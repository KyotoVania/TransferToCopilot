// Fichier : Scripts/Nodes/Building/Action/SpawnUnitNode.cs (Version Complètement Rework)
using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
[NodeDescription(
    name: "Spawn Unit & Assign Target",
    story: "Instancie une unité, lui assigne un objectif initial depuis le blackboard du spawner, et met à jour le compte d'unités.",
    category: "Building Actions"
)]
public class SpawnUnitNode : Unity.Behavior.Action
{
    // ---- CLÉS BLACKBOARD DU BÂTIMENT SPAWNER ----
    // (Input) Le bâtiment spawner lui-même. Doit être défini dans le blackboard du spawner.
    private const string BB_SELF_BUILDING = "SelfBuilding";
    // (Input) L'objectif à assigner à l'unité. OPTIONNEL.
    private const string BB_TARGET_BUILDING = "TargetBuilding";
    // (Input) Le préfabriqué de l'unité à spawner.
    private const string BB_UNIT_TO_SPAWN = "UnitToSpawn";
    // (Output) Le compteur d'unités actives pour ce spawner.
    private const string BB_CURRENT_UNIT_COUNT = "CurrentUnitCount";

    // ---- CLÉ BLACKBOARD DE L'UNITÉ SPAWNÉE ----
    // (Output) L'objectif de l'unité.
    private const string BB_UNIT_OBJECTIVE = "ObjectiveBuilding";


    // --- Variables Blackboard mises en cache ---
    private BlackboardVariable<Building> bbSelfBuilding;
    private BlackboardVariable<Building> bbTargetBuilding; // La cible à assigner
    private BlackboardVariable<GameObject> bbUnitToSpawnPrefab;
    private BlackboardVariable<int> bbCurrentUnitCount;

    // Référence à l'agent pour éviter les appels répétés à GetComponent
    private BehaviorGraphAgent agent;

    /// <summary>
    /// Logique principale exécutée lorsque le nœud est activé.
    /// </summary>
    protected override Status OnStart()
    {
        // 1. Initialisation et validation
        if (!InitializeAgentAndBlackboard())
        {
            return Status.Failure;
        }

        Building selfBuilding = bbSelfBuilding?.Value;
        GameObject unitPrefab = bbUnitToSpawnPrefab?.Value;

        if (selfBuilding == null || unitPrefab == null)
        {
            Debug.LogError($"[SpawnUnitNode] 'SelfBuilding' ou 'UnitToSpawn' est nul dans le blackboard.", agent.gameObject);
            return Status.Failure;
        }

        // 2. Trouver une tuile de spawn valide
        Tile spawnTile = FindAvailableAdjacentTile(selfBuilding);
        if (spawnTile == null)
        {
            Debug.LogWarning($"[{selfBuilding.name}] Aucune tuile adjacente valide trouvée pour le spawn.", selfBuilding);
            return Status.Failure;
        }

        // 3. Instancier l'unité
        Vector3 spawnPosition = spawnTile.transform.position; // L'offset Y est géré par l'unité elle-même
        GameObject spawnedUnitGO = UnityEngine.Object.Instantiate(unitPrefab, spawnPosition, Quaternion.identity);

        // 4. Assigner l'objectif à la nouvelle unité
        AssignObjectiveToNewUnit(spawnedUnitGO, selfBuilding);

        // 5. Mettre à jour le compteur d'unités
        if (bbCurrentUnitCount != null)
        {
            bbCurrentUnitCount.Value++;
        }

        Debug.Log($"[{selfBuilding.name}] Unité '{unitPrefab.name}' générée. Compte total: {bbCurrentUnitCount?.Value ?? 0}", selfBuilding);
        return Status.Success;
    }

    /// <summary>
    /// Assigne la cible du spawner (si elle existe) au blackboard de la nouvelle unité.
    /// </summary>
    private void AssignObjectiveToNewUnit(GameObject newUnitInstance, Building spawner)
    {
        // On vérifie d'abord si le spawner a une cible à donner
        if (bbTargetBuilding == null || bbTargetBuilding.Value == null)
        {
            Debug.Log($"[{spawner.name}] Ne donne pas d'objectif initial car '{BB_TARGET_BUILDING}' n'est pas défini ou est nul sur son blackboard.", spawner);
            return;
        }

        // Ensuite on récupère le blackboard de la nouvelle unité
        var newUnitAgent = newUnitInstance.GetComponent<BehaviorGraphAgent>();
        if (newUnitAgent == null || newUnitAgent.BlackboardReference == null)
        {
            Debug.LogError($"L'unité '{newUnitInstance.name}' n'a pas de BehaviorGraphAgent ou de Blackboard.", newUnitInstance);
            return;
        }

        // On essaie de récupérer la variable 'ObjectiveBuilding' sur l'unité
        if (newUnitAgent.BlackboardReference.GetVariable(BB_UNIT_OBJECTIVE, out BlackboardVariable<Building> unitObjective))
        {
            // On assigne la cible !
            unitObjective.Value = bbTargetBuilding.Value;
            Debug.Log($"[{spawner.name}] Objectif '{bbTargetBuilding.Value.name}' assigné à la nouvelle unité '{newUnitInstance.name}'.", spawner);
        }
        else
        {
            Debug.LogWarning($"L'unité '{newUnitInstance.name}' n'a pas de variable '{BB_UNIT_OBJECTIVE}' sur son blackboard pour recevoir un objectif.", newUnitInstance);
        }
    }

    /// <summary>
    /// Trouve une tuile adjacente non occupée et non réservée.
    /// </summary>
    private Tile FindAvailableAdjacentTile(Building building)
    {
        Tile buildingTile = building.GetOccupiedTile();
        if (buildingTile == null || HexGridManager.Instance == null) return null;

        List<Tile> neighbors = HexGridManager.Instance.GetAdjacentTiles(buildingTile);

        // On filtre pour ne garder que les tuiles valides
        List<Tile> availableTiles = neighbors.Where(t => t != null && !t.IsOccupied && !t.IsReserved && t.tileType == TileType.Ground).ToList();

        if (availableTiles.Count > 0)
        {
            // On retourne une tuile au hasard parmi les disponibles
            return availableTiles[UnityEngine.Random.Range(0, availableTiles.Count)];
        }

        return null;
    }

    /// <summary>
    /// Met en cache l'agent et les variables du blackboard pour éviter les recherches répétées.
    /// </summary>
    private bool InitializeAgentAndBlackboard()
    {
        // On ne fait le cache qu'une seule fois
        if (agent != null) return true;

        agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null)
        {
            Debug.LogError("SpawnUnitNode: BehaviorGraphAgent ou Blackboard manquant sur cet objet.", GameObject);
            return false;
        }

        var blackboard = agent.BlackboardReference;

        // Récupération des variables requises
        if (!blackboard.GetVariable(BB_SELF_BUILDING, out bbSelfBuilding) ||
            !blackboard.GetVariable(BB_UNIT_TO_SPAWN, out bbUnitToSpawnPrefab))
        {
            Debug.LogError($"SpawnUnitNode: Les variables requises '{BB_SELF_BUILDING}' ou '{BB_UNIT_TO_SPAWN}' sont introuvables.", agent.gameObject);
            return false;
        }

        // Récupération des variables optionnelles (pas d'erreur si elles manquent)
        blackboard.GetVariable(BB_TARGET_BUILDING, out bbTargetBuilding);
        blackboard.GetVariable(BB_CURRENT_UNIT_COUNT, out bbCurrentUnitCount);

        return true;
    }

    // Le travail est fait dans OnStart, donc OnUpdate retourne immédiatement Success.
    protected override Status OnUpdate() { return Status.Success; }

    // On ne réinitialise pas le cache ici pour de meilleures performances si le noeud est appelé souvent.
    // L'agent ne change pas pendant la durée de vie de l'objet.
    protected override void OnEnd() { }
}