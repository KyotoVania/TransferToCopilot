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
                var blackboard = attackingAlly.Blackboard;

                if (blackboard != null)
                {
                    BlackboardVariable<bool> isDefendingVar;
                    if (blackboard.GetVariable("IsDefending", out isDefendingVar))
                    {
                        if (isDefendingVar.Value)
                        {
                            if (MomentumManager.Instance != null && attackingAlly.MomentumGainOnObjectiveComplete > 0)
                            {
                                MomentumManager.Instance.AddMomentum(attackingAlly.MomentumGainOnObjectiveComplete);
                                Debug.Log($"[AllyUnitRegistry] L'unité défensive {attackingAlly.name} a tué une unité et a rapporté {attackingAlly.MomentumGainOnObjectiveComplete} de momentum.");
                            }
                            
                            OnDefensiveKillConfirmed?.Invoke(attackingAlly);
                        }
                    }
                }
            }
        }
    }
}