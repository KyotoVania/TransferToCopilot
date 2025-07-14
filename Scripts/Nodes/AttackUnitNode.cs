using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System.Collections;
using System;
using Unity.Properties;
using System.Collections.Generic;
using ScriptableObjects;

/// <summary>
/// Unity Behavior Graph action node for performing unit attacks.
/// Handles rhythmic combat with attack delays, range checking, and target validation.
/// Supports both single-tile and multi-tile target units (bosses).
/// </summary>
[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Attack Unit",
    story: "Performs an attack on the InteractionTargetUnit from the Blackboard, respecting the unit's AttackDelay.",
    category: "Unit Actions",
    id: "Action_AttackUnit_v4"
)]
public class AttackUnitNode : Unity.Behavior.Action
{
    // --- BLACKBOARD VARIABLE NAMES ---
    /// <summary>Blackboard variable name for the unit performing the attack.</summary>
    private const string SELF_UNIT_VAR = "SelfUnit";
    /// <summary>Blackboard variable name for the target unit being attacked.</summary>
    private const string TARGET_UNIT_VAR = "InteractionTargetUnit";
    /// <summary>Blackboard variable name for attacking state flag.</summary>
    private const string IS_ATTACKING_VAR = "IsAttacking";
    /// <summary>Blackboard variable name for selected action type (used for action redirection).</summary>
    private const string SELECTED_ACTION_TYPE_VAR = "SelectedActionType";

    // --- CACHED BLACKBOARD VARIABLES ---
    /// <summary>Cached blackboard reference to the attacking unit.</summary>
    private BlackboardVariable<Unit> bbSelfUnit;
    /// <summary>Cached blackboard reference to the target unit.</summary>
    private BlackboardVariable<Unit> bbTargetUnit;
    /// <summary>Cached blackboard reference to attacking state flag.</summary>
    private BlackboardVariable<bool> bbIsAttackingBlackboard;
    /// <summary>Cached blackboard reference to selected action type.</summary>
    private BlackboardVariable<AIActionType> bbSelectedActionType;

    // --- ATTACK CYCLE MANAGEMENT VARIABLES ---
    /// <summary>Cached reference to the attacking unit instance.</summary>
    private Unit selfUnitInstance = null;
    /// <summary>Cached reference to the current target unit for this node.</summary>
    private Unit currentTargetUnitForThisNode = null;
    /// <summary>Coroutine handling the attack cycle execution.</summary>
    private Coroutine nodeManagedAttackCycleCoroutine = null;
    /// <summary>Whether the node is currently waiting for attack delay.</summary>
    private bool isWaitingForAttackDelay = false;
    /// <summary>Current beat counter for attack delay timing.</summary>
    private int currentAttackBeatCounter = 0;
    /// <summary>Whether the node has subscribed to beat events for attack delay.</summary>
    private bool hasSubscribedToBeatForAttackDelay = false;

    /// <summary>
    /// Initializes the attack node and validates targets.
    /// </summary>
    /// <returns>Status indicating node initialization result.</returns>
    protected override Status OnStart()
    {
        ResetNodeInternalState();

        if (!CacheBlackboardVariables())
        {
            SetIsAttackingBlackboardVar(false);
            return Status.Failure;
        }

        selfUnitInstance = bbSelfUnit?.Value;
        if (selfUnitInstance == null)
        {
            SetIsAttackingBlackboardVar(false);
            return Status.Failure;
        }

        currentTargetUnitForThisNode = bbTargetUnit?.Value;
        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0)
        {
            SetIsAttackingBlackboardVar(false);
            return Status.Success;
        }

