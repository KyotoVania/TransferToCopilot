using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Has No Initial Objective Set",
    story: "Has No Initial Objective Set",
    category: "Ally Conditions",
    id: "AllyCondition_HasNoInitialObjectiveSet_v1"
)]
public partial class HasNoInitialObjectiveSetCondition : Unity.Behavior.Condition
{
    private const string BB_HAS_INITIAL_OBJECTIVE_SET = "HasInitialObjectiveSet";
    
    private BlackboardVariable<bool> bbHasInitialObjectiveSet;
    private bool blackboardVariableCached = false;
    private BehaviorGraphAgent agent;

    public override void OnStart()
    {
        base.OnStart();
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        CacheBlackboardVariable();
    }

    private void CacheBlackboardVariable()
    {
        if (blackboardVariableCached) return;
        
        if (agent == null || agent.BlackboardReference == null)
        {
            if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
            if (agent == null || agent.BlackboardReference == null)
            {
                Debug.LogError("[HasNoInitialObjectiveSetCondition] BehaviorGraphAgent or Blackboard not found.", GameObject);
                return;
            }
        }
        
        var blackboard = agent.BlackboardReference;
        if (!blackboard.GetVariable(BB_HAS_INITIAL_OBJECTIVE_SET, out bbHasInitialObjectiveSet))
        {
            Debug.LogWarning($"[HasNoInitialObjectiveSetCondition] Blackboard variable '{BB_HAS_INITIAL_OBJECTIVE_SET}' not found.", GameObject);
        }
        
        blackboardVariableCached = true;
    }

    public override bool IsTrue()
    {
        if (bbHasInitialObjectiveSet == null)
        {
            CacheBlackboardVariable();
            if (bbHasInitialObjectiveSet == null) 
            {
                // Si la variable n'existe pas, on considère qu'il n'y a pas d'objectif
                return true;
            }
        }
        
        // Retourne TRUE si l'objectif N'EST PAS set (inverse de HasInitialObjectiveSetCondition)
        return !bbHasInitialObjectiveSet.Value;
    }

    public override void OnEnd()
    {
        blackboardVariableCached = false;
        bbHasInitialObjectiveSet = null;
        base.OnEnd();
    }
}