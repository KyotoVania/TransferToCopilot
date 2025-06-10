namespace ScriptableObjects
{
    using UnityEngine;
    using System.Collections.Generic;

// Enum to define which character stat a modifier affects.
    public enum StatType
    {
        Health,
        Attack,
        Defense,
        // Add other stats as needed, e.g., Speed, Magic, CriticalChance
    }

// Enum for equipment slots to ensure items can only be equipped in the correct place.
    public enum EquipmentSlotType
    {
        Weapon,
        Helmet,
        Armor,
        Accessory
    }

// A simple struct to define a stat modification.
    [System.Serializable]
    public struct StatModifier
    {
        public StatType StatToModify;
        public int Value;
    }

    [CreateAssetMenu(fileName = "EQP_NewEquipment", menuName = "GameData/Equipment Data")]
    public class EquipmentData_SO : ScriptableObject
    {
        [Header("Identification")]
        public string EquipmentID; // e.g., "WEP_IRON_SWORD"
        public string DisplayName; // e.g., "Iron Sword"
        public Sprite Icon;
    
        [Header("Categorization")]
        public EquipmentSlotType SlotType;

        [TextArea]
        public string Description;

        [Header("Stat Modifiers")]
        public List<StatModifier> Modifiers;
    }
}
