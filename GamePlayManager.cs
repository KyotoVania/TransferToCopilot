namespace Gameplay
{
    using UnityEngine;
    using System.Collections.Generic;
    using System;
    using System.Linq;
    using ScriptableObjects;

    public class GameplayManager : MonoBehaviour
    {
        private LevelData_SO currentLevelData;
        private TeamManager teamManager;
        private SequenceController sequenceController;
        private LevelScenarioManager scenarioManager; 
        
        private GlobalSpellManager globalSpellManager;
        private UnitSpawner unitSpawner;
        [Header("Configuration")]
        [SerializeField] private string globalSpellsResourcePath = "Data/GlobalSpells";
        
        private List<GlobalSpellData_SO> availableGlobalSpells = new List<GlobalSpellData_SO>();
        private Dictionary<string, float> _spellCooldowns = new Dictionary<string, float>();
        
        // Les cooldowns des unités sont maintenant gérés par UnitSpawner
        public IReadOnlyDictionary<string, float> UnitCooldowns => unitSpawner?.UnitCooldowns;
        public IReadOnlyDictionary<string, float> SpellCooldowns => globalSpellManager?.SpellCooldowns;
    
        private float _beatInterval;

        void Start()
        {
            // Récupération des instances
            teamManager = TeamManager.Instance;
            sequenceController = FindFirstObjectByType<SequenceController>();
            scenarioManager = FindFirstObjectByType<LevelScenarioManager>(); 
            unitSpawner = FindFirstObjectByType<UnitSpawner>();
            globalSpellManager = FindFirstObjectByType<GlobalSpellManager>();

            // Validation des dépendances critiques
            if (GameManager.Instance == null || GameManager.CurrentLevelToLoad == null)
            {
                Debug.LogError("[GameplayManager] GameManager ou CurrentLevelToLoad est null! Impossible d'initialiser le niveau.", this);
                enabled = false;
                return;
            }
            else
            {
                currentLevelData = GameManager.CurrentLevelToLoad;
            }
            
            if (teamManager == null || sequenceController == null || unitSpawner == null) // Ajout de unitSpawner
            {
                Debug.LogError("[GameplayManager] Un ou plusieurs managers/contrôleurs critiques (TeamManager, SequenceController, UnitSpawner) sont introuvables!", this);
                enabled = false;
                return;
            }
            InitializeLevel();
        }

        void InitializeLevel()
        {
            Debug.Log($"[GameplayManager] Initialisation du niveau : {currentLevelData.DisplayName}");
            ConfigureAudioAndRhythm();
            globalSpellManager.LoadSpells();
            InitializeSequenceController();
            SubscribeToSequenceEvents();
            Unit.OnUnitAttacked += HandleCombatDetection;
            Building.OnBuildingAttackedByUnit += HandleCombatDetection;
            
            if (scenarioManager != null && currentLevelData.scenario != null)
            {
                scenarioManager.Initialize(currentLevelData.scenario);
            }
            else if (scenarioManager != null)
            {
                Debug.LogWarning($"[GameplayManager] Le niveau '{currentLevelData.DisplayName}' n'a pas de LevelScenario_SO assigné.");
            }
        }

        void ConfigureAudioAndRhythm()
        {
            if (MusicManager.Instance != null && currentLevelData.RhythmBPM > 0)
            {
                MusicManager.Instance.SetBPM(currentLevelData.RhythmBPM);
                _beatInterval = MusicManager.Instance.GetBeatDuration();
            }

            if (MusicManager.Instance != null)
            {
                if (currentLevelData.MusicStateSwitch != null && currentLevelData.MusicStateSwitch.IsValid())
                {
                    MusicManager.Instance.SetMusicState(currentLevelData.MusicStateSwitch.Name);
                }
                else if (currentLevelData.BackgroundMusic != null && currentLevelData.BackgroundMusic.IsValid())
                {
                    currentLevelData.BackgroundMusic.Post(MusicManager.Instance.gameObject);
                }
                else
                {
                    MusicManager.Instance.SetMusicState("Exploration");
                }
            }
        }
        
        void InitializeSequenceController()
        {
            if (teamManager == null || sequenceController == null) return;

            List<CharacterData_SO> activeTeam = teamManager.ActiveTeam;
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
            SequenceController.OnCharacterInvocationSequenceComplete -= HandleCharacterInvocation;
            SequenceController.OnGlobalSpellSequenceComplete -= HandleGlobalSpell;

            SequenceController.OnCharacterInvocationSequenceComplete += HandleCharacterInvocation;
            SequenceController.OnGlobalSpellSequenceComplete += HandleGlobalSpell;
        }

        /// <summary>
        /// Méthode déléguée qui reçoit l'événement d'invocation.
        /// Appelle maintenant le UnitSpawner pour gérer la logique.
        /// </summary>
        public void HandleCharacterInvocation(CharacterData_SO characterData, int perfectCount)
        {
            Debug.Log($"[GameplayManager] Tentative d'invocation reçue pour {characterData.DisplayName}. Délégation à UnitSpawner.");
            // On délègue TOUTE la logique au spawner.
            unitSpawner.TrySpawnUnit(characterData, perfectCount);
        }

        /// <summary>
        /// Délègue l'exécution du sort au GlobalSpellManager.
        /// </summary>
        void HandleGlobalSpell(GlobalSpellData_SO spellData, int perfectCount)
        {
            Debug.Log($"[GameplayManager] Tentative de sort reçue pour {spellData.DisplayName}. Délégation à GlobalSpellManager.");
            // On délègue TOUTE la logique au manager de sorts.
            globalSpellManager.TryExecuteSpell(spellData, perfectCount);
        }
        
        private void HandleCombatDetection(Unit attacker, Unit target, int damage)
        {
            bool isCrossTeamCombat = (attacker is EnemyUnit && target is AllyUnit) || (attacker is AllyUnit && target is EnemyUnit);
            if (isCrossTeamCombat) TriggerCombatState();
        }

        private void HandleCombatDetection(Building target, Unit attacker)
        {
            bool isCrossTeamCombat = (attacker is EnemyUnit && target is PlayerBuilding);
            if (isCrossTeamCombat) TriggerCombatState();
        }

        private void TriggerCombatState()
        {
            var gameStateManager = FindObjectOfType<GameStateManager>();
            if (gameStateManager != null && gameStateManager.CurrentState == GameStateManager.GameState.Exploration)
            {
                gameStateManager.UpdateGameState(GameStateManager.GameState.Combat);
            }
        }
        
        private void OnDestroy()
        {
            if (sequenceController != null)
            {
                SequenceController.OnCharacterInvocationSequenceComplete -= HandleCharacterInvocation;
                SequenceController.OnGlobalSpellSequenceComplete -= HandleGlobalSpell;
            }
            Unit.OnUnitAttacked -= HandleCombatDetection;
            Building.OnBuildingAttackedByUnit -= HandleCombatDetection;
        }
    }
}