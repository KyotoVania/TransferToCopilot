// Fichier: Scripts/Nodes/CaptureBuildingNode.cs
using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System.Collections;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Capture Building",
    story: "Capture Building",
    category: "My Actions",
    id: "YOUR_UNIQUE_ID_CaptureBuilding_Active_V1" // Nouvel ID pour cette version
)]
public class CaptureBuildingNode : Unity.Behavior.Action
{
    // Constants for Blackboard variable names
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string TARGET_BUILDING_VAR = "InteractionTargetBuilding";
    private const string IS_CAPTURING_VAR = "IsCapturing";
    private const string BB_INITIAL_TARGET_BUILDING = "InitialTargetBuilding"; 
    private const string BB_IS_OBJECTIVE_COMPLETED = "IsObjectiveCompleted";   

    // La variable IS_OBJECTIVE_COMPLETED_VAR n'est plus gérée directement par ce noeud.

    // --- Node State ---
    private bool blackboardVariablesCached = false;
    private Unit selfUnitInstance = null;
    private NeutralBuilding currentTargetBuildingInstance = null;

    // --- Blackboard Variable Cache ---
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Building> bbTargetBuilding;
    private BlackboardVariable<bool> bbIsCapturingBlackboard;
    
    private BlackboardVariable<Building> bbInitialTargetBuilding; 
    private BlackboardVariable<bool> bbIsObjectiveCompleted; 
    private string nodeInstanceIdForLog;
    private bool captureSuccessfullyInitiated = false;

    protected override Status OnStart()
    {
        nodeInstanceIdForLog = Guid.NewGuid().ToString("N").Substring(0, 6);
        LogNodeMessage("OnStart - Beginning.", false, true);
        ResetNodeInternalState();

        if (!CacheBlackboardVariables())
        {
            LogNodeMessage("CRITICAL: Failed to cache Blackboard variables. Node Failure.", true, true);
            SetIsCapturingBlackboardVar(false);
            return Status.Failure;
        }

        selfUnitInstance = bbSelfUnit?.Value;
        Building generalTargetBuilding = bbTargetBuilding?.Value;

        if (selfUnitInstance == null)
        {
            LogNodeMessage($"'{SELF_UNIT_VAR}' value is NULL. Node Failure.", true, true);
            SetIsCapturingBlackboardVar(false);
            return Status.Failure;
        }

        if (generalTargetBuilding == null)
        {
            LogNodeMessage($"'{TARGET_BUILDING_VAR}' value is NULL. No building to capture. Node Failure.", false, true);
            SetIsCapturingBlackboardVar(false);
            return Status.Failure;
        }

        currentTargetBuildingInstance = generalTargetBuilding as NeutralBuilding;
        if (currentTargetBuildingInstance == null)
        {
            LogNodeMessage($"Target Building '{generalTargetBuilding.name}' is not a NeutralBuilding. Cannot capture. Node Failure.", false, true);
            SetIsCapturingBlackboardVar(false);
            return Status.Failure;
        }

        if (!currentTargetBuildingInstance.IsRecapturable)
        {
            LogNodeMessage($"Target Building '{currentTargetBuildingInstance.name}' is not recapturable. Node Failure.", false, true);
            SetIsCapturingBlackboardVar(false);
            return Status.Failure;
        }

        if (currentTargetBuildingInstance.Team == TeamType.Player)
        {
            LogNodeMessage($"Target Building '{currentTargetBuildingInstance.name}' already belongs to Player. No capture needed. Node SUCCESS.", false, true);
            SetIsCapturingBlackboardVar(false);
            // La logique de complétion d'objectif est maintenant gérée par AllyUnit.OnCaptureComplete si ce bâtiment était l'objectif.
            return Status.Success;
        }

        if (!selfUnitInstance.IsBuildingInCaptureRange(currentTargetBuildingInstance))
        {
            LogNodeMessage($"Target Building '{currentTargetBuildingInstance.name}' is out of capture range. Node Failure.", false, true);
            SetIsCapturingBlackboardVar(false);
            return Status.Failure;
        }

        captureSuccessfullyInitiated = selfUnitInstance.PerformCapture(currentTargetBuildingInstance);

        if (captureSuccessfullyInitiated)
        {
            LogNodeMessage($"Capture INITIATED on '{currentTargetBuildingInstance.name}'. Setting BB '{IS_CAPTURING_VAR}' to true. Node will run.", false, true);
            SetIsCapturingBlackboardVar(true);
            return Status.Running;
        }
        else
        {
            LogNodeMessage($"Failed to INITIATE capture on '{currentTargetBuildingInstance.name}'. Node Failure.", false, true);
            SetIsCapturingBlackboardVar(false);
            return Status.Failure;
        }
    }

