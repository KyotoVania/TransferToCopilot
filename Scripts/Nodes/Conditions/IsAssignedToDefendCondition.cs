using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Is Assigned To Defend",
    story: "Checks if the unit's initial objective is a valid PlayerBuilding to defend.",
    category: "Ally Conditions",
    id: "AllyCondition_IsAssignedToDefend_v1" 
)]
public partial class IsAssignedToDefendCondition : Unity.Behavior.Condition
{
    // Blackboard variable Noms
    private const string SELF_UNIT_VAR = "SelfUnit"; // Utilisé pour obtenir des logs plus clairs
    private const string INITIAL_TARGET_BUILDING_VAR = "InitialTargetBuilding";

    // Cache des variables Blackboard
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Building> bbInitialTargetBuilding;
    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;

    public override void OnStart()
    {
        base.OnStart(); // Initialise GameObject
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        CacheBlackboardVariables();
    }

    private void CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return;
        if (agent == null || agent.BlackboardReference == null)
        {
            if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
            if (agent == null || agent.BlackboardReference == null)
            {
                Debug.LogError($"[{GameObject?.name} - IsAssignedToDefendCondition] BehaviorGraphAgent or Blackboard not found.", GameObject);
                return; // Empêche les erreurs si le blackboard n'est pas prêt
            }
        }
        var blackboard = agent.BlackboardReference;
        bool success = true;
        if (!blackboard.GetVariable(INITIAL_TARGET_BUILDING_VAR, out bbInitialTargetBuilding))
        {
            Debug.LogWarning($"[{GameObject?.name} - IsAssignedToDefendCondition] Blackboard variable '{INITIAL_TARGET_BUILDING_VAR}' not found.", GameObject);
            success = false; // Peut être critique si la logique en dépend toujours
        }
        // bbSelfUnit est optionnel ici, mais utile pour les logs
        blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit);

        blackboardVariablesCached = success; // Ne devient true que si les variables critiques sont trouvées
    }

    public override bool IsTrue()
    {
        if (!blackboardVariablesCached) // Tentative de recache si OnStart n'a pas réussi
        {
            CacheBlackboardVariables();
            if (!blackboardVariablesCached) return false; // Si toujours pas mis en cache, échouer
        }

        Building initialBuilding = bbInitialTargetBuilding?.Value;
        Unit self = bbSelfUnit?.Value; // Pour les logs

        if (initialBuilding == null)
        {
            // Si enableVerboseLogging est une propriété de votre AllyUnit, vous pouvez y accéder via self
            // if (self is AllyUnit allySelf && allySelf.enableVerboseLogging)
            //     Debug.Log($"[{self?.name} - IsAssignedToDefendCondition] InitialTargetBuilding is null. Returning False.");
            return false;
        }

        bool isPlayerBuilding = initialBuilding is PlayerBuilding; // Ou initialBuilding.Team == TeamType.Player
        bool isAlive = initialBuilding.CurrentHealth > 0;

        bool result = isPlayerBuilding && isAlive;
        // if (self is AllyUnit allySelf && allySelf.enableVerboseLogging)
        //    Debug.Log($"[{self?.name} - IsAssignedToDefendCondition] Initial: {initialBuilding.name}, IsPlayerBuilding: {isPlayerBuilding}, IsAlive: {isAlive}. Result: {result}");

        return result;
    }

    public override void OnEnd()
    {
        blackboardVariablesCached = false; // Prêt pour la prochaine fois
        bbInitialTargetBuilding = null;
        bbSelfUnit = null;
        base.OnEnd();
    }
}