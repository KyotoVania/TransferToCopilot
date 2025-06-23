using System;

namespace Gameplay
{
    using UnityEngine;
    using System.Collections.Generic;
    using System.Linq;
    using Unity.Behavior;


    public class AllyUnitRegistry : MonoBehaviour
    {
        public static AllyUnitRegistry Instance { get; private set; }

        private List<AllyUnit> activeAllyUnits = new List<AllyUnit>();
        public IReadOnlyList<AllyUnit> ActiveAllyUnits => activeAllyUnits.AsReadOnly(); // Exposition en lecture seule
        public event Action<AllyUnit> OnDefensiveKillConfirmed;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("[AllyUnitRegistry] Multiple instances detected. Destroying duplicate.", gameObject);
                Destroy(gameObject);
            }
        }

        public void RegisterUnit(AllyUnit unit)
        {
            if (unit != null && !activeAllyUnits.Contains(unit))
            {
                activeAllyUnits.Add(unit);
            }
        }

        public void UnregisterUnit(AllyUnit unit)
        {
            if (unit != null)
            {
                activeAllyUnits.Remove(unit);
            }
        }
        
        private void OnEnable()
        {
            Unit.OnUnitKilled += HandleUnitKilled;
        }

        private void OnDisable()
        {
            Unit.OnUnitKilled -= HandleUnitKilled;
        }
        private void HandleUnitKilled(Unit attacker, Unit victim)
        {
            // On s'intéresse uniquement aux cas où l'attaquant est une unité alliée.
            if (attacker is AllyUnit attackingAlly)
            {
                var blackboard = attackingAlly.Blackboard; // Ceci est bien une BlackboardReference

                if (blackboard != null)
                {
                    // 1. Déclarer une variable pour recevoir la "BlackboardVariable" elle-même.
                    BlackboardVariable<bool> isDefendingVar;

                    // 2. Essayer de récupérer la variable depuis le blackboard.
                    // La méthode retourne 'true' si la variable "IsDefending" existe.
                    if (blackboard.GetVariable("IsDefending", out isDefendingVar))
                    {
                        // 3. Si elle existe, on vérifie sa propriété ".Value".
                        if (isDefendingVar.Value)
                        {
                            // L'unité était bien en mode défensif, on déclenche l'événement.
                            OnDefensiveKillConfirmed?.Invoke(attackingAlly);
                        }
                    }
                }
            }
        }
    }
}