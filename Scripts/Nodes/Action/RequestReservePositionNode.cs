using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Request Reserve Position",
    story: "Request a reserve tile from the initial target PlayerBuilding",
    category: "Ally Actions",
    id: "AllyAction_RequestReservePosition_v1"
)]
public partial class RequestReservePositionNode : Unity.Behavior.Action
{
    // Input Blackboard variables
    private const string BB_SELF_UNIT = "SelfUnit";
    private const string BB_INITIAL_TARGET_BUILDING = "InitialTargetBuilding";
    
    // Output Blackboard variables
    private const string BB_FINAL_DESTINATION_POSITION = "FinalDestinationPosition";
    private const string BB_SELECTED_ACTION_TYPE = "SelectedActionType";
    private const string BB_RESERVE_POSITION_ASSIGNED = "ReservePositionAssigned"; // Nouveau flag

    // Cached Blackboard variables
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Building> bbInitialTargetBuilding;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    private BlackboardVariable<AIActionType> bbSelectedActionType;
    private BlackboardVariable<bool> bbReservePositionAssigned;
    
    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;
    private AllyUnit selfUnit;

    protected override Status OnStart()
    {
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        
        if (!CacheBlackboardVariables())
        {
            Debug.LogError("[RequestReservePositionNode] Failed to cache blackboard variables.", GameObject);
            return Status.Failure;
        }

        selfUnit = bbSelfUnit?.Value as AllyUnit;
        if (selfUnit == null)
        {
            Debug.LogError("[RequestReservePositionNode] SelfUnit is null or not an AllyUnit.", GameObject);
            return Status.Failure;
        }

        Building initialBuilding = bbInitialTargetBuilding?.Value;
        if (initialBuilding == null)
        {
            Debug.LogError("[RequestReservePositionNode] InitialTargetBuilding is null.", GameObject);
            return Status.Failure;
        }

        // Vérifier que c'est bien un PlayerBuilding (en mode défensif)
        PlayerBuilding playerBuilding = initialBuilding as PlayerBuilding;
        if (playerBuilding == null)
        {
            Debug.LogError($"[RequestReservePositionNode] InitialTargetBuilding '{initialBuilding.name}' is not a PlayerBuilding.", GameObject);
            return Status.Failure;
        }

        // Vérifier si l'unité a déjà une position de réserve assignée
        if (selfUnit.currentReserveTile != null && selfUnit.currentReserveBuilding == playerBuilding)
        {
            Debug.Log($"[RequestReservePositionNode] Unit already has reserve position assigned: ({selfUnit.currentReserveTile.column}, {selfUnit.currentReserveTile.row})", GameObject);
            
            // Mettre à jour le Blackboard avec la position existante
            if (bbFinalDestinationPosition != null)
                bbFinalDestinationPosition.Value = new Vector2Int(selfUnit.currentReserveTile.column, selfUnit.currentReserveTile.row);
            if (bbSelectedActionType != null)
                bbSelectedActionType.Value = AIActionType.MoveToBuilding;
            if (bbReservePositionAssigned != null)
                bbReservePositionAssigned.Value = true;
                
            return Status.Success;
        }

        // Demander une case de réserve au PlayerBuilding
        if (!playerBuilding.HasAvailableReserveTiles())
        {
            Debug.LogWarning($"[RequestReservePositionNode] PlayerBuilding '{playerBuilding.name}' has no available reserve tiles.", GameObject);
            
            // Fallback : aller au bâtiment directement
            Tile buildingTile = playerBuilding.GetOccupiedTile();
            if (buildingTile != null)
            {
                if (bbFinalDestinationPosition != null)
                    bbFinalDestinationPosition.Value = new Vector2Int(buildingTile.column, buildingTile.row);
                if (bbSelectedActionType != null)
                    bbSelectedActionType.Value = AIActionType.MoveToBuilding;
                if (bbReservePositionAssigned != null)
                    bbReservePositionAssigned.Value = false; // Pas de vraie réserve
                    
                return Status.Success;
            }
            else
            {
                return Status.Failure;
            }
        }

        // Obtenir une case de réserve disponible
        Tile availableReserveTile = playerBuilding.GetAvailableReserveTileForUnit(selfUnit);
        if (availableReserveTile == null)
        {
            Debug.LogWarning($"[RequestReservePositionNode] Failed to get available reserve tile from '{playerBuilding.name}'.", GameObject);
            return Status.Failure;
        }

        // Assigner l'unité à cette case de réserve
        selfUnit.SetReservePosition(playerBuilding, availableReserveTile);
        
        Debug.Log($"[RequestReservePositionNode] Reserve position assigned: ({availableReserveTile.column}, {availableReserveTile.row}) at '{playerBuilding.name}'", GameObject);
        
        // Mettre à jour le Blackboard
        if (bbFinalDestinationPosition != null)
            bbFinalDestinationPosition.Value = new Vector2Int(availableReserveTile.column, availableReserveTile.row);
        if (bbSelectedActionType != null)
            bbSelectedActionType.Value = AIActionType.MoveToBuilding;
        if (bbReservePositionAssigned != null)
            bbReservePositionAssigned.Value = true;
        
        return Status.Success;
    }

    protected override Status OnUpdate()
    {
        return Status.Success; // Action instantanée
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        if (agent == null || agent.BlackboardReference == null)
        {
            Debug.LogError("[RequestReservePositionNode] Agent or BlackboardReference missing.", GameObject);
            return false;
        }

        var blackboard = agent.BlackboardReference;
        bool success = true;

        // Input variables
        if (!blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit))
        {
            Debug.LogError($"[RequestReservePositionNode] '{BB_SELF_UNIT}' not found.", GameObject);
            success = false;
        }
        
        if (!blackboard.GetVariable(BB_INITIAL_TARGET_BUILDING, out bbInitialTargetBuilding))
        {
            Debug.LogError($"[RequestReservePositionNode] '{BB_INITIAL_TARGET_BUILDING}' not found.", GameObject);
            success = false;
        }

        // Output variables
        if (!blackboard.GetVariable(BB_FINAL_DESTINATION_POSITION, out bbFinalDestinationPosition))
        {
            Debug.LogError($"[RequestReservePositionNode] '{BB_FINAL_DESTINATION_POSITION}' not found.", GameObject);
            success = false;
        }
        
        if (!blackboard.GetVariable(BB_SELECTED_ACTION_TYPE, out bbSelectedActionType))
        {
            Debug.LogError($"[RequestReservePositionNode] '{BB_SELECTED_ACTION_TYPE}' not found.", GameObject);
            success = false;
        }
        
        if (!blackboard.GetVariable(BB_RESERVE_POSITION_ASSIGNED, out bbReservePositionAssigned))
        {
            Debug.LogWarning($"[RequestReservePositionNode] '{BB_RESERVE_POSITION_ASSIGNED}' not found. Creating it.", GameObject);
            // Pas critique, on peut continuer
        }

        blackboardVariablesCached = success;
        return success;
    }

    protected override void OnEnd()
    {
        blackboardVariablesCached = false;
        bbSelfUnit = null;
        bbInitialTargetBuilding = null;
        bbFinalDestinationPosition = null;
        bbSelectedActionType = null;
        bbReservePositionAssigned = null;
        selfUnit = null;
    }
}