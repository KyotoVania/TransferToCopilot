using UnityEngine;

public abstract class BaseSpellEffect_SO : ScriptableObject
{
    // GameObject caster: l'entité qui lance le sort (pourrait être null pour des sorts "globaux")
    private int perfectCount;
    public abstract void ExecuteEffect(GameObject caster, int perfectCount);
    public virtual string GetEffectDescription() { return "Effet de sort générique."; } // Pour l'UI
}