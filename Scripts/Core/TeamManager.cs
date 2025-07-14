using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using ScriptableObjects;

/// <summary>
/// Persistent singleton manager for handling team composition and character availability.
/// Manages the player's active team of up to 4 characters and tracks which characters are available.
/// Syncs with PlayerDataManager to maintain persistence across sessions.
/// </summary>
public class TeamManager : SingletonPersistent<TeamManager>
{
    // --- Static Events ---
    /// <summary>
    /// Triggered when the player's active team composition is modified.
    /// Provides the new list of CharacterData_SO for the active team.
    /// </summary>
    public static event Action<List<CharacterData_SO>> OnActiveTeamChanged;

    // --- Internal Lists ---
    /// <summary>
    /// List of characters currently unlocked and available to the player.
    /// </summary>
    private List<CharacterData_SO> _availableCharacters = new List<CharacterData_SO>();
    
    /// <summary>
    /// List of characters forming the active team (initialized with 4 null slots).
    /// </summary>
    private List<CharacterData_SO> _activeTeam = new List<CharacterData_SO>(new CharacterData_SO[4]);

    // --- Public Properties (Accessors) ---
    /// <summary>
    /// Returns a COPY of the list of characters currently unlocked by the player.
    /// </summary>
    public List<CharacterData_SO> AvailableCharacters => new List<CharacterData_SO>(_availableCharacters);

    /// <summary>
    /// Returns a COPY of the list of characters forming the active team (may contain nulls if fewer than 4 characters).
    /// </summary>
    public List<CharacterData_SO> ActiveTeam => new List<CharacterData_SO>(_activeTeam);

    // --- Unity Methods ---
    /// <summary>
    /// Initializes the TeamManager singleton and subscribes to PlayerDataManager events.
    /// </summary>
    protected override void Awake()
    {
        base.Awake(); // Handles Singleton pattern and DontDestroyOnLoad

        // Subscribe to PlayerDataManager events
        // These subscriptions will work even if PlayerDataManager.Instance isn't ready yet,
        // because the events are static. The invocation will happen at the right time.
        PlayerDataManager.OnPlayerDataLoaded += HandlePlayerDataLoaded;
        PlayerDataManager.OnCharacterUnlocked += HandleCharacterUnlocked;
        Debug.Log("[TeamManager] Awake complété et abonné aux événements de PlayerDataManager.");
    }

    /// <summary>
    /// Unsubscribes from PlayerDataManager events to prevent memory leaks.
    /// </summary>
    private void OnDestroy()
    {
        // Always unsubscribe to avoid memory leaks
        PlayerDataManager.OnPlayerDataLoaded -= HandlePlayerDataLoaded;
        PlayerDataManager.OnCharacterUnlocked -= HandleCharacterUnlocked;
        Debug.Log("[TeamManager] OnDestroy appelé et désabonné des événements.");
    }

    // --- Initialization Logic ---

