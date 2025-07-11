// Fichier: Scripts/Scenarios/LevelScenario_SO.cs
using UnityEngine;
using System.Collections.Generic;
using ScriptableObjects;

// --- Enums pour le système de scénario ---

/// <summary>
/// Définit les conditions qui peuvent déclencher un événement de scénario.
/// </summary>
public enum TriggerType
{
    OnLevelStart,
    OnZoneEnter,
    OnBossDied,
    OnWaveCleared,
    OnTimerElapsed,
    OnAllTargetsWithTagDestroyed,
    OnSpecificTargetDestroyed,
    OnBuildingCaptured
}

/// <summary>
/// Définit les actions que le scénario peut exécuter.
/// </summary>
public enum ActionType
{
    StartWave,
    ActivateSpawnerBuilding,
    DeactivateSpawnerBuilding,
    EndLevel,
    TriggerVictory,
    TriggerDefeat,
    TriggerGameObject,
    ShowBossWarningBanner,
    SpawnPrefabAtLocation
}

// --- Classes sérialisables pour les données d'action ---

/// <summary>
/// Contient les données nécessaires pour faire apparaître un préfabriqué à une position spécifique.
/// </summary>
[System.Serializable]
public class SpawnActionData
{
    [Tooltip("Le préfabriqué à faire apparaître (unité ou bâtiment).")]
    public GameObject prefabToSpawn;
    
    [Tooltip("La position où faire apparaître le préfabriqué.")]
    public Vector3 spawnPosition;
    
    [Tooltip("La rotation du préfabriqué (angles d'Euler).")]
    public Vector3 spawnRotation;
}

// --- Classe sérialisable pour un événement ---

[System.Serializable]
public class ScenarioEvent
{
    [Tooltip("Nom de l'événement pour l'organisation (ex: 'Première Vague d'Assaut').")]
    public string eventName;

    [Header("Trigger Settings")]
    [Tooltip("La condition qui déclenchera cet événement.")]
    public TriggerType triggerType;

    [Tooltip("Paramètre pour le trigger. Ex: l'ID de la TriggerZone, le nom du GameObject pour OnSpecificTargetDestroyed ou OnBuildingCaptured, ou le Tag pour OnAllTargetsWithTagDestroyed.")]
    public string triggerParameter;

    [Header("Action Settings")]
    [Tooltip("L'action à exécuter lorsque l'événement est déclenché.")]
    public ActionType actionType;

    [Tooltip("Délai en secondes avant que l'action ne s'exécute après le déclenchement.")]
    public float delay;

    [Header("Action Parameters")]
    [Tooltip("La vague d'ennemis à lancer (si ActionType = StartWave).")]
    public Wave_SO actionParameter_Wave;

    [Tooltip("Le résultat du niveau (true = Victoire, false = Défaite) si ActionType = EndLevel.")]
    public bool actionParameter_Victory;

    // ----- MODIFICATION ICI -----
    [Tooltip("Le nom EXACT du GameObject cible pour les actions comme TriggerGameObject, StartWave, ActivateSpawnerBuilding, etc.")]
    public string actionParameter_GameObjectName;
    
    [Tooltip("Liste des objets à faire apparaître (si ActionType = SpawnPrefabAtLocation).")]
    public List<SpawnActionData> actionParameter_SpawnData = new List<SpawnActionData>();
    // ---------------------------
}

// --- Définition du ScriptableObject ---

[CreateAssetMenu(fileName = "LevelScenario_New", menuName = "KyotoVania/Level Scenario")]
public class LevelScenario_SO : ScriptableObject
{
    [Tooltip("Nom descriptif de ce scénario pour une identification facile.")]
    public string scenarioName;

    [Tooltip("La séquence d'événements qui constitue ce scénario.")]
    public List<ScenarioEvent> events = new List<ScenarioEvent>();
}