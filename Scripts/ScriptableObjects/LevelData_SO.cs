using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic; // Ajouté pour les listes potentielles


public enum LevelType
{
    SystemScene,    // Pour MainMenu, Hub, Core, etc.
    GameplayLevel,  // Pour les niveaux jouables classiques
    Cinematic,      // Pour les scènes cinématiques
}

[CreateAssetMenu(fileName = "LevelData_New", menuName = "GameData/Level Data")]
public class LevelData_SO : ScriptableObject
{
    [BoxGroup("Identification")]
    [InfoBox("ID Unique utilisé pour la sauvegarde et les références internes.")]
    public string LevelID = "Level_Default";

    [BoxGroup("Identification")]
    public string DisplayName = "Niveau par défaut";

    [BoxGroup("Scene")]
    [Required("Le nom de la scène à charger est requis.")]
    [SceneObjectsOnly] // Ou garde string si tu préfères taper le nom
    public string SceneName = "Level_Gameplay_Template"; // Mets un nom de scène template

    [TextArea(3, 5)]
    [BoxGroup("Description")]
    public string Description;

    [Title("Conditions de Déblocage")]
    [InfoBox("Laissez vide si le niveau est débloqué par défaut ou via une autre logique.")]
    [AssetsOnly] // Si tu débloques via l'ID d'un autre LevelData_SO
    public LevelData_SO RequiredPreviousLevel; // Remplacé string par référence directe

    public int RequiredPlayerLevel; // 0 si pas requis
    [Title("Affichage & Accès")]
    public LevelType TypeOfLevel = LevelType.GameplayLevel; // Par défaut

    [Title("Récompenses")]
    public int ExperienceReward;
    public int CurrencyReward;
    [AssetsOnly] // Pour lier directement au SO du personnage à débloquer
    public CharacterData_SO CharacterUnlockReward; // Remplacé string par référence directe

    [Title("Gameplay Settings")]
    [Range(1, 5)] public int Difficulty = 1; // Exemple d'échelle
    public float RhythmBPM = 120f; // Remplacé speed par BPM pour être clair
    // public List<EnemySpawnData> EnemyWaves; // Tu auras peut-être besoin d'un autre SO pour ça

    [Title("Audio (Wwise)")]
    public AK.Wwise.Event BackgroundMusic; // Décommenté
    public AK.Wwise.Switch MusicStateSwitch; // Potentiel Switch Wwise pour ce niveau
    // public AK.Wwise.Event VictoryMusic;
    // public AK.Wwise.Event DefeatMusic;
}