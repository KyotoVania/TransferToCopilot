using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System.Collections;
using System;
using Unity.Properties;

/// <summary>
/// Unity Behavior Graph action node for attacking buildings.
/// Handles rhythmic building attacks with delay management and objective completion tracking.
/// </summary>
[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Attack Building",
    story: "Attack Building",
    category: "My Actions",
    id: "YOUR_UNIQUE_ID_AttackBuilding"
)]
public class AttackBuildingNode : Unity.Behavior.Action
{
    // BLACKBOARD VARIABLE NAMES
    /// <summary>Blackboard variable name for the attacking unit.</summary>
    private const string SELF_UNIT_VAR = "SelfUnit";
    /// <summary>Blackboard variable name for the target building.</summary>
    private const string TARGET_BUILDING_VAR = "InteractionTargetBuilding";
    /// <summary>Blackboard variable name for attacking state flag.</summary>
    private const string IS_ATTACKING_VAR = "IsAttacking";
    /// <summary>Blackboard variable name for objective completion status.</summary>
    private const string IS_OBJECTIVE_COMPLETED_VAR = "IsObjectiveCompleted";

    // CACHED BLACKBOARD VARIABLES
    /// <summary>Cached blackboard reference to the attacking unit.</summary>
    private BlackboardVariable<Unit> bbSelfUnit;
    /// <summary>Cached blackboard reference to the target building.</summary>
    private BlackboardVariable<Building> bbTargetBuilding;
    /// <summary>Cached blackboard reference to attacking state flag.</summary>
    private BlackboardVariable<bool> bbIsAttackingBlackboard;
    /// <summary>Cached blackboard reference to objective completion status.</summary>
    private BlackboardVariable<bool> bbIsObjectiveCompleted;

    // INTERNAL NODE STATE
    /// <summary>Cached reference to the attacking unit instance.</summary>
    private Unit selfUnitInstance = null;
    /// <summary>Cached reference to the target building for this node.</summary>
    private Building currentTargetBuildingForThisNode = null;
    /// <summary>Whether blackboard variables have been successfully cached.</summary>
    private bool blackboardVariablesCached = false;
    /// <summary>Coroutine handling the attack cycle execution.</summary>
    private Coroutine nodeManagedAttackCycleCoroutine = null;

    /// <summary>Current beat counter for attack delay timing.</summary>
    private int currentAttackBeatCounter = 0;
    /// <summary>Whether the node is currently waiting for attack delay.</summary>
    private bool isWaitingForAttackDelay = false;
    /// <summary>Whether the node has subscribed to beat events for attack delay.</summary>
    private bool hasSubscribedToBeatForAttackDelay = false;

    /// <summary>Unique identifier for this node instance for debugging.</summary>
    private string nodeInstanceIdForLog;

