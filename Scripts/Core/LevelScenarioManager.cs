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
        
        // S'abonner aux événements de mort des boss
        SubscribeToBossDeathEvents();
    }

    private void UnsubscribeFromGameEvents()
    {
        Building.OnBuildingDestroyed -= HandleTargetDestroyed;
        TriggerZone.OnZoneEntered -= HandleZoneEntered;
        Building.OnBuildingTeamChangedGlobal -= HandleBuildingTeamChanged;
        
        // Se désabonner des événements de mort des boss
        UnsubscribeFromBossDeathEvents();
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

    private void HandleBossDeath()
    {
        // Cette méthode sera appelée par l'événement OnUnitDestroyed de chaque boss individuellement
        // On ne peut pas identifier quel boss spécifique car l'événement ne passe pas de paramètre
        Debug.Log("[LevelScenarioManager] Un boss est mort. Déclenchement des événements OnBossDied.");
        ProcessEvents(TriggerType.OnBossDied, "Boss"); // Utiliser un nom générique pour tous les boss
    }

    #endregion

    #region Boss Death Event Management

    private void SubscribeToBossDeathEvents()
    {
        // Trouver tous les boss dans la scène et s'abonner à leur événement de mort
        BossUnit[] bosses = FindObjectsByType<BossUnit>(FindObjectsSortMode.None);
        foreach (BossUnit boss in bosses)
        {
            if (boss != null)
            {
                boss.OnUnitDestroyed += HandleBossDeath;
                Debug.Log($"[LevelScenarioManager] Abonné aux événements de mort du boss '{boss.name}'.");
            }
        }
    }

    private void UnsubscribeFromBossDeathEvents()
    {
        // Trouver tous les boss dans la scène et se désabonner de leur événement de mort
        BossUnit[] bosses = FindObjectsByType<BossUnit>(FindObjectsSortMode.None);
        foreach (BossUnit boss in bosses)
        {
            if (boss != null)
            {
                boss.OnUnitDestroyed -= HandleBossDeath;
            }
        }
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
                    trigger == TriggerType.OnBuildingCaptured ||
                    trigger == TriggerType.OnBossDied)
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

    private void ExecuteSpawnPrefabAction(List<SpawnActionData> spawnDataList)
    {
        if (spawnDataList == null || spawnDataList.Count == 0)
        {
            Debug.LogWarning("[LevelScenarioManager] Aucune donnée de spawn fournie pour l'action SpawnPrefabAtLocation.", this);
            return;
        }

        foreach (var spawnData in spawnDataList)
        {
            if (spawnData.prefabToSpawn == null)
            {
                Debug.LogWarning("[LevelScenarioManager] Un prefab à spawner est null dans la liste SpawnActionData.", this);
                continue;
            }

            try
            {
                GameObject spawnedObject = Instantiate(spawnData.prefabToSpawn, spawnData.spawnPosition, Quaternion.Euler(spawnData.spawnRotation));
                
                Debug.Log($"[LevelScenarioManager] Objet '{spawnData.prefabToSpawn.name}' spawné à la position {spawnData.spawnPosition}.");

                EnemyUnit enemyUnit = spawnedObject.GetComponent<EnemyUnit>();
                if (enemyUnit != null)
                {
                    if (EnemyRegistry.Instance != null)
                    {
                        EnemyRegistry.Instance.Register(enemyUnit);
                        Debug.Log($"[LevelScenarioManager] Unité ennemie '{enemyUnit.name}' enregistrée auprès du EnemyRegistry.");
                    }
                    else
                    {
                        Debug.LogWarning("[LevelScenarioManager] EnemyRegistry.Instance est null. L'unité ennemie ne peut pas être enregistrée.");
                    }
                }

                BossUnit bossUnit = spawnedObject.GetComponent<BossUnit>();
                if (bossUnit != null)
                {
                    bossUnit.OnUnitDestroyed += HandleBossDeath;
                    Debug.Log($"[LevelScenarioManager] Boss '{bossUnit.name}' enregistré pour les événements de mort.");
                    
                    AnimateBossSpawn(spawnedObject);
                }
                else
                {
                    Building building = spawnedObject.GetComponent<Building>();
                    if (building != null)
                    {
                        AnimateBuildingSpawn(spawnedObject);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LevelScenarioManager] Erreur lors du spawn de '{spawnData.prefabToSpawn.name}': {ex.Message}", this);
            }
        }
    }

    private void AnimateBossSpawn(GameObject bossObject)
    {
        Vector3 originalPosition = bossObject.transform.position;
        Vector3 airPosition = originalPosition + Vector3.up * 15f;
        
        bossObject.transform.position = airPosition;
        
        if (bossObject.GetComponent<Rigidbody>() != null)
        {
            Debug.Log($"[LevelScenarioManager] Boss '{bossObject.name}' fait une entrée dramatique depuis les airs!");
        }
        else
        {
            StartCoroutine(AnimateBossFall(bossObject, airPosition, originalPosition));
        }
    }

    private System.Collections.IEnumerator AnimateBossFall(GameObject bossObject, Vector3 startPos, Vector3 endPos)
    {
        float duration = 2f;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            t = 1f - (1f - t) * (1f - t);
            
            bossObject.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        
        bossObject.transform.position = endPos;
        Debug.Log($"[LevelScenarioManager] Boss '{bossObject.name}' a atterri!");
    }

    private void AnimateBuildingSpawn(GameObject buildingObject)
    {
        Vector3 originalScale = buildingObject.transform.localScale;
        buildingObject.transform.localScale = new Vector3(originalScale.x, 0f, originalScale.z);
        
        StartCoroutine(AnimateBuildingRise(buildingObject, originalScale));
    }

    private System.Collections.IEnumerator AnimateBuildingRise(GameObject buildingObject, Vector3 targetScale)
    {
        float duration = 1.5f;
        float elapsedTime = 0f;
        Vector3 startScale = buildingObject.transform.localScale;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            t = t * t * (3f - 2f * t);
            
            buildingObject.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        
        buildingObject.transform.localScale = targetScale;
        Debug.Log($"[LevelScenarioManager] Bâtiment '{buildingObject.name}' a émergé du sol!");
    }
}