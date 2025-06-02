using UnityEngine;
using System.Collections;

public interface IAttack
{
    IEnumerator PerformAttack(Transform attacker, Transform target, int damage, float duration);
    bool CanAttack(Transform attacker, Transform target, float attackRange);
}