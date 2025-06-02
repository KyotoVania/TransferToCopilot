using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

// Assure-toi que ce script est dans le bon dossier, par exemple Scripts/Core/
// et qu'il hérite de SingletonPersistent<TeamManager>
public class TeamManager : SingletonPersistent<TeamManager>
{
    // --- Événements Statiques ---
    /// <summary>
    /// Déclenché lorsque l'équipe active du joueur est modifiée.
    /// Fournit la nouvelle liste de CharacterData_SO de l'équipe active.
    /// </summary>
    public static event Action<List<CharacterData_SO>> OnActiveTeamChanged;

    // --- Listes Internes ---
    private List<CharacterData_SO> _availableCharacters = new List<CharacterData_SO>();
    private List<CharacterData_SO> _activeTeam = new List<CharacterData_SO>(new CharacterData_SO[4]); // Initialise avec 4 emplacements nulls

    // --- Propriétés Publiques (Accesseurs) ---
    /// <summary>
    /// Retourne une COPIE de la liste des personnages actuellement débloqués par le joueur.
    /// </summary>
    public List<CharacterData_SO> AvailableCharacters => new List<CharacterData_SO>(_availableCharacters);

    /// <summary>
    /// Retourne une COPIE de la liste des personnages formant l'équipe active (peut contenir des nulls si moins de 4 persos).
    /// </summary>
    public List<CharacterData_SO> ActiveTeam => new List<CharacterData_SO>(_activeTeam);

    // --- Méthodes Unity ---
    protected override void Awake()
    {
        base.Awake(); // Gère le pattern Singleton et DontDestroyOnLoad

        // S'abonner aux événements du PlayerDataManager
        // Ces abonnements se feront même si PlayerDataManager.Instance n'est pas encore prêt,
        // car les events sont statiques. L'invocation se fera au bon moment.
        PlayerDataManager.OnPlayerDataLoaded += HandlePlayerDataLoaded;
        PlayerDataManager.OnCharacterUnlocked += HandleCharacterUnlocked;
        Debug.Log("[TeamManager] Awake complété et abonné aux événements de PlayerDataManager.");
    }

    private void OnDestroy()
    {
        // Toujours se désabonner pour éviter les fuites de mémoire
        PlayerDataManager.OnPlayerDataLoaded -= HandlePlayerDataLoaded;
        PlayerDataManager.OnCharacterUnlocked -= HandleCharacterUnlocked;
        Debug.Log("[TeamManager] OnDestroy appelé et désabonné des événements.");
    }

    // --- Logique d'Initialisation ---

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

    private void HandleCharacterUnlocked(string characterID)
    {
        CharacterData_SO character = FindCharacterDataByID(characterID);
        if (character != null)
        {
            if (!_availableCharacters.Contains(character))
            {
                _availableCharacters.Add(character);
                Debug.Log($"[TeamManager] Personnage '{character.DisplayName}' (ID: {characterID}) ajouté à la liste des disponibles.");
                // Optionnel : Notifier un changement dans les personnages disponibles si l'UI doit se mettre à jour.
                // public static event Action<List<CharacterData_SO>> OnAvailableCharactersChanged;
                // OnAvailableCharactersChanged?.Invoke(new List<CharacterData_SO>(_availableCharacters));
            }
        }
        else
        {
            Debug.LogWarning($"[TeamManager] Tentative de gestion du déblocage pour l'ID '{characterID}', mais CharacterData_SO non trouvé.");
        }
    }

    // --- Gestion de l'Équipe Active ---

    /// <summary>
    /// Définit la nouvelle équipe active du joueur.
    /// </summary>
    /// <param name="newTeamComposition">Liste des CharacterData_SO pour la nouvelle équipe (taille max 4). Les éléments peuvent être null.</param>
    /// <returns>True si l'équipe a été mise à jour, false sinon.</returns>
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

    // --- Méthodes Utilitaires ---

    private CharacterData_SO FindCharacterDataByID(string id)
    {
        // Assure-toi que le chemin correspond à l'emplacement de tes SOs dans le dossier Resources
        CharacterData_SO data = Resources.Load<CharacterData_SO>($"Data/Characters/{id}");
        // Le nom du fichier SO doit correspondre exactement à son CharacterID pour que cela fonctionne.
        // Par exemple, si CharacterID = "CD_Hero", le fichier doit être "CD_Hero.asset".
        if (data == null)
        {
            Debug.LogWarning($"[TeamManager] Impossible de charger CharacterData_SO depuis 'Resources/Data/Characters/{id}'. Vérifiez le chemin et le nom du fichier.");
        }
        return data;
    }

    private CharacterData_SO FindCharacterDataInList(List<CharacterData_SO> list, string id)
    {
        return list.Find(c => c != null && c.CharacterID == id);
    }

    /// <summary>
    /// Vérifie si un personnage spécifique est dans l'équipe active.
    /// </summary>
    public bool IsCharacterInActiveTeam(CharacterData_SO character)
    {
        if (character == null) return false;
        return _activeTeam.Contains(character);
    }

    /// <summary>
    /// Tente d'ajouter un personnage à la première place disponible dans l'équipe active.
    /// </summary>
    /// <returns>True si le personnage a été ajouté, false sinon (équipe pleine ou personnage non dispo).</returns>
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
    /// Tente de retirer un personnage de l'équipe active.
    /// </summary>
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