using UnityEngine;
using System.Collections.Generic;
using ScriptableObjects;
using Gameplay;

public enum StatToBuff
{
    Attack,
    Defense,
    Speed
}

[CreateAssetMenu(fileName = "GlobalUnitBuffEffect_New", menuName = "GameData/Effects/Global Unit Buff Effect")]
public class GlobalUnitBuffEffect_SO : BaseSpellEffect_SO
{
    public StatToBuff Stat; 
    public float BuffMultiplier = 1.2f; 
    public float BuffDuration = 10f; 
    public int BonusDurationPerPerfectInput = 2; //

    public override void ExecuteEffect(GameObject caster, int perfectCount) //
    {
        float totalDuration = BuffDuration + (BonusDurationPerPerfectInput * perfectCount); //

        // Utiliser AllyUnitRegistry pour obtenir les unités alliées actives
        if (AllyUnitRegistry.Instance == null)
        {
            Debug.LogWarning("[GlobalUnitBuffEffect] AllyUnitRegistry.Instance non trouvé. Impossible d'appliquer le buff.");
            return;
        }

        IReadOnlyList<AllyUnit> activeAllyUnits = AllyUnitRegistry.Instance.ActiveAllyUnits; //
        if (activeAllyUnits.Count == 0)
        {
            Debug.LogWarning("[GlobalUnitBuffEffect] Aucune unité alliée active trouvée dans AllyUnitRegistry.");
            return;
        }

        Debug.Log($"[GlobalUnitBuffEffect] Application du buff '{Stat}' (x{BuffMultiplier}) pour {totalDuration}s à {activeAllyUnits.Count} unités alliées.");

        foreach (AllyUnit allyUnit in activeAllyUnits)
        {
            if (allyUnit != null)
            {
                allyUnit.ApplyBuff(Stat, BuffMultiplier, totalDuration);
            }
        }
    }

    public override string GetEffectDescription() //
    {
        // Calcule la durée totale avec un exemple d'un input parfait pour la description.
        float exampleTotalDuration = BuffDuration + (BonusDurationPerPerfectInput * 1); //
        // Calcule le pourcentage d'augmentation pour l'affichage.
        int percentBuff = Mathf.RoundToInt((BuffMultiplier - 1f) * 100); //
        // Retourne la description de l'effet.
        return $"Augmente {Stat} de {percentBuff}% pour toutes les unités alliées pendant {exampleTotalDuration}s.";
    }
    
}