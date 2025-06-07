// Fichier: Scripts/Gameplay/TriggerZone.cs
using UnityEngine;
using System;

[RequireComponent(typeof(Collider))]
public class TriggerZone : MonoBehaviour
{
    [Tooltip("ID unique pour cette zone, utilisé par le LevelScenarioManager.")]
    public string ZoneID;

    public static event Action<string> OnZoneEntered;

    private bool _hasBeenTriggered = false;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[TriggerZone] Le collider sur {gameObject.name} n'est pas réglé sur 'Is Trigger'. Correction automatique.", this);
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasBeenTriggered) return;

        // Vérifier si l'objet entrant est une unité du joueur (ajuster le tag si nécessaire)
        if (other.CompareTag("PlayerUnit")) // Assurez-vous que vos unités alliées ont ce tag
        {
            _hasBeenTriggered = true;
            Debug.Log($"[TriggerZone] L'unité joueur '{other.name}' est entrée dans la zone '{ZoneID}'.", this);
            OnZoneEntered?.Invoke(ZoneID);
        }
    }
}