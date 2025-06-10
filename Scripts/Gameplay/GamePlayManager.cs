using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Events;
using System.Linq;
using System;
using ScriptableObjects;

public class GameplayManager : MonoBehaviour
{
    private LevelData_SO currentLevelData;
    // Références aux autres managers/contrôleurs
    private TeamManager teamManager;
    private SequenceController sequenceController;
    private GoldController goldController;
    private LevelScenarioManager scenarioManager; 

    [Header("Configuration")]
    [SerializeField] private string globalSpellsResourcePath = "Data/GlobalSpells"; // Chemin dans Resources pour les sorts globaux
    [SerializeField] private Transform defaultPlayerUnitSpawnPoint; 

    private List<GlobalSpellData_SO> availableGlobalSpells = new List<GlobalSpellData_SO>();
    
    private Dictionary<string, float> _unitCooldowns = new Dictionary<string, float>();
    private Dictionary<string, float> _spellCooldowns = new Dictionary<string, float>();
    
    public IReadOnlyDictionary<string, float> UnitCooldowns => _unitCooldowns;
    public IReadOnlyDictionary<string, float> SpellCooldowns => _spellCooldowns;
    public IReadOnlyList<GlobalSpellData_SO> AvailableGlobalSpells => availableGlobalSpells;
  	/// <summary>
    /// Invoqué après que les sorts globaux aient été chargés depuis les Resources.
    /// Fournit la liste des sorts chargés.
    /// </summary>
    public static event Action<IReadOnlyList<GlobalSpellData_SO>> OnGlobalSpellsLoaded;

    private float _beatInterval;

    void Start()
    {
        teamManager = TeamManager.Instance;
        sequenceController = FindFirstObjectByType<SequenceController>(); // SequenceController n'est pas un Singleton persistent
        goldController = GoldController.Instance;
        scenarioManager = FindFirstObjectByType<LevelScenarioManager>(); 

        if (GameManager.Instance == null || GameManager.CurrentLevelToLoad == null)
        {
            Debug.LogError("[GameplayManager] GameManager ou CurrentLevelToLoad est null! Impossible d'initialiser le niveau. Assurez-vous de lancer le niveau via le Hub ou d'avoir une donnée de niveau par défaut pour les tests.");
            // Optionnel: Charger un LevelData_SO de test par défaut pour faciliter le développement en standalone.
            // currentLevelData = Resources.Load<LevelData_SO>("Path/To/DefaultTestLevelData");
            if (currentLevelData == null) // Si même le fallback échoue
            {
                enabled = false; // Désactiver ce manager si aucune donnée n'est disponible
                return;
            }
        }
        else
        {
            currentLevelData = GameManager.CurrentLevelToLoad;
        }
        
        if (teamManager == null || sequenceController == null || goldController == null)
        {
            Debug.LogError("[GameplayManager] Un ou plusieurs managers/contrôleurs critiques (TeamManager, SequenceController, GoldController) sont introuvables! Assurez-vous qu'ils sont dans la scène Core ou chargés correctement.");
            enabled = false;
            return;
        }
        InitializeLevel();
    }

    void InitializeLevel()
    {
        Debug.Log($"[GameplayManager] Initialisation du niveau : {currentLevelData.DisplayName}");

        // --- 1. Configuration Audio (Musique & Rythme) ---
        ConfigureAudioAndRhythm();

        // --- 2. Configuration des Objectifs du Niveau ---
        // ConfigureObjectives(); // À implémenter

        // --- 3. Configuration des Unités (Joueur, Ennemis, etc.) ---
        // ConfigureUnits(); // À implémenter, incluant la récupération de l'équipe active du joueur
        LoadGlobalSpells();
        InitializeSequenceController();
        SubscribeToSequenceEvents();
        Unit.OnUnitAttacked += HandleCombatDetection;
        Building.OnBuildingAttackedByUnit += HandleCombatDetection;
        
        // --- 4. Configuration de l'Environnement / Visuels ---
        // ConfigureEnvironment(); // À implémenter si LevelData_SO contient des infos de mood
        if (scenarioManager != null && currentLevelData.scenario != null)
        {
            scenarioManager.Initialize(currentLevelData.scenario);
        }
        else if (scenarioManager != null)
        {
            Debug.LogWarning($"[GameplayManager] Le niveau '{currentLevelData.DisplayName}' n'a pas de LevelScenario_SO assigné.");
        }
        // calcul l'intervalle de battement basé sur le BPM qui est dans currentLevelData
        
        // --- 5. Démarrage de la Logique du Niveau ---
        
        // StartLevelLogic(); // Ex: Lancer la première vague d'ennemis, activer les inputs joueur
    }

