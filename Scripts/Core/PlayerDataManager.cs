using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Ajouté pour FindAssetsByType

/// <summary>
/// Structure pour stocker les données de sauvegarde du joueur.
/// </summary>
[System.Serializable]
public class PlayerSaveData
{
    public int Currency = 0;
    public int Experience = 0;
    public List<string> UnlockedCharacterIDs = new List<string>();
    public List<string> CompletedLevelIDs = new List<string>(); // Peut-être utile plus tard
    public List<string> ActiveTeamCharacterIDs = new List<string>();
}

/// <summary>
/// Manager persistant (Singleton) pour gérer les données du joueur (sauvegarde, chargement, accès).
/// Déclenche des événements lors des modifications.
/// </summary>
public class PlayerDataManager : SingletonPersistent<PlayerDataManager>
{
    // --- Événements Statiques ---
    public static event Action<int> OnCurrencyChanged;
    public static event Action<string> OnCharacterUnlocked;
    public static event Action<int> OnExperienceGained;
    /// <summary>
    /// Déclenché après le chargement initial des données ou la création de nouvelles données par défaut.
    /// Utile pour les autres managers qui dépendent de ces données (ex: TeamManager).
    /// </summary>
    public static event Action OnPlayerDataLoaded;

    // --- Propriétés ---
    public PlayerSaveData Data { get; private set; }

    // --- Variables Privées ---
    private string _saveFilePath;
    private const string SAVE_FILE_NAME = "playerData.json";
    private bool _isDataLoaded = false; // Pour s'assurer que OnPlayerDataLoaded n'est appelé qu'une fois

    // --- Méthodes Unity ---

    protected override void Awake()
    {
        base.Awake(); // Gère le pattern Singleton et DontDestroyOnLoad
        _saveFilePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        Debug.Log($"[PlayerDataManager] Chemin de sauvegarde : {_saveFilePath}");
        LoadData();
    }

    private void Start()
    {
        // Déclencher l'événement OnPlayerDataLoaded ici, après que Awake ait pu s'exécuter sur tous les singletons
        // et que les données soient garanties d'être chargées ou créées.
        if (!_isDataLoaded)
        {
             Debug.Log("[PlayerDataManager] Déclenchement de OnPlayerDataLoaded.");
             OnPlayerDataLoaded?.Invoke();
            _isDataLoaded = true;
        }
    }

    // --- Sauvegarde / Chargement ---

    public void SaveData()
    {
        try
        {
            string json = JsonUtility.ToJson(Data, true); // 'true' pour pretty print (lisible)
            File.WriteAllText(_saveFilePath, json);
            Debug.Log($"[PlayerDataManager] Données sauvegardées dans {_saveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerDataManager] Échec de la sauvegarde des données : {e.Message}\n{e.StackTrace}");
        }
    }

    public void LoadData()
    {
        if (File.Exists(_saveFilePath))
        {
            try
            {
                string json = File.ReadAllText(_saveFilePath);
                Data = JsonUtility.FromJson<PlayerSaveData>(json);

                // Sécurité : S'assurer que les listes ne sont pas null après désérialisation
                if (Data.UnlockedCharacterIDs == null) Data.UnlockedCharacterIDs = new List<string>();
                if (Data.CompletedLevelIDs == null) Data.CompletedLevelIDs = new List<string>();
                if (Data.ActiveTeamCharacterIDs == null) Data.ActiveTeamCharacterIDs = new List<string>();

                Debug.Log($"[PlayerDataManager] Données chargées depuis {_saveFilePath}. Monnaie: {Data.Currency}, XP: {Data.Experience}, Persos: {Data.UnlockedCharacterIDs.Count}, Équipe: {Data.ActiveTeamCharacterIDs.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerDataManager] Échec du chargement du fichier de sauvegarde : {e.Message}. Création de nouvelles données par défaut.");
                CreateDefaultData();
                SaveData(); // Sauvegarder les nouvelles données par défaut
            }
        }
        else
        {
            Debug.Log("[PlayerDataManager] Aucun fichier de sauvegarde trouvé. Création de nouvelles données par défaut.");
            CreateDefaultData();
            SaveData(); // Sauvegarder les nouvelles données par défaut
        }
         _isDataLoaded = false; // Reset flag pour que Start puisse déclencher l'event
    }

    private void CreateDefaultData()
    {
        Data = new PlayerSaveData();
        Data.Currency = 50; // Monnaie initiale
        Data.Experience = 0;
        Data.UnlockedCharacterIDs = new List<string>();
        Data.ActiveTeamCharacterIDs = new List<string>();
        Data.CompletedLevelIDs = new List<string>();

        // Débloquer les personnages par défaut
        // Important: Ceci nécessite que les assets CharacterData_SO soient accessibles,
        // par exemple dans un dossier Resources ou via un Asset Database spécifique.
        CharacterData_SO[] allCharacters = Resources.LoadAll<CharacterData_SO>("Data/Characters"); // Adapte le chemin si nécessaire

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
                    // Ajouter les premiers persos débloqués à l'équipe par défaut (jusqu'à 4)
                    if (defaultTeam.Count < 4)
                    {
                        defaultTeam.Add(charData.CharacterID);
                    }
                }
            }
        }

         // Si aucun perso n'est débloqué par défaut, ajoutons le premier trouvé comme perso initial (sécurité)
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


        // Définir l'équipe active par défaut
        Data.ActiveTeamCharacterIDs = defaultTeam;
        Debug.Log($"[PlayerDataManager] Équipe par défaut définie : {string.Join(", ", defaultTeam)}");

        Debug.Log("[PlayerDataManager] Nouvelles données par défaut créées.");
    }

    // --- Gestion de la Monnaie ---

    public void AddCurrency(int amount)
    {
        if (amount <= 0) return;
        Data.Currency += amount;
        Debug.Log($"[PlayerDataManager] Monnaie ajoutée: +{amount}. Total: {Data.Currency}");
        OnCurrencyChanged?.Invoke(Data.Currency);
        SaveData();
    }

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
}