using UnityEngine;
using Unity.Behavior;

[System.Serializable]
[Unity.Properties.GeneratePropertyBag]
[NodeDescription(
    name: "Detect Perimeter Threat",
    description: "Détecte les ennemis dans le périmètre défensif",
    category: "Condition",
    id:"DetectePerimv1"
)]
public partial class DetectPerimeterThreatNode : Unity.Behavior.Condition
{
    [SerializeReference] public BlackboardVariable<Unit> DetectedEnemyUnit;
    [SerializeReference] public BlackboardVariable<bool> PerimeterThreatDetected;
    
    public float detectionRange = 3f;

    public override bool IsTrue()
    {
        var selfUnit = GameObject.GetComponent<AllyUnit>();
        if (selfUnit == null) return false;

        Unit nearestEnemy = selfUnit.FindNearestEnemyUnit();
        
        if (nearestEnemy != null && nearestEnemy.Health > 0)
        {
            Tile currentTile = selfUnit.GetOccupiedTile();
            Tile enemyTile = nearestEnemy.GetOccupiedTile();
            
            if (currentTile != null && enemyTile != null)
            {
                int distance = HexGridManager.Instance.HexDistance(
                    currentTile.column, currentTile.row,
                    enemyTile.column, enemyTile.row
                );
                
                if (distance <= detectionRange)
                {
                    DetectedEnemyUnit.Value = nearestEnemy;
                    PerimeterThreatDetected.Value = true;
                    
                    Debug.Log($"[{selfUnit.name}] Menace détectée: {nearestEnemy.name} à distance {distance}");
                    return true;
                }
            }
        }

        DetectedEnemyUnit.Value = null;
        PerimeterThreatDetected.Value = false;
        return false;
    }
}