    /// <summary>
    /// Handles the PlayerDataManager's data loaded event to initialize team composition.
    /// Updates available characters and active team based on player data.
    /// </summary>
    private void HandlePlayerDataLoaded()
    {
        Debug.Log("[TeamManager] Réception de OnPlayerDataLoaded. Initialisation de l'équipe...");
        if (PlayerDataManager.Instance == null)
        {
            Debug.LogError("[TeamManager] PlayerDataManager.Instance est null lors de HandlePlayerDataLoaded ! Impossible d'initialiser.");
            return;
        }

        // 1. Mettre à jour les personnages disponibles
        _availableCharacters.Clear();
        List<string> unlockedIDs = PlayerDataManager.Instance.GetUnlockedCharacterIDs();
        Debug.Log($"[TeamManager] IDs de personnages débloqués reçus : {string.Join(", ", unlockedIDs)}");

        foreach (string id in unlockedIDs)
        {
            CharacterData_SO character = FindCharacterDataByID(id); // Utilise le chemin Resources
            if (character != null)
            {
                if (!_availableCharacters.Contains(character))
                {
                    _availableCharacters.Add(character);
                }
            }
            else
            {
                Debug.LogWarning($"[TeamManager] CharacterData_SO non trouvé pour l'ID débloqué via Resources : {id}");
            }
        }
        Debug.Log($"[TeamManager] {_availableCharacters.Count} personnages disponibles après chargement.");

        // 2. Mettre à jour l'équipe active
        List<string> activeTeamIDs = PlayerDataManager.Instance.GetActiveTeamIDs();
        Debug.Log($"[TeamManager] IDs d'équipe active reçus : {string.Join(", ", activeTeamIDs)}");

        // Réinitialiser l'équipe active avec potentiellement des nulls
        for(int i = 0; i < _activeTeam.Capacity; i++) _activeTeam[i] = null;


        for (int i = 0; i < activeTeamIDs.Count && i < _activeTeam.Capacity; i++)
        {
            string id = activeTeamIDs[i];
            if (!string.IsNullOrEmpty(id)) // Gérer le cas où un ID pourrait être null/vide
            {
                CharacterData_SO character = FindCharacterDataInList(_availableCharacters, id);
                if (character != null)
                {
                    _activeTeam[i] = character;
                }
                else
                {
                    Debug.LogWarning($"[TeamManager] Personnage '{id}' de l'équipe active non trouvé parmi les personnages disponibles. Sera null dans l'équipe.");
                }
            }
        }

        // S'assurer que la liste _activeTeam a toujours 4 éléments, même si certains sont null
        while(_activeTeam.Count < 4)
        {
            _activeTeam.Add(null);
        }
        if(_activeTeam.Count > 4)
        {
            _activeTeam = _activeTeam.Take(4).ToList();
        }


        Debug.Log($"[TeamManager] Équipe active initialisée : {string.Join(", ", _activeTeam.Select(c => c != null ? c.DisplayName : "NULL"))}");
        OnActiveTeamChanged?.Invoke(new List<CharacterData_SO>(_activeTeam));
    }

    /// <summary>
    /// Handles the character unlocked event to update available characters list.
    /// </summary>
    /// <param name="characterID">The ID of the newly unlocked character.</param>
    private void HandleCharacterUnlocked(string characterID)
    {
        CharacterData_SO character = FindCharacterDataByID(characterID);
        if (character != null)
        {
            if (!_availableCharacters.Contains(character))
            {
                _availableCharacters.Add(character);
                Debug.Log($"[TeamManager] Personnage '{character.DisplayName}' (ID: {characterID}) ajouté à la liste des disponibles.");
                // Optional: Notify a change in available characters if UI needs to update.
                // public static event Action<List<CharacterData_SO>> OnAvailableCharactersChanged;
                // OnAvailableCharactersChanged?.Invoke(new List<CharacterData_SO>(_availableCharacters));
            }
        }
        else
        {
            Debug.LogWarning($"[TeamManager] Tentative de gestion du déblocage pour l'ID '{characterID}', mais CharacterData_SO non trouvé.");
        }
    }

    // --- Active Team Management ---

    /// <summary>
    /// Sets the new active team composition for the player.
    /// </summary>
    /// <param name="newTeamComposition">List of CharacterData_SO for the new team (max size 4). Elements can be null.</param>
    /// <returns>True if the team was successfully updated, false otherwise.</returns>
    public bool SetActiveTeam(List<CharacterData_SO> newTeamComposition)
    {
        if (newTeamComposition == null)
        {
            Debug.LogError("[TeamManager] Tentative de définir une équipe active avec une liste null.");
            return false;
        }
        if (newTeamComposition.Count > 4)
        {
            Debug.LogError($"[TeamManager] Tentative de définir une équipe avec {newTeamComposition.Count} personnages. Maximum autorisé : 4.");
            return false;
        }

        // Valider que tous les personnages de la nouvelle composition sont disponibles
        foreach (CharacterData_SO character in newTeamComposition)
        {
            if (character != null && !_availableCharacters.Contains(character))
            {
                Debug.LogError($"[TeamManager] Personnage '{character.DisplayName}' (ID: {character.CharacterID}) n'est pas dans la liste des personnages disponibles. Impossible de l'ajouter à l'équipe.");
                return false;
            }
        }

        // Mettre à jour _activeTeam. S'assurer qu'elle a toujours 4 éléments.
        _activeTeam.Clear();
        for(int i = 0; i < 4; i++)
        {
            if (i < newTeamComposition.Count)
            {
                _activeTeam.Add(newTeamComposition[i]);
            }
            else
            {
                _activeTeam.Add(null); // Remplir avec null si moins de 4 persos fournis
            }
        }


        Debug.Log($"[TeamManager] Équipe active mise à jour : {string.Join(", ", _activeTeam.Select(c => c != null ? c.DisplayName : "NULL"))}");

        // Sauvegarder les IDs de l'équipe active via PlayerDataManager
        List<string> activeTeamIDs = _activeTeam.Where(c => c != null).Select(c => c.CharacterID).ToList();
        PlayerDataManager.Instance?.SetActiveTeam(activeTeamIDs); // PlayerDataManager gère la sauvegarde

        OnActiveTeamChanged?.Invoke(new List<CharacterData_SO>(_activeTeam)); // Notifier les auditeurs
        return true;
    }

