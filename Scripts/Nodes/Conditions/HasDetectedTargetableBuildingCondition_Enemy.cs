using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Has Detected Targetable Building (Enemy)",
    story: "Has Detected Targetable Building (Enemy)",
    category: "Enemy Conditions",
    id: "EnemyCondition_HasDetectedBuilding_v1"
)]
public partial class HasDetectedTargetableBuildingCondition_Enemy : Unity.Behavior.Condition
{
    private BlackboardVariable<Building> bbDetectedTargetableBuilding;
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
                Debug.LogError("[HasDetectedTargetableBuildingCondition_Enemy] BehaviorGraphAgent or Blackboard not found.", GameObject);
                return;
            }
        }
        var blackboard = agent.BlackboardReference;
        if (!blackboard.GetVariable(EnemyUnit.BB_DETECTED_TARGETABLE_BUILDING, out bbDetectedTargetableBuilding))
        {
            Debug.LogWarning($"[HasDetectedTargetableBuildingCondition_Enemy] Blackboard variable '{EnemyUnit.BB_DETECTED_TARGETABLE_BUILDING}' not found.", GameObject);
        }
        blackboardVariableCached = true;
    }

    public override bool IsTrue()
    {
        if (bbDetectedTargetableBuilding == null)
        {
            CacheBlackboardVariable();
            if (bbDetectedTargetableBuilding == null) return false;
        }
        // Vérifie si le bâtiment détecté est non nul et encore "en vie" (PV > 0)
        return bbDetectedTargetableBuilding.Value != null && bbDetectedTargetableBuilding.Value.CurrentHealth > 0;
    }

    public override void OnEnd()
    {
        blackboardVariableCached = false;
        bbDetectedTargetableBuilding = null;
        base.OnEnd();
    }
}