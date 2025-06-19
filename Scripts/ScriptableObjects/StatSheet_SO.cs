using UnityEngine;
using Sirenix.OdinInspector;

namespace ScriptableObjects
{
    public enum UnitType
    {
        Regular,
        Elite,
        Boss,
        Null
    }
    [CreateAssetMenu(fileName = "StatSheet_New", menuName = "GameData/Stat Sheet")]
    public class StatSheet_SO : ScriptableObject
    {
        [Header("Base Stats (Level 1)")]
        [Title("Unit Combat Stats")] [MinValue(1)] // Assure que la vie est au moins 1
        public int BaseHealth = 100;

        [MinValue(0)] // La défense peut être 0
        public int BaseDefense = 10;

        [MinValue(0)] // L'attaque peut être 0
        public int BaseAttack = 15;

        [MinValue(1)] // Portée d'attaque minimale de 1 (ou 0 si tu permets des attaques sans portée?)
        public int AttackRange = 1; // En nombre de tuiles

        [MinValue(1)] // Délai minimum d'une pulsation/beat
        public int AttackDelay = 1; // En nombre de beats

        [Title("Unit Movement & Detection")] [MinValue(1)] // Délai minimum d'une pulsation/beat
        public int MovementDelay = 1; // En nombre de beats avant de bouger

        [MinValue(0)] // La détection peut être 0 (ne voit rien)
        public int DetectionRange = 3; // En nombre de tuiles

        [Title("Unit Type")] [EnumToggleButtons] // Permet de choisir le type de manière plus visuelle
        public UnitType Type = UnitType.Regular; // Type de l'unité, peut être Regular, Elite ou Boss

        
        
        [Header("Courbe d'Expérience")]
        [Tooltip("Définit le montant total d'XP requis pour atteindre un niveau. X=Niveau, Y=XP Requis.")]
        public AnimationCurve XpRequirementCurve;
        [Header("Progression Curves (Bonus per level)")]
        [Tooltip("Définit la statistique de base (SANS équipement) à un niveau donné. X=Niveau, Y=Valeur de la Stat.")]
        public AnimationCurve HealthCurve;
        public AnimationCurve AttackCurve;
        public AnimationCurve DefenseCurve;
        
        /// <summary>
        /// Retourne le montant total d'XP requis pour atteindre un niveau donné.
        /// </summary>
        /// <param name="level">Le niveau pour lequel on veut connaître le requis en XP.</param>
        /// <returns>Le montant d'XP requis. Retourne int.MaxValue si la courbe n'est pas définie.</returns>
        public int GetXPRequiredForLevel(int level)
        {
            if (XpRequirementCurve == null || XpRequirementCurve.keys.Length == 0)
            {
                Debug.LogError("XpRequirementCurve n'est pas configurée pour le StatSheet: " + this.name);
                // On retourne une valeur très élevée pour éviter des level-ups accidentels.
                return int.MaxValue;
            }

            // On évalue la courbe au niveau demandé.
            return Mathf.RoundToInt(XpRequirementCurve.Evaluate(level));
        }
    }
}