using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; 
using ScriptableObjects;
using Newtonsoft.Json;

/// <summary>
/// Represents the progression data for a character including level and experience.
/// </summary>
[System.Serializable]
public class CharacterProgress
{
    /// <summary>
    /// The current level of the character.
    /// </summary>
    public int CurrentLevel = 1;
    
    /// <summary>
    /// The current experience points of the character.
    /// </summary>
    public int CurrentXP = 0;
}

/// <summary>
/// Data structure for storing player save data including progress, settings, and inventory.
/// </summary>
[System.Serializable]
public class PlayerSaveData
{
    /// <summary>
    /// The player's current currency amount.
    /// </summary>
    public int Currency = 0;
    
    /// <summary>
    /// The player's current experience points.
    /// </summary>
    public int Experience = 0;
    
    /// <summary>
    /// List of character IDs that have been unlocked by the player.
    /// </summary>
    public List<string> UnlockedCharacterIDs = new List<string>();
    
    /// <summary>
    /// Dictionary mapping level IDs to their completion status (star rating).
    /// </summary>
    public Dictionary<string, int> CompletedLevels = new Dictionary<string, int>();
    
    /// <summary>
    /// List of character IDs currently in the player's active team.
    /// </summary>
    public List<string> ActiveTeamCharacterIDs = new List<string>();
    
    /// <summary>
    /// List of equipment IDs that have been unlocked by the player.
    /// </summary>
    public List<string> UnlockedEquipmentIDs = new List<string>();

    /// <summary>
    /// Dictionary mapping character IDs to their equipped items.
    /// </summary>
    public Dictionary<string, List<string>> EquippedItems = new Dictionary<string, List<string>>();
    
    /// <summary>
    /// Dictionary mapping character IDs to their progression data.
    /// </summary>
    public Dictionary<string, CharacterProgress> CharacterProgressData = new Dictionary<string, CharacterProgress>();

    // Game options
    /// <summary>
    /// Music volume setting (0.0 to 1.0).
    /// </summary>
    public float MusicVolume = 0.7f;
    
    /// <summary>
    /// Sound effects volume setting (0.0 to 1.0).
    /// </summary>
    public float SfxVolume = 0.75f;
    
    /// <summary>
    /// Whether haptic feedback/vibration is enabled.
    /// </summary>
    public bool VibrationEnabled = true;
    
    /// <summary>
    /// Whether the beat indicator is shown in gameplay.
    /// </summary>
    public bool ShowBeatIndicator = false;
}

/// <summary>
/// Persistent singleton manager for handling player data (save, load, access).
/// Manages player progression, currency, team composition, and game settings.
/// Triggers events when data is modified to notify other systems.
/// </summary>
public class PlayerDataManager : SingletonPersistent<PlayerDataManager>
{
    // --- Static Events ---
    /// <summary>
    /// Triggered when the player's currency amount changes.
    /// </summary>
    public static event Action<int> OnCurrencyChanged;
    
    /// <summary>
    /// Triggered when a new character is unlocked.
    /// </summary>
    public static event Action<string> OnCharacterUnlocked;
    
    /// <summary>
    /// Triggered when the player gains experience points.
    /// </summary>
    public static event Action<int> OnExperienceGained;
    
    /// <summary>
    /// Triggered after initial data loading or creation of new default data.
    /// Useful for other managers that depend on this data (e.g., TeamManager).
    /// </summary>
    public static event Action OnPlayerDataLoaded;

    // --- Properties ---
    /// <summary>
    /// Gets the player's save data.
    /// </summary>
    public PlayerSaveData Data { get; private set; }

    // --- Private Variables ---
    /// <summary>
    /// The file path where save data is stored.
    /// </summary>
    private string _saveFilePath;
    
    /// <summary>
    /// The name of the save file.
    /// </summary>
    private const string SAVE_FILE_NAME = "playerData.json";
    
