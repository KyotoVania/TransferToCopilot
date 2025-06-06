using UnityEngine;
using Unity.Behavior;

// S'assure que ce script s'exécute avant le Behavior Graph
[DefaultExecutionOrder(-100)] 
public class EnnemyBuildingBlackboardInitializer : MonoBehaviour
{
    // La clé que nous utiliserons dans le Blackboard. C'est plus propre que "SelfUnit".
    public const string BB_SELF_BUILDING = "SelfBuilding";

    void Awake()
    {
        var agent = GetComponent<BehaviorGraphAgent>();
        var building = GetComponent<Building>(); // On récupère le composant Building

        if (agent == null || agent.BlackboardReference == null || building == null)
        {
            Debug.LogError($"[{gameObject.name}] BuildingBlackboardInitializer: " +
                           "Composants critiques manquants (Agent, Blackboard ou Building)!", gameObject);
            return;
        }

        // On cherche la variable "SelfBuilding" de type Building dans le Blackboard
        if (agent.BlackboardReference.GetVariable(BB_SELF_BUILDING, out BlackboardVariable<Building> bbSelfBuilding))
        {
            // On lui assigne notre propre référence de bâtiment.
            bbSelfBuilding.Value = building;
            Debug.Log($"[{gameObject.name}] Initializer: Variable Blackboard '{BB_SELF_BUILDING}' initialisée avec succès.", gameObject);
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] Initializer: La variable Blackboard '{BB_SELF_BUILDING}' " +
                           "(de type Building) est INTROUVABLE sur l'asset Blackboard. Veuillez la créer.", gameObject);
        }
    }
}