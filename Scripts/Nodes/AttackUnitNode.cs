using UnityEngine;
using Unity.Behavior;
using System.Collections;
using System;
using Unity.Properties;
using Unity.Behavior.GraphFramework; // Ajout du using manquant

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Attack Unit",
    story: "Attack Unit",
    category: "My Actions",
    id: "YOUR_UNIQUE_ID_AttackUnit_V2"
)]
public class AttackUnitNode : Unity.Behavior.Action
{
    // Constants for Blackboard variable names
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string TARGET_UNIT_VAR = "DetectedEnemyUnit";
    private const string IS_ATTACKING_VAR = "IsAttacking";

    // --- Node State ---
    private bool blackboardVariablesCached = false;
    private Coroutine nodeManagedAttackCycleCoroutine = null;
    private Unit selfUnitInstance = null;
    private Unit currentTargetUnitForThisNode = null;

    // --- Blackboard Variable Cache ---
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbTargetUnit;
    private BlackboardVariable<bool> bbIsAttackingBlackboard;

    // --- Attack Cycle State ---
    private int currentAttackBeatCounter = 0;
    private bool isWaitingForAttackDelay = false;
    private bool hasSubscribedToBeatForAttackDelay = false;
    private string nodeInstanceIdForLog;

    protected override Status OnStart()
    {
        nodeInstanceIdForLog = Guid.NewGuid().ToString("N").Substring(0, 6);
        LogNodeMessage($"OnStart BEGIN. Current Status: {CurrentStatus}", isVerbose: true, forceLog: true);
        ResetNodeInternalState();

        if (!CacheBlackboardVariables())
        {
            LogNodeMessage("OnStart: CacheBlackboardVariables FAILED.", true, forceLog: true);
            SetIsAttackingBlackboardVar(false);
            return Status.Failure;
        }

        selfUnitInstance = bbSelfUnit?.Value;
        if (selfUnitInstance == null)
        {
            LogNodeMessage("OnStart: SelfUnit is null. Node Failure.", true, forceLog: true);
            SetIsAttackingBlackboardVar(false);
            return Status.Failure;
        }

        currentTargetUnitForThisNode = bbTargetUnit?.Value;
        LogNodeMessage($"OnStart: Fetched target from BB: {(currentTargetUnitForThisNode == null ? "NULL" : currentTargetUnitForThisNode.name)}. Health: {currentTargetUnitForThisNode?.Health ?? -1}", isVerbose: true, forceLog: true);

        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0)
        {
            LogNodeMessage($"OnStart: Target Unit '{(currentTargetUnitForThisNode?.name ?? "Unknown/Dead")}' is null or already dead. Node SUCCESS (target gone).", isVerbose: true, forceLog: true);
            SetIsAttackingBlackboardVar(false);
            return Status.Success;
        }

        if (!selfUnitInstance.IsValidUnitTarget(currentTargetUnitForThisNode) ||
            !selfUnitInstance.IsUnitInRange(currentTargetUnitForThisNode))
        {
            LogNodeMessage($"OnStart: Target Unit '{currentTargetUnitForThisNode.name}' (H:{currentTargetUnitForThisNode.Health}) is NOT a valid attack target (not enemy or out of range). Node FAILURE.", isError: false, forceLog: true);
            SetIsAttackingBlackboardVar(false);
            return Status.Failure;
        }

        LogNodeMessage($"OnStart: Engaging target {currentTargetUnitForThisNode.name}. Unit AttackDelay: {selfUnitInstance.AttackDelay}", isVerbose: true, forceLog: true);
        SetIsAttackingBlackboardVar(true);

        currentAttackBeatCounter = 0;
        isWaitingForAttackDelay = (selfUnitInstance.AttackDelay > 0);

        if (isWaitingForAttackDelay)
        {
            LogNodeMessage($"OnStart: Waiting for attack delay ({selfUnitInstance.AttackDelay} beats). Subscribing to beat.", isVerbose: true, forceLog: true);
            SubscribeToBeatForAttackDelay();
        }
        else
        {
            LogNodeMessage($"OnStart: No attack delay (AttackDelay: {selfUnitInstance.AttackDelay}). Will proceed to attack cycle in OnUpdate.", isVerbose: true, forceLog: true);
        }
        LogNodeMessage("OnStart END. Returning Status.Running.", isVerbose: true, forceLog: true);
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (selfUnitInstance == null || !selfUnitInstance.gameObject.activeInHierarchy)
        {
            LogNodeMessage("OnUpdate: SelfUnit is null or inactive. Node Failure.", true, forceLog: true);
            return Status.Failure;
        }

        if (currentTargetUnitForThisNode == null|| !currentTargetUnitForThisNode.gameObject.activeInHierarchy)
        {
            LogNodeMessage($"OnUpdate: Target UnitUnknown/Dead is null or dead. Node SUCCESS.", isVerbose: true, forceLog: true);
            return Status.Success;
        }
        if ( currentTargetUnitForThisNode.Health <= 0 )
        {
            LogNodeMessage($"OnUpdate: Target Unit '{(currentTargetUnitForThisNode?.name ?? "Unknown/Dead")}' is null or dead. Node SUCCESS.", isVerbose: true, forceLog: true);
            return Status.Success;
        }   
        if (isWaitingForAttackDelay && !selfUnitInstance.IsUnitInRange(currentTargetUnitForThisNode))
        {
            LogNodeMessage($"OnUpdate: Target '{currentTargetUnitForThisNode.name}' moved out of range while waiting for attack delay. Node FAILURE.", isVerbose: true, forceLog:true);
            return Status.Failure;
        }


        if (isWaitingForAttackDelay)
        {
            return Status.Running;
        }

        if (nodeManagedAttackCycleCoroutine == null)
        {
            if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0)
            {
                LogNodeMessage($"OnUpdate: Target '{currentTargetUnitForThisNode?.name ?? "DESTROYED TARGET"}' became invalid during beat delay just before starting cycle. Node SUCCESS.", isVerbose: true, forceLog: true);
                return Status.Success;
            }
            LogNodeMessage($"OnUpdate: Delay complete. Starting PerformSingleAttackCycle for {currentTargetUnitForThisNode.name}.", isVerbose: true, forceLog: true);
            nodeManagedAttackCycleCoroutine = selfUnitInstance.StartCoroutine(PerformSingleAttackCycle());
        }
        return Status.Running;
    }

    private IEnumerator PerformSingleAttackCycle()
    {
        LogNodeMessage($"PerformSingleAttackCycle: BEGIN for {currentTargetUnitForThisNode?.name ?? "TARGET UNKNOWN"}", isVerbose: true, forceLog: true);

        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0)
        {
            LogNodeMessage($"PerformSingleAttackCycle: Target '{currentTargetUnitForThisNode?.name ?? "DESTROYED TARGET"}' already invalid before attack. Ending cycle.", isVerbose: true, forceLog: true);
            nodeManagedAttackCycleCoroutine = null;
            yield break;
        }
        if (!selfUnitInstance.IsUnitInRange(currentTargetUnitForThisNode))
        {
            LogNodeMessage($"PerformSingleAttackCycle: Target '{currentTargetUnitForThisNode.name}' moved out of range before attack. Ending cycle, will fail in OnUpdate.", isVerbose: true, forceLog:true);
            nodeManagedAttackCycleCoroutine = null;
            yield break;
        }

        LogNodeMessage($"PerformSingleAttackCycle: Calling unit's PerformAttackCoroutine for {currentTargetUnitForThisNode.name}.", isVerbose: true, forceLog: true);
        yield return selfUnitInstance.StartCoroutine(selfUnitInstance.PerformAttackCoroutine(currentTargetUnitForThisNode));
        LogNodeMessage($"PerformSingleAttackCycle: Unit's PerformAttackCoroutine completed for {(currentTargetUnitForThisNode != null ? currentTargetUnitForThisNode.name : "DESTROYED TARGET")}.", isVerbose: true, forceLog: true);

        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0)
        {
            LogNodeMessage($"PerformSingleAttackCycle: Target {(currentTargetUnitForThisNode != null ? currentTargetUnitForThisNode.name : "DESTROYED TARGET")} defeated or became invalid AFTER attack. Ending cycle.", isVerbose: true, forceLog: true);
            nodeManagedAttackCycleCoroutine = null;
            yield break;
        }

        currentAttackBeatCounter = 0;
        isWaitingForAttackDelay = (selfUnitInstance.AttackDelay > 0);
        LogNodeMessage($"PerformSingleAttackCycle: Attack landed on {currentTargetUnitForThisNode.name}. Target HP: {currentTargetUnitForThisNode.Health}. Preparing for next AttackDelay ({selfUnitInstance.AttackDelay} beats). isWaiting: {isWaitingForAttackDelay}", isVerbose: true, forceLog: true);

        if (isWaitingForAttackDelay)
        {
            SubscribeToBeatForAttackDelay();
        }

        nodeManagedAttackCycleCoroutine = null;
        LogNodeMessage($"PerformSingleAttackCycle: END for {currentTargetUnitForThisNode?.name ?? "TARGET UNKNOWN"}. Coroutine handle set to null.", isVerbose: true, forceLog: true);
    }

    // --- MODIFICATION : Signature de la méthode mise à jour ---
    private void HandleAttackBeatDelay(float beatDuration)
    {
        if (!isWaitingForAttackDelay || selfUnitInstance == null)
        {
            if (hasSubscribedToBeatForAttackDelay) UnsubscribeFromBeatForAttackDelay();
            return;
        }

        currentAttackBeatCounter++;
        if (currentAttackBeatCounter >= selfUnitInstance.AttackDelay)
        {
            isWaitingForAttackDelay = false;
            UnsubscribeFromBeatForAttackDelay();
            LogNodeMessage("HandleAttackBeatDelay: AttackDelay reached. Ready for next attack action.", isVerbose: true, forceLog: true);
        }
    }

    private void SubscribeToBeatForAttackDelay()
    {
        // --- MODIFICATION : Utilisation de MusicManager.Instance ---
        if (MusicManager.Instance != null && !hasSubscribedToBeatForAttackDelay)
        {
            MusicManager.Instance.OnBeat += HandleAttackBeatDelay;
            hasSubscribedToBeatForAttackDelay = true;
            LogNodeMessage("Subscribed to OnBeat for AttackDelay.", isVerbose: true, forceLog: true);
        }
        else if (MusicManager.Instance == null)
        {
            LogNodeMessage("MusicManager is null, cannot subscribe for AttackDelay. Attack will be continuous if delay was > 0.", true, forceLog: true);
            isWaitingForAttackDelay = false;
        }
    }

    private void UnsubscribeFromBeatForAttackDelay()
    {
        // --- MODIFICATION : Utilisation de MusicManager.Instance ---
        if (MusicManager.Instance != null && hasSubscribedToBeatForAttackDelay)
        {
            MusicManager.Instance.OnBeat -= HandleAttackBeatDelay;
            hasSubscribedToBeatForAttackDelay = false;
            LogNodeMessage("Unsubscribed from OnBeat for AttackDelay.", isVerbose: true, forceLog: true);
        }
    }

    protected override void OnEnd()
    {
        LogNodeMessage($"OnEnd called. Status: {CurrentStatus}. Cleaning up.", isVerbose: true, forceLog: true);
        UnsubscribeFromBeatForAttackDelay();

        if (nodeManagedAttackCycleCoroutine != null && selfUnitInstance != null && selfUnitInstance.gameObject.activeInHierarchy)
        {
            selfUnitInstance.StopCoroutine(nodeManagedAttackCycleCoroutine);
            LogNodeMessage("Stopped nodeManagedAttackCycleCoroutine.", isVerbose: true, forceLog: true);
        }

        SetIsAttackingBlackboardVar(false);
        LogNodeMessage($"OnEnd: Set BB '{IS_ATTACKING_VAR}' to false.", isVerbose: true, forceLog: true);

        ResetNodeInternalState();
    }

    // --- Le reste des méthodes utilitaires reste inchangé ---
    private void ResetNodeInternalState()
    {
        nodeManagedAttackCycleCoroutine = null;
        currentAttackBeatCounter = 0;
        isWaitingForAttackDelay = false;
        LogNodeMessage("ResetNodeInternalState complete.", isVerbose: true, forceLog: true);
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;
        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null)
        {
            Debug.LogError($"[{nodeInstanceIdForLog} | {GameObject?.name}] CacheBlackboardVariables: Agent or BlackboardRef missing.", GameObject);
            return false;
        }
        var blackboard = agent.BlackboardReference;
        bool success = true;
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit)) { LogNodeMessage($"BBVar '{SELF_UNIT_VAR}' missing.", true, forceLog:true); success = false; }
        if (!blackboard.GetVariable(TARGET_UNIT_VAR, out bbTargetUnit)) { LogNodeMessage($"BBVar '{TARGET_UNIT_VAR}' missing.", true, forceLog:true); success = false; }
        if (!blackboard.GetVariable(IS_ATTACKING_VAR, out bbIsAttackingBlackboard)) { LogNodeMessage($"BBVar '{IS_ATTACKING_VAR}' missing.", true, forceLog:true); success = false; }
        blackboardVariablesCached = success;
        if(!success) LogNodeMessage("CacheBlackboardVariables FAILED for one or more vars.", true, forceLog:true);
        else LogNodeMessage("CacheBlackboardVariables SUCCESS.", isVerbose:true, forceLog:true);
        return success;
    }

    private void SetIsAttackingBlackboardVar(bool value)
    {
        if (!blackboardVariablesCached)
        {
            if (!CacheBlackboardVariables())
            {
                LogNodeMessage($"SetIsAttackingBlackboardVar: Cannot set '{IS_ATTACKING_VAR}', Blackboard variables not cached.", true, forceLog:true);
                return;
            }
        }
        if (bbIsAttackingBlackboard != null)
        {
            if (bbIsAttackingBlackboard.Value != value)
            {
                bbIsAttackingBlackboard.Value = value;
                LogNodeMessage($"SetIsAttackingBlackboardVar: Set BB '{IS_ATTACKING_VAR}' to {value}.", isVerbose: true, forceLog: true);
            }
        }
        else
        {
            LogNodeMessage($"SetIsAttackingBlackboardVar: bbIsAttackingBlackboard reference is null. Cannot set '{IS_ATTACKING_VAR}'.", true, forceLog:true);
        }
    }

    private void LogNodeMessage(string message, bool isError = false, bool isVerbose = false, bool forceLog = false)
    {
        string unitName = selfUnitInstance != null ? selfUnitInstance.name : (bbSelfUnit?.Value != null ? bbSelfUnit.Value.name : "NoUnit");
        UnityEngine.Object contextObject = this.GameObject ?? (UnityEngine.Object)selfUnitInstance;
        string log = $"[{nodeInstanceIdForLog} | {unitName} | AttackUnitNode] {message}";
        if (isError) Debug.LogError(log, contextObject);
    }
}