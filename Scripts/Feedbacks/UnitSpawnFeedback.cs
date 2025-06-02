using UnityEngine;
using MoreMountains.Feedbacks;

/// <summary>
/// Gère les feedbacks visuels et audio lors du spawn d'une unité
/// Utilise l'asset Feel de More Mountains pour créer des effets immersifs
/// </summary>
public class UnitSpawnFeedback : MonoBehaviour
{
    [Header("Feel Integration")]
    [Tooltip("Le MMF_Player qui contient tous les feedbacks de spawn")]
    public MMF_Player SpawnFeedbacksPlayer;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    /// <summary>
    /// Déclenche les feedbacks de spawn. À appeler depuis Unit.Start() après initialisation
    /// </summary>
    public void PlaySpawnFeedback()
    {
        if (SpawnFeedbacksPlayer != null)
        {
            if (enableDebugLogs)
                Debug.Log($"[UnitSpawnFeedback] Déclenchement des feedbacks de spawn pour {gameObject.name}");
            
            SpawnFeedbacksPlayer.PlayFeedbacks();
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[UnitSpawnFeedback] SpawnFeedbacksPlayer n'est pas assigné sur {gameObject.name}");
        }
    }

    /// <summary>
    /// Arrête les feedbacks si nécessaire (ex: si l'unité est détruite pendant l'animation)
    /// </summary>
    public void StopSpawnFeedback()
    {
        if (SpawnFeedbacksPlayer != null)
        {
            SpawnFeedbacksPlayer.StopFeedbacks();
        }
    }

    /// <summary>
    /// Initialise le MMF_Player si besoin (pour l'auto-setup)
    /// </summary>
    private void Awake()
    {
        // Si le MMF_Player n'est pas assigné, on tente de le trouver sur le même GameObject
        if (SpawnFeedbacksPlayer == null)
        {
            SpawnFeedbacksPlayer = GetComponent<MMF_Player>();
            
            if (SpawnFeedbacksPlayer == null)
            {
                // On cherche dans les enfants
                SpawnFeedbacksPlayer = GetComponentInChildren<MMF_Player>();
            }
        }
    }

    private void OnDestroy()
    {
        // S'assurer d'arrêter les feedbacks si l'objet est détruit
        StopSpawnFeedback();
    }
}