    void ConfigureAudioAndRhythm()
    {
        if (RhythmManager.Instance != null && currentLevelData.RhythmBPM > 0)
        {
            RhythmManager.Instance.SetBPM(currentLevelData.RhythmBPM);
            Debug.Log($"[GameplayManager] BPM du niveau réglé à : {currentLevelData.RhythmBPM}");

            // Stocker l'intervalle de battement pour utilisation dans les cooldowns
            _beatInterval = RhythmManager.Instance.GetBeatDuration();
            Debug.Log($"[GameplayManager] Intervalle de battement calculé : {_beatInterval} secondes.");
        }

        if (MusicManager.Instance != null)
        {
            // Priorité au Switch spécifique du niveau s'il est défini
            if (currentLevelData.MusicStateSwitch != null && currentLevelData.MusicStateSwitch.IsValid()) //
            {
                // MusicManager a SetMusicState(string, bool). On pourrait l'étendre
                // pour prendre un AK.Wwise.Switch ou utiliser son nom.
                MusicManager.Instance.SetMusicState(currentLevelData.MusicStateSwitch.Name); //
                Debug.Log($"[GameplayManager] Wwise Music Switch '{currentLevelData.MusicStateSwitch.Name}' appliqué pour le niveau.");
            }
            // Sinon, si un Event musical de fond est défini
            else if (currentLevelData.BackgroundMusic != null && currentLevelData.BackgroundMusic.IsValid()) //
            {
                // MusicManager pourrait avoir une méthode pour jouer un event musical principal,
                // ou on pourrait le poster directement.
                // MusicManager.Instance.PlayLevelMusic(currentLevelData.BackgroundMusic); // Méthode à créer
                currentLevelData.BackgroundMusic.Post(MusicManager.Instance.gameObject); // Assumant que MusicManager a un GameObject pour poster les sons globaux
                Debug.Log($"[GameplayManager] Wwise BackgroundMusic Event '{currentLevelData.BackgroundMusic.Name}' posté.");
            }
            else
            {
                // Si rien n'est spécifié, GameManager.SetState(GameState.InLevel)
                // devrait déjà avoir mis un état musical par défaut (ex: "Exploration").
                // GameManager.Instance.SetState(GameState.InLevel); // Déjà fait par le chargement de scène
                Debug.Log("[GameplayManager] Aucune musique spécifique ou switch défini dans LevelData. Utilisation de l'état musical par défaut pour InLevel.");
            }
        }
    }
    
    
    void LoadGlobalSpells()
    {
        GlobalSpellData_SO[] spells = Resources.LoadAll<GlobalSpellData_SO>(globalSpellsResourcePath);
        availableGlobalSpells = new List<GlobalSpellData_SO>(spells);
        Debug.Log($"[GameplayManager] Chargé {availableGlobalSpells.Count} sorts globaux depuis '{globalSpellsResourcePath}'.");
		OnGlobalSpellsLoaded?.Invoke(availableGlobalSpells);

	
	}

