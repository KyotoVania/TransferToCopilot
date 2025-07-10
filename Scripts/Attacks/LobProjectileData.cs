using UnityEngine;

/// <summary>
/// Composant de données pour les projectiles qui doivent suivre une trajectoire en arc (lob).
/// S'ajoute dynamiquement aux projectiles tirés par les tours.
/// </summary>
public class LobProjectileData : MonoBehaviour
{
    [Tooltip("Hauteur maximale de l'arc (plus haut = arc plus prononcé).")]
    public float lobHeight = 5f;
    
    [Tooltip("Indique si ce projectile doit utiliser une trajectoire en arc.")]
    public bool useLobTrajectory = true;
    
    // Variables internes pour calculer la trajectoire
    [HideInInspector] public Vector3 startPosition;
    [HideInInspector] public Vector3 targetPosition;
    [HideInInspector] public float totalDistance;
    [HideInInspector] public bool isInitialized = false;
    
    /// <summary>
    /// Initialise les données pour la trajectoire lob.
    /// </summary>
    /// <param name="start">Position de départ</param>
    /// <param name="target">Position cible</param>
    public void Initialize(Vector3 start, Vector3 target)
    {
        startPosition = start;
        targetPosition = target;
        totalDistance = Vector3.Distance(start, target);
        isInitialized = true;
    }
    
    /// <summary>
    /// Calcule la position sur la trajectoire lob basée sur le pourcentage de progression.
    /// </summary>
    /// <param name="progress">Progression de 0 à 1</param>
    /// <returns>Position calculée avec l'arc</returns>
    public Vector3 GetLobPosition(float progress)
    {
        if (!isInitialized) return Vector3.zero;
        
        // Position linéaire entre start et target
        Vector3 linearPosition = Vector3.Lerp(startPosition, targetPosition, progress);
        
        // Calcul de la hauteur de l'arc (parabole)
        // Maximum à 50% de la progression
        float arcHeight = lobHeight * 4 * progress * (1 - progress);
        
        // Ajouter la hauteur de l'arc
        linearPosition.y += arcHeight;
        
        return linearPosition;
    }
    
    /// <summary>
    /// Calcule la direction du projectile à un point donné de la trajectoire.
    /// </summary>
    /// <param name="progress">Progression actuelle</param>
    /// <param name="deltaProgress">Petit increment pour calculer la direction</param>
    /// <returns>Direction normalisée</returns>
    public Vector3 GetLobDirection(float progress, float deltaProgress = 0.01f)
    {
        if (!isInitialized) return Vector3.forward;
        
        Vector3 currentPos = GetLobPosition(progress);
        Vector3 nextPos = GetLobPosition(Mathf.Clamp01(progress + deltaProgress));
        
        Vector3 direction = (nextPos - currentPos).normalized;
        return direction != Vector3.zero ? direction : Vector3.forward;
    }
}