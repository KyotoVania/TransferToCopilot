using UnityEngine;
using System.Collections.Generic;
using System;
using ScriptableObjects;


public class EnemyRegistry : MonoBehaviour
{
    public static EnemyRegistry Instance { get; private set; }

    // Événement statique pour l'apparition d'un boss
    public static event Action<EnemyUnit> OnBossSpawned;

    // Événements pour les unités classiques si besoin
    public static event Action<EnemyUnit> OnEnemySpawned;
    public static event Action<EnemyUnit> OnEnemyDied;

    private List<EnemyUnit> _activeEnemies = new List<EnemyUnit>();
    private EnemyUnit _activeBoss = null;

    // Propriétés publiques pour l'accès en lecture seule
    public IReadOnlyList<EnemyUnit> ActiveEnemies => _activeEnemies;
    public EnemyUnit ActiveBoss => _activeBoss;
    public bool IsBossActive => _activeBoss != null;
    
    
    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Enregistre une unité ennemie lors de son spawn.
    /// </summary>
    public void Register(EnemyUnit enemy)
    {
        if (enemy == null || _activeEnemies.Contains(enemy)) return;

        _activeEnemies.Add(enemy);
        OnEnemySpawned?.Invoke(enemy); // Notifier qu'un ennemi a spawn

        // Vérification spécifique pour le boss
        if (enemy.GetUnitType() == UnitType.Boss)
        {
            if (_activeBoss == null)
            {
                _activeBoss = enemy;
                Debug.Log($"[EnemyRegistry] Boss '{enemy.name}' a été enregistré ! Déclenchement de OnBossSpawned.");
                OnBossSpawned?.Invoke(enemy); // C'est ici que la magie opère
            }
            else
            {
                Debug.LogWarning($"[EnemyRegistry] Un nouveau boss '{enemy.name}' tente de s'enregistrer alors qu'un boss ('{_activeBoss.name}') est déjà actif !");
            }
        }
    }

    /// <summary>
    /// Désenregistre une unité ennemie lors de sa mort.
    /// </summary>
    public void Unregister(EnemyUnit enemy)
    {
        if (enemy == null || !_activeEnemies.Contains(enemy)) return;

        _activeEnemies.Remove(enemy);
        OnEnemyDied?.Invoke(enemy); // Notifier qu'un ennemi est mort

        if (_activeBoss == enemy)
        {
            _activeBoss = null;
            Debug.Log($"[EnemyRegistry] Le Boss '{enemy.name}' a été désenregistré (probablement mort).");
            // On pourrait avoir un événement OnBossDied ici
        }
    }
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}