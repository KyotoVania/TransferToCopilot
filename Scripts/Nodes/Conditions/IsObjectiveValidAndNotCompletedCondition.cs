using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Is Objective Valid & Not Completed (Enemy)",
    story: "Is Objective Valid & Not Completed (Enemy)",
    category: "Enemy Conditions",
    id: "EnemyCondition_IsObjectiveValidNotCompleted_v1"
)]
public partial class IsObjectiveValidAndNotCompletedCondition : Unity.Behavior.Condition
{
    private BlackboardVariable<Building> bbObjectiveBuilding;
    private BlackboardVariable<bool> bbIsObjectiveCompleted;
    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;

    public override void OnStart()
    {
        base.OnStart();
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
                 Debug.LogError("[IsObjectiveValidAndNotCompletedCondition] BehaviorGraphAgent or Blackboard not found.", GameObject);
                return;
            }
        }
        var blackboard = agent.BlackboardReference;
        bool foundAll = true;
        if (!blackboard.GetVariable(EnemyUnit.BB_OBJECTIVE_BUILDING, out bbObjectiveBuilding))
        {
            Debug.LogWarning($"[IsObjectiveValidAndNotCompletedCondition] Blackboard variable '{EnemyUnit.BB_OBJECTIVE_BUILDING}' not found.", GameObject);
            foundAll = false;
        }
        if (!blackboard.GetVariable(EnemyUnit.BB_IS_OBJECTIVE_COMPLETED, out bbIsObjectiveCompleted))
        {
            Debug.LogWarning($"[IsObjectiveValidAndNotCompletedCondition] Blackboard variable '{EnemyUnit.BB_IS_OBJECTIVE_COMPLETED}' not found.", GameObject);
            foundAll = false;
        }
        blackboardVariablesCached = foundAll;
    }

    public override bool IsTrue()
    {
        if (bbObjectiveBuilding == null || bbIsObjectiveCompleted == null)
        {
            CacheBlackboardVariables();
            if (bbObjectiveBuilding == null || bbIsObjectiveCompleted == null) return false;
        }

        var objective = bbObjectiveBuilding.Value;
        if (objective == null || objective.CurrentHealth <= 0) return false; // Objectif invalide ou détruit

        // Si l'objectif est un bâtiment ennemi (du point de vue de l'IA ennemie), il ne peut pas être un "objectif à compléter" de cette manière.
        // Un objectif est généralement quelque chose à capturer (Neutre, Joueur) ou à détruire (Joueur).
        if (objective.Team == TeamType.Enemy)
        {
            // Si c'est un NeutralBuilding qui a été recapturé par l'ennemi, il est "complété" pour cette unité si son but était de le prendre.
            // Mais si le but est de défendre un bâtiment ennemi, cette condition est mal nommée.
            // Pour l'instant, considérons qu'un objectif ennemi n'est pas "à compléter" par une attaque/capture ennemie.
            // Cela dépendra de la sémantique exacte de votre jeu.
            // Potentiellement, bbIsObjectiveCompleted devrait être le seul juge.
        }

        return !bbIsObjectiveCompleted.Value;
    }

    public override void OnEnd()
    {
        blackboardVariablesCached = false;
        bbObjectiveBuilding = null;
        bbIsObjectiveCompleted = null;
        base.OnEnd();
    }
}