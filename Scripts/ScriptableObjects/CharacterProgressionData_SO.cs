namespace ScriptableObjects
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "ProgressionData_New", menuName = "GameData/Character Progression Data")]
    public class CharacterProgressionData_SO : ScriptableObject
    {
        [Header("Courbe d'Expérience")]
        [Tooltip("Définit le montant total d'XP requis pour atteindre un niveau. X=Niveau, Y=XP Requis.")]
        public AnimationCurve XpRequirementCurve;

        [Header("Courbes de Statistiques")]
        [Tooltip("Définit la statistique de base (SANS équipement) à un niveau donné. X=Niveau, Y=Valeur de la Stat.")]
        public AnimationCurve HealthCurve;
        public AnimationCurve AttackCurve;
        public AnimationCurve DefenseCurve;

        /// <summary>
        /// Retourne le total d'XP requis pour atteindre un niveau spécifique.
        /// </summary>
        public int GetXPRequiredForLevel(int level)
        {
            return Mathf.RoundToInt(XpRequirementCurve.Evaluate(level));
        }

        /// <summary>
        /// Calcule les statistiques d'un personnage pour un niveau donné.
        /// </summary>
        public UnitStats_SO GetStatsForLevel(UnitStats_SO baseStats, int level)
        {
            // On crée une copie pour ne pas modifier l'asset de base.
            UnitStats_SO calculatedStats = Instantiate(baseStats);

            calculatedStats.Health += Mathf.RoundToInt(HealthCurve.Evaluate(level));
            calculatedStats.Attack += Mathf.RoundToInt(AttackCurve.Evaluate(level));
            calculatedStats.Defense += Mathf.RoundToInt(DefenseCurve.Evaluate(level));

            return calculatedStats;
        }
    }
}