    protected override Status OnUpdate()
    {
        if (!captureSuccessfullyInitiated || selfUnitInstance == null || currentTargetBuildingInstance == null)
        {
            LogNodeMessage("OnUpdate: State invalid (capture not initiated or unit/target null). Node Failure.", true, true);
            // S'assurer que IsCapturing est false si on sort en échec ici
            SetIsCapturingBlackboardVar(false);
            return Status.Failure;
        }

        if (!currentTargetBuildingInstance.gameObject.activeInHierarchy || currentTargetBuildingInstance.CurrentHealth <= 0) { // Vérifier si le GO du bâtiment est encore actif
            LogNodeMessage($"Target '{currentTargetBuildingInstance.name}' destroyed or inactive. Capture ended. Node SUCCESS.", false, true);
            
            return Status.Success;
        }

        if (currentTargetBuildingInstance.Team == TeamType.Player)
        {
            LogNodeMessage($"Target '{currentTargetBuildingInstance.name}' has been captured by Player. Capture SUCCESS.", false, true);


            var initialObjective = bbInitialTargetBuilding?.Value;
            if (initialObjective != null && initialObjective == currentTargetBuildingInstance)
            {
                if (bbIsObjectiveCompleted != null)
                {
                    LogNodeMessage($"L'objectif principal '{initialObjective.name}' est complété. Mise à jour du Blackboard.", false, true);
                    bbIsObjectiveCompleted.Value = true;
                }
            }

            return Status.Success;
        }

        if (!selfUnitInstance.IsBuildingInCaptureRange(currentTargetBuildingInstance))
        {
            LogNodeMessage($"Unit '{selfUnitInstance.name}' moved out of capture range of '{currentTargetBuildingInstance.name}'. Capture interrupted. Node FAILURE.", false, true);
            selfUnitInstance.StopCapturing();
            // SetIsCapturingBlackboardVar(false) sera fait dans OnEnd.
            return Status.Failure;
        }

        if (currentTargetBuildingInstance.IsBeingCaptured && currentTargetBuildingInstance.CapturingInProgressByTeam != TeamType.Player)
        {
            LogNodeMessage($"Target '{currentTargetBuildingInstance.name}' is now being captured by {currentTargetBuildingInstance.CapturingInProgressByTeam}. Player capture interrupted. Node FAILURE.", false, true);
            selfUnitInstance.StopCapturing();
            // SetIsCapturingBlackboardVar(false) sera fait dans OnEnd.
            return Status.Failure;
        }

        // Si on arrive ici, la capture est toujours "activement tentée" par ce noeud.
        // On vérifie si le flag blackboard IsCapturing est toujours vrai.
        // Si une autre logique (ex: AllyUnit.OnCaptureComplete) l'a mis à false, alors le noeud doit réussir.
        if (bbIsCapturingBlackboard != null && !bbIsCapturingBlackboard.Value)
        {
            LogNodeMessage($"Blackboard var '{IS_CAPTURING_VAR}' is false, but node was still running. Capture likely completed by unit. Node SUCCESS.", false, true);
            return Status.Success;
        }


        return Status.Running;
    }

