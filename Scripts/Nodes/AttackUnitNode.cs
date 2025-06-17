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
    // --- NOMS DES VARIABLES BLACKBOARD ---
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string TARGET_UNIT_VAR = "InteractionTargetUnit";
    private const string IS_ATTACKING_VAR = "IsAttacking";

    // --- CACHE DES VARIABLES ---
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbTargetUnit;
    private BlackboardVariable<bool> bbIsAttackingBlackboard;

    // --- NOUVELLES VARIABLES POUR GÉRER LE CYCLE D'ATTAQUE ---
    private Unit selfUnitInstance = null;
    private Unit currentTargetUnitForThisNode = null;
    private Coroutine nodeManagedAttackCycleCoroutine = null;
    private bool isWaitingForAttackDelay = false;
    private int currentAttackBeatCounter = 0;
    private bool hasSubscribedToBeatForAttackDelay = false;
    // --- FIN DES NOUVELLES VARIABLES ---

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

        if (!selfUnitInstance.IsUnitInRange(currentTargetUnitForThisNode))
        {
            SetIsAttackingBlackboardVar(false);
            return Status.Failure;
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
        if (!selfUnitInstance.IsUnitInRange(currentTargetUnitForThisNode))
        {
            return Status.Failure;
        }

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
        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0 || !selfUnitInstance.IsUnitInRange(currentTargetUnitForThisNode))
        {
            nodeManagedAttackCycleCoroutine = null;
            yield break;
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

    // --- Méthodes pour la gestion du délai ---

    // --- CORRECTION APPLIQUÉE ICI ---
    // La méthode accepte maintenant un paramètre float pour correspondre à la signature de l'événement MusicManager.OnBeat.
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

    // --- Méthodes utilitaires ---

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