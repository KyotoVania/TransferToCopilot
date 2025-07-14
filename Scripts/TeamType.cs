using UnityEngine;

/// <summary>
/// Enumerates the different teams or affiliations possible for game entities (units, buildings, etc.).
/// </summary>
public enum TeamType
{
    /// <summary>
    /// The entity is neutral and does not belong to any team.
    /// </summary>
    Neutral,

    /// <summary>
    /// The entity is initially neutral but can be influenced or captured by the player.
    /// </summary>
    NeutralPlayer,

    /// <summary>
    /// The entity is initially neutral but is hostile or can be captured by the enemy.
    /// </summary>
    NeutralEnemy,

    /// <summary>
    /// The entity belongs to the player's team.
    /// </summary>
    Player,

    /// <summary>
    /// The entity belongs to the enemy team (AI).
    /// </summary>
    Enemy
}