using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Has Detected Enemy Unit",
    story: "Checks if an enemy unit has been detected",
    category: "Ally Conditions",
    id: "AllyCondition_HasDetectedEnemyUnit_v1"
)]
public class HasDetectedEnemyUnitCondition : Unity.Behavior.Condition
{
    private const string DETECTED_ENEMY_UNIT_VAR = "DetectedEnemyUnit";
    private BlackboardVariable<Unit> bbDetectedEnemyUnit;
    private bool blackboardVariableCached = false;
    private BehaviorGraphAgent agent;
    private bool blackboardVariablesCached = false;

    public override void OnStart()
    {
        base.OnStart();
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        CacheBlackboardVariables();
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        if (agent == null || agent.BlackboardReference == null)
        {
            if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
            if (agent == null || agent.BlackboardReference == null) {
                Debug.LogError($"[ScanForNearbyTargetsNode(Ally)] CacheBB: Agent or BlackboardRef missing on {GameObject?.name}.", GameObject);
                return false;
            }
        }
        var blackboard = agent.BlackboardReference;
        bool allEssentialFound = true;

        if (!blackboard.GetVariable(DETECTED_ENEMY_UNIT_VAR, out bbDetectedEnemyUnit))
        { 
            Debug.LogWarning($"[ScanForNearbyTargetsNode(Ally)] CacheBB: Blackboard variable '{DETECTED_ENEMY_UNIT_VAR}' not found.", GameObject);
            allEssentialFound = false;
        }

        blackboardVariablesCached = allEssentialFound;
        if (!allEssentialFound)
        {
            Debug.LogWarning($"[ScanForNearbyTargetsNode(Ally)] CacheBB: Not all essential variables found on {GameObject?.name}.", GameObject);
        }
        return blackboardVariablesCached;
    }

    public override bool IsTrue()
    {
        if (bbDetectedEnemyUnit == null)
        {
            CacheBlackboardVariables();
            if (bbDetectedEnemyUnit == null) return false;
        }
        return bbDetectedEnemyUnit.Value;
    }

    public override void OnEnd()
    {
        blackboardVariableCached = false;
        bbDetectedEnemyUnit = null;
        base.OnEnd();
    }
}