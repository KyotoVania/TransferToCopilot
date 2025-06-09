
using UnityEngine;
using Unity.Behavior.GraphFramework;
using Unity.Behavior;
using System;
using System.Collections;
using System.Collections.Generic;

public class LevelScenarioManager : MonoBehaviour
{
    private LevelScenario_SO _currentScenario;
    private readonly Dictionary<ScenarioEvent, bool> _processedEvents = new Dictionary<ScenarioEvent, bool>();
    private float _levelStartTime;

    // Structure de suivi simplifiée pour les objectifs "Détruire tout"
    private class ObjectiveTracker
    {
        public int CurrentCount;
    }
    private readonly Dictionary<string, ObjectiveTracker> _destroyAllTrackers = new Dictionary<string, ObjectiveTracker>();

    // Plus besoin de _protectTags, la logique est gérée par ProcessEvents

    public void Initialize(LevelScenario_SO scenario)
    {
        if (scenario == null) {
            Debug.LogWarning("[LevelScenarioManager] Aucun scénario fourni.", this);
            enabled = false;
            return;
        }
        _currentScenario = scenario;
        _processedEvents.Clear();
        _destroyAllTrackers.Clear();

        Debug.Log($"[LevelScenarioManager] Initialisé avec le scénario: '{_currentScenario.scenarioName}'.", this);

        ParseScenarioForObjectives();
        SubscribeToGameEvents();
        _levelStartTime = Time.time;
        ProcessEvents(TriggerType.OnLevelStart);
    }

    private void OnDestroy()
    {
        UnsubscribeFromGameEvents();
    }
    
    private void Update()
    {
        if (_currentScenario != null) ProcessTimerEvents();
    }

    private void ParseScenarioForObjectives()
    {
        foreach (var evt in _currentScenario.events)
        {
            // On ne configure que les trackers pour les objectifs "Détruire TOUT"
            if (evt.triggerType == TriggerType.OnAllTargetsWithTagDestroyed)
            {
                string tag = evt.triggerParameter;
                if (string.IsNullOrEmpty(tag)) continue;

                if (!_destroyAllTrackers.ContainsKey(tag))
                {
                    int initialCount = GameObject.FindGameObjectsWithTag(tag).Length;
                    _destroyAllTrackers[tag] = new ObjectiveTracker { CurrentCount = initialCount };
                    Debug.Log($"[LevelScenarioManager] OBJECTIF SUIVI : Détruire toutes les cibles avec le tag '{tag}'. Nombre initial : {initialCount}.");
                }
            }
        }
    }

    private void SubscribeToGameEvents()
    {
        // On s'abonne à tous les événements potentiellement utiles
        Building.OnBuildingDestroyed += HandleTargetDestroyed;
        // Si vous voulez aussi traquer la mort d'unités:
        // EnemyRegistry.OnEnemyDied += HandleEnemyDestroyed;
        TriggerZone.OnZoneEntered += HandleZoneEntered;
        // etc.
    }

    private void UnsubscribeFromGameEvents()
    {
        Building.OnBuildingDestroyed -= HandleTargetDestroyed;
        TriggerZone.OnZoneEntered -= HandleZoneEntered;
    }

    #region Handlers
    
    private void HandleTargetDestroyed(Building destroyedBuilding)
    {
        if (destroyedBuilding == null) return;
        string destroyedTag = destroyedBuilding.tag;

        // 1. Déclencher les événements "OnSpecificTargetDestroyed"
        ProcessEvents(TriggerType.OnSpecificTargetDestroyed, destroyedTag);

        // 2. Mettre à jour les objectifs "OnAllTargetsWithTagDestroyed"
        if (_destroyAllTrackers.ContainsKey(destroyedTag))
        {
            var tracker = _destroyAllTrackers[destroyedTag];
            tracker.CurrentCount--;
            Debug.Log($"[LevelScenarioManager] Cible avec tag '{destroyedTag}' détruite. Restant: {tracker.CurrentCount}.");

            if (tracker.CurrentCount <= 0)
            {
                ProcessEvents(TriggerType.OnAllTargetsWithTagDestroyed, destroyedTag);
            }
        }
    }

    private void HandleZoneEntered(string zoneID)
    {
        ProcessEvents(TriggerType.OnZoneEnter, zoneID);
    }

    #endregion