        SetIsAttackingBlackboardVar(true);
        isWaitingForAttackDelay = false;
        return Status.Running;

    }

    /// <summary>
    /// Updates the attack node, managing attack cycles and range validation.
    /// </summary>
    /// <returns>Status indicating current execution state.</returns>
    protected override Status OnUpdate()
    {
        if (selfUnitInstance == null || !selfUnitInstance.gameObject.activeInHierarchy) return Status.Failure;
        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0 || !currentTargetUnitForThisNode.gameObject.activeInHierarchy)
        {
            return Status.Success;
        }
        
        // Check if target is still in attack range
        if (!IsTargetInAttackRange())
        {
            Debug.Log($"[{selfUnitInstance.name}] Target {currentTargetUnitForThisNode.name} is no longer in attack range. Switching action to MoveToUnit.");
            
            // Update blackboard to redirect to movement
            if (bbSelectedActionType != null)
            {
                bbSelectedActionType.Value = AIActionType.MoveToUnit;
            }
            
            return Status.Success; // Exit node completely
        }

        if (isWaitingForAttackDelay)
        {
            return Status.Running;
        }

        if (nodeManagedAttackCycleCoroutine == null)
        {
            Debug.Log($"[{selfUnitInstance.name}] Starting attack cycle for {currentTargetUnitForThisNode.name}.");
            nodeManagedAttackCycleCoroutine = selfUnitInstance.StartCoroutine(PerformSingleAttackCycle());
        }

        return Status.Running;
    }

    /// <summary>
    /// Coroutine that performs a single attack cycle with delay management.
    /// </summary>
    /// <returns>Coroutine enumerator.</returns>
    private IEnumerator PerformSingleAttackCycle()
    {
        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0)
        {
            nodeManagedAttackCycleCoroutine = null;
            yield break;
        }
       
        // Vérifier si nous sommes toujours dans la portée de l'ennemi
        if (!IsTargetInAttackRange())
        {
            Debug.Log($"[{selfUnitInstance.name}] Cible {currentTargetUnitForThisNode.name} n'est plus à portée durant le cycle d'attaque. Arrêt du cycle.");
            nodeManagedAttackCycleCoroutine = null;
            yield break; // Le nœud sortira au prochain OnUpdate via la vérification de portée
        }

        yield return selfUnitInstance.StartCoroutine(selfUnitInstance.PerformAttackCoroutine(currentTargetUnitForThisNode));

        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0)
        {
            nodeManagedAttackCycleCoroutine = null;
            yield break;
        }

        currentAttackBeatCounter = 0;
        isWaitingForAttackDelay = (selfUnitInstance.AttackDelay > 0);

        if (isWaitingForAttackDelay)
        {
            SubscribeToBeatForAttackDelay();
        }

        nodeManagedAttackCycleCoroutine = null;
    }

    /// <summary>
    /// Cleans up attack node resources and resets state.
    /// </summary>
    protected override void OnEnd()
    {
        UnsubscribeFromBeatForAttackDelay();
        if (nodeManagedAttackCycleCoroutine != null && selfUnitInstance != null)
        {
            selfUnitInstance.StopCoroutine(nodeManagedAttackCycleCoroutine);
        }
        SetIsAttackingBlackboardVar(false);
        ResetNodeInternalState();
    }

    // --- ATTACK DELAY MANAGEMENT METHODS ---

    /// <summary>
    /// Handles beat events for attack delay timing.
    /// </summary>
    /// <param name="beatDuration">Duration of the current beat.</param>
    private void HandleAttackBeatDelay(float beatDuration)
    {
        if (!isWaitingForAttackDelay)
        {
            UnsubscribeFromBeatForAttackDelay();
            return;
        }

        currentAttackBeatCounter++;
        if (currentAttackBeatCounter >= selfUnitInstance.AttackDelay)
        {
            isWaitingForAttackDelay = false;
            UnsubscribeFromBeatForAttackDelay();
        }
    }

    /// <summary>
    /// Subscribes to music beat events for attack delay management.
    /// </summary>
    private void SubscribeToBeatForAttackDelay()
    {
        if (MusicManager.Instance != null && !hasSubscribedToBeatForAttackDelay)
        {
            MusicManager.Instance.OnBeat += HandleAttackBeatDelay;
            hasSubscribedToBeatForAttackDelay = true;
        }
    }

    /// <summary>
    /// Unsubscribes from music beat events for attack delay management.
    /// </summary>
    private void UnsubscribeFromBeatForAttackDelay()
    {
        if (MusicManager.Instance != null && hasSubscribedToBeatForAttackDelay)
        {
            MusicManager.Instance.OnBeat -= HandleAttackBeatDelay;
            hasSubscribedToBeatForAttackDelay = false;
        }
    }

    // --- UTILITY METHODS ---

    /// <summary>
    /// Resets the internal state of the attack node.
    /// </summary>
    private void ResetNodeInternalState()
    {
        nodeManagedAttackCycleCoroutine = null;
        selfUnitInstance = null;
        currentTargetUnitForThisNode = null;
        currentAttackBeatCounter = 0;
        isWaitingForAttackDelay = false;
        hasSubscribedToBeatForAttackDelay = false;
    }

    /// <summary>
    /// Sets the IsAttacking blackboard variable value.
    /// </summary>
    /// <param name="value">New attacking state value.</param>
    private void SetIsAttackingBlackboardVar(bool value)
    {
        if(bbIsAttackingBlackboard != null && bbIsAttackingBlackboard.Value != value)
        {
            bbIsAttackingBlackboard.Value = value;
        }
    }

    /// <summary>
    /// Caches blackboard variable references for performance.
    /// </summary>
    /// <returns>True if all required variables were cached successfully.</returns>
    private bool CacheBlackboardVariables()
    {
        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null) return false;

        var blackboard = agent.BlackboardReference;
        bool success = true;
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit)) success = false;
        if (!blackboard.GetVariable(TARGET_UNIT_VAR, out bbTargetUnit)) success = false;
        if (!blackboard.GetVariable(IS_ATTACKING_VAR, out bbIsAttackingBlackboard)) success = false;
        
        // Try to get the SelectedActionType variable (optional)
        if (!blackboard.GetVariable(SELECTED_ACTION_TYPE_VAR, out bbSelectedActionType))
        {
            Debug.LogWarning($"[{GameObject?.name}] Blackboard variable '{SELECTED_ACTION_TYPE_VAR}' not found. Action redirection will not be available.");
        }

        return success;
    }

    /// <summary>
    /// Checks if the target unit is within attack range.
    /// Handles both single-tile and multi-tile (boss) targets.
    /// </summary>
    /// <returns>True if target is within attack range.</returns>
    private bool IsTargetInAttackRange()
    {
        if (selfUnitInstance == null || currentTargetUnitForThisNode == null)
        {
            return false;
        }

        bool isEnemyInRange;
        if (currentTargetUnitForThisNode.GetUnitType() == UnitType.Boss)
        {
            // Robust logic for multi-tile boss targets
            Tile selfTile = selfUnitInstance.GetOccupiedTile();
            List<Tile> targetTiles = currentTargetUnitForThisNode.GetOccupiedTiles(); // Gets all boss tiles correctly
            if (selfTile == null || targetTiles.Count == 0 || HexGridManager.Instance == null)
            {
                return false; // Safety check if we don't have tiles or grid manager
            }
            isEnemyInRange = false; // No part of the boss was in range

            // Check distance to each boss tile
            foreach (var targetTile in targetTiles)
            {
                if (targetTile != null)
                {
                    int distance = HexGridManager.Instance.HexDistance(selfTile.column, selfTile.row, targetTile.column, targetTile.row);
                    if (distance <= selfUnitInstance.AttackRange)
                    {
                        isEnemyInRange = true;
                        break; // No need to check other tiles
                    }
                }
            }
        }
        else
        {
            isEnemyInRange = selfUnitInstance.IsUnitInRange(currentTargetUnitForThisNode);
        }

        return isEnemyInRange;
    }
}