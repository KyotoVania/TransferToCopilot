using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Is Building Capturable (Enemy)",
    story: "Is Building Capturable (Enemy)",
    category: "Enemy Conditions",
    id: "EnemyCondition_IsBuildingCapturable_v1"
)]
public partial class IsBuildingCapturableCondition_Enemy : Unity.Behavior.Condition
{
    private BlackboardVariable<Building> bbInteractionTargetBuilding;
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
                Debug.LogError("[IsBuildingCapturableCondition_Enemy] BehaviorGraphAgent or Blackboard not found.", GameObject);
                return;
            }
        }
        var blackboard = agent.BlackboardReference;
        if (!blackboard.GetVariable(EnemyUnit.BB_INTERACTION_TARGET_BUILDING, out bbInteractionTargetBuilding))
        {
            Debug.LogWarning($"[IsBuildingCapturableCondition_Enemy] Blackboard variable '{EnemyUnit.BB_INTERACTION_TARGET_BUILDING}' not found.", GameObject);
        }
        blackboardVariableCached = true;
    }

    public override bool IsTrue()
    {
        if (bbInteractionTargetBuilding == null)
        {
            CacheBlackboardVariable();
            if (bbInteractionTargetBuilding == null) return false;
        }

        var building = bbInteractionTargetBuilding.Value;
        if (building == null) return false;

        NeutralBuilding neutralBuilding = building as NeutralBuilding; //
        if (neutralBuilding == null || !neutralBuilding.IsRecapturable) return false; //

        // L'ennemi peut capturer les bâtiments Neutres ou ceux du Joueur (s'ils sont de type NeutralBuilding et IsRecapturable)
        return neutralBuilding.Team == TeamType.Neutral || neutralBuilding.Team == TeamType.Player;
    }

    public override void OnEnd()
    {
        blackboardVariableCached = false;
        bbInteractionTargetBuilding = null;
        base.OnEnd();
    }
}