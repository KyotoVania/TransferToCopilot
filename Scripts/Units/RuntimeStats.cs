using UnityEditor;
using UnityEngine;


/// <summary>
/// Contient les statistiques finales d'une unité au moment de son exécution (runtime),
/// après calcul (niveau, équipement, etc.).
/// Il s'agit d'une simple classe de données (POCO).
/// </summary>
public class RuntimeStats
{
    public int MaxHealth { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int AttackRange { get; set; }
    public int AttackDelay { get; set; }
    public int MovementDelay { get; set; }
    public int DetectionRange { get; set; }

    [ContextMenu("Log Stats")]
    public void LogStats()
    {
        Debug.Log($"MaxHealth: {MaxHealth}, Attack: {Attack}, Defense: {Defense}, " +
                  $"AttackRange: {AttackRange}, AttackDelay: {AttackDelay}, " +
                  $"MovementDelay: {MovementDelay}, DetectionRange: {DetectionRange}");
    }
}