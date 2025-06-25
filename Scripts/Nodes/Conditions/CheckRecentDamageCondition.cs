using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Check Recent Damage (Seconds)",
    story: "Checks if unit was damaged within a time window (in seconds). If so, sets the attacker as the interaction target.",
    category: "Enemy Conditions",
    id: "EnemyCondition_CheckRecentDamage_v2" // Version 2, sans dépendance au rythme
)]
public class CheckRecentDamageCondition : Unity.Behavior.Condition
{
    [Tooltip("Le temps en secondes pendant lequel l'unité se souvient d'une attaque.")]
    public float ForgetTimeInSeconds = 4f;

    // Clés Blackboard
    private const string BB_SELF_UNIT = "SelfUnit";
    private const string BB_INTERACTION_TARGET_UNIT = "InteractionTargetUnit";

    // Cache des variables
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbInteractionTargetUnit;
    private bool blackboardVariablesCached = false;
    private BehaviorGraphAgent agent;

    /// <summary>
    /// Appelé une seule fois quand le nœud devient actif. Idéal pour mettre en cache les variables.
    /// </summary>
    public override void OnStart()
    {
        if (agent == null && GameObject != null)
        {
            agent = GameObject.GetComponent<BehaviorGraphAgent>();
        }
        // On met en cache les variables une seule fois pour la performance.
        CacheBlackboardVariables();
        Debug.Log($"[{GameObject?.name}] CheckRecentDamageCondition: Initialisation terminée. Variables mises en cache : {blackboardVariablesCached}");
    }

    /// <summary>
    /// La logique de la condition, exécutée à chaque frame où le BT l'évalue.
    /// </summary>
    public override bool IsTrue()
    {
        if (!blackboardVariablesCached || bbSelfUnit == null || bbInteractionTargetUnit == null)
        {
            Debug.LogError($"[{GameObject?.name}] CheckRecentDamageCondition: Variables Blackboard non mises en cache ou manquantes.");
            // Si le cache a échoué dans OnStart, on ne fait rien.
            return false;
        }

        var enemyUnit = bbSelfUnit.Value as EnemyUnit;
        if (enemyUnit == null || !enemyUnit.LastAttackerInfo.HasValue)
        {
            Debug.LogWarning($"[{GameObject?.name}] CheckRecentDamageCondition: Aucune unité ennemie valide ou pas d'attaquant enregistré.");
            return false; // Pas d'attaquant enregistré
        }

        var lastDamage = enemyUnit.LastAttackerInfo.Value;

        // --- LOGIQUE SIMPLIFIÉE ---
        // On compare directement le temps écoulé avec notre variable en secondes.
        float timeSinceDamage = Time.time - lastDamage.Time;

        if (timeSinceDamage <= ForgetTimeInSeconds && lastDamage.Attacker != null && lastDamage.Attacker.Health > 0)
        {
            // Menace récente et valide !
            bbInteractionTargetUnit.Value = lastDamage.Attacker;
            
            Debug.Log($"[{enemyUnit.name}] Réagit aux dégâts récents de {lastDamage.Attacker.name}.");
            return true; // Succès, on a une cible
        }

        return false; // Pas de menace récente ou la menace n'est plus valide
    }

    /// <summary>
    /// Appelé quand le nœud se termine ou est interrompu.
    /// </summary>
    public override void OnEnd()
    {
        // On réinitialise le cache pour la prochaine exécution.
        blackboardVariablesCached = false;
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        if (agent == null || agent.BlackboardReference == null)
        {
            Debug.LogError($"[{GameObject?.name}] CheckRecentDamageCondition: Agent ou Blackboard non trouvé.", GameObject);
            return false;
        }

        var blackboard = agent.BlackboardReference;
        bool success = true;

        if (!blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit)) success = false;
        if (!blackboard.GetVariable(BB_INTERACTION_TARGET_UNIT, out bbInteractionTargetUnit)) success = false;
        Debug.Log($"[{GameObject?.name}] CheckRecentDamageCondition: Variables Blackboard mises en cache : " +
                  $"{(bbSelfUnit != null ? BB_SELF_UNIT : "non trouvé")}, " +
                  $"{(bbInteractionTargetUnit != null ? BB_INTERACTION_TARGET_UNIT : "non trouvé")}");
        blackboardVariablesCached = success;
        return success;
    }
}