    private void ProcessEvents(TriggerType trigger, string param = "")
    {
        if (_currentScenario == null) return;

        foreach (var evt in _currentScenario.events)
        {
            if (evt.triggerType == trigger && !_processedEvents.ContainsKey(evt))
            {
                bool match = false;
                // Pour ces triggers, le paramètre DOIT correspondre.
                if (trigger == TriggerType.OnZoneEnter || 
                    trigger == TriggerType.OnSpecificTargetDestroyed || 
                    trigger == TriggerType.OnAllTargetsWithTagDestroyed)
                {
                    match = (evt.triggerParameter == param);
                }
                else // Pour les triggers sans paramètre (OnLevelStart, OnTimerElapsed, etc.)
                {
                    match = true;
                }

                if (match)
                {
                    _processedEvents.Add(evt, true);
                    StartCoroutine(ExecuteActionWithDelay(evt));
                }
            }
        }
    }
    
    private void ProcessTimerEvents()
    {
        if (_currentScenario == null) return;
        float elapsedTime = Time.time - _levelStartTime;
        foreach (var evt in _currentScenario.events)
        {
            if (evt.triggerType == TriggerType.OnTimerElapsed && !_processedEvents.ContainsKey(evt))
            {
                if (float.TryParse(evt.triggerParameter, out float triggerTime) && elapsedTime >= triggerTime)
                {
                    _processedEvents.Add(evt, true);
                    StartCoroutine(ExecuteActionWithDelay(evt));
                }
            }
        }
    }

    private IEnumerator ExecuteActionWithDelay(ScenarioEvent scenarioEvent)
    {
        yield return new WaitForSeconds(scenarioEvent.delay);
        
        Debug.Log($"[LevelScenarioManager] Exécution de l'action '{scenarioEvent.actionType}' pour '{scenarioEvent.eventName}'.", this);
        
        switch (scenarioEvent.actionType)
        {
            case ActionType.ActivateSpawnerBuilding:
            case ActionType.DeactivateSpawnerBuilding:
                bool isActive = scenarioEvent.actionType == ActionType.ActivateSpawnerBuilding;
                SetSpawnerBuildingActive(scenarioEvent.actionParameter_BuildingTag, isActive);
                break;
            case ActionType.EndLevel:
                if (GameManager.Instance != null) GameManager.Instance.LoadHub();
                break;
            case ActionType.StartWave:
                CommandWaveToSpawners(scenarioEvent.actionParameter_BuildingTag, scenarioEvent.actionParameter_Wave);
                break;
            case ActionType.TriggerVictory:
                if (WinLoseController.Instance != null)
                    WinLoseController.Instance.TriggerWinCondition();
                else
                    Debug.LogError("[LevelScenarioManager] WinLoseController.Instance non trouvé pour TriggerVictory !");
                break;
            case ActionType.TriggerDefeat:
                if (WinLoseController.Instance != null)
                    WinLoseController.Instance.TriggerLoseCondition();
                else
                    Debug.LogError("[LevelScenarioManager] WinLoseController.Instance non trouvé pour TriggerDefeat !");
                break;
        }
    }


    private void SetSpawnerBuildingActive(string tag, bool isActive)
    {
        GameObject[] spawners = GameObject.FindGameObjectsWithTag(tag);
        if (spawners.Length == 0)
        {
            Debug.LogWarning($"[LevelScenarioManager] Aucun spawner avec le tag '{tag}' trouvé.", this);
            return;
        }

        foreach (var spawnerGO in spawners)
        {
            var agent = spawnerGO.GetComponent<BehaviorGraphAgent>();
            if (agent != null && agent.BlackboardReference != null)
            {
                if (agent.BlackboardReference.GetVariable("IsActive", out BlackboardVariable<bool> bbIsActive))
                {
                    bbIsActive.Value = isActive;
                    Debug.Log($"[LevelScenarioManager] Bâtiment spawner '{spawnerGO.name}' mis à IsActive = {isActive}.", this);
                }
            }
        }
    }
    private void CommandWaveToSpawners(string tag, Wave_SO wave)
    {
        if (wave == null || string.IsNullOrEmpty(tag)) return;
        GameObject[] spawners = GameObject.FindGameObjectsWithTag(tag);
        if (spawners.Length == 0)
        {
            Debug.LogWarning($"[LevelScenarioManager] No spawner with tag '{tag}' found for StartWave action.");
            return;
        }

        Debug.Log($"[LevelScenarioManager] Commanding wave '{wave.waveName}' to {spawners.Length} spawner(s) with tag '{tag}'.");

        foreach (var spawnerGO in spawners)
        {
            var agent = spawnerGO.GetComponent<BehaviorGraphAgent>();
            if (agent != null && agent.BlackboardReference != null)
            {
                // This assumes your BT has a "CommandedWave" variable of type Wave_SO.
                if (agent.BlackboardReference.GetVariable("CommandedWave", out BlackboardVariable<Wave_SO> bbCommandedWave))
                {
                    bbCommandedWave.Value = wave;
                    Debug.Log($"[LevelScenarioManager] Sent wave '{wave.waveName}' to spawner '{spawnerGO.name}' blackboard.");
                }
            }
        }
    }
}