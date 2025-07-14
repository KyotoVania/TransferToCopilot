using UnityEngine;
using Unity.Behavior;
using System.Collections;
using System;
using Unity.Properties;
using Unity.Behavior.GraphFramework; 

/// <summary>
/// Unity Behavior Graph action node for step-by-step movement to a target position.
/// Handles movement delays, beat synchronization, and pathfinding for rhythmic movement.
/// </summary>
[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Move To Target (Step)",
    story: "Move To Target (Step)",
    category: "My Actions",
    id: "YOUR_UNIQUE_ID_MoveToTarget_Step1"
)]
public class MoveToTargetNode_WithInternalBeatWait : Unity.Behavior.Action
{
    // --- BLACKBOARD VARIABLE NAMES ---
    /// <summary>Blackboard variable name for the moving unit.</summary>
    private const string SELF_UNIT_VAR = "SelfUnit";
    /// <summary>Blackboard variable name for the movement target position.</summary>
    private const string MOVEMENT_TARGET_POS_VAR = "MovementTargetPosition";
    /// <summary>Blackboard variable name for the moving state flag.</summary>
    private const string IS_MOVING_BB_VAR = "IsMoving";

    // --- CACHED BLACKBOARD VARIABLES ---
    /// <summary>Cached blackboard reference to the moving unit.</summary>
    private BlackboardVariable<Unit> bbSelfUnit;
    /// <summary>Cached blackboard reference to the movement target position.</summary>
    private BlackboardVariable<Vector2Int> bbMovementTargetPosition;
    /// <summary>Cached blackboard reference to the moving state flag.</summary>
    private BlackboardVariable<bool> bbIsMoving;

    // --- INTERNAL NODE STATE ---
    /// <summary>Cached reference to the moving unit instance.</summary>
    private Unit selfUnitInstanceInternal;
    /// <summary>Internal beat counter for movement delay timing.</summary>
    private int beatCounterInternal = 0;
    /// <summary>Required movement delay in beats.</summary>
    private int requiredMovementDelayInternal = 0;
    /// <summary>Whether the node has subscribed to beat events.</summary>
    private bool isSubscribedToBeat = false;
    /// <summary>Whether the delay phase has been completed.</summary>
    private bool delayPhaseComplete = false;
    /// <summary>Whether the movement action has been started.</summary>
    private bool movementActionStarted = false;
    /// <summary>Coroutine handle for the unit step movement.</summary>
    private Coroutine unitStepCoroutineHandle;

    /// <summary>Unique identifier for this node instance for debugging.</summary>
    private string nodeInstanceId;
    /// <summary>Whether blackboard variables have been successfully cached.</summary>
    private bool blackboardVariablesAreValid = false;

    /// <summary>
    /// Initializes the movement node and sets up beat synchronization if needed.
    /// </summary>
    /// <returns>Status indicating node initialization result.</returns>
    protected override Status OnStart()
    {
        nodeInstanceId = Guid.NewGuid().ToString("N").Substring(0, 6);
        ResetInternalState();

        if (!CacheBlackboardVariables())
        {
            return Status.Failure;
        }

        selfUnitInstanceInternal = bbSelfUnit.Value;
        if (selfUnitInstanceInternal == null)
            return Status.Failure;

        SetBlackboardIsMoving(true);

        Vector2Int finalDestination = bbMovementTargetPosition.Value;
        Tile currentTile = selfUnitInstanceInternal.GetOccupiedTile();
        if (currentTile != null && currentTile.column == finalDestination.x && currentTile.row == finalDestination.y)
        {
            return Status.Success;
        }

        requiredMovementDelayInternal = selfUnitInstanceInternal.MovementDelay;

        if (requiredMovementDelayInternal > 0)
        {
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.OnBeat += OnBeatReceived;
                isSubscribedToBeat = true;
            }
            else
                return Status.Failure;
        }
        else
            delayPhaseComplete = true;