    void InitializeSequenceController()
    {
        if (teamManager == null || sequenceController == null) return;

        List<CharacterData_SO> activeTeam = teamManager.ActiveTeam; // ActiveTeam retourne une COPIE
        if (activeTeam == null)
        {
            Debug.LogWarning("[GameplayManager] L'équipe active est null. Le SequenceController sera initialisé avec une équipe vide.");
            activeTeam = new List<CharacterData_SO>();
        }
        sequenceController.InitializeWithPlayerTeamAndSpells(activeTeam, availableGlobalSpells);
    }

    void SubscribeToSequenceEvents()
    {
        if (sequenceController == null) return;
        // Se désabonner d'abord au cas où (par exemple, si InitializeLevel est appelé plusieurs fois)
        SequenceController.OnCharacterInvocationSequenceComplete -= HandleCharacterInvocation;
        SequenceController.OnGlobalSpellSequenceComplete -= HandleGlobalSpell;

        // S'abonner
        SequenceController.OnCharacterInvocationSequenceComplete += HandleCharacterInvocation;
        SequenceController.OnGlobalSpellSequenceComplete += HandleGlobalSpell;
        Debug.Log("[GameplayManager] Abonné aux événements OnCharacterInvocationSequenceComplete et OnGlobalSpellSequenceComplete.");
    }