    protected override void OnEnd()
    {
        LogNodeMessage($"OnEnd called. Status: {CurrentStatus}. Cleaning up.", false, true);

        if (captureSuccessfullyInitiated && selfUnitInstance != null && currentTargetBuildingInstance != null)
        {
            // Si le CurrentStatus n'est pas Success (donc Failure ou Aborted),
            // ET que le bâtiment n'appartient toujours pas au joueur,
            // cela signifie que la capture a été interrompue.
            if (CurrentStatus != Status.Success && currentTargetBuildingInstance.Team != TeamType.Player)
            {
                LogNodeMessage($"OnEnd: Capture of '{currentTargetBuildingInstance.name}' ended prematurely (Status: {CurrentStatus}). Calling StopCapturing on unit.", false, true);
                selfUnitInstance.StopCapturing();
            }
        }
        // Toujours s'assurer que IsCapturing est false en sortant de ce noeud,
        // car le noeud n'est plus activement en train de gérer la capture.
        SetIsCapturingBlackboardVar(false);

        ResetNodeInternalState();
    }

    private void ResetNodeInternalState()
    {
        selfUnitInstance = null;
        currentTargetBuildingInstance = null;
        captureSuccessfullyInitiated = false;
        // blackboardVariablesCached est géré par OnEnd / CacheBlackboardVariables
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null)
        {
            Debug.LogError($"[{nodeInstanceIdForLog} | {GameObject?.name}] CacheBB: Agent or BlackboardRef missing.", GameObject);
            return false;
        }
        var blackboard = agent.BlackboardReference;
        bool success = true;

        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit))
        { LogNodeMessage($"BBVar '{SELF_UNIT_VAR}' missing.", true); success = false; }

        if (!blackboard.GetVariable(TARGET_BUILDING_VAR, out bbTargetBuilding))
        { LogNodeMessage($"BBVar '{TARGET_BUILDING_VAR}' missing.", true); success = false; }

        if (!blackboard.GetVariable(IS_CAPTURING_VAR, out bbIsCapturingBlackboard))
        { LogNodeMessage($"BBVar Output '{IS_CAPTURING_VAR}' missing.", true); success = false; }

        if (!blackboard.GetVariable(BB_INITIAL_TARGET_BUILDING, out bbInitialTargetBuilding))
        { 
            // Ce n'est pas une erreur fatale, car on peut capturer un bâtiment qui n'est pas l'objectif principal
            LogNodeMessage($"BBVar '{BB_INITIAL_TARGET_BUILDING}' manquant (optionnel).", false); 
        }

        if (!blackboard.GetVariable(BB_IS_OBJECTIVE_COMPLETED, out bbIsObjectiveCompleted))
        { 
            LogNodeMessage($"BBVar '{BB_IS_OBJECTIVE_COMPLETED}' manquant.", true); 
            success = false; // C'est critique, car on ne peut pas signaler la fin de la mission
        }
        blackboardVariablesCached = success;
        if (!success)
        {
            LogNodeMessage("CacheBB: CRITICAL - Failed to cache one or more ESSENTIAL Blackboard variables.", true, true);
        }
        return success;
    }

    private void SetIsCapturingBlackboardVar(bool value)
    {
        if (bbIsCapturingBlackboard != null)
        {
            if (bbIsCapturingBlackboard.Value != value)
            {
                bbIsCapturingBlackboard.Value = value;
                LogNodeMessage($"Set Blackboard Var '{IS_CAPTURING_VAR}' to {value}", false, true);
            }
        }
        else if (blackboardVariablesCached)
        {
            LogNodeMessage($"Cannot set Blackboard Var '{IS_CAPTURING_VAR}': bbIsCapturingBlackboard reference is null despite cache success.", true, true);
        }
    }

    // Supprimé: private void CheckAndSignalObjectiveCompletion()

    private void LogNodeMessage(string message, bool isError = false, bool forceLog = false)
    {
        Unit unit = selfUnitInstance ?? bbSelfUnit?.Value;
        string unitName = unit?.name ?? GameObject?.name ?? "CaptureBuildingNode";
        string logPrefix = $"[{nodeInstanceIdForLog} | {unitName} | CaptureBuildingNode(Active)]";

        if (isError)
        {
            Debug.LogError($"{logPrefix} {message}", GameObject);
        }
    }
}