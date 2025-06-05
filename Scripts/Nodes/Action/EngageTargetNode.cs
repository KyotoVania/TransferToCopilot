using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;


[System.Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "EngageTargetNode",
    description: "Engage une cible ennemie",
    category: "Ally Actions",
    id: "AllyAction_EngageTargetNodev2_v1"
)]
public partial class EngageTargetNode : Unity.Behavior.Action
{
    [SerializeReference] public BlackboardVariable<Unit> TargetUnit;
    [SerializeReference] public BlackboardVariable<AIActionType> SelectedActionType;
    [SerializeReference] public BlackboardVariable<Vector2Int> FinalDestinationPosition;
    [SerializeReference] public BlackboardVariable<Unit> InteractionTargetUnit;
    [SerializeReference] public BlackboardVariable<bool> IsDefending;

    private AllyUnit selfUnit;

    protected override Status OnStart()
    {
        selfUnit = GameObject.GetComponent<AllyUnit>();
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (selfUnit == null) return Status.Failure;

        Unit target = TargetUnit?.Value;
        if (target == null || target.Health <= 0) return Status.Failure;

        Tile targetTile = target.GetOccupiedTile();
        if (targetTile == null) return Status.Failure;

        // Désactiver temporairement la défense pour l'engagement
        IsDefending.Value = false;
        
        // Configurer l'engagement
        InteractionTargetUnit.Value = target;
        FinalDestinationPosition.Value = new Vector2Int(targetTile.column, targetTile.row);
        
        // Déterminer l'action (attaque ou mouvement)
        if (selfUnit.IsUnitInRange(target))
        {
            SelectedActionType.Value = AIActionType.AttackUnit;
            Debug.Log($"[{selfUnit.name}] Attaque {target.name}");
        }
        else
        {
            SelectedActionType.Value = AIActionType.MoveToUnit;
            Debug.Log($"[{selfUnit.name}] Se déplace vers {target.name}");
        }

        return Status.Success;
    }
}