    // --- Utility Methods ---

    /// <summary>
    /// Finds a CharacterData_SO by its ID from the Resources folder.
    /// </summary>
    /// <param name="id">The character ID to search for.</param>
    /// <returns>The CharacterData_SO if found, null otherwise.</returns>
    private CharacterData_SO FindCharacterDataByID(string id)
    {
        // Make sure the path matches your SO location in the Resources folder
        CharacterData_SO data = Resources.Load<CharacterData_SO>($"Data/Characters/{id}");
        // The SO file name must exactly match its CharacterID for this to work.
        // For example, if CharacterID = "CD_Hero", the file must be "CD_Hero.asset".
        if (data == null)
        {
            Debug.LogWarning($"[TeamManager] Impossible de charger CharacterData_SO depuis 'Resources/Data/Characters/{id}'. Vérifiez le chemin et le nom du fichier.");
        }
        return data;
    }

    /// <summary>
    /// Finds a CharacterData_SO by its ID within a given list.
    /// </summary>
    /// <param name="list">The list to search in.</param>
    /// <param name="id">The character ID to search for.</param>
    /// <returns>The CharacterData_SO if found, null otherwise.</returns>
    private CharacterData_SO FindCharacterDataInList(List<CharacterData_SO> list, string id)
    {
        return list.Find(c => c != null && c.CharacterID == id);
    }

    /// <summary>
    /// Checks if a specific character is in the active team.
    /// </summary>
    /// <param name="character">The character to check for.</param>
    /// <returns>True if the character is in the active team, false otherwise.</returns>
    public bool IsCharacterInActiveTeam(CharacterData_SO character)
    {
        if (character == null) return false;
        return _activeTeam.Contains(character);
    }

    /// <summary>
    /// Attempts to add a character to the first available slot in the active team.
    /// </summary>
    /// <param name="characterToAdd">The character to add to the active team.</param>
    /// <returns>True if the character was added, false otherwise (team full or character not available).</returns>
    public bool TryAddCharacterToActiveTeam(CharacterData_SO characterToAdd)
    {
        if (characterToAdd == null || !_availableCharacters.Contains(characterToAdd) || IsCharacterInActiveTeam(characterToAdd))
        {
            Debug.LogWarning($"[TeamManager] Impossible d'ajouter '{characterToAdd?.CharacterID}'. Soit non disponible, soit déjà dans l'équipe.");
            return false;
        }

        for (int i = 0; i < _activeTeam.Count; i++)
        {
            if (_activeTeam[i] == null)
            {
                List<CharacterData_SO> tempTeam = new List<CharacterData_SO>(_activeTeam);
                tempTeam[i] = characterToAdd;
                return SetActiveTeam(tempTeam); // Utilise SetActiveTeam pour la validation et la sauvegarde
            }
        }
        Debug.LogWarning("[TeamManager] Impossible d'ajouter le personnage, l'équipe active est pleine.");
        return false;
    }

    /// <summary>
    /// Attempts to remove a character from the active team.
    /// </summary>
    /// <param name="characterToRemove">The character to remove from the active team.</param>
    /// <returns>True if the character was removed, false otherwise.</returns>
    public bool TryRemoveCharacterFromActiveTeam(CharacterData_SO characterToRemove)
    {
        if (characterToRemove == null || !IsCharacterInActiveTeam(characterToRemove))
        {
            Debug.LogWarning($"[TeamManager] Impossible de retirer '{characterToRemove?.CharacterID}'. Personnage non trouvé dans l'équipe active.");
            return false;
        }

        List<CharacterData_SO> tempTeam = new List<CharacterData_SO>(_activeTeam);
        int index = tempTeam.IndexOf(characterToRemove);
        if (index != -1)
        {
            tempTeam[index] = null; // Laisser un slot vide
            return SetActiveTeam(tempTeam); // Utilise SetActiveTeam pour la validation et la sauvegarde
        }
        return false;
    }
}