namespace Gameplay
{
    using UnityEngine;
    using System.Collections.Generic;
    using System.Linq;


    public class AllyUnitRegistry : MonoBehaviour
    {
        public static AllyUnitRegistry Instance { get; private set; }

        private List<AllyUnit> activeAllyUnits = new List<AllyUnit>();
        public IReadOnlyList<AllyUnit> ActiveAllyUnits => activeAllyUnits.AsReadOnly(); // Exposition en lecture seule

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
    }
}