    protected override Status OnStart()
    {
        nodeInstanceIdForLog = Guid.NewGuid().ToString("N").Substring(0, 6);
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

        currentTargetBuildingForThisNode = bbTargetBuilding?.Value;
        LogNodeMessage($"OnStart: Fetched target from BB: {(currentTargetBuildingForThisNode == null ? "NULL" : currentTargetBuildingForThisNode.name)}. Health: {currentTargetBuildingForThisNode?.CurrentHealth ?? -1}", isVerbose: true, forceLog: true);

        if (currentTargetBuildingForThisNode == null)
        {
            LogNodeMessage("OnStart: currentTargetBuildingForThisNode is NULL (destroyed or not set). Node SUCCESS (target gone).", isVerbose: true, forceLog: true);
            SetIsAttackingBlackboardVar(false);
            if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = true;
            return Status.Success;
        }
        if (currentTargetBuildingForThisNode.CurrentHealth <= 0)
        {
            LogNodeMessage($"OnStart: currentTargetBuildingForThisNode '{currentTargetBuildingForThisNode.name}' Health <= 0. Node SUCCESS (target gone).", isVerbose: true, forceLog: true);
            SetIsAttackingBlackboardVar(false);
            if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = true;
            return Status.Success;
        }

        if (!selfUnitInstance.IsValidBuildingTarget(currentTargetBuildingForThisNode))
        {
            // Vérifier si c'est un bâtiment neutre qui devrait être capturé
            if (currentTargetBuildingForThisNode is NeutralBuilding neutralBuilding &&
                neutralBuilding.IsRecapturable)
            {
                LogNodeMessage($"Target Building '{currentTargetBuildingForThisNode.name}' is a capturable neutral building, not attackable.cNode FAILURE.", isError: false, forceLog: true);
            }
            else
            {
                LogNodeMessage($"Target Building '{currentTargetBuildingForThisNode.name}' is not a valid attack target. Node FAILURE.",
                    isError: false, forceLog: true);
            }

            SetIsAttackingBlackboardVar(false);
            return Status.Failure;
        }
        // La vérification de IsValidBuildingTarget est maintenant la seule autorité pour la validité de la cible.

        if (!selfUnitInstance.IsBuildingInRange(currentTargetBuildingForThisNode))
        {
            LogNodeMessage($"OnStart: Target Building '{currentTargetBuildingForThisNode.name}' est hors de portée pour {selfUnitInstance.name}. Node FAILURE.", isError: false, forceLog: true);
            SetIsAttackingBlackboardVar(false);
            return Status.Failure;
        }
        // Si on arrive ici, l'unité considère le bâtiment comme une cible valide ET il est à portée.

        LogNodeMessage($"OnStart: Engaging target {currentTargetBuildingForThisNode.name}. Unit AttackDelay: {selfUnitInstance.AttackDelay}", isVerbose: true, forceLog: true);
        SetIsAttackingBlackboardVar(true);

        currentAttackBeatCounter = 0;
        isWaitingForAttackDelay = (selfUnitInstance.AttackDelay > 0);

        if (isWaitingForAttackDelay)
        {
            LogNodeMessage($"OnStart: Waiting for attack delay ({selfUnitInstance.AttackDelay} beats). Subscribing to beat.", isVerbose: true, forceLog: true);
            SubscribeToBeatForAttackDelay();
        } else {
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
            SetIsAttackingBlackboardVar(false); // Important de nettoyer le flag
            return Status.Failure;
        }

        // Vérifier la cible principale du nœud (celle définie dans OnStart)
        if (currentTargetBuildingForThisNode == null)
        {
            LogNodeMessage($"OnUpdate: currentTargetBuildingForThisNode is NULL (likely destroyed or became invalid). Node SUCCESS.", isVerbose: true, forceLog: true);
            if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = true; // Objectif disparu/détruit
            SetIsAttackingBlackboardVar(false);
            return Status.Success;
        }
        if (currentTargetBuildingForThisNode.CurrentHealth <= 0)
        {
            LogNodeMessage($"OnUpdate: currentTargetBuildingForThisNode '{currentTargetBuildingForThisNode.name}' Health <= 0. Node SUCCESS.", isVerbose: true, forceLog: true);
            if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = true; // Objectif détruit
            SetIsAttackingBlackboardVar(false);
            return Status.Success;
        }

          if (!selfUnitInstance.IsValidBuildingTarget(currentTargetBuildingForThisNode))
        {
            LogNodeMessage($"OnUpdate: Target Building '{currentTargetBuildingForThisNode.name}' (H:{currentTargetBuildingForThisNode.CurrentHealth}, T:{currentTargetBuildingForThisNode.Team}) n'est PLUS une cible valide selon {selfUnitInstance.name}.IsValidBuildingTarget(). Node FAILURE.", isError: false, forceLog: true);
            SetIsAttackingBlackboardVar(false);
            return Status.Failure; // L'IA devrait réévaluer.
        }


        if (isWaitingForAttackDelay)
        {
            // Si la cible sort de portée pendant qu'on attend le délai de battement
            if (!selfUnitInstance.IsBuildingInRange(currentTargetBuildingForThisNode))
            {
                LogNodeMessage($"OnUpdate: Target '{currentTargetBuildingForThisNode.name}' moved out of range while waiting for attack delay. Node FAILURE.", isVerbose: true, forceLog:true);
                SetIsAttackingBlackboardVar(false);
                return Status.Failure; // Forcera une réévaluation, potentiellement vers un MoveToBuildingNode
            }
            return Status.Running; // On attend que HandleAttackBeatDelay mette isWaitingForAttackDelay à false
        }

        // Si le délai est terminé (ou était de 0) et que la coroutine de cycle d'attaque n'est pas déjà lancée
        if (nodeManagedAttackCycleCoroutine == null)
        {
            // Revérifier la cible avant de lancer l'attaque, au cas où.
            if (currentTargetBuildingForThisNode == null || currentTargetBuildingForThisNode.CurrentHealth <= 0)
            {
                LogNodeMessage($"OnUpdate: Target '{currentTargetBuildingForThisNode?.name ?? "DESTROYED TARGET"}' became invalid during beat delay just before restarting attack cycle. Node SUCCESS.", isVerbose: true, forceLog: true);
                if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = true;
                SetIsAttackingBlackboardVar(false);
                return Status.Success;
            }
            // Revérifier la portée aussi.
            if (!selfUnitInstance.IsBuildingInRange(currentTargetBuildingForThisNode))
            {
                LogNodeMessage($"OnUpdate: Target '{currentTargetBuildingForThisNode.name}' is no longer in range before restarting attack cycle. Node FAILURE.", isVerbose: true, forceLog: true);
                SetIsAttackingBlackboardVar(false);
                return Status.Failure;
            }

            LogNodeMessage($"OnUpdate: Delay complete or not needed. Starting/Restarting PerformUnitAttackCycle for {currentTargetBuildingForThisNode.name}.", isVerbose: true, forceLog: true);
            nodeManagedAttackCycleCoroutine = selfUnitInstance.StartCoroutine(PerformUnitAttackCycle());
        }

        return Status.Running; // Le cycle d'attaque (coroutine) est en cours.
    }

