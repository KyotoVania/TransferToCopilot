using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "NewTileReactionProfile", menuName = "GameData/Tile Reaction Profile")]
public class TileReactionProfile_SO : ScriptableObject
{
    [Title("Common Settings")]
    [Range(0f, 1f)]
    public float reactionProbability = 0.65f;

    [Tooltip("For debugging or specific effects, always react regardless of probability.")]
    public bool alwaysReact = false;

    // --- Ground Tile Specific Parameters ---
    [BoxGroup("Ground Tile Settings")]
    [ShowIf("IsProfileForGround")]
    [Range(0f, 1f)] public float upMin = 0.05f;

    [BoxGroup("Ground Tile Settings")]
    [ShowIf("IsProfileForGround")]
    [Range(0f, 1f)] public float upMax = 0.2f;

    [BoxGroup("Ground Tile Settings")]
    [ShowIf("IsProfileForGround")]
    [Range(-1f, 0f)] public float downMin = -0.2f;

    [BoxGroup("Ground Tile Settings")]
    [ShowIf("IsProfileForGround")]
    [Range(-1f, 0f)] public float downMax = -0.05f;

    [BoxGroup("Ground Tile Settings")]
    [ShowIf("IsProfileForGround")]
    [Tooltip("Duration of ground movement as a multiplier of the beat duration.")]
    public float groundAnimBeatMultiplier = 0.4f;

    [BoxGroup("Ground Tile Settings")]
    [ShowIf("IsProfileForGround")]
    [Tooltip("Absolute variation in movement duration (seconds). Can be positive or negative range.")]
    public float durationVariation = 0.05f;

    [BoxGroup("Ground Tile Settings")]
    [ShowIf("IsProfileForGround")]
    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [BoxGroup("Ground Tile Settings")]
    [ShowIf("IsProfileForGround")]
    [Range(0f, 1f)] public float bouncePercentage = 0.4f;

    [BoxGroup("Ground Tile Settings")]
    [ShowIf("IsProfileForGround")]
    public float bounceDuration = 0.2f;

    // --- Combo Reaction Settings ---
    [Title("Combo Reaction Settings")]
    [Tooltip("Should this profile's reactivity change with combo?")]
    public bool reactToCombo = true;

    [ShowIf("reactToCombo")]
    [Tooltip("Increase reaction probability by this percentage for each combo threshold reached")]
    public float comboReactionBoostPercentage = 20f;

    [ShowIf("reactToCombo")]
    [Tooltip("Combo count required to increase the reaction probability")]
    public int comboThreshold = 5;

    [ShowIf("reactToCombo")]
    [Tooltip("Maximum increase to reaction probability (percentage)")]
    public float maxReactionBoostPercentage = 100f;


    // --- Water Tile Specific Parameters ---
    [BoxGroup("Water Tile Settings")]
    [ShowIf("IsProfileForWater")]
    public float waterWaveAmplitude = 0.1f;

    [BoxGroup("Water Tile Settings")]
    [ShowIf("IsProfileForWater")]
    public float waterWaveFrequency = 2.0f;

    // RETIRÉ: waterFixedSequenceNumber
    // RETIRÉ: waterFixedSequenceTotal

    [BoxGroup("Water Tile Settings")]
    [ShowIf("IsProfileForWater")]
    [Tooltip("Number of beats to wait between wave cycles for this profile.")]
    [Range(0, 10)] public int waterBeatsBetweenWaves = 0; // Conservé dans le profil

    [BoxGroup("Water Tile Settings")]
    [ShowIf("IsProfileForWater")]
    public float waterScaleFactor = 1.2f;

    [BoxGroup("Water Tile Settings")]
    [ShowIf("IsProfileForWater")]
    public float waterMoveHeight = 0.15f;

    [BoxGroup("Water Tile Settings")]
    [ShowIf("IsProfileForWater")]
    [Range(0.1f, 0.9f)] public float preBeatFraction = 0.5f;

    [BoxGroup("Water Tile Settings")] 
    [ShowIf("IsProfileForWater")]
    [Range(0.1f, 2f)] public float waterAnimationDurationMultiplier = 0.8f;

    // --- Mountain Tile Specific Parameters ---
    [BoxGroup("Mountain Tile Settings")]
    [ShowIf("IsProfileForMountain")]
    [Range(0f, 1f)] public float mountainReactionStrength = 0.5f;
    // Ajoutez ici une variable pour la durée de la secousse des montagnes si nécessaire,
    // ex: public float mountainShakeDurationMultiplier = 0.3f;


    [Title("Profile Type Hint")] // Renommé pour plus de clarté
    public enum ProfileApplicability
    {
        Generic,
        Ground,
        Water,
        Mountain
    }
    [EnumToggleButtons]
    [Tooltip("Hint for the primary intended use of this profile. Does not restrict assignment but helps with ShowIf in inspector.")]
    public ProfileApplicability applicableTileType = ProfileApplicability.Generic;

    private bool IsProfileForGround() => applicableTileType == ProfileApplicability.Ground || applicableTileType == ProfileApplicability.Generic;
    private bool IsProfileForWater() => applicableTileType == ProfileApplicability.Water || applicableTileType == ProfileApplicability.Generic;
    private bool IsProfileForMountain() => applicableTileType == ProfileApplicability.Mountain || applicableTileType == ProfileApplicability.Generic;
}