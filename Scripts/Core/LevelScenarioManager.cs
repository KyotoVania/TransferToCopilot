using UnityEngine;
using Unity.Behavior.GraphFramework;
using Unity.Behavior;
using System;
using System.Collections;
using Unity.Properties;
using System.Collections.Generic;
public class LevelScenarioManager : MonoBehaviour
{
    private LevelScenario_SO _currentScenario;
    private readonly Dictionary<ScenarioEvent, bool> _processedEvents = new Dictionary<ScenarioEvent, bool>();
    private float _levelStartTime;

    public void Initialize(LevelScenario_SO scenario)
    {
        if (scenario == null)
        {
            Debug.LogWarning("[LevelScenarioManager] Aucun scénario fourni. Le manager sera inactif.", this);
            enabled = false;
            return;
        }
        _currentScenario = scenario;
        _processedEvents.Clear();
        Debug.Log($"[LevelScenarioManager] Initialisé avec le scénario: '{_currentScenario.scenarioName}'.", this);

        SubscribeToEvents();
        _levelStartTime = Time.time;
        ProcessEvents(TriggerType.OnLevelStart);
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
    
    private void Update()
    {
        if (_currentScenario == null) return;
        ProcessTimerEvents();
    }

    private void SubscribeToEvents()
    {
        // CORRIGÉ: S'abonne à OnBossSpawned, car OnBossDied n'existe pas.
        EnemyRegistry.OnBossSpawned += HandleBossEvent; 
        // CORRIGÉ: La signature du handler est maintenant correcte.
        EnemyRegistry.OnEnemyDied += HandleEnemyDied;
        // CORRIGÉ: S'abonne à l'événement de la nouvelle classe TriggerZone.
        TriggerZone.OnZoneEntered += HandleZoneEntered;
    }

    private void UnsubscribeFromEvents()
    {
        if (EnemyRegistry.Instance != null)
        {
            EnemyRegistry.OnBossSpawned -= HandleBossEvent;
            EnemyRegistry.OnEnemyDied -= HandleEnemyDied;
        }
        TriggerZone.OnZoneEntered -= HandleZoneEntered;
    }

    #region Handlers
    
    // CORRIGÉ: Le handler accepte maintenant un EnemyUnit.
    private void HandleBossEvent(EnemyUnit bossUnit)
    {
        // Note : L'événement s'appelle OnBossSpawned. Si vous voulez un trigger à la mort,
        // il faudra créer un événement OnBossDied dans EnemyRegistry.
        // Pour l'instant, on se base sur ce qui existe.
        Debug.Log("[LevelScenarioManager] Trigger détecté: OnBossDied (via OnBossSpawned).", this);
        ProcessEvents(TriggerType.OnBossDied);
    }

    // CORRIGÉ: Le handler accepte un EnemyUnit.
    private void HandleEnemyDied(EnemyUnit deadUnit)
    {
        // TODO: Implémenter la logique pour vérifier si une vague est terminée.
        // Cela nécessite que les unités sachent à quelle vague elles appartiennent.
    }

    private void HandleZoneEntered(string zoneID)
    {
        Debug.Log($"[LevelScenarioManager] Trigger détecté: OnZoneEnter pour la zone ID: {zoneID}.", this);
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
                bool match = (evt.triggerType != TriggerType.OnZoneEnter) || (evt.triggerParameter == param);
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
                GameManager.Instance.LoadHub();
                break;
            case ActionType.StartWave:
                //log :
                    Debug.Log($"[LevelScenarioManager] Démarrage de la vague: {scenarioEvent.actionParameter_Wave?.waveName ?? "Aucune vague spécifiée"}", this);
                 // TODO: Implémenter la logique de spawn de vague. 
                 // Cela pourrait impliquer de trouver des spawners et de leur passer le Wave_SO.
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
            // CORRIGÉ: Utilise .BlackboardReference au lieu de .blackboard
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
}