namespace ScriptableObjects
{
    using UnityEngine;
    using Sirenix.OdinInspector;

    [CreateAssetMenu(fileName = "NewEnvironmentStats", menuName = "Game/Environment Stats")]
    public class EnvironmentStats : ScriptableObject
    {
        [Header("Basic Settings")] [Tooltip("The name of this environment type")]
        public string environmentTypeName;

        [Tooltip("Descriptive text for this environment")] [TextArea(3, 5)]
        public string description;

        [Header("Gameplay Effects")] [Tooltip("Does this environment provide cover?")]
        public bool providesCover;

        [ShowIf("providesCover")] [Range(0, 50)] [Tooltip("Percentage of damage reduction when unit is in cover")]
        public int coverDamageReduction = 20;
    }
}