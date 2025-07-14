using UnityEngine;
using System.Collections;

/// <summary>
/// Interface for implementing movement behaviors in units.
/// Provides a method for moving a unit to a specified tile position.
/// </summary>
public interface IMovement
{
    /// <summary>
    /// Moves the unit from its current position to the target tile position over a specified duration.
    /// </summary>
    /// <param name="unitTransform">Transform of the unit to move.</param>
    /// <param name="startPos">Starting position of the unit.</param>
    /// <param name="targetPos">Target position to move the unit to.</param>
    /// <param name="duration">Time in seconds for the movement to complete.</param>
    /// <returns>Coroutine for movement execution.</returns>
    IEnumerator MoveToTile(Transform unitTransform,
        Vector3 startPos,
        Vector3 targetPos,
        float duration);
}