using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Is Enemy Mode",
    story: "Is Enemy Mode",
    category: "Enemy Conditions", // Nouvelle catégorie
    id: "EnemyCondition_IsEnemyMode_v1"
)]
public partial class IsEnemyModeCondition : Unity.Behavior.Condition
{
    [Tooltip("The behavior mode to check for.")]
    public CurrentBehaviorMode DesiredMode;

    private BlackboardVariable<CurrentBehaviorMode> bbCurrentBehaviorMode;
    private bool blackboardVariableCached = false;
    private BehaviorGraphAgent agent;


    public override void OnStart()
    {
        base.OnStart(); // Appelle base.OnStart() pour initialiser GameObject
        if (GameObject != null)
        {
            agent = GameObject.GetComponent<BehaviorGraphAgent>();
        }
        CacheBlackboardVariable();
    }

    private void CacheBlackboardVariable()
    {
        if (blackboardVariableCached) return;
        if (agent == null || agent.BlackboardReference == null)
        {
            if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>(); // Tentative de récupération
            if (agent == null || agent.BlackboardReference == null)
            {
                Debug.LogError("[IsEnemyModeCondition] BehaviorGraphAgent or Blackboard not found.", GameObject);
                return;
            }
        }
        var blackboard = agent.BlackboardReference;
        if (!blackboard.GetVariable(EnemyUnit.BB_CURRENT_BEHAVIOR_MODE, out bbCurrentBehaviorMode))
        {
            Debug.LogWarning($"[IsEnemyModeCondition] Blackboard variable '{EnemyUnit.BB_CURRENT_BEHAVIOR_MODE}' not found.", GameObject);
        }
        blackboardVariableCached = true; // Marquer comme mis en cache même si non trouvé pour éviter les logs répétés
    }

    public override bool IsTrue()
    {
        if (bbCurrentBehaviorMode == null)
        {
            CacheBlackboardVariable(); // Essayer de recacher si null
            if (bbCurrentBehaviorMode == null)
            {
                // Log si toujours null après tentative, mais ne pas spammer
                // Debug.LogWarning($"[IsEnemyModeCondition] IsTrue: '{EnemyUnit.BB_CURRENT_BEHAVIOR_MODE}' not available.", GameObject);
                return false;
            }
        }
        return bbCurrentBehaviorMode.Value == DesiredMode;
    }

    public override void OnEnd()
    {
        // Réinitialiser le cache pour la prochaine utilisation potentielle
        blackboardVariableCached = false;
        bbCurrentBehaviorMode = null;
        // agent = null; // l'agent peut persister
        base.OnEnd();
    }
}