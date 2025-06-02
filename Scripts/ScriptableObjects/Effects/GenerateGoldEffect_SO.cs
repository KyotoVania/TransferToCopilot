using UnityEngine;

[CreateAssetMenu(fileName = "GenerateGoldEffect_New", menuName = "GameData/Effects/Generate Gold Effect")]
public class GenerateGoldEffect_SO : BaseSpellEffect_SO
{
    public int BaseGoldAmount = 50;
    public int BonusPerPerfectInput = 10;

    public override void ExecuteEffect(GameObject caster, int perfectCount)
    {
        int totalGold = BaseGoldAmount + (BonusPerPerfectInput * perfectCount);
        if (GoldController.Instance != null)
        {
            GoldController.Instance.AddGold(totalGold);
            Debug.Log($"[GenerateGoldEffect] Added {totalGold} gold (Base: {BaseGoldAmount}, Bonus: {BonusPerPerfectInput} x {perfectCount})");
        }
        else
        {
            Debug.LogWarning("[GenerateGoldEffect] GoldController instance not found.");
        }
    }

    public override string GetEffectDescription()
    {
        return $"Génère {BaseGoldAmount} or (+{BonusPerPerfectInput} par input parfait)";
    }
}
