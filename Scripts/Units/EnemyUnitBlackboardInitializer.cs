using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;

// S'assurer que cet initialiseur s'exécute avant les graphs par défaut
[DefaultExecutionOrder(-100)] // Exécuter avant les scripts par défaut (Default Time)
public class EnemyUnitBlackboardInitializer : MonoBehaviour
{
    private BehaviorGraphAgent m_Agent;
    private Unit m_EnemyUnit; // Renommé pour clarté

    // Awake est appelé avant tous les Start()
    void Awake()
    {
        m_Agent = GetComponent<BehaviorGraphAgent>();
        m_EnemyUnit = GetComponent<Unit>(); // Récupère le composant EnemyUnit

        if (m_Agent == null)
        {
            Debug.LogError($"[{gameObject.name}] EnemyUnitBlackboardInitializer: BehaviorGraphAgent component not found!", gameObject);
            enabled = false; // Désactiver si l'agent est manquant
            return;
        }
        if (m_EnemyUnit == null)
        {
            Debug.LogError($"[{gameObject.name}] EnemyUnitBlackboardInitializer: EnemyUnit component not found!", gameObject);
            enabled = false; // Désactiver si l'unité est manquante
            return;
        }

        // L'initialisation du Blackboard peut se faire ici ou dans Start().
        // Le faire dans Awake est généralement plus sûr pour les dépendances.
        InitializeBlackboard();
    }

    // La méthode Start du BehaviorGraphAgent s'exécutera après cet Awake.
    // Si vous préférez initialiser dans Start, assurez-vous que cet ordre est respecté
    // via Script Execution Order settings.
    void Start()
    {
        // Si le Blackboard n'a pas pu être initialisé dans Awake (par exemple, BlackboardReference était null à ce moment-là),
        // on peut tenter une nouvelle fois ici. C'est une sécurité.
        if (m_Agent != null && m_Agent.BlackboardReference != null && m_Agent.BlackboardReference.GetVariable(EnemyUnit.BB_SELF_UNIT, out BlackboardVariable<EnemyUnit> temp) && temp.Value == null)
        {
            Debug.LogWarning($"[{gameObject.name}] EnemyUnitBlackboardInitializer: Re-attempting Blackboard initialization in Start().", gameObject);
            InitializeBlackboard();
        }
    }


    void InitializeBlackboard()
    {
        if (m_Agent == null || m_EnemyUnit == null) return; // Déjà vérifié dans Awake

        if (m_Agent.BlackboardReference == null)
        {
            // Cela peut arriver si le Blackboard est assigné tardivement à l'agent.
            // Le Start() du BehaviorGraphAgent pourrait ne pas encore s'être exécuté.
            // Dans ce cas, le Behavior Graph lui-même ne devrait pas démarrer avant que BlackboardReference ne soit prêt.
            Debug.LogWarning($"[{gameObject.name}] EnemyUnitBlackboardInitializer: BlackboardReference is null on BehaviorGraphAgent during InitializeBlackboard. Will retry or fail if graph starts.", gameObject);
            return;
        }

        var blackboardRef = m_Agent.BlackboardReference;

        BlackboardVariable<Unit> bbSelfUnitForGraph;

        if (blackboardRef.GetVariable(EnemyUnit.BB_SELF_UNIT, out bbSelfUnitForGraph))
        {
            if (bbSelfUnitForGraph.Value == null) // N'écraser que si c'est null
                bbSelfUnitForGraph.Value = m_EnemyUnit;
            else if (bbSelfUnitForGraph.Value != m_EnemyUnit)
                 bbSelfUnitForGraph.Value = m_EnemyUnit;
        }
    }
}