using UnityEngine;
using Unity.Behavior; // Tu pourrais avoir besoin de Unity.Behavior.GraphFramework aussi
// using Unity.Behavior.GraphFramework; // Ajoute ceci si BlackboardVariable n'est pas trouvé

public class AllyUnitBlackboardInitializer : MonoBehaviour
{
    private BehaviorGraphAgent m_Agent;

    void Start()
    {
        m_Agent = GetComponent<BehaviorGraphAgent>();
        var allyUnit = GetComponent<Unit>(); // Bien, tu récupères le composant en tant que Unit

        if (m_Agent == null) Debug.LogError($"[{gameObject.name}] Initializer: m_Agent is NULL.");
        else if (m_Agent.BlackboardReference == null) Debug.LogError($"[{gameObject.name}] Initializer: m_Agent.BlackboardReference is NULL.");

        // Cette vérification est bonne
        if (allyUnit == null) Debug.LogError($"[{gameObject.name}] Initializer: allyUnit component (of type Unit) is NULL.");

        if (m_Agent == null || m_Agent.BlackboardReference == null || allyUnit == null)
        {
            Debug.LogError($"[{gameObject.name}] Initializer missing critical components! Cannot set SelfUnit.", gameObject);
            return;
        }

        var blackboardRef = m_Agent.BlackboardReference;

        // La variable sur le Blackboard doit être de type Unit (ou un parent compatible)
        BlackboardVariable<Unit> bbSelfUnitForGraph;
        if (blackboardRef.GetVariable("SelfUnit", out bbSelfUnitForGraph)) // Clé "SelfUnit"
        {
            bbSelfUnitForGraph.Value = allyUnit; // C'est correct, tu assignes l'instance de Unit (qui est en fait ton AllyUnit)
            Debug.Log($"[{gameObject.name}] Initializer: Successfully set 'SelfUnit' on Blackboard with component of type {allyUnit.GetType().Name}.", gameObject);
        }
        else
        {
            // Cette erreur est cruciale si elle apparaît
            Debug.LogError($"[{gameObject.name}] Initializer: Blackboard variable 'SelfUnit' (expecting type Unit) NOT FOUND on the Blackboard Asset. Ensure it exists and is correctly named.", gameObject);
        }
    }
}