    /// <summary>
    /// Gère la logique d'invocation d'un personnage lorsque la séquence a déjà été validée.
    /// </summary>
    /// <param name="characterData">Le ScriptableObject du personnage à invoquer.</param>
    /// <param name="perfectCount">Le nombre d'inputs parfaits (pour une future utilisation).</param>
    public void HandleCharacterInvocation(CharacterData_SO characterData, int perfectCount)
    {
        // On utilise le format de log que vous avez spécifié.
        // Note: J'utilise .Name car DisplayName n'est pas dans la définition de CharacterData_SO.
        Debug.Log($"[GameplayManager] Tentative d'invocation reçue : {characterData.CharacterID}, Inputs Parfaits: {perfectCount}");
    
        if (characterData == null)
        {
            Debug.LogWarning("[GameplayManager] Tentative d'invocation avec un CharacterData_SO nul.");
            return;
        }
        
        // Vérifier si l'unité est en cooldown
        if (_unitCooldowns.ContainsKey(characterData.CharacterID) && Time.time < _unitCooldowns[characterData.CharacterID])
        {
            Debug.Log($"{characterData.CharacterID} is on cooldown.");
            return; // Stop l'invocation si l'unité est en cooldown
        }

        
        if (goldController.GetCurrentGold() >= characterData.GoldCost)
        {
            goldController.RemoveGold(characterData.GoldCost); // Utiliser RemoveGold pour la déduction
            Debug.Log($"[GameplayManager] Or dépensé : {characterData.GoldCost}. Or restant : {goldController.GetCurrentGold()}");

            // Logique de Spawn (simplifiée pour l'instant)
            // Trouver un PlayerBuilding actif pour spawner l'unité.
            // Correction de l'avertissement CS0618 pour FindObjectsOfType
            PlayerBuilding[] playerBuildings = FindObjectsByType<PlayerBuilding>(FindObjectsSortMode.None);
            PlayerBuilding spawnerBuilding = null;
            foreach(var pb in playerBuildings)
            {
                if(pb.gameObject.activeInHierarchy && pb.Team == TeamType.Player) // S'assurer qu'il appartient bien au joueur
                {
                    spawnerBuilding = pb;
                    break;
                }
            }

            if (spawnerBuilding != null)
            {
                List<Tile> adjacentTiles = HexGridManager.Instance.GetAdjacentTiles(spawnerBuilding.GetOccupiedTile());
                Tile spawnTile = null;
                foreach (Tile tile in adjacentTiles)
                {
                    if (!tile.IsOccupied && !tile.IsReserved && tile.tileType == TileType.Ground) // Doit être praticable
                    {
                        spawnTile = tile;
                        break;
                    }
                }

                if (spawnTile != null)
                {
                    GameObject unitGO = Instantiate(characterData.GameplayUnitPrefab, spawnTile.transform.position + Vector3.up * 0.1f, Quaternion.identity);
                    Unit newUnit = unitGO.GetComponentInChildren<Unit>(true); // true pour inclure les enfants inactifs, par sécurité
                    if (newUnit != null)
                    {
                        newUnit.InitializeFromCharacterData(characterData); // Passe le CharacterData_SO complet

                        // Pour l'instant, on logue juste. L'unité devrait chercher ses stats dans son Start().
                        Debug.Log($"[GameplayManager] Unité {characterData.DisplayName} invoquée sur la tuile ({spawnTile.column},{spawnTile.row}).");
                        float cooldownInSeconds = characterData.InvocationCooldown * _beatInterval;
                        _unitCooldowns[characterData.CharacterID] = Time.time + cooldownInSeconds;
                        //DEBUG log qui affiche le cooldown et tous les cooldowns
                        Debug.Log($"[GameplayManager] Cooldown pour {characterData.DisplayName} défini à {cooldownInSeconds} secondes. Cooldowns actuels: {string.Join(", ", _unitCooldowns.Select(kvp => $"{kvp.Key}: {kvp.Value - Time.time:F2}s"))}");
                        
                    }
                  
                }
                else
                {
                    Debug.LogWarning($"[GameplayManager] Impossible d'invoquer {characterData.DisplayName}: Aucune tuile adjacente libre au bâtiment joueur {spawnerBuilding.name}.");
                    goldController.AddGold(characterData.GoldCost); // Rembourser si le spawn échoue
                }
            }
            else if (defaultPlayerUnitSpawnPoint != null)
            {
                Debug.LogWarning("[GameplayManager] Aucun PlayerBuilding trouvé pour spawner l'unité. Tentative de spawn au point par défaut.");
                Tile spawnTile = HexGridManager.Instance.GetClosestTile(defaultPlayerUnitSpawnPoint.position);
                if (spawnTile != null && !spawnTile.IsOccupied && !spawnTile.IsReserved && spawnTile.tileType == TileType.Ground)
                {
                     GameObject unitGO = Instantiate(characterData.GameplayUnitPrefab, spawnTile.transform.position + Vector3.up * 0.1f, Quaternion.identity);
                     Debug.Log($"[GameplayManager] Unité {characterData.DisplayName} invoquée sur la tuile ({spawnTile.column},{spawnTile.row}) via point de spawn par défaut.");
                }
                else
                {
                    Debug.LogError($"[GameplayManager] Impossible d'invoquer {characterData.DisplayName}: La tuile au point de spawn par défaut est occupée, réservée ou invalide.");
                    goldController.AddGold(characterData.GoldCost); // Rembourser
                }
            }
            else
            {
                Debug.LogError($"[GameplayManager] Impossible d'invoquer {characterData.DisplayName}: Aucun PlayerBuilding ou point de spawn par défaut trouvé.");
                goldController.AddGold(characterData.GoldCost); // Rembourser
            }
        }
        else
        {
            Debug.LogWarning($"[GameplayManager] Pas assez d'or pour invoquer {characterData.DisplayName}. Requis : {characterData.GoldCost}, Actuel : {goldController.GetCurrentGold()}");
            // Idéalement, jouer un son d'échec ici.
        }
    }

