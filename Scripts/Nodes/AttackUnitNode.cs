using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System.Collections;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Attack Unit",
    story: "Performs an attack on the InteractionTargetUnit from the Blackboard, respecting the unit's AttackDelay.",
    category: "Unit Actions",
    id: "Action_AttackUnit_v4" // Nouvelle version avec délai
)]
public class AttackUnitNode : Unity.Behavior.Action
{
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string TARGET_UNIT_VAR = "InteractionTargetUnit";
    private const string IS_ATTACKING_VAR = "IsAttacking";

    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbTargetUnit;
    private BlackboardVariable<bool> bbIsAttackingBlackboard;

    private Unit selfUnitInstance = null;
    private Unit currentTargetUnitForThisNode = null;
    private Coroutine nodeManagedAttackCycleCoroutine = null;

    // --- NEW: STATE TRACKING ---
    private bool isWaitingForAttackDelay = false;
    private int currentAttackBeatCounter = 0;
    private bool hasSubscribedToBeatForAttackDelay = false;
    private Vector3 startingPosition; // We'll store the unit's position here.

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

        // --- NEW: Record starting position ---
        startingPosition = selfUnitInstance.transform.position;

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

    protected override Status OnUpdate()
    {
        if (selfUnitInstance == null || !selfUnitInstance.gameObject.activeInHierarchy) return Status.Failure;
        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0 || !currentTargetUnitForThisNode.gameObject.activeInHierarchy)
        {
            return Status.Success;
        }

        // --- THE FIX ---
        // On every update, check if the unit has been moved from its starting attack position.
        if (selfUnitInstance.transform.position != startingPosition)
        {
            // The unit was moved (knocked back)! Stop attacking immediately.
            return Status.Failure;
        }
        // --- END OF FIX ---

        if (isWaitingForAttackDelay)
        {
            return Status.Running;
        }

        if (nodeManagedAttackCycleCoroutine == null)
        {
            nodeManagedAttackCycleCoroutine = selfUnitInstance.StartCoroutine(PerformSingleAttackCycle());
        }

        return Status.Running;
    }

    private IEnumerator PerformSingleAttackCycle()
    {
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

    // --- Helper methods for beat delay, blackboard, and state reset remain the same ---

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

    private void SubscribeToBeatForAttackDelay()
    {
        if (MusicManager.Instance != null && !hasSubscribedToBeatForAttackDelay)
        {
            MusicManager.Instance.OnBeat += HandleAttackBeatDelay;
            hasSubscribedToBeatForAttackDelay = true;
        }
    }

    private void UnsubscribeFromBeatForAttackDelay()
    {
        if (MusicManager.Instance != null && hasSubscribedToBeatForAttackDelay)
        {
            MusicManager.Instance.OnBeat -= HandleAttackBeatDelay;
            hasSubscribedToBeatForAttackDelay = false;
        }
    }

    private void ResetNodeInternalState()
    {
        nodeManagedAttackCycleCoroutine = null;
        selfUnitInstance = null;
        currentTargetUnitForThisNode = null;
        currentAttackBeatCounter = 0;
        isWaitingForAttackDelay = false;
        hasSubscribedToBeatForAttackDelay = false;
    }

    private void SetIsAttackingBlackboardVar(bool value)
    {
        if(bbIsAttackingBlackboard != null && bbIsAttackingBlackboard.Value != value)
        {
            bbIsAttackingBlackboard.Value = value;
        }
    }

    private bool CacheBlackboardVariables()
    {
        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null) return false;

        var blackboard = agent.BlackboardReference;
        bool success = true;
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit)) success = false;
        if (!blackboard.GetVariable(TARGET_UNIT_VAR, out bbTargetUnit)) success = false;
        if (!blackboard.GetVariable(IS_ATTACKING_VAR, out bbIsAttackingBlackboard)) success = false;

        return success;
    }
}