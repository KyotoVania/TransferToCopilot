using UnityEngine;
using MoreMountains.Feedbacks;
using System.Collections;

/// <summary>
/// Gère les feedbacks de spawn simplifié - CHARGE SEULEMENT
/// </summary>
public class UnitSpawnFeedback : MonoBehaviour
{
    [Header("Feel Integration")]
    [Tooltip("MMF_Player pour la phase Charge")]
    public MMF_Player ChargeFeedbacks;

    [Header("Timing Configuration")]
    [Tooltip("Durée de la phase Charge")]
    public float ChargeDuration = 0.3f;

    [Header("Spawn Configuration")]
    [Tooltip("Délai avant de commencer la séquence")]
    public float DelayBeforeSpawn = 0f;
    
    // --- MODIFICATION : Texte du Tooltip mis à jour ---
    [Tooltip("Synchroniser avec le beat du MusicManager")]
    public bool SyncWithRhythm = false;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private bool hasPlayedSpawnFeedback = false;
    private Coroutine spawnSequenceCoroutine;
    private Unit _unit; // Référence au composant Unit
    public System.Action OnSpawnCompleted;

    /// <summary>
    /// Déclenche la séquence de spawn
    /// </summary>
    private IEnumerator ExecuteSpawnSequence()
    {
        if (_unit == null)
        {
            _unit = GetComponent<Unit>();
            if (_unit == null && enableDebugLogs)
            {
                Debug.LogWarning($"[UnitSpawnFeedback] Pas de composant Unit trouvé sur {gameObject.name}");
            }
        }

        if (_unit != null)
        {
            _unit.SetSpawningState(true);
        }
        
        hasPlayedSpawnFeedback = true;

        if (enableDebugLogs)
            Debug.Log($"[UnitSpawnFeedback] Début séquence spawn pour {gameObject.name}. _unit.IsSpawning: {(_unit != null ? _unit.IsSpawning.ToString() : "N/A")}");

        // PHASE UNIQUE: CHARGE
        if (ChargeFeedbacks != null)
        {
            if (enableDebugLogs) Debug.Log($"[UnitSpawnFeedback] Phase CHARGE ({ChargeDuration:F2}s)");
            ChargeFeedbacks.PlayFeedbacks();
        }
        yield return new WaitForSeconds(ChargeDuration);

        if (enableDebugLogs)
            Debug.Log($"[UnitSpawnFeedback] Séquence terminée pour {gameObject.name}");

        if (_unit != null)
        {
            _unit.SetSpawningState(false);
            if (enableDebugLogs) Debug.Log($"[UnitSpawnFeedback] {gameObject.name} _unit.IsSpawning set to false.");
        }

        // 🔥 NOUVEAU: Notifier AllyUnit que c'est terminé
        OnSpawnCompleted?.Invoke();
        
        spawnSequenceCoroutine = null;
    }

    /// <summary>
    /// Déclenche la séquence de feedback de spawn (public pour AllyUnit)
    /// </summary>
    public void PlaySpawnFeedback()
    {
        if (hasPlayedSpawnFeedback)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[UnitSpawnFeedback] Séquence déjà jouée pour {gameObject.name}");
            return;
        }

        if (spawnSequenceCoroutine != null)
        {
            StopCoroutine(spawnSequenceCoroutine);
        }
        spawnSequenceCoroutine = StartCoroutine(ExecuteSpawnSequence());
    }

    /// <summary>
    /// Arrête la séquence en cours
    /// </summary>
    public void StopSpawnFeedback()
    {
        if (spawnSequenceCoroutine != null)
        {
            StopCoroutine(spawnSequenceCoroutine);
            spawnSequenceCoroutine = null;
        }

        ChargeFeedbacks?.StopFeedbacks();
    }
    private void OnDestroy()
    {
        StopSpawnFeedback();
        OnSpawnCompleted = null; // Cleanup des événements
    }
    /// <summary>
    /// Remet à zéro pour réutilisation
    /// </summary>
    public void ResetForReuse()
    {
        hasPlayedSpawnFeedback = false;
        StopSpawnFeedback();
    }

    /// <summary>
    /// 🔥 NOUVELLE MÉTHODE : Lier manuellement le composant Unit
    /// </summary>
    public void SetUnit(Unit unit)
    {
        _unit = unit;
        if (enableDebugLogs)
            Debug.Log($"[UnitSpawnFeedback] Unit lié manuellement: {(_unit != null ? _unit.name : "null")}");
    }

    private void Awake()
    {
        // 🔥 CORRECTION : Essayer de récupérer Unit automatiquement
        if (_unit == null)
        {
            _unit = GetComponent<Unit>();
            if (enableDebugLogs)
            {
                Debug.Log($"[UnitSpawnFeedback] Unit auto-détecté: {(_unit != null ? "✅ Trouvé" : "❌ Pas trouvé")}");
            }
        }

        // Auto-setup du MMF_Player si pas assigné
        SetupMMFPlayer();
    }

    private void SetupMMFPlayer()
    {
        if (ChargeFeedbacks == null)
        {
            var mmfPlayer = GetComponent<MMF_Player>();
            if (mmfPlayer != null)
            {
                ChargeFeedbacks = mmfPlayer;
                if (enableDebugLogs)
                    Debug.Log($"[UnitSpawnFeedback] MMF_Player auto-assigné: {mmfPlayer.name}");
            }
        }
    }   
}
