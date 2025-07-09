using UnityEngine;
using Unity.Behavior;
using System.Collections;
using System;
using Unity.Properties;
using Unity.Behavior.GraphFramework; 

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Move To Target (Step)",
    story: "Move To Target (Step)",
    category: "My Actions",
    id: "YOUR_UNIQUE_ID_MoveToTarget_Step1" // ID mis à jour pour une nouvelle version
)]
public class MoveToTargetNode_WithInternalBeatWait : Unity.Behavior.Action
{
    // --- Blackboard Variable Noms ---
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string MOVEMENT_TARGET_POS_VAR = "MovementTargetPosition";
    private const string IS_MOVING_BB_VAR = "IsMoving";

    // --- Références Blackboard mises en cache ---
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Vector2Int> bbMovementTargetPosition;
    private BlackboardVariable<bool> bbIsMoving;

    // --- État Interne du Nœud ---
    private Unit selfUnitInstanceInternal;
    private int beatCounterInternal = 0;
    private int requiredMovementDelayInternal = 0;
    private bool isSubscribedToBeat = false;
    private bool delayPhaseComplete = false;
    private bool movementActionStarted = false;
    private Coroutine unitStepCoroutineHandle;

    private string nodeInstanceId;
    private bool blackboardVariablesAreValid = false;

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

    private Status AttemptMovementStep()
    {
        movementActionStarted = true;

        Vector2Int finalDestination = bbMovementTargetPosition.Value;
        Tile currentUnitTile = selfUnitInstanceInternal.GetOccupiedTile();

        if (currentUnitTile == null)
        {
            LogNodeMessage($"AttemptMovementStep: L'unité {selfUnitInstanceInternal.name} n'est pas sur une tuile valide. Échec.", true, true);
            return Status.Failure;
        }
        if (currentUnitTile.column == finalDestination.x && currentUnitTile.row == finalDestination.y)
        {
            LogNodeMessage($"AttemptMovementStep: L'unité {selfUnitInstanceInternal.name} est déjà sur la destination finale ({finalDestination.x},{finalDestination.y}).", false, true);
            return Status.Success;
        }

        if (HexGridManager.Instance == null)
            return Status.Failure;

        Tile nextStepTile = selfUnitInstanceInternal.GetNextTileTowardsDestinationForBG(finalDestination);

        if (nextStepTile == null)
        {
            LogNodeMessage($"AttemptMovementStep: GetNextTileTowardsDestinationForBG n'a retourné aucun pas valide vers ({finalDestination.x},{finalDestination.y}) depuis ({currentUnitTile.column},{currentUnitTile.row}). Échec du pathfinding pour ce pas.", true, true);
            return Status.Failure;
        }
        LogNodeMessage($"AttemptMovementStep: Prochain pas vers ({finalDestination.x},{finalDestination.y}) est la tuile ({nextStepTile.column},{nextStepTile.row}).", false, true);

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
    
    // --- Le reste du script reste inchangé (méthodes utilitaires) ---
    private void ResetInternalState()
    {
        beatCounterInternal = 0;
        requiredMovementDelayInternal = 0;
        delayPhaseComplete = false;
        movementActionStarted = false;
        unitStepCoroutineHandle = null;
    }

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