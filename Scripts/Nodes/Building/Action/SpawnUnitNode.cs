// Fichier : Scripts/Nodes/Action/SpawnUnitNode.cs (Version Corrigée)
using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Spawn Unit (Building)",
    story: "Instancie une unité sur une tuile adjacente valide.",
    category: "Building Actions",
    id: "BuildingAction_SpawnUnit_v2" // v2 pour la version corrigée
)]
public partial class SpawnUnitNode : Unity.Behavior.Action
{
    // --- CORRIGÉ : On utilise la clé définie dans l'initialiseur ---
    private const string BB_SELF_BUILDING = "SelfBuilding"; 
    
    // Clés pour les autres variables
    private const string BB_UNIT_TO_SPAWN = "UnitToSpawn";
    private const string BB_CURRENT_UNIT_COUNT = "CurrentUnitCount";

    // --- CORRIGÉ : Le type de la variable est maintenant Building ---
    private BlackboardVariable<Building> bbSelfBuilding;
    
    private BlackboardVariable<GameObject> bbUnitToSpawnPrefab;
    private BlackboardVariable<int> bbCurrentUnitCount;

    private bool blackboardVariablesCached = false;

    protected override Status OnStart()
    {
        if (!CacheBlackboardVariables())
        {
            Debug.LogError($"[{GameObject?.name}] SpawnUnitNode: Échec du cache des variables Blackboard.", GameObject);
            return Status.Failure;
        }

        // --- CORRIGÉ : Plus besoin de cast, la variable est déjà du bon type ---
        Building selfBuilding = bbSelfBuilding.Value;
        GameObject unitPrefab = bbUnitToSpawnPrefab.Value;
        
        if (selfBuilding == null)
        {
            Debug.LogError($"[{GameObject?.name}] SpawnUnitNode: La variable '{BB_SELF_BUILDING}' est nulle.", GameObject);
            return Status.Failure;
        }

        if (unitPrefab == null)
        {
            Debug.LogError($"[{GameObject?.name}] SpawnUnitNode: La variable '{BB_UNIT_TO_SPAWN}' (prefab) est nulle.", GameObject);
            return Status.Failure;
        }

        Tile spawnTile = FindAvailableAdjacentTile(selfBuilding);

        if (spawnTile != null)
        {
            Vector3 spawnPosition = spawnTile.transform.position + Vector3.up * 0.1f;
            UnityEngine.Object.Instantiate(unitPrefab, spawnPosition, Quaternion.identity);
            bbCurrentUnitCount.Value++;
            
            Debug.Log($"[{selfBuilding.name}] SpawnUnitNode: Unité '{unitPrefab.name}' générée sur la tuile ({spawnTile.column},{spawnTile.row}). Nouveau compte : {bbCurrentUnitCount.Value}", selfBuilding);
            return Status.Success;
        }
        else
        {
            Debug.LogWarning($"[{selfBuilding.name}] SpawnUnitNode: Aucune tuile adjacente valide trouvée pour le spawn.", selfBuilding);
            return Status.Failure;
        }
    }

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
            return availableTiles[UnityEngine.Random.Range(0, availableTiles.Count)];
        }

        return null;
    }

    protected override Status OnUpdate() { return Status.Success; }
    
    protected override void OnEnd() { blackboardVariablesCached = false; }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;
        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null) return false;

        var blackboard = agent.BlackboardReference;
        bool success = true;
        
        // --- CORRIGÉ : On cherche une variable de type Building ---
        if (!blackboard.GetVariable(BB_SELF_BUILDING, out bbSelfBuilding)) success = false;
        
        if (!blackboard.GetVariable(BB_UNIT_TO_SPAWN, out bbUnitToSpawnPrefab)) success = false;
        if (!blackboard.GetVariable(BB_CURRENT_UNIT_COUNT, out bbCurrentUnitCount)) success = false;
        
        blackboardVariablesCached = success;
        return success;
    }
}