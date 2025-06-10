using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "NewUnitStats", menuName = "Unit/Stats")]
public class UnitStats : SerializedScriptableObject
{
    [Title("Unit Stats")]
    public int health = 100;
    public int movementDelay = 1;
    public int defense = 10;
    public int attack = 15;
    public int attackDelay = 1;
    public int attackRange = 1;
    public int detectionRange = 2; // Detection range in tiles
}