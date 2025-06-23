namespace ScriptableObjects
{
	using UnityEngine;
	using System.Collections.Generic;
	using Sirenix.OdinInspector; // Assuming ValidateInput attribute is from Odin Inspector
	using AK.Wwise;

	[CreateAssetMenu(fileName = "NewGlobalSpellData", menuName = "ScriptableObjects/GlobalSpellData", order = 1)]
	public class GlobalSpellData_SO : ScriptableObject
	{
    public string SpellID; // Identifiant unique, ex: "SPELL_GOLD_BOOST"
    public string DisplayName; // Nom du sort pour l'UI, ex: "Pluie d'Or"

    [TextArea]
    public string Description;

    public Sprite Icon; // Optionnel, pour affichage UI

    [ValidateInput("ValidateSpellSequence", "La séquence doit contenir exactement 4 éléments.")]
    public List<InputType> SpellSequence = new List<InputType>(4);

    public int GoldCost = 0; // Optionnel, si les sorts ont aussi un coût

    public AK.Wwise.Event ActivationSound; // Optionnel, pour un son spécifique au lancement du sort

    public BaseSpellEffect_SO SpellEffect; // Partie clé pour la flexibilité des effets

    [Header("Cooldown")]
    [Tooltip("Temps de rechargement en secondes avant de pouvoir relancer ce sort.")]
    [MinValue(0)]
    public float BeatCooldown = 15f; 
    
    [Tooltip("Cost in momentum charges to cast this spell.")]
    public int MomentumCost = 1;
    
    private bool ValidateSpellSequence(List<InputType> sequence)
    {
        return sequence != null && sequence.Count == 4;
    }
	}

}