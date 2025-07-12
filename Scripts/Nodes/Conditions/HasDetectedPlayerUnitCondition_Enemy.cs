using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Has Detected Player Unit (Enemy)",
    story: "Has Detected Player Unit (Enemy)",
    category: "Enemy Conditions",
    id: "EnemyCondition_HasDetectedPlayer_v1"
)]
public partial class HasDetectedPlayerUnitCondition_Enemy : Unity.Behavior.Condition
{
   
    private BlackboardVariable<Unit> bbDetectedPlayerUnit;

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
                Debug.LogError("[HasDetectedPlayerUnitCondition_Enemy] BehaviorGraphAgent or Blackboard not found.", GameObject);
                return;
            }
        }
        var blackboard = agent.BlackboardReference;
        // On cherche maintenant une variable de type Unit, ce qui réussira.
        if (!blackboard.GetVariable(EnemyUnit.BB_DETECTED_PLAYER_UNIT, out bbDetectedPlayerUnit))
        {
            Debug.LogWarning($"[HasDetectedPlayerUnitCondition_Enemy] Blackboard variable '{EnemyUnit.BB_DETECTED_PLAYER_UNIT}' not found.", GameObject);
        }
        blackboardVariableCached = true;
    }

    public override bool IsTrue()
    {
        if (bbDetectedPlayerUnit == null)
        {
            CacheBlackboardVariable();
            if (bbDetectedPlayerUnit == null) return false;
        }
        // La logique reste la même : on vérifie juste si la variable n'est pas nulle et si l'unité est en vie.
        return bbDetectedPlayerUnit.Value != null && bbDetectedPlayerUnit.Value.Health > 0;
    }

    public override void OnEnd()
    {
        blackboardVariableCached = false;
        bbDetectedPlayerUnit = null;
        base.OnEnd();
    }
}