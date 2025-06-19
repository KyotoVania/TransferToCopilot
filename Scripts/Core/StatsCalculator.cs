using System.Collections.Generic;
using ScriptableObjects; 
using UnityEngine; 

/// <summary>
/// Classe statique utilitaire responsable de tous les calculs de statistiques des unités.
/// C'est le point d'entrée unique pour déterminer les stats finales d'une unité.
/// </summary>
public static class StatsCalculator
{
    /// <summary>
    /// Calcule les statistiques finales d'une unité en combinant ses stats de base,
    /// sa progression de niveau et les modificateurs de ses équipements.
    /// </summary>
    /// <param name="character">Les données du personnage (contenant le StatSheet).</param>
    /// <param name="level">Le niveau actuel du personnage.</param>
    /// <param name="equippedItems">La liste des équipements portés par le personnage.</param>
    /// <returns>Un objet RuntimeStats contenant les statistiques finales calculées.</returns>
    public static RuntimeStats GetFinalStats(CharacterData_SO character, int level, List<EquipmentData_SO> equippedItems)
    {
        // --- 1. Validation des entrées ---
        if (character == null || character.Stats == null)
        {
            Debug.LogError("StatsCalculator: CharacterData_SO ou son StatSheet est null. Retour de stats par défaut.");
            return new RuntimeStats(); // Retourne des stats vides pour éviter les erreurs
        }

        var statSheet = character.Stats;
        var finalStats = new RuntimeStats();

        // --- 2. Calcul à partir du StatSheet (Base + Niveau) ---
        // Les stats qui évoluent avec le niveau sont calculées via les AnimationCurves.
        finalStats.MaxHealth = statSheet.BaseHealth + Mathf.RoundToInt(statSheet.HealthCurve.Evaluate(level));
        finalStats.Attack = statSheet.BaseAttack + Mathf.RoundToInt(statSheet.AttackCurve.Evaluate(level));
        finalStats.Defense = statSheet.BaseDefense + Mathf.RoundToInt(statSheet.DefenseCurve.Evaluate(level));

        // Les stats qui sont fixes sont directement copiées depuis le StatSheet.
        finalStats.AttackRange = statSheet.AttackRange;
        finalStats.AttackDelay = statSheet.AttackDelay;
        finalStats.MovementDelay = statSheet.MovementDelay;
        finalStats.DetectionRange = statSheet.DetectionRange;

        // --- 3. Application des modificateurs d'équipement ---
        if (equippedItems != null)
        {
            foreach (var item in equippedItems)
            {
                if (item == null || item.Modifiers == null) continue;

                foreach (var modifier in item.Modifiers)
                {
                    // On utilise un switch pour appliquer le modificateur à la bonne stat.
                    // C'est propre et facile à étendre si vous ajoutez de nouvelles stats.
                    switch (modifier.StatToModify)
                    {
                        case StatType.Health:
                            finalStats.MaxHealth += modifier.Value;
                            break;
                        case StatType.Attack:
                            finalStats.Attack += modifier.Value;
                            break;
                        case StatType.Defense:
                            finalStats.Defense += modifier.Value;
                            break;
                        // Ajoutez d'autres cas ici si vos équipements peuvent modifier d'autres stats.
                    }
                }
            }
        }

        // --- 4. Retourner le résultat ---
        return finalStats;
    }
}