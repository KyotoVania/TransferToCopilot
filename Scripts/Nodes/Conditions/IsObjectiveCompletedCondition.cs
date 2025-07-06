using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Is Objective Completed",
    story: "Checks if the main objective (building or unit) is completed/destroyed.",
    category: "Ally Conditions",
    id: "AllyCondition_IsObjectiveCompleted_v1"
)]
public partial class IsObjectiveCompletedCondition : Unity.Behavior.Condition
{
    // --- Clés des variables du Blackboard ---
    private const string BB_HAS_INITIAL_OBJECTIVE_SET = "HasInitialObjectiveSet";
    private const string BB_INITIAL_TARGET_BUILDING = "InitialTargetBuilding";
    private const string BB_INTERACTION_TARGET_UNIT = "InteractionTargetUnit";

    // --- Cache des variables du Blackboard ---
    private BlackboardVariable<bool> bbHasInitialObjectiveSet;
    private BlackboardVariable<Building> bbInitialTargetBuilding;
    private BlackboardVariable<Unit> bbInteractionTargetUnit;

    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;

    public override void OnStart()
    {
        base.OnStart();
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        CacheBlackboardVariables();
    }

    public override bool IsTrue()
    {
        if (!blackboardVariablesCached || bbHasInitialObjectiveSet?.Value == false)
        {
            // S'il n'y a jamais eu d'objectif, on ne considère pas qu'il est "complété".
            return false;
        }

        var targetBuilding = bbInitialTargetBuilding?.Value;
        var targetUnit = bbInteractionTargetUnit?.Value;

        // L'objectif est un bâtiment
        if (targetBuilding != null)
        {
            // La condition est vraie si le bâtiment a été détruit (l'objet est null)
            // ou si sa vie est à zéro, ou s'il est devenu allié (capturé).
            return targetBuilding == null || targetBuilding.CurrentHealth <= 0 || targetBuilding.Team == TeamType.Player;
        }

        // L'objectif est une unité (Boss)
        if (targetUnit != null)
        {
            // La condition est vraie si l'unité a été détruite (l'objet est null)
            // ou si sa vie est à zéro.
            return targetUnit == null || targetUnit.Health <= 0;
        }

        // Si on arrive ici, cela veut dire qu'un objectif était fixé mais que les variables
        // sont maintenant nulles (par exemple, un bâtiment a été détruit et la référence a disparu).
        // Dans ce cas, l'objectif EST complété.
        return true;
    }

    private void CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return;
        if (agent == null || agent.BlackboardReference == null) return;

        var blackboard = agent.BlackboardReference;
        bool success = true;

        if (!blackboard.GetVariable(BB_HAS_INITIAL_OBJECTIVE_SET, out bbHasInitialObjectiveSet)) success = false;
        if (!blackboard.GetVariable(BB_INITIAL_TARGET_BUILDING, out bbInitialTargetBuilding)) success = false;
        if (!blackboard.GetVariable(BB_INTERACTION_TARGET_UNIT, out bbInteractionTargetUnit)) success = false;

        blackboardVariablesCached = success;
    }

    public override void OnEnd()
    {
        blackboardVariablesCached = false;
        agent = null;
        base.OnEnd();
    }
}