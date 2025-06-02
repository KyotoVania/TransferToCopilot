using UnityEngine;
using System.Collections;

public interface IMovement
{
    IEnumerator MoveToTile(Transform unitTransform,
        Vector3 startPos,
        Vector3 targetPos,
        float duration);
}