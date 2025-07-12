namespace ScriptableObjects
{
	using UnityEngine;
	using System.Collections.Generic;
	using Sirenix.OdinInspector;
	#if UNITY_EDITOR
	using UnityEditor;
	#endif



	public enum InputType { X, C, V }

	[CreateAssetMenu(fileName = "CharacterData_New", menuName = "GameData/Character Data")]
	public class CharacterData_SO : ScriptableObject
	{
    [BoxGroup("Identification", ShowLabel = false)]
    [HorizontalGroup("Identification/Split", Width = 100)]
    [PreviewField(100, ObjectFieldAlignment.Left), HideLabel]
    public Sprite Icon;

    [VerticalGroup("Identification/Split/Info")]
    [InfoBox("ID Unique utilisé pour la sauvegarde et les références internes.")]
    public string CharacterID = "Char_Default";

    [VerticalGroup("Identification/Split/Info")]
    public string DisplayName = "Default Character";

    [TextArea(3, 5)]
    [BoxGroup("Description")]
    public string Description;

    [Title("Gameplay")]

    [BoxGroup("Gameplay")]
    [BoxGroup("Gameplay/Stats", ShowLabel = false)]
    [InfoBox("ScriptableObject contenant les statistiques de ce personnage.")]
    [InlineEditor(InlineEditorModes.FullEditor)]
    public StatSheet_SO Stats; 
    
    public GameObject GameplayUnitPrefab;

    [BoxGroup("Gameplay/Invocation")]
    [ListDrawerSettings(NumberOfItemsPerPage = 4)] // Affichage compact
    [InfoBox("Séquence de 4 inputs (X, C, ou V) pour invoquer ce personnage.")]
    [ValidateInput("ValidateSequenceLength", "La séquence d'invocation doit comporter exactement 4 inputs.")]
    public List<InputType> InvocationSequence = new List<InputType>(4); // Initialise avec 4 éléments par défaut si besoin

    [BoxGroup("Gameplay/Invocation")] // Placé dans le même groupe que la séquence
    [MinValue(0)]
    [SuffixLabel("or")]
    [GUIColor(0.9f, 0.9f, 0.2f)] // Pour le rendre plus visible
    public int GoldCost = 0;
    // --- Validation pour Odin Inspector ---
    #if UNITY_EDITOR
    private bool ValidateSequenceLength(List<InputType> sequence)
    {
        return sequence != null && sequence.Count == 4;
    }
    #endif
    // ------------------------------------
	[Header("Momentum System")]
    [Tooltip("Cost in momentum charges to invoke this unit. 0 for basic units.")]
    public int MomentumCost = 0;

    [Tooltip("Momentum gained (as a fraction of a charge) when this unit is successfully invoked.")]
    [Range(0f, 1f)]
    public float MomentumGainOnInvoke = 0.2f;

    [Tooltip("Momentum gained (as a fraction of a charge) when this unit completes its primary objective (capture or defensive kill).")]
    [Range(0f, 1f)]
    public float MomentumGainOnObjectiveComplete = 0.5f;
	
    [Title("Fever Mode Buffs")]
    [InfoBox("Modificateurs appliqués lorsque le Mode Fever est actif.")]
    public FeverBuffs feverBuffs;

    [Title("Hub & UI")]
    [AssetsOnly]
    public GameObject HubVisualPrefab;
    [AssetsOnly]
    public GameObject MenuAnimationPrefab;
    [Title("Audio (Wwise)")]
    [InfoBox("Assigner les Events Wwise spécifiques à ce personnage.")]
    public AK.Wwise.Event SelectionSound;
    public AK.Wwise.Switch CharacterSwitch; // Switch pour les personnages, utile pour les sons de sélection
    // Ajouter d'autres sons si nécessaire (attaque, capacité spéciale, etc.)

    [Title("Statut Initial")]
    [InfoBox("Cocher si ce personnage est débloqué dès le début du jeu.")]
    public bool UnlockedByDefault = false;

    [Title("Cooldown d'Invocation")]
    [BoxGroup("Cooldown")]
    [MinValue(0)]
    [Tooltip("Temps en beats avant de pouvoir invoquer à nouveau ce personnage.")]
    public float InvocationCooldown = 5;
	}
	
	[System.Serializable]
	public struct FeverBuffs
	{
		[Tooltip("Multiplicateur de la vitesse d'attaque. Ex: 1.3 pour +30%. Mettre à 1 pour aucun changement.")]
		[Range(1f, 3f)]
		public float AttackSpeedMultiplier;

		[Tooltip("Nombre de projectiles supplémentaires tirés par les unités à distance.")]
		[Range(0, 5)]
		public int ExtraProjectiles;

		[Tooltip("Multiplicateur de défense. Ex: 2.0 pour doubler la défense.")]
		[Range(1f, 5f)]
		public float DefenseMultiplier;
	}
}

