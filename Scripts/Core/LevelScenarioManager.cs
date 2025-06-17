using UnityEngine;
using Unity.Behavior.GraphFramework;
using Unity.Behavior;
using System;
using System.Collections;
using System.Collections.Generic;
using ScriptableObjects;
using Gameplay;

public class LevelScenarioManager : MonoBehaviour
{
    private LevelScenario_SO _currentScenario;
    private readonly Dictionary<ScenarioEvent, bool> _processedEvents = new Dictionary<ScenarioEvent, bool>();
    private float _levelStartTime;

    private class ObjectiveTracker
    {
        public int CurrentCount;
    }
    private readonly Dictionary<string, ObjectiveTracker> _destroyAllTrackers = new Dictionary<string, ObjectiveTracker>();

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
        Building.OnBuildingDestroyed += HandleTargetDestroyed;
        TriggerZone.OnZoneEntered += HandleZoneEntered;
        Building.OnBuildingTeamChangedGlobal += HandleBuildingTeamChanged;
    }

    private void UnsubscribeFromGameEvents()
    {
        Building.OnBuildingDestroyed -= HandleTargetDestroyed;
        TriggerZone.OnZoneEntered -= HandleZoneEntered;
        Building.OnBuildingTeamChangedGlobal -= HandleBuildingTeamChanged;
    }

    #region Handlers

    private void HandleBuildingTeamChanged(Building building, TeamType oldTeam, TeamType newTeam)
    {
        if (building != null && newTeam == TeamType.Player)
        {
            Debug.Log($"[LevelScenarioManager] Bâtiment '{building.name}' capturé par le joueur. Déclenchement des événements OnBuildingCaptured.");
            ProcessEvents(TriggerType.OnBuildingCaptured, building.name);
        }
    }

    private void HandleTargetDestroyed(Building destroyedBuilding)
    {
        if (destroyedBuilding == null) return;

        string destroyedName = destroyedBuilding.name;
        string destroyedTag = destroyedBuilding.tag;

        ProcessEvents(TriggerType.OnSpecificTargetDestroyed, destroyedName);

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

                if (trigger == TriggerType.OnZoneEnter ||
                    trigger == TriggerType.OnSpecificTargetDestroyed ||
                    trigger == TriggerType.OnAllTargetsWithTagDestroyed ||
                    trigger == TriggerType.OnBuildingCaptured)
                {
                    match = (evt.triggerParameter == param);
                }
                else
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
            // ----- MODIFICATION ICI -----
            case ActionType.ActivateSpawnerBuilding:
            case ActionType.DeactivateSpawnerBuilding:
                bool isActive = scenarioEvent.actionType == ActionType.ActivateSpawnerBuilding;
                SetSpawnerBuildingActive(scenarioEvent.actionParameter_GameObjectName, isActive);
                break;
            case ActionType.StartWave:
                CommandWaveToSpawner(scenarioEvent.actionParameter_GameObjectName, scenarioEvent.actionParameter_Wave);
                break;
            case ActionType.TriggerGameObject:
                 TriggerGameObjectByName(scenarioEvent.actionParameter_GameObjectName);
                break;
            // ---------------------------
            case ActionType.EndLevel:
                if (GameManager.Instance != null) GameManager.Instance.LoadHub();
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

    private void SetSpawnerBuildingActive(string spawnerName, bool isActive)
    {
        if (string.IsNullOrEmpty(spawnerName))
        {
            Debug.LogWarning($"[LevelScenarioManager] Aucun nom de spawner fourni pour SetSpawnerBuildingActive.", this);
            return;
        }

        GameObject spawnerGO = GameObject.Find(spawnerName);
        if (spawnerGO == null)
        {
            Debug.LogWarning($"[LevelScenarioManager] Aucun spawner avec le nom '{spawnerName}' trouvé.", this);
            return;
        }

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

    private void CommandWaveToSpawner(string spawnerName, Wave_SO wave)
    {
        if (wave == null || string.IsNullOrEmpty(spawnerName)) return;

        GameObject spawnerGO = GameObject.Find(spawnerName);
        if (spawnerGO == null)
        {
            Debug.LogWarning($"[LevelScenarioManager] Aucun spawner avec le nom '{spawnerName}' trouvé pour l'action StartWave.");
            return;
        }

        Debug.Log($"[LevelScenarioManager] Commande de la vague '{wave.waveName}' au spawner '{spawnerName}'.");

        var agent = spawnerGO.GetComponent<BehaviorGraphAgent>();
        if (agent != null && agent.BlackboardReference != null)
        {
            if (agent.BlackboardReference.GetVariable("CommandedWave", out BlackboardVariable<Wave_SO> bbCommandedWave))
            {
                bbCommandedWave.Value = wave;
                Debug.Log($"[LevelScenarioManager] Vague '{wave.waveName}' envoyée au blackboard du spawner '{spawnerGO.name}'.");
            }
        }
    }

    private void TriggerGameObjectByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning($"[LevelScenarioManager] L'action TriggerGameObject a été appelée avec un nom de GameObject vide.", this);
            return;
        }

        GameObject targetObject = GameObject.Find(name);

        if (targetObject == null)
        {
            Debug.LogError($"[LevelScenarioManager] Impossible de trouver le GameObject nommé '{name}' pour le déclencher.", this);
            return;
        }

        IScenarioTriggerable triggerable = targetObject.GetComponent<IScenarioTriggerable>();

        if (triggerable != null)
        {
            Debug.Log($"[LevelScenarioManager] Déclenchement de l'action sur '{name}'.", this);
            triggerable.TriggerAction();
        }
        else
        {
            Debug.LogWarning($"[LevelScenarioManager] Le GameObject '{name}' a été trouvé, mais il n'a pas de composant implémentant l'interface IScenarioTriggerable.", this);
        }
    }
}