    private IEnumerator PerformUnitAttackCycle()
    {
        LogNodeMessage($"PerformUnitAttackCycle: BEGIN for {currentTargetBuildingForThisNode?.name ?? "TARGET UNKNOWN"}", isVerbose:true, forceLog:true); // LOG FORCE

        if (currentTargetBuildingForThisNode == null || currentTargetBuildingForThisNode.CurrentHealth <= 0)
        {
            LogNodeMessage($"PerformUnitAttackCycle: Target '{currentTargetBuildingForThisNode?.name ?? "DESTROYED TARGET"}' already invalid before attack. Ending cycle.", isVerbose: true, forceLog: true); // LOG FORCE
            nodeManagedAttackCycleCoroutine = null;
            // Si bbIsObjectiveCompleted existe, le mettre à true ici peut être redondant si OnUpdate le fait déjà, mais ne fait pas de mal.
            if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = true;
            yield break;
        }

        LogNodeMessage($"PerformUnitAttackCycle: Calling unit's PerformAttackBuildingCoroutine for {currentTargetBuildingForThisNode.name}.", isVerbose: true, forceLog: true); // LOG FORCE
        // La coroutine de l'unité doit gérer sa propre animation et l'application des dégâts.
        // Elle doit aussi gérer son propre flag Unit._isAttacking.
        yield return selfUnitInstance.StartCoroutine(selfUnitInstance.PerformAttackBuildingCoroutine(currentTargetBuildingForThisNode));
        LogNodeMessage($"PerformUnitAttackCycle: Unit's PerformAttackBuildingCoroutine completed for {(currentTargetBuildingForThisNode != null ? currentTargetBuildingForThisNode.name : "DESTROYED TARGET")}.", isVerbose: true, forceLog: true); // LOG FORCE

        // Après que la coroutine de l'unité se soit terminée (UN coup a été porté)
        if (currentTargetBuildingForThisNode == null || currentTargetBuildingForThisNode.CurrentHealth <= 0)
        {
            LogNodeMessage($"PerformUnitAttackCycle: Target {(currentTargetBuildingForThisNode != null ? currentTargetBuildingForThisNode.name : "DESTROYED TARGET")} destroyed or became invalid AFTER attack. Ending cycle.", isVerbose: true, forceLog: true); // LOG FORCE
            if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = true;
            nodeManagedAttackCycleCoroutine = null; // Crucial pour que OnUpdate puisse retourner Success
            yield break;
        }

        // La cible est toujours valide, préparer le prochain délai d'attaque
        currentAttackBeatCounter = 0;
        isWaitingForAttackDelay = (selfUnitInstance.AttackDelay > 0);
        LogNodeMessage($"PerformUnitAttackCycle: Attack landed on {currentTargetBuildingForThisNode.name}. Target HP: {currentTargetBuildingForThisNode.CurrentHealth}. Preparing for next AttackDelay ({selfUnitInstance.AttackDelay} beats). isWaiting: {isWaitingForAttackDelay}", isVerbose: true, forceLog: true); // LOG FORCE

        if (isWaitingForAttackDelay)
        {
            SubscribeToBeatForAttackDelay();
        }
        // Si pas de délai, OnUpdate relancera un nouveau cycle à la prochaine frame car nodeManagedAttackCycleCoroutine sera null.

        nodeManagedAttackCycleCoroutine = null; // Ce cycle d'attaque (un coup + setup délai) est terminé.
        LogNodeMessage($"PerformUnitAttackCycle: END for {currentTargetBuildingForThisNode?.name ?? "TARGET UNKNOWN"}. nodeManagedAttackCycleCoroutine set to null.", isVerbose:true, forceLog:true); // LOG FORCE
    }

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

