using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(name: "Set Return To Guard Post", story: "Sets the unit's destination to its saved guard post.", category: "Enemy Actions", id: "EnemyAction_ReturnToGuardPost_v1")]
public class SetReturnToGuardPostNode : Unity.Behavior.Action
{
    private const string BB_GUARD_POST_POS = "GuardPostPosition";
    private const string BB_FINAL_DEST_POS = "FinalDestinationPosition";
    private const string BB_SELECTED_ACTION = "SelectedActionType";

    protected override Status OnStart()
    {
        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null) return Status.Failure;
        var blackboard = agent.BlackboardReference;

        BlackboardVariable<Vector2Int> bbGuardPost;
        BlackboardVariable<Vector2Int> bbFinalDest;
        BlackboardVariable<AIActionType> bbActionType;

        if (!blackboard.GetVariable(BB_GUARD_POST_POS, out bbGuardPost) ||
            !blackboard.GetVariable(BB_FINAL_DEST_POS, out bbFinalDest) ||
            !blackboard.GetVariable(BB_SELECTED_ACTION, out bbActionType))
            return Status.Failure;

        if (bbGuardPost.Value.x >= 0)
        {
            bbFinalDest.Value = bbGuardPost.Value;
            bbActionType.Value = AIActionType.MoveToBuilding; // Ou MoveToPosition si tu as un type dédié
            return Status.Success;
        }

        return Status.Failure; // Pas de poste de garde défini
    }
}