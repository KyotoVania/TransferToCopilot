using UnityEngine;

/// <summary>
/// Composant de données pour les projectiles qui doivent infliger des dégâts en pourcentage aux boss.
/// S'ajoute dynamiquement aux projectiles tirés par les tours.
/// </summary>
public class BossProjectileData : MonoBehaviour
{
    [Tooltip("Pourcentage de dégâts à infliger au boss (0-100).")]
    public float damagePercentage = 1f;
    
    [Tooltip("Indique si ce projectile vient d'une tour (pour les logs).")]
    public bool isFromTower = false;
}