namespace ScriptableObjects
{
	using UnityEngine;
	using System.Collections.Generic;
	using Sirenix.OdinInspector;
	using AK.Wwise;

	/// <summary>
	/// ScriptableObject defining global spell data for the game's magic system.
	/// Contains spell configuration including input sequences, costs, effects, and audio.
	/// Used by the GlobalSpellsUIController to manage spell casting and cooldowns.
	/// </summary>
	[CreateAssetMenu(fileName = "NewGlobalSpellData", menuName = "ScriptableObjects/GlobalSpellData", order = 1)]
	public class GlobalSpellData_SO : ScriptableObject
	{
		/// <summary>
		/// Unique identifier for this spell (e.g., "SPELL_GOLD_BOOST").
		/// Used for save data serialization and spell reference throughout the system.
		/// </summary>
		public string SpellID; 
		
		/// <summary>
		/// Display name shown to players in the UI (e.g., "Pluie d'Or").
		/// Supports localization through the game's text system.
		/// </summary>
		public string DisplayName;

		/// <summary>
		/// Detailed description of the spell's effects and usage.
		/// Displayed in tooltips and spell selection interfaces.
		/// </summary>
		[TextArea]
		public string Description;

		/// <summary>
		/// Icon sprite displayed in UI elements for this spell.
		/// Optional field for spell visualization in the interface.
		/// </summary>
		public Sprite Icon; 

		/// <summary>
		/// Sequence of 4 input types required to cast this spell.
		/// Players must input this exact sequence within the time limit to activate the spell.
		/// Validated to ensure exactly 4 elements are present.
		/// </summary>
		[ValidateInput("ValidateSpellSequence", "La séquence doit contenir exactement 4 éléments.")]
		public List<InputType> SpellSequence = new List<InputType>(4);

		/// <summary>
		/// Gold cost required to cast this spell.
		/// Optional field - set to 0 for spells with no gold cost.
		/// </summary>
		public int GoldCost = 0; // Optionnel, si les sorts ont aussi un coût

		/// <summary>
		/// Wwise audio event triggered when the spell is successfully activated.
		/// Optional field for providing audio feedback during spell casting.
		/// </summary>
		public AK.Wwise.Event ActivationSound; // Optionnel, pour un son spécifique au lancement du sort

		/// <summary>
		/// The core effect implementation for this spell.
		/// Key component for spell effect flexibility and polymorphism.
		/// </summary>
		public BaseSpellEffect_SO SpellEffect; // Partie clé pour la flexibilité des effets

		[Header("Cooldown")]
		/// <summary>
		/// Cooldown duration in beats before this spell can be cast again.
		/// Prevents spell spamming and adds strategic timing to spell usage.
		/// </summary>
		[Tooltip("Temps de rechargement en secondes avant de pouvoir relancer ce sort.")]
		[MinValue(0)]
		public float BeatCooldown = 15f;

		/// <summary>
		/// Cost in momentum charges required to cast this spell.
		/// Players must accumulate sufficient momentum through gameplay actions.
		/// </summary>
		[Tooltip("Cost in momentum charges to cast this spell.")]
		public int MomentumCost = 1;

		/// <summary>
		/// Validates that the spell sequence contains exactly 4 input elements.
		/// Used by Odin Inspector for editor-time validation feedback.
		/// </summary>
		/// <param name="sequence">The spell sequence to validate.</param>
		/// <returns>True if the sequence is valid, false otherwise.</returns>
		private bool ValidateSpellSequence(List<InputType> sequence)
		{
			return sequence != null && sequence.Count == 4;
		}
	}

}