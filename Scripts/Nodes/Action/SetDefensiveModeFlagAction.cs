using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(name: "Set Defensive Mode Flag", story: "Sets the [IsInDefensiveMode] to [TargetDefensiveModeState].", category: "Action", id: "AllyAction_SetDefensiveMode_v1")]
public partial class SetDefensiveModeFlagAction : Unity.Behavior.Action
{
    [SerializeReference] public BlackboardVariable<bool> TargetDefensiveModeState = new();
    
    // Blackboard variable Noms
    private const string IS_IN_DEFENSIVE_MODE_VAR = "IsInDefensiveMode";
    private const string SELF_UNIT_VAR = "SelfUnit"; // Pour logs

    // Cache des variables Blackboard
    private BlackboardVariable<bool> bbIsInDefensiveMode;
    private BlackboardVariable<Unit> bbSelfUnit; // Pour logs
    private bool blackboardVariableCached = false;
    private BehaviorGraphAgent agent;

    protected override Status OnStart()
    {
        base.OnStart();
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();

        if (!CacheBlackboardVariable())
        {
            Debug.LogError($"[{GameObject?.name} - SetDefensiveModeAction] Failed to cache Blackboard variable '{IS_IN_DEFENSIVE_MODE_VAR}'. Action failed.", GameObject);
            return Status.Failure;
        }

        if (bbIsInDefensiveMode != null)
        {
            // Utilise la valeur du BlackboardVariable au lieu du champ direct
            bbIsInDefensiveMode.Value = TargetDefensiveModeState.Value;
            return Status.Success;
        }
        else
        {
            Debug.LogError($"[{GameObject?.name} - SetDefensiveModeAction] bbIsInDefensiveMode is null even after cache attempt. Action failed.", GameObject);
            return Status.Failure;
        }
    }

    private bool CacheBlackboardVariable()
    {
        if (blackboardVariableCached) return true;

        if (agent == null || agent.BlackboardReference == null)
        {
            if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
            if (agent == null || agent.BlackboardReference == null) return false;
        }
        var blackboard = agent.BlackboardReference;

        bool success = blackboard.GetVariable(IS_IN_DEFENSIVE_MODE_VAR, out bbIsInDefensiveMode);
        blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit);

        blackboardVariableCached = success;
        return success;
    }

    protected override Status OnUpdate()
    {
        return Status.Success;
    }

    protected override void OnEnd()
    {
        blackboardVariableCached = false;
        bbIsInDefensiveMode = null;
        bbSelfUnit = null;
        base.OnEnd();
    }
}