        return Status.Running;
    }

    /// <summary>
    /// Updates the movement node, managing delay phases and movement execution.
    /// </summary>
    /// <returns>Status indicating current execution state.</returns>
    protected override Status OnUpdate()
    {
        if (selfUnitInstanceInternal == null)
            return Status.Failure;

        if (!delayPhaseComplete)
        {
            return Status.Running;
        }
        
        if (!movementActionStarted)
            return AttemptMovementStep();

        if (selfUnitInstanceInternal.IsMoving)
        {
            return Status.Running;
        }
        else
        {
            return Status.Success;
        }
    }

    /// <summary>
    /// Cleans up movement node resources and resets state.
    /// </summary>
    protected override void OnEnd()
    {
        if (isSubscribedToBeat && MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= OnBeatReceived;
        }
        isSubscribedToBeat = false;
        
        if (movementActionStarted && selfUnitInstanceInternal != null && unitStepCoroutineHandle != null)
        {
            selfUnitInstanceInternal.StopCoroutine(unitStepCoroutineHandle);
            unitStepCoroutineHandle = null;

            if (selfUnitInstanceInternal.IsMoving)
            {
                selfUnitInstanceInternal.IsMoving = false;
                selfUnitInstanceInternal.ReleaseCurrentReservation();
            }
        }
        
        SetBlackboardIsMoving(false);
        ResetInternalState();
    }
    
    /// <summary>
    /// Handles beat events for movement delay timing.
    /// </summary>
    /// <param name="beatDuration">Duration of the current beat.</param>
    private void OnBeatReceived(float beatDuration)
    {
        if (selfUnitInstanceInternal == null)
        {
            if (isSubscribedToBeat && MusicManager.Instance != null) MusicManager.Instance.OnBeat -= OnBeatReceived;
            isSubscribedToBeat = false;
            return;
        }

        if (!delayPhaseComplete)
        {
            beatCounterInternal++;
            if (beatCounterInternal >= requiredMovementDelayInternal)
            {
                delayPhaseComplete = true;
                if (isSubscribedToBeat && MusicManager.Instance != null)
                {
                    MusicManager.Instance.OnBeat -= OnBeatReceived;
                    isSubscribedToBeat = false;
                }
            }
        }
        else
        {
             if (isSubscribedToBeat && MusicManager.Instance != null) MusicManager.Instance.OnBeat -= OnBeatReceived;
             isSubscribedToBeat = false;
        }
    }

    /// <summary>
    /// Attempts to perform a single movement step towards the target.
    /// </summary>
    /// <returns>Status indicating movement step result.</returns>
    private Status AttemptMovementStep()
    {
        movementActionStarted = true;

        Vector2Int finalDestination = bbMovementTargetPosition.Value;
        Tile currentUnitTile = selfUnitInstanceInternal.GetOccupiedTile();

        if (currentUnitTile == null)
        {
            LogNodeMessage($"AttemptMovementStep: Unit {selfUnitInstanceInternal.name} is not on a valid tile. Failure.", true, true);
            return Status.Failure;
        }
        if (currentUnitTile.column == finalDestination.x && currentUnitTile.row == finalDestination.y)
        {
            LogNodeMessage($"AttemptMovementStep: Unit {selfUnitInstanceInternal.name} is already at final destination ({finalDestination.x},{finalDestination.y}).", false, true);
            return Status.Success;
        }

        if (HexGridManager.Instance == null)
            return Status.Failure;

        Tile nextStepTile = selfUnitInstanceInternal.GetNextTileTowardsDestinationForBG(finalDestination);

        if (nextStepTile == null)
        {
            LogNodeMessage($"AttemptMovementStep: GetNextTileTowardsDestinationForBG returned no valid step towards ({finalDestination.x},{finalDestination.y}) from ({currentUnitTile.column},{currentUnitTile.row}). Pathfinding failure for this step.", true, true);
            return Status.Failure;
        }
        LogNodeMessage($"AttemptMovementStep: Next step towards ({finalDestination.x},{finalDestination.y}) is tile ({nextStepTile.column},{nextStepTile.row}).", false, true);

        if(selfUnitInstanceInternal.gameObject.activeInHierarchy && selfUnitInstanceInternal.enabled)
        {
            unitStepCoroutineHandle = selfUnitInstanceInternal.StartCoroutine(selfUnitInstanceInternal.MoveToTile(nextStepTile));
            if (unitStepCoroutineHandle == null)
            {
                movementActionStarted = false;
                return Status.Failure;
            }
        }
        else
        {
            movementActionStarted = false;
            return Status.Failure;
        }

        return Status.Running;
    }
    
    // --- UTILITY METHODS ---
    /// <summary>
    /// Resets the internal state of the movement node.
    /// </summary>
    private void ResetInternalState()
    {
        beatCounterInternal = 0;
        requiredMovementDelayInternal = 0;
        delayPhaseComplete = false;
        movementActionStarted = false;
        unitStepCoroutineHandle = null;
    }

    /// <summary>
    /// Caches blackboard variable references for performance.
    /// </summary>
    /// <returns>True if all required variables were cached successfully.</returns>
    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesAreValid) return true;
        var agent = this.GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null)
        {
            return false;
        }
        var blackboard = agent.BlackboardReference;
        bool allFound = true;
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit)) { allFound = false; }
        if (!blackboard.GetVariable(MOVEMENT_TARGET_POS_VAR, out bbMovementTargetPosition)) { allFound = false; }
        if (!blackboard.GetVariable(IS_MOVING_BB_VAR, out bbIsMoving)) { allFound = false; }
        blackboardVariablesAreValid = allFound;
        return allFound;
    }

    /// <summary>
    /// Sets the IsMoving blackboard variable value.
    /// </summary>
    /// <param name="value">New moving state value.</param>
    private void SetBlackboardIsMoving(bool value)
    {
        if (bbIsMoving != null)
        {
            if (bbIsMoving.Value != value)
            {
                bbIsMoving.Value = value;
            }
        }
        else
        {
            if (!blackboardVariablesAreValid && CacheBlackboardVariables() && bbIsMoving != null)
            {
                bbIsMoving.Value = value;
            }
        }
    }
    
    /// <summary>
    /// Logs a message from this node with proper formatting and context.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="isError">Whether this is an error message.</param>
    /// <param name="forceLog">Whether to force logging even if not in verbose mode.</param>
    private void LogNodeMessage(string message, bool isError = false, bool forceLog = false)
    {
        if(!blackboardVariablesAreValid && !isError) return;
        string unitName = selfUnitInstanceInternal != null ? selfUnitInstanceInternal.name : (bbSelfUnit?.Value != null ? bbSelfUnit.Value.name : "NoUnit");
        string logPrefix = $"<color=orange>[{nodeInstanceId} | {unitName} | MoveToTargetNode]</color>";
        if (isError)
        {
            Debug.LogError($"{logPrefix} {message}", this.GameObject);
        }
        else if (forceLog)
        {
            Debug.Log($"{logPrefix} {message}", this.GameObject);
        }
    }
}