// Fichier: Scripts/Scenarios/LevelScenario_SO.cs
using UnityEngine;
using System.Collections.Generic;

// --- Enums pour le système de scénario ---

/// <summary>
/// Définit les conditions qui peuvent déclencher un événement de scénario.
/// </summary>
public enum TriggerType
{
    OnLevelStart,
    OnZoneEnter,
    OnBossDied, // Nous allons utiliser OnBossSpawned pour l'instant, mais gardons ceci pour le futur
    OnWaveCleared,
    OnTimerElapsed,
    OnAllTargetsWithTagDestroyed, 
    OnSpecificTargetDestroyed     
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
    TriggerDefeat     
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
    
    [Tooltip("Paramètre optionnel pour le trigger. Ex: l'ID de la TriggerZone, ou le temps en secondes pour OnTimerElapsed.")]
    public string triggerParameter;

    [Header("Action Settings")]
    [Tooltip("L'action à exécuter lorsque l'événement est déclenché.")]
    public ActionType actionType;
    
    [Tooltip("Délai en secondes avant que l'action ne s'exécute après le déclenchement.")]
    public float delay;

    [Header("Action Parameters")]
    [Tooltip("La vague d'ennemis à lancer (si ActionType = StartWave).")]
    public Wave_SO actionParameter_Wave;
    
    [Tooltip("Le tag du GameObject du bâtiment spawner à activer/désactiver.")]
    public string actionParameter_BuildingTag;

    [Tooltip("Le résultat du niveau (true = Victoire, false = Défaite) si ActionType = EndLevel.")]
    public bool actionParameter_Victory;
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