    void HandleGlobalSpell(GlobalSpellData_SO spellData, int perfectCount)
    {
        Debug.Log($"[GameplayManager] Sort global activé : {spellData.DisplayName}, Inputs Parfaits: {perfectCount}");
 		 if (_spellCooldowns.ContainsKey(spellData.SpellID) && Time.time < _spellCooldowns[spellData.SpellID])
        {
            Debug.Log($"Sort '{spellData.DisplayName}' est en rechargement.");
            // Jouer un son d'échec ici serait une bonne idée
            return;
        }

        if (spellData == null)
        {
            Debug.LogWarning("[GameplayManager] Tentative d'activation d'un sort avec un GlobalSpellData_SO nul.");
            return;
        }

        // Vérifier si le sort est en cooldown
        if (_spellCooldowns.ContainsKey(spellData.SpellID) && Time.time < _spellCooldowns[spellData.SpellID])
        {
            Debug.Log($"[GameplayManager] Sort {spellData.DisplayName} en cooldown. Temps restant : {_spellCooldowns[spellData.SpellID] - Time.time:F2} secondes.");
            return;
        }

        if (spellData.SpellEffect != null)
        {
            if (goldController.GetCurrentGold() >= spellData.GoldCost)
            {
                goldController.RemoveGold(spellData.GoldCost);
                Debug.Log($"[GameplayManager] Or dépensé pour le sort : {spellData.GoldCost}. Or restant : {goldController.GetCurrentGold()}");
                spellData.SpellEffect.ExecuteEffect(this.gameObject, perfectCount);

                if (spellData.ActivationSound != null && spellData.ActivationSound.IsValid())
                {
                    spellData.ActivationSound.Post(gameObject);
                }

                // Définir le cooldown pour le sort
                float cooldownInSeconds = spellData.BeatCooldown * _beatInterval; // Exemple basé sur la longueur de la séquence
                _spellCooldowns[spellData.SpellID] = Time.time + cooldownInSeconds;
                Debug.Log($"[GameplayManager] Cooldown pour {spellData.DisplayName} défini à {cooldownInSeconds} secondes. Cooldowns actuels: {string.Join(", ", _spellCooldowns.Select(kvp => $"{kvp.Key}: {kvp.Value - Time.time:F2}s"))}");
            }
            else
            {
                Debug.LogWarning($"[GameplayManager] Pas assez d'or pour lancer le sort {spellData.DisplayName}. Requis : {spellData.GoldCost}, Actuel : {goldController.GetCurrentGold()}");
            }
        }
        else
        {
            Debug.LogWarning($"[GameplayManager] Le sort '{spellData.DisplayName}' n'a pas de SpellEffect assigné.");
        }
    }

    
    private void HandleCombatDetection(Unit attacker, Unit target, int damage)
    {
        // On vérifie que ce n'est pas un combat "interne" (allié vs allié si c'était possible)
        bool isCrossTeamCombat = (attacker is EnemyUnit && target is AllyUnit) || (attacker is AllyUnit && target is EnemyUnit);
        if (isCrossTeamCombat)
        {
            TriggerCombatState();
        }
    }

    // --- NOUVEAU HANDLER (Surcharge pour les bâtiments) ---
    private void HandleCombatDetection(Building target, Unit attacker)
    {
        // On vérifie que c'est bien un ennemi qui attaque un bâtiment joueur
        bool isCrossTeamCombat = (attacker is EnemyUnit && target is PlayerBuilding);
        if (isCrossTeamCombat)
        {
            TriggerCombatState();
        }
    }

    private void TriggerCombatState()
    {
        // On récupère le GameStateManager qui est dans la scène Core
        var gameStateManager = FindObjectOfType<GameStateManager>();
        if (gameStateManager == null) return;
        
        // On ne change l'état que si on est actuellement en "Exploration" on check avec le get rajouté pour acceder a l'état actuel
        if (gameStateManager.CurrentState == GameStateManager.GameState.Exploration)
        {
            Debug.Log("[GameplayManager] Combat détecté ! Passage à l'état de jeu 'Combat'.");
            gameStateManager.UpdateGameState(GameStateManager.GameState.Combat);
        }
    }
    
    private void OnDestroy()
    {
        // Se désabonner proprement pour éviter les erreurs
        if (sequenceController != null)
        {
            SequenceController.OnCharacterInvocationSequenceComplete -= HandleCharacterInvocation;
            SequenceController.OnGlobalSpellSequenceComplete -= HandleGlobalSpell;
        }
        Unit.OnUnitAttacked -= HandleCombatDetection;
        Building.OnBuildingAttackedByUnit -= HandleCombatDetection;
    }
}
