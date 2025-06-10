using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "NewBuildingStats", menuName = "Building/Stats")]
public class BuildingStats : SerializedScriptableObject
{
    [Title("Building Stats")]
    public int health = 500;
    public int defense = 5;

    [Title("Resource Generation")]
    public int goldGeneration = 10;
    public int goldGenerationDelay = 4; // How many beats between generating gold

    [Title("Garrison")]
    public int Garrison = 0; // Number of units that can be garrisoned
}