using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System.Collections;
using System;
using Unity.Properties;
using System.Collections.Generic;
using ScriptableObjects;

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
    // --- NOMS DES VARIABLES BLACKBOARD ---
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string TARGET_UNIT_VAR = "InteractionTargetUnit";
    private const string IS_ATTACKING_VAR = "IsAttacking";
    private const string SELECTED_ACTION_TYPE_VAR = "SelectedActionType"; // Nouvelle variable pour rediriger l'action

    // --- CACHE DES VARIABLES ---
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbTargetUnit;
    private BlackboardVariable<bool> bbIsAttackingBlackboard;
    private BlackboardVariable<AIActionType> bbSelectedActionType; // Nouvelle variable

    // --- NOUVELLES VARIABLES POUR GÉRER LE CYCLE D'ATTAQUE ---
    private Unit selfUnitInstance = null;
    private Unit currentTargetUnitForThisNode = null;
    private Coroutine nodeManagedAttackCycleCoroutine = null;
    private bool isWaitingForAttackDelay = false;
    private int currentAttackBeatCounter = 0;
    private bool hasSubscribedToBeatForAttackDelay = false;
    // --- FIN DES NOUVELLES VARIABLES ---

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

        currentTargetUnitForThisNode = bbTargetUnit?.Value;
        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0)
        {
            SetIsAttackingBlackboardVar(false);
            return Status.Success;
        }

        SetIsAttackingBlackboardVar(true);
        isWaitingForAttackDelay = false;
        return Status.Running;

    }

    protected override Status OnUpdate()
    {
        if (selfUnitInstance == null || !selfUnitInstance.gameObject.activeInHierarchy) return Status.Failure;
        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0 || !currentTargetUnitForThisNode.gameObject.activeInHierarchy)
        {
            return Status.Success;
        }
        
        // Vérifier si la cible est toujours à portée d'attaque
        if (!IsTargetInAttackRange())
        {
            Debug.Log($"[{selfUnitInstance.name}] Cible {currentTargetUnitForThisNode.name} n'est plus à portée d'attaque. Changement d'action vers MoveToUnit.");
            
            // Mettre à jour le Blackboard pour rediriger vers le mouvement
            if (bbSelectedActionType != null)
            {
                bbSelectedActionType.Value = AIActionType.MoveToUnit;
            }
            
            return Status.Success; // Sortir complètement du nœud
        }

        if (isWaitingForAttackDelay)
        {
            return Status.Running;
        }

        if (nodeManagedAttackCycleCoroutine == null)
        {
            Debug.Log($"[{selfUnitInstance.name}] Démarrage du cycle d'attaque pour {currentTargetUnitForThisNode.name}.");
            nodeManagedAttackCycleCoroutine = selfUnitInstance.StartCoroutine(PerformSingleAttackCycle());
        }

        return Status.Running;
    }

    private IEnumerator PerformSingleAttackCycle()
    {
        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0)
        {
            nodeManagedAttackCycleCoroutine = null;
            yield break;
        }
       
        // Vérifier si nous sommes toujours dans la portée de l'ennemi
        if (!IsTargetInAttackRange())
        {
            Debug.Log($"[{selfUnitInstance.name}] Cible {currentTargetUnitForThisNode.name} n'est plus à portée durant le cycle d'attaque. Arrêt du cycle.");
            nodeManagedAttackCycleCoroutine = null;
            yield break; // Le nœud sortira au prochain OnUpdate via la vérification de portée
        }

        yield return selfUnitInstance.StartCoroutine(selfUnitInstance.PerformAttackCoroutine(currentTargetUnitForThisNode));

        if (currentTargetUnitForThisNode == null || currentTargetUnitForThisNode.Health <= 0)
        {
            nodeManagedAttackCycleCoroutine = null;
            yield break;
        }

        currentAttackBeatCounter = 0;
        isWaitingForAttackDelay = (selfUnitInstance.AttackDelay > 0);

        if (isWaitingForAttackDelay)
        {
            SubscribeToBeatForAttackDelay();
        }

        nodeManagedAttackCycleCoroutine = null;
    }

    protected override void OnEnd()
    {
        UnsubscribeFromBeatForAttackDelay();
        if (nodeManagedAttackCycleCoroutine != null && selfUnitInstance != null)
        {
            selfUnitInstance.StopCoroutine(nodeManagedAttackCycleCoroutine);
        }
        SetIsAttackingBlackboardVar(false);
        ResetNodeInternalState();
    }

    // --- Méthodes pour la gestion du délai ---

    // La méthode accepte maintenant un paramètre float pour correspondre à la signature de l'événement MusicManager.OnBeat.
    private void HandleAttackBeatDelay(float beatDuration)
    {
        if (!isWaitingForAttackDelay)
        {
            UnsubscribeFromBeatForAttackDelay();
            return;
        }

        currentAttackBeatCounter++;
        if (currentAttackBeatCounter >= selfUnitInstance.AttackDelay)
        {
            isWaitingForAttackDelay = false;
            UnsubscribeFromBeatForAttackDelay();
        }
    }

    private void SubscribeToBeatForAttackDelay()
    {
        if (MusicManager.Instance != null && !hasSubscribedToBeatForAttackDelay)
        {
            MusicManager.Instance.OnBeat += HandleAttackBeatDelay;
            hasSubscribedToBeatForAttackDelay = true;
        }
    }

    private void UnsubscribeFromBeatForAttackDelay()
    {
        if (MusicManager.Instance != null && hasSubscribedToBeatForAttackDelay)
        {
            MusicManager.Instance.OnBeat -= HandleAttackBeatDelay;
            hasSubscribedToBeatForAttackDelay = false;
        }
    }

    // --- Méthodes utilitaires ---

    private void ResetNodeInternalState()
    {
        nodeManagedAttackCycleCoroutine = null;
        selfUnitInstance = null;
        currentTargetUnitForThisNode = null;
        currentAttackBeatCounter = 0;
        isWaitingForAttackDelay = false;
        hasSubscribedToBeatForAttackDelay = false;
    }

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
        
        // Essayer d'obtenir la variable SelectedActionType (optionnelle)
        if (!blackboard.GetVariable(SELECTED_ACTION_TYPE_VAR, out bbSelectedActionType))
        {
            Debug.LogWarning($"[{GameObject?.name}] Variable Blackboard '{SELECTED_ACTION_TYPE_VAR}' non trouvée. La redirection d'action ne sera pas disponible.");
        }

        return success;
    }

    // --- Méthode pour vérifier la portée d'attaque ---
    private bool IsTargetInAttackRange()
    {
        if (selfUnitInstance == null || currentTargetUnitForThisNode == null)
        {
            return false;
        }

        bool isEnemyInRange;
        if (currentTargetUnitForThisNode.GetUnitType() == UnitType.Boss)
        {
            // C'est la logique robuste de V2, maintenant utilisée spécifiquement pour les boss multi-tuiles.
            Tile selfTile = selfUnitInstance.GetOccupiedTile();
            List<Tile> targetTiles = currentTargetUnitForThisNode.GetOccupiedTiles(); // Récupère correctement toutes les tuiles du boss
            if (selfTile == null || targetTiles.Count == 0 || HexGridManager.Instance == null)
            {
                return false; // Sécurité, si on n'a pas de tuile ou de gestionnaire de grille
            }
            isEnemyInRange = false; // Aucune partie du boss n'était à portée

            // Vérifie la distance par rapport à chaque tuile du boss
            foreach (var targetTile in targetTiles)
            {
                if (targetTile != null)
                {
                    int distance = HexGridManager.Instance.HexDistance(selfTile.column, selfTile.row, targetTile.column, targetTile.row);
                    if (distance <= selfUnitInstance.AttackRange)
                    {
                        isEnemyInRange = true;
                        break; // Pas besoin de vérifier les autres tuiles
                    }
                }
            }
        }
        else
        {
            isEnemyInRange = selfUnitInstance.IsUnitInRange(currentTargetUnitForThisNode);
        }

        return isEnemyInRange;
    }
}