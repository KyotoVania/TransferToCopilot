using UnityEngine;
using System.Collections;

/// <summary>
/// Interface for implementing attack behaviors in units.
/// Provides methods for executing attacks and validating attack conditions.
/// </summary>
public interface IAttack
{
    /// <summary>
    /// Executes an attack action from the attacker to the target.
    /// </summary>
    /// <param name="attacker">Transform of the attacking unit.</param>
    /// <param name="target">Transform of the target unit.</param>
    /// <param name="damage">Amount of damage to inflict.</param>
    /// <param name="duration">Duration of the attack animation or effect.</param>
    /// <returns>Coroutine for attack execution.</returns>
    IEnumerator PerformAttack(Transform attacker, Transform target, int damage, float duration);

    /// <summary>
    /// Determines if the attacker can attack the target based on range and other conditions.
    /// </summary>
    /// <param name="attacker">Transform of the attacking unit.</param>
    /// <param name="target">Transform of the target unit.</param>
    /// <param name="attackRange">Maximum range for the attack.</param>
    /// <returns>True if the attack is possible; otherwise, false.</returns>
    bool CanAttack(Transform attacker, Transform target, float attackRange);
}