    /// <summary>
    /// Flag to ensure OnPlayerDataLoaded is only called once.
    /// </summary>
    private bool _isDataLoaded = false;

    // --- Unity Methods ---

    /// <summary>
    /// Initializes the PlayerDataManager singleton and loads player data.
    /// </summary>
    protected override void Awake()
    {
        base.Awake(); // Handles Singleton pattern and DontDestroyOnLoad
        _saveFilePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        Debug.Log($"[PlayerDataManager] Chemin de sauvegarde : {_saveFilePath}");
        LoadData();
    }

    /// <summary>
    /// Triggers the OnPlayerDataLoaded event after all singletons have been initialized.
    /// </summary>
    private void Start()
    {
        // Trigger the OnPlayerDataLoaded event here, after Awake has been executed on all singletons
        // and data is guaranteed to be loaded or created.
        if (!_isDataLoaded)
        {
             Debug.Log("[PlayerDataManager] Déclenchement de OnPlayerDataLoaded.");
             OnPlayerDataLoaded?.Invoke();
            _isDataLoaded = true;
        }
    }

    // --- Save / Load ---

    /// <summary>
    /// Saves the current player data to the persistent data path.
    /// </summary>
    public void SaveData()
    {
        try
        {
            string json = JsonConvert.SerializeObject(Data, Formatting.Indented);
            File.WriteAllText(_saveFilePath, json);
            Debug.Log($"[PlayerDataManager] Données sauvegardées dans {_saveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerDataManager] Échec de la sauvegarde des données : {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// Loads player data from the save file, or creates default data if no save file exists.
    /// </summary>
    public void LoadData()
    {
        if (File.Exists(_saveFilePath))
        {
            try
            {
                string json = File.ReadAllText(_saveFilePath);
                Data = JsonConvert.DeserializeObject<PlayerSaveData>(json);

                // Safety: Ensure lists are not null after deserialization
                if (Data.UnlockedCharacterIDs == null) Data.UnlockedCharacterIDs = new List<string>();
                if (Data.CompletedLevels == null) Data.CompletedLevels = new Dictionary<string, int>();
                if (Data.ActiveTeamCharacterIDs == null) Data.ActiveTeamCharacterIDs = new List<string>();

                Debug.Log($"[PlayerDataManager] Données chargées depuis {_saveFilePath}. Monnaie: {Data.Currency}, XP: {Data.Experience}, Persos: {Data.UnlockedCharacterIDs.Count}, Équipe: {Data.ActiveTeamCharacterIDs.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerDataManager] Échec du chargement du fichier de sauvegarde : {e.Message}. Création de nouvelles données par défaut.");
                CreateDefaultData();
                SaveData();
            }
        }
        else
        {
            Debug.Log("[PlayerDataManager] Aucun fichier de sauvegarde trouvé. Création de nouvelles données par défaut.");
            CreateDefaultData();
            SaveData(); // Save the new default data
        }
         _isDataLoaded = false; // Reset flag so Start can trigger the event
    }

    /// <summary>
    /// Creates default player data when no save file exists.
    /// Automatically unlocks default characters and sets up a starting team.
    /// </summary>
    private void CreateDefaultData()
    {
        Data = new PlayerSaveData();
        Data.Currency = 50; // Initial currency
        Data.Experience = 0;
        Data.UnlockedCharacterIDs = new List<string>();
        Data.ActiveTeamCharacterIDs = new List<string>();
        Data.CompletedLevels = new Dictionary<string, int>();

        // Unlock default characters
        // Important: This requires CharacterData_SO assets to be accessible,
        // for example in a Resources folder or via a specific Asset Database.
        CharacterData_SO[] allCharacters = Resources.LoadAll<CharacterData_SO>("Data/Characters"); // Adapt path if necessary

        Debug.Log($"[PlayerDataManager] Recherche des personnages par défaut parmi {allCharacters.Length} assets CharacterData_SO trouvés dans Resources/Data/Characters.");

        List<string> defaultTeam = new List<string>();
        foreach (var charData in allCharacters)
        {
            if (charData.UnlockedByDefault)
            {
                if (!Data.UnlockedCharacterIDs.Contains(charData.CharacterID))
                {
                    Data.UnlockedCharacterIDs.Add(charData.CharacterID);
                    Debug.Log($"[PlayerDataManager] Personnage par défaut débloqué : {charData.CharacterID}");
                    // Give 50 initial XP for each character unlocked by default
                    AddXPToCharacter(charData.CharacterID, 50); 
                    // Add the first unlocked characters to the default team (up to 4)
                    if (defaultTeam.Count < 4)
                    {
                        defaultTeam.Add(charData.CharacterID);
                    }
                }
            }
        }

         // If no character is unlocked by default, add the first one found as initial character (safety)
         if (Data.UnlockedCharacterIDs.Count == 0 && allCharacters.Length > 0)
         {
             CharacterData_SO firstChar = allCharacters[0];
             Data.UnlockedCharacterIDs.Add(firstChar.CharacterID);
             Debug.LogWarning($"[PlayerDataManager] Aucun personnage marqué comme 'UnlockedByDefault'. Ajout du premier trouvé : {firstChar.CharacterID}");
              if (defaultTeam.Count < 4)
              {
                  defaultTeam.Add(firstChar.CharacterID);
              }
         }


        // Set the default active team
        Data.ActiveTeamCharacterIDs = defaultTeam;
        Debug.Log($"[PlayerDataManager] Équipe par défaut définie : {string.Join(", ", defaultTeam)}");

        Debug.Log("[PlayerDataManager] Nouvelles données par défaut créées.");
    }
    /// <summary>
    /// Marks a level as completed with the given star rating.
    /// Only updates if the new rating is better than the existing one.
    /// </summary>
    /// <param name="levelID">The ID of the level to mark as completed.</param>
    /// <param name="stars">The star rating (0-3) achieved in the level.</param>
    public void CompleteLevel(string levelID, int stars)
    {
        if (string.IsNullOrEmpty(levelID)) return;
        stars = Mathf.Clamp(stars, 0, 3); // Ensure stars are between 0 and 3.

        // Update if exists with a better score, or add if new.
        if (Data.CompletedLevels.ContainsKey(levelID))
        {
            if (stars > Data.CompletedLevels[levelID])
            {
                Data.CompletedLevels[levelID] = stars;
                Debug.Log($"[PlayerDataManager] Score du niveau '{levelID}' mis à jour à {stars} étoile(s).");
            }
        }
        else
        {
            Data.CompletedLevels.Add(levelID, stars);
            Debug.Log($"[PlayerDataManager] Niveau '{levelID}' complété avec {stars} étoile(s).");
        }
        SaveData();
    }
    // --- Currency Management ---

    /// <summary>
    /// Adds currency to the player's total and triggers the currency changed event.
    /// </summary>
    /// <param name="amount">The amount of currency to add (must be positive).</param>
    public void AddCurrency(int amount)
    {
        if (amount <= 0) return;
        Data.Currency += amount;
        Debug.Log($"[PlayerDataManager] Monnaie ajoutée: +{amount}. Total: {Data.Currency}");
        OnCurrencyChanged?.Invoke(Data.Currency);
        SaveData();
    }

    /// <summary>
    /// Attempts to spend the specified amount of currency.
    /// </summary>
    /// <param name="amount">The amount of currency to spend (must be positive).</param>
    /// <returns>True if the transaction was successful, false if insufficient funds.</returns>
    public bool SpendCurrency(int amount)
    {
        if (amount <= 0) return false;
        if (Data.Currency >= amount)
        {
            Data.Currency -= amount;
            Debug.Log($"[PlayerDataManager] Monnaie dépensée: -{amount}. Reste: {Data.Currency}");
            OnCurrencyChanged?.Invoke(Data.Currency);
            SaveData();
            return true;
        }
        else
        {
            Debug.LogWarning($"[PlayerDataManager] Tentative de dépenser {amount}, mais seulement {Data.Currency} disponible.");
            return false;
        }
    }

    // --- Gestion de l'Expérience ---

    public void AddExperience(int amount)
    {
        if (amount <= 0) return;
        Data.Experience += amount;
        Debug.Log($"[PlayerDataManager] XP ajoutée: +{amount}. Total: {Data.Experience}");
        OnExperienceGained?.Invoke(Data.Experience);
        SaveData();
        // TODO: Ajouter la logique de montée de niveau si nécessaire
    }

    // --- Gestion des Déblocages ---

    public bool IsCharacterUnlocked(string characterID)
    {
        return Data.UnlockedCharacterIDs.Contains(characterID);
    }

    public void UnlockCharacter(string characterID)
    {
        if (!Data.UnlockedCharacterIDs.Contains(characterID))
        {
            Data.UnlockedCharacterIDs.Add(characterID);
            Debug.Log($"[PlayerDataManager] Personnage débloqué : {characterID}");
            AddXPToCharacter(characterID, 50); 
            OnCharacterUnlocked?.Invoke(characterID); // Notifier les autres systèmes
            SaveData();
        }
        else
        {
            Debug.LogWarning($"[PlayerDataManager] Tentative de débloquer un personnage déjà débloqué : {characterID}");
        }
    }

    public List<string> GetUnlockedCharacterIDs()
    {
        return new List<string>(Data.UnlockedCharacterIDs); // Retourne une copie
    }

    // --- Gestion de l'Équipe Active ---

    public List<string> GetActiveTeamIDs()
    {
        return new List<string>(Data.ActiveTeamCharacterIDs); // Retourne une copie
    }

    public bool SetActiveTeam(List<string> teamCharacterIDs)
    {
        if (teamCharacterIDs == null)
        {
             Debug.LogError("[PlayerDataManager] Tentative de définir une équipe active null.");
             return false;
        }
        if (teamCharacterIDs.Count > 4)
        {
            Debug.LogError($"[PlayerDataManager] Tentative de définir une équipe de {teamCharacterIDs.Count} personnages (Max 4).");
            return false;
        }

        // Vérifier que tous les personnages de la nouvelle équipe sont débloqués
        foreach (string id in teamCharacterIDs)
        {
            if (!IsCharacterUnlocked(id))
            {
                Debug.LogError($"[PlayerDataManager] Tentative d'ajouter le personnage non débloqué '{id}' à l'équipe active.");
                return false; // Échouer si un personnage n'est pas débloqué
            }
        }

        Data.ActiveTeamCharacterIDs = new List<string>(teamCharacterIDs); // Assigner la nouvelle liste
        Debug.Log($"[PlayerDataManager] Équipe active mise à jour : {string.Join(", ", Data.ActiveTeamCharacterIDs)}");
        SaveData();
        // Note : TeamManager écoutera OnPlayerDataLoaded pour se mettre à jour,
        // mais on pourrait aussi avoir un event OnActiveTeamChanged spécifique ici si besoin.
        return true;
    }

     // --- Utilitaires ---

     [ContextMenu("Supprimer Sauvegarde")]
     public void DeleteSaveFile()
     {
         if (File.Exists(_saveFilePath))
         {
             try
             {
                 File.Delete(_saveFilePath);
                 Debug.Log($"[PlayerDataManager] Fichier de sauvegarde supprimé : {_saveFilePath}");
                 // Recharger les données par défaut après suppression
                 LoadData();
                 OnPlayerDataLoaded?.Invoke(); // Notifier que de nouvelles données sont prêtes
             }
             catch (Exception e)
             {
                 Debug.LogError($"[PlayerDataManager] Échec de la suppression du fichier de sauvegarde : {e.Message}");
             }
         }
         else
         {
             Debug.LogWarning("[PlayerDataManager] Aucun fichier de sauvegarde à supprimer.");
         }
     }
     
     [ContextMenu("Simulate First 2 Levels Complete")]
     private void SimulateLevelCompletion()
     {
         // This requires you to know the IDs of your first few levels.
         // Replace "Level_01" and "Level_02" with actual LevelIDs from your SOs.
         LevelData_SO[] allLevels = Resources.LoadAll<LevelData_SO>("Data/Levels").OrderBy(l => l.DisplayName).ToArray();
         if (allLevels.Length > 0) CompleteLevel(allLevels[0].LevelID, 3); // 3 stars on first level
         if (allLevels.Length > 1) CompleteLevel(allLevels[1].LevelID, 1); // 1 star on second level
        
         Debug.Log("[PlayerDataManager] Données de test de complétion de niveau appliquées.");
         OnPlayerDataLoaded?.Invoke(); // Notify systems to refresh with new data
     }
     
     public void AddXPToCharacter(string characterID, int xpAmount)
     {
         // 1. Trouver le CharacterData pour obtenir sa courbe de progression
         CharacterData_SO charData = Resources.Load<CharacterData_SO>($"Data/Characters/{characterID}");
         if (charData == null || charData.Stats == null)
         {
             Debug.LogError($"[PlayerDataManager] Impossible d'ajouter de l'XP : CharacterData ou ProgressionData manquant pour l'ID {characterID}");
             return;
         }

         // 2. Initialiser la progression si elle n'existe pas
         if (!Data.CharacterProgressData.ContainsKey(characterID))
         {
             Data.CharacterProgressData[characterID] = new CharacterProgress();
         }

         CharacterProgress progress = Data.CharacterProgressData[characterID];
         progress.CurrentXP += xpAmount;

         // 3. Boucle de montée de niveau
         int xpForNextLevel = charData.Stats.GetXPRequiredForLevel(progress.CurrentLevel + 1);
         Debug.Log($"[PlayerDataManager] Ajout de {xpAmount} XP à {characterID}. XP actuel: {progress.CurrentXP}, Niveau actuel: {progress.CurrentLevel}, XP pour le niveau suivant: {xpForNextLevel}");
         while (progress.CurrentXP >= xpForNextLevel && xpForNextLevel > 0)
         {
             progress.CurrentLevel++;
             // On ne soustrait pas l'XP, car la courbe représente le total requis.
             // On pourrait le faire si la courbe représentait l'XP *par* niveau.
             // Pour l'instant, on garde le total.
            
             Debug.Log($"Personnage {characterID} est monté au niveau {progress.CurrentLevel}!");
             // Mettre à jour le montant requis pour le niveau suivant
             xpForNextLevel = charData.Stats.GetXPRequiredForLevel(progress.CurrentLevel + 1);
             if (xpForNextLevel == charData.Stats.GetXPRequiredForLevel(progress.CurrentLevel))
             {
                 Debug.Log($"[PlayerDataManager] Niveau maximum atteint pour {characterID} : {progress.CurrentLevel}");
                 // On a atteint le niveau max défini dans la courbe, on sort.
                 break;
             }
         }
         SaveData();
     }

     
     public void UnlockEquipment(string equipmentID)
     {
         if (!Data.UnlockedEquipmentIDs.Contains(equipmentID))
         {
             Data.UnlockedEquipmentIDs.Add(equipmentID);
             Debug.Log($"Equipment unlocked: {equipmentID}");
             SaveData();
         }
     }

     public void EquipItemOnCharacter(string characterID, string equipmentID)
     {
         if (!Data.UnlockedEquipmentIDs.Contains(equipmentID)) return;

         // Initialize dictionary entry if it doesn't exist
         if (!Data.EquippedItems.ContainsKey(characterID))
         {
             Data.EquippedItems[characterID] = new List<string>();
         }

         // TODO: Add logic to check if a slot of the same type is already occupied
         // before adding the new item.

         Data.EquippedItems[characterID].Add(equipmentID);
         SaveData();
     }

     public void UnequipItemFromCharacter(string characterID, string equipmentID)
     {
         if (Data.EquippedItems.ContainsKey(characterID))
         {
             Data.EquippedItems[characterID].Remove(equipmentID);
             SaveData();
         }
     }
     
     
     /// <summary>
     /// Débloque une liste d'équipements pour le joueur.
     /// </summary>
     /// <param name="equipmentIDs">La liste des EquipmentID à ajouter à l'inventaire.</param>
     public void UnlockMultipleEquipment(List<string> equipmentIDs)
     {
         if (equipmentIDs == null) return;

         int itemsAdded = 0;
         foreach (string id in equipmentIDs)
         {
             if (!string.IsNullOrEmpty(id) && !Data.UnlockedEquipmentIDs.Contains(id))
             {
                 Data.UnlockedEquipmentIDs.Add(id);
                 itemsAdded++;
             }
         }

         if (itemsAdded > 0)
         {
             Debug.Log($"[PlayerDataManager] {itemsAdded} nouveaux items d'équipement débloqués.");
             SaveData();
             // Optionnel : Déclencher un événement si l'UI de l'inventaire doit être notifiée globalement.
         }
     }
     
     // --- Gestion des Options de Jeu ---
     
     /// <summary>
     /// Événements pour notifier les changements d'options
     /// </summary>
     public static event Action<float> OnMusicVolumeChanged;
     public static event Action<float> OnSfxVolumeChanged;
     public static event Action<bool> OnVibrationChanged;
     public static event Action<bool> OnShowBeatIndicatorChanged; // Ajout de l'événement
     
     public void SetMusicVolume(float volume)
     {
         volume = Mathf.Clamp01(volume);
         if (!Mathf.Approximately(Data.MusicVolume, volume))
         {
             Data.MusicVolume = volume;
             OnMusicVolumeChanged?.Invoke(volume);
             SaveData();
             Debug.Log($"[PlayerDataManager] Volume musique réglé : {volume * 100:F0}%");
         }
     }
     
     public void SetSfxVolume(float volume)
     {
         volume = Mathf.Clamp01(volume);
         if (!Mathf.Approximately(Data.SfxVolume, volume))
         {
             Data.SfxVolume = volume;
             OnSfxVolumeChanged?.Invoke(volume);
             SaveData();
             Debug.Log($"[PlayerDataManager] Volume SFX réglé : {volume * 100:F0}%");
         }
     }
     
     public void SetVibrationEnabled(bool enabled)
     {
         if (Data.VibrationEnabled != enabled)
         {
             Data.VibrationEnabled = enabled;
             OnVibrationChanged?.Invoke(enabled);
             SaveData();
             Debug.Log($"[PlayerDataManager] Vibrations {(enabled ? "activées" : "désactivées")}");
         }
     }
     
     // Ajout de la méthode pour le beat indicator
     public void SetShowBeatIndicator(bool enabled)
     {
         if (Data.ShowBeatIndicator != enabled)
         {
             Data.ShowBeatIndicator = enabled;
             OnShowBeatIndicatorChanged?.Invoke(enabled);
             SaveData();
             Debug.Log($"[PlayerDataManager] Beat Indicator {(enabled ? "activé" : "désactivé")}");
         }
     }
     
     public float GetMusicVolume() => Data.MusicVolume;
     public float GetSfxVolume() => Data.SfxVolume;
     public bool IsVibrationEnabled() => Data.VibrationEnabled;
     // Ajout du getter pour le beat indicator
     public bool IsShowBeatIndicatorEnabled() => Data.ShowBeatIndicator;
}