    private void ResetNodeInternalState()
    {
        nodeManagedAttackCycleCoroutine = null;
        currentAttackBeatCounter = 0;
        isWaitingForAttackDelay = false;
        // hasSubscribedToBeatForAttackDelay est géré par Unsubscribe...
        LogNodeMessage("ResetNodeInternalState complete.", isVerbose: true, forceLog:true); // LOG FORCE
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null)
        {
            Debug.LogError($"[{nodeInstanceIdForLog} | {GameObject?.name}] CacheBlackboardVariables: Agent or BlackboardRef missing.", GameObject); // LOG FORCE
            return false;
        }
        var blackboard = agent.BlackboardReference;
        bool success = true;
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit)) { LogNodeMessage($"BBVar '{SELF_UNIT_VAR}' missing.", true, forceLog:true); success = false; }
        if (!blackboard.GetVariable(TARGET_BUILDING_VAR, out bbTargetBuilding)) { LogNodeMessage($"BBVar '{TARGET_BUILDING_VAR}' missing.", true, forceLog:true); success = false; }
        if (!blackboard.GetVariable(IS_ATTACKING_VAR, out bbIsAttackingBlackboard)) { LogNodeMessage($"BBVar '{IS_ATTACKING_VAR}' missing.", true, forceLog:true); success = false; }
        if (!blackboard.GetVariable(IS_OBJECTIVE_COMPLETED_VAR, out bbIsObjectiveCompleted)) { LogNodeMessage($"BBVar '{IS_OBJECTIVE_COMPLETED_VAR}' missing (Cannot set objective completed by this node).", false, forceLog:true); /* Peut être optionnel */}

        blackboardVariablesCached = success;
        if(!success) LogNodeMessage("CacheBlackboardVariables FAILED for one or more vars.", true, forceLog:true); // LOG FORCE
        else LogNodeMessage("CacheBlackboardVariables SUCCESS.", isVerbose:true, forceLog:true); // LOG FORCE
        return success;
    }

    private void SetIsAttackingBlackboardVar(bool value)
    {
        if (!blackboardVariablesCached) // Tenter de recacher si pas déjà fait
        {
            if (!CacheBlackboardVariables()) // Si le recache échoue
            {
                LogNodeMessage($"SetIsAttackingBlackboardVar: Cannot set '{IS_ATTACKING_VAR}', Blackboard variables not cached.", true, forceLog:true); // LOG FORCE
                return;
            }
        }
        // À ce point, blackboardVariablesCached devrait être true si CacheBlackboardVariables a réussi.

        if (bbIsAttackingBlackboard != null)
        {
            if (bbIsAttackingBlackboard.Value != value)
            {
                bbIsAttackingBlackboard.Value = value;
                LogNodeMessage($"SetIsAttackingBlackboardVar: Set BB '{IS_ATTACKING_VAR}' to {value}.", isVerbose: true, forceLog: true); // LOG FORCE
            }
        }
        else
        {
            LogNodeMessage($"SetIsAttackingBlackboardVar: bbIsAttackingBlackboard reference is null. Cannot set '{IS_ATTACKING_VAR}'.", true, forceLog:true); // LOG FORCE
        }
    }

    // Ajout d'un paramètre forceLog pour certains logs critiques
    private void LogNodeMessage(string message, bool isError = false, bool isVerbose = false, bool forceLog = false)
    {
        string unitName = selfUnitInstance != null ? selfUnitInstance.name : (bbSelfUnit?.Value != null ? bbSelfUnit.Value.name : "NoUnit");
        UnityEngine.Object contextObject = this.GameObject ?? (UnityEngine.Object)selfUnitInstance;
        string log = $"[{nodeInstanceIdForLog} | {unitName} | AttackBuildingNode] {message}";

        if (isError) Debug.LogError(log, contextObject);
    }
}