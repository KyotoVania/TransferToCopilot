using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System.Collections;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Attack Unit",
    story: "Performs an attack on the InteractionTargetUnit from the Blackboard, respecting the unit's AttackDelay.",
    category: "Unit Actions",
    id: "Action_AttackUnit_v4" // Nouvelle version avec délai
)]
public class AttackUnitNode : Unity.Behavior.Action
{
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string TARGET_UNIT_VAR = "InteractionTargetUnit";
    private const string IS_ATTACKING_VAR = "IsAttacking";

    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbTargetUnit;
    private BlackboardVariable<bool> bbIsAttackingBlackboard;

    private Unit selfUnitInstance = null;
    private Unit currentTargetUnitForThisNode = null;

    // MODIFIÉ : Une seule coroutine pour gérer toute la boucle d'attaque.
    private Coroutine attackLoopCoroutine = null;

    // NOUVEAU : Un compteur de temps pour le délai, géré au sein de la coroutine.
    private int currentBeatCounterForDelay = 0;
    private bool hasSubscribedToBeat = false;
    private Vector3 startingPosition;

    protected override Status OnStart()
    {
        ResetNodeInternalState();

        if (!CacheBlackboardVariables())
        {
            SetIsAttackingBlackboardVar(false);
            return Status.Failure;
        }

        selfUnitInstance = bbSelfUnit?.Value;
        if (selfUnitInstance == null)
        {
            SetIsAttackingBlackboardVar(false);
            return Status.Failure;
        }

        startingPosition = selfUnitInstance.transform.position;

        currentTargetUnitForThisNode = bbTargetUnit?.Value;
        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0)
        {
            SetIsAttackingBlackboardVar(false);
            return Status.Success; // La cible est déjà invalide, succès car il n'y a rien à faire.
        }

        SetIsAttackingBlackboardVar(true);

        // MODIFIÉ : On lance la coroutine principale qui gérera tout le cycle.
        attackLoopCoroutine = selfUnitInstance.StartCoroutine(AttackLoop());

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        // OnUpdate est maintenant beaucoup plus simple !
        // Sa seule responsabilité est de vérifier les conditions qui pourraient interrompre l'action.

        // La cible est morte ou a disparu ?
        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0 || !currentTargetUnitForThisNode.gameObject.activeInHierarchy)
        {
            return Status.Success; // L'action est terminée avec succès.
        }

        // L'unité a-t-elle été déplacée (ex: repoussée) ?
        if (selfUnitInstance.transform.position != startingPosition)
        {
            return Status.Failure; // L'action a échoué car la condition de position n'est plus respectée.
        }

        // Si aucune condition de sortie n'est remplie, on laisse la coroutine continuer son travail.
        return Status.Running;
    }

    // NOUVEAU : La coroutine qui gère la boucle complète : Attaque -> Délai -> Répétition
    private IEnumerator AttackLoop()
    {
        // Boucle infinie qui sera interrompue par le changement de statut du nœud (Success ou Failure dans OnUpdate)
        while (true)
        {
            // 1. Exécuter l'attaque
            yield return selfUnitInstance.StartCoroutine(selfUnitInstance.PerformAttackCoroutine(currentTargetUnitForThisNode));

            // Une vérification supplémentaire ici au cas où la cible meurt pendant l'animation d'attaque
            if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0)
            {
                yield break; // Termine la coroutine
            }

            // 2. Gérer le délai d'attaque basé sur le rythme
            if (selfUnitInstance.AttackDelay > 0)
            {
                currentBeatCounterForDelay = 0;
                SubscribeToBeat();

                // Attendre que le nombre de battements requis soit atteint
                while (currentBeatCounterForDelay < selfUnitInstance.AttackDelay)
                {
                    yield return null; // Attend la frame suivante
                }

                UnsubscribeFromBeat();
            }
            else
            {
                // S'il n'y a pas de délai, on attend juste une frame pour éviter une boucle infinie sans pause.
                yield return null;
            }
        }
    }

    protected override void OnEnd()
    {
        // Nettoyage propre à l'arrêt du nœud
        UnsubscribeFromBeat();
        if (attackLoopCoroutine != null && selfUnitInstance != null)
        {
            selfUnitInstance.StopCoroutine(attackLoopCoroutine);
        }
        SetIsAttackingBlackboardVar(false);
        ResetNodeInternalState();
    }

    // MODIFIÉ : Le gestionnaire de battements se contente maintenant d'incrémenter un compteur.
    private void HandleBeat(float beatDuration)
    {
        currentBeatCounterForDelay++;
    }

    private void SubscribeToBeat()
    {
        if (MusicManager.Instance != null && !hasSubscribedToBeat)
        {
            MusicManager.Instance.OnBeat += HandleBeat;
            hasSubscribedToBeat = true;
        }
    }

    private void UnsubscribeFromBeat()
    {
        if (MusicManager.Instance != null && hasSubscribedToBeat)
        {
            MusicManager.Instance.OnBeat -= HandleBeat;
            hasSubscribedToBeat = false;
        }
    }

    private void ResetNodeInternalState()
    {
        attackLoopCoroutine = null;
        selfUnitInstance = null;
        currentTargetUnitForThisNode = null;
        currentBeatCounterForDelay = 0;
        hasSubscribedToBeat = false;
    }

    // Les helpers pour le Blackboard restent inchangés
    private void SetIsAttackingBlackboardVar(bool value)
    {
        if(bbIsAttackingBlackboard != null && bbIsAttackingBlackboard.Value != value)
        {
            bbIsAttackingBlackboard.Value = value;
        }
    }

    private bool CacheBlackboardVariables()
    {
        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null) return false;

        var blackboard = agent.BlackboardReference;
        bool success = true;
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit)) success = false;
        if (!blackboard.GetVariable(TARGET_UNIT_VAR, out bbTargetUnit)) success = false;
        if (!blackboard.GetVariable(IS_ATTACKING_VAR, out bbIsAttackingBlackboard)) success = false;

        return success;
    }
}