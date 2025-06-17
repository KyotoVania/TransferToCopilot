using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[Condition(
    name: "Is Building Capture Needed",
    story: "Is Building Capture Needed",
    category: "Ally Conditions",
    id: "AllyCondition_IsBuildingCaptureNeeded_v1"
)]
public partial class IsBuildingCaptureNeededCondition : Unity.Behavior.Condition
{
    // La variable du Blackboard que ce nœud va lire.
    // Elle doit être définie par un nœud précédent, comme SelectTargetNode_Ally.
    private const string BB_INTERACTION_TARGET_BUILDING = "InteractionTargetBuilding";

    private BlackboardVariable<Building> bbTargetBuilding;
    private bool blackboardVariableCached = false;
    private BehaviorGraphAgent agent;

    public override void OnStart()
    {
        // On s'assure d'avoir une référence à l'agent et on met en cache la variable du Blackboard.
        if (agent == null && GameObject != null)
        {
            agent = GameObject.GetComponent<BehaviorGraphAgent>();
        }
        CacheBlackboardVariable();
    }

    /// <summary>
    /// La logique principale de la condition.
    /// </summary>
    /// <returns>True si le bâtiment n'est pas à l'équipe du joueur, sinon False.</returns>
    public override bool IsTrue()
    {
        if (bbTargetBuilding == null)
        {
            // Si le cache a échoué ou si la variable n'existe pas, on considère la condition comme fausse.
            if (!CacheBlackboardVariable() || bbTargetBuilding == null)
            {
                Debug.LogWarning($"[{GameObject?.name}] IsBuildingCaptureNeededCondition: La variable Blackboard '{BB_INTERACTION_TARGET_BUILDING}' est introuvable ou nulle.", GameObject);
                return false;
            }
        }

        Building targetBuilding = bbTargetBuilding.Value;

        // Si aucune cible n'est définie, il n'y a rien à capturer.
        if (targetBuilding == null)
        {
            return false;
        }

        // La condition est VRAIE si l'équipe du bâtiment N'EST PAS "Player".
        bool captureIsNeeded = targetBuilding.Team != TeamType.Player;

        // Log pour le débogage
        // Debug.Log($"[{GameObject?.name}] Checking if '{targetBuilding.name}' (Team: {targetBuilding.Team}) needs capture. Result: {captureIsNeeded}");

        return captureIsNeeded;
    }

    public override void OnEnd()
    {
        // Réinitialiser le cache pour la prochaine exécution.
        blackboardVariableCached = false;
        bbTargetBuilding = null;
    }

    private bool CacheBlackboardVariable()
    {
        if (blackboardVariableCached) return true;

        if (agent == null || agent.BlackboardReference == null)
        {
            return false;
        }

        var blackboard = agent.BlackboardReference;
        if (!blackboard.GetVariable(BB_INTERACTION_TARGET_BUILDING, out bbTargetBuilding))
        {
            // Ce n'est pas forcément une erreur, la variable peut ne pas être définie à chaque tick.
            // On le marque comme "caché" pour éviter de vérifier à chaque frame.
            blackboardVariableCached = true;
            return false;
        }

        blackboardVariableCached = true;
        return true;
    }
}