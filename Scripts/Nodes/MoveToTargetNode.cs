using UnityEngine;
using Unity.Behavior;
using System.Collections;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Move To Target (Step)",
    story: "Move To Target (Step)",
    category: "My Actions",
    id: "YOUR_UNIQUE_ID_MoveToTarget_Step" // ID mis à jour pour une nouvelle version
)]
public class MoveToTargetNode_WithInternalBeatWait : Unity.Behavior.Action
{
    // --- Blackboard Variable Noms ---
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string MOVEMENT_TARGET_POS_VAR = "MovementTargetPosition";
    private const string IS_MOVING_BB_VAR = "IsMoving"; // Flag global sur le BB

    // --- Références Blackboard mises en cache ---
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Vector2Int> bbMovementTargetPosition;
    private BlackboardVariable<bool> bbIsMoving;

    // --- État Interne du Nœud ---
    private Unit selfUnitInstanceInternal;
    private int beatCounterInternal = 0;
    private int requiredMovementDelayInternal = 0;
    private bool isSubscribedToBeat = false;
    private bool delayPhaseComplete = false;
    private bool movementActionStarted = false; // True si la coroutine MoveToTile de l'unité a été appelée
    private Coroutine unitStepCoroutineHandle;   // Pour potentiellement arrêter la coroutine de l'unité si le noeud est interrompu

    private string nodeInstanceId; // Pour des logs plus clairs si plusieurs instances tournent
    private bool blackboardVariablesAreValid = false; // Flag pour le cache

    // =================================================================================================================
    // CYCLE DE VIE DU NŒUD
    // =================================================================================================================

    protected override Status OnStart()
    {
        nodeInstanceId = Guid.NewGuid().ToString("N").Substring(0, 6);
        ResetInternalState(); // Toujours réinitialiser l'état au démarrage

        if (!CacheBlackboardVariables()) // Tente de mettre en cache les variables du BB
        {
            return Status.Failure;
        }

        selfUnitInstanceInternal = bbSelfUnit.Value;
        if (selfUnitInstanceInternal == null)
            return Status.Failure;

        // Mettre à jour le Blackboard : l'unité est maintenant engagée dans ce processus de mouvement.
        SetBlackboardIsMoving(true);

        // Vérifier si l'unité est déjà à la destination finale
        Vector2Int finalDestination = bbMovementTargetPosition.Value;
        Tile currentTile = selfUnitInstanceInternal.GetOccupiedTile();
        if (currentTile != null && currentTile.column == finalDestination.x && currentTile.row == finalDestination.y)
        {
            // SetBlackboardIsMoving(false); // L'état sera remis à false dans OnEnd
            return Status.Success; // Objectif déjà atteint
        }

        requiredMovementDelayInternal = selfUnitInstanceInternal.MovementDelay;

        if (requiredMovementDelayInternal > 0)
        {
            if (RhythmManager.Instance != null)
            {
                RhythmManager.OnBeat += OnBeatReceived;
                isSubscribedToBeat = true;
            }
            else
                return Status.Failure; // Le délai ne peut pas être respecté
        }
        else
            delayPhaseComplete = true; // Pas de délai, on passe directement à la phase de mouvement

        return Status.Running; // Le nœud commence à s'exécuter (soit en attente de délai, soit prêt à bouger)
    }

    protected override Status OnUpdate()
    {
        LogNodeMessage("OnUpdate BEGIN", false, true);
        if (selfUnitInstanceInternal == null) // Sécurité si l'unité a été détruite pendant que le nœud tournait
            return Status.Failure;

        // --- Phase 1: Attente du délai de battement ---
        if (!delayPhaseComplete)
        {
            return Status.Running; // On attend que OnBeatReceived mette delayPhaseComplete à true
        }
        
        // --- Phase 2: Exécution de l'action de mouvement (si pas encore démarrée) ---
        if (!movementActionStarted)
            return AttemptMovementStep();

        // --- Phase 3: Mouvement en cours, surveillance de selfUnitInstanceInternal.IsMoving ---
        // Si movementActionStarted est true, cela signifie qu'on a lancé la coroutine de l'unité.
        if (selfUnitInstanceInternal.IsMoving)
        {
            return Status.Running; // L'unité signale qu'elle est toujours en train de bouger
        }
        else
        {
            // Unit.IsMoving est false. Cela signifie que la coroutine Unit.MoveToTile s'est terminée.
            return Status.Success; // Le pas de mouvement est terminé
        }
    }

    protected override void OnEnd()
    {

        if (isSubscribedToBeat && RhythmManager.Instance != null)
        {
            RhythmManager.OnBeat -= OnBeatReceived;
        }
        isSubscribedToBeat = false;

        // Si le nœud est terminé (ou interrompu) alors qu'une coroutine de mouvement de l'unité était potentiellement en cours
        if (movementActionStarted && selfUnitInstanceInternal != null && unitStepCoroutineHandle != null)
        {
            selfUnitInstanceInternal.StopCoroutine(unitStepCoroutineHandle);
            unitStepCoroutineHandle = null; // Important

            // Si la coroutine de l'unité a été interrompue par le Behavior Graph,
            // il faut s'assurer que l'état de l'unité est cohérent.
            if (selfUnitInstanceInternal.IsMoving)
            {
                selfUnitInstanceInternal.IsMoving = false;
                selfUnitInstanceInternal.ReleaseCurrentReservation(); // Crucial
            }
        }

        // Assurer que le Blackboard reflète que CETTE action de mouvement spécifique est terminée.
        SetBlackboardIsMoving(false);

        ResetInternalState(); // Prêt pour la prochaine exécution
    }

    // =================================================================================================================
    // LOGIQUE SPÉCIFIQUE DU NŒUD
    // =================================================================================================================

    private void OnBeatReceived()
    {
        if (selfUnitInstanceInternal == null) // Unité détruite entre-temps ?
        {
            if (isSubscribedToBeat && RhythmManager.Instance != null) RhythmManager.OnBeat -= OnBeatReceived;
            isSubscribedToBeat = false;
            return;
        }

        if (!delayPhaseComplete) // Si on est toujours dans la phase d'attente
        {
            beatCounterInternal++;

            if (beatCounterInternal >= requiredMovementDelayInternal)
            {
                delayPhaseComplete = true;
                if (isSubscribedToBeat && RhythmManager.Instance != null)
                {
                    RhythmManager.OnBeat -= OnBeatReceived; // Se désabonner dès que le délai est passé
                    isSubscribedToBeat = false;
                }
            }
        }
        else // Reçu un battement alors que le délai est déjà passé (ne devrait pas arriver si désabonnement correct)
        {
             if (isSubscribedToBeat && RhythmManager.Instance != null) RhythmManager.OnBeat -= OnBeatReceived;
             isSubscribedToBeat = false;
        }
    }

    private Status AttemptMovementStep()
    {
        movementActionStarted = true; // Marquer que l'on a tenté de démarrer le mouvement

        Vector2Int finalDestination = bbMovementTargetPosition.Value;
        Tile currentUnitTile = selfUnitInstanceInternal.GetOccupiedTile();

        if (currentUnitTile == null)
        {
            LogNodeMessage($"AttemptMovementStep: L'unité {selfUnitInstanceInternal.name} n'est pas sur une tuile valide. Échec.", true, true);
            return Status.Failure;
        }
        // Vérification (redondante si OnStart l'a fait, mais bonne sécurité si délai=0)
        if (currentUnitTile.column == finalDestination.x && currentUnitTile.row == finalDestination.y)
        {
            LogNodeMessage(
                $"AttemptMovementStep: L'unité {selfUnitInstanceInternal.name} est déjà sur la destination finale ({finalDestination.x},{finalDestination.y}).",
                false, true);
            return Status.Success;
        }

        if (HexGridManager.Instance == null)
            return Status.Failure;

        Tile nextStepTile = selfUnitInstanceInternal.GetNextTileTowardsDestinationForBG(finalDestination);

        if (nextStepTile == null)
        {
            // Si GetNextTile... retourne null, cela signifie soit qu'on EST sur la cible (déjà géré au-dessus),
            // soit qu'aucun chemin n'est possible vers la tuile finale.
            LogNodeMessage($"AttemptMovementStep: GetNextTileTowardsDestinationForBG n'a retourné aucun pas valide vers ({finalDestination.x},{finalDestination.y}) depuis ({currentUnitTile.column},{currentUnitTile.row}). Échec du pathfinding pour ce pas.", true, true);
            return Status.Failure; // Pas de chemin trouvé pour ce pas.
        }
        LogNodeMessage($"AttemptMovementStep: Prochain pas vers ({finalDestination.x},{finalDestination.y}) est la tuile ({nextStepTile.column},{nextStepTile.row}).", false, true);

        // Démarrer la coroutine de mouvement sur l'instance de l'unité.
        // Unit.MoveToTile est responsable de mettre Unit.IsMoving = true au début et false à la fin.
        if(selfUnitInstanceInternal.gameObject.activeInHierarchy && selfUnitInstanceInternal.enabled)
        {
            unitStepCoroutineHandle = selfUnitInstanceInternal.StartCoroutine(selfUnitInstanceInternal.MoveToTile(nextStepTile));
            if (unitStepCoroutineHandle == null)
            {
                movementActionStarted = false; // N'a pas pu démarrer
                return Status.Failure;
            }
            // À ce stade, on s'attend à ce que Unit.MoveToTile mette selfUnitInstanceInternal.IsMoving à true si le mouvement commence réellement.
            // OnUpdate va ensuite surveiller selfUnitInstanceInternal.IsMoving.
        }
        else
        {
            movementActionStarted = false; // N'a pas pu démarrer
            return Status.Failure;
        }

        return Status.Running; // Le mouvement a été initié, OnUpdate surveillera la suite.
    }

    // =================================================================================================================
    // MÉTHODES UTILITAIRES ET DE NETTOYAGE
    // =================================================================================================================

    private void ResetInternalState()
    {
        // Références Blackboard (le cache est géré par blackboardVariablesAreValid)
        // selfUnitInstanceInternal sera mis à jour par CacheBlackboardVariables

        // État du nœud
        beatCounterInternal = 0;
        requiredMovementDelayInternal = 0;
        // isSubscribedToBeat est géré par OnStart/OnEnd
        delayPhaseComplete = false;
        movementActionStarted = false;
        unitStepCoroutineHandle = null; // Important de nullifier

    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesAreValid) return true;

        var agent = this.GameObject.GetComponent<BehaviorGraphAgent>(); // this.GameObject est le GameObject de l'agent
        if (agent == null || agent.BlackboardReference == null)
        {
            Debug.LogError($"[{nodeInstanceId}] CacheBlackboardVariables: BehaviorGraphAgent or BlackboardReference is null on GameObject '{this.GameObject?.name}'.");
            return false;
        }

        var blackboard = agent.BlackboardReference;
        bool allFound = true;

        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit)) { Debug.LogError($"[{nodeInstanceId}] BBVar '{SELF_UNIT_VAR}' missing."); allFound = false; }
        if (!blackboard.GetVariable(MOVEMENT_TARGET_POS_VAR, out bbMovementTargetPosition)) { Debug.LogError($"[{nodeInstanceId}] BBVar '{MOVEMENT_TARGET_POS_VAR}' missing."); allFound = false; }
        if (!blackboard.GetVariable(IS_MOVING_BB_VAR, out bbIsMoving)) { Debug.LogError($"[{nodeInstanceId}] BBVar '{IS_MOVING_BB_VAR}' missing (CRITICAL)."); allFound = false; }

        blackboardVariablesAreValid = allFound;
        if (!allFound) Debug.LogError($"[{nodeInstanceId}] CacheBlackboardVariables: Failed to cache one or more critical Blackboard variables.");
        return allFound;
    }

    private void SetBlackboardIsMoving(bool value)
    {
        if (bbIsMoving != null)
        {
            if (bbIsMoving.Value != value) // Évite écritures inutiles
            {
                bbIsMoving.Value = value;
            }
        }
        else
        {
            // Tenter de recacher si bbIsMoving est null mais que les variables n'étaient pas valides avant
            if (!blackboardVariablesAreValid && CacheBlackboardVariables() && bbIsMoving != null)
            {
                bbIsMoving.Value = value;
            }
        }
    }
    private void LogNodeMessage(string message, bool isError = false, bool forceLog = false)
    {
        if(!blackboardVariablesAreValid && !isError) return; // Ne pas logger les messages normaux si BB pas prêt

        string unitName = selfUnitInstanceInternal != null ? selfUnitInstanceInternal.name : (bbSelfUnit?.Value != null ? bbSelfUnit.Value.name : "NoUnit");
        // string log = $"[{nodeInstanceId} | {unitName} | MoveToTargetNode] {message}";
        string logPrefix = $"<color=orange>[{nodeInstanceId} | {unitName} | MoveToTargetNode]</color>";


        if (isError)
        {
            Debug.LogError($"{logPrefix} {message}", this.GameObject);
        }
        else if (forceLog || (selfUnitInstanceInternal != null /* && selfUnitInstanceInternal.enableVerboseLogging // Adaptez si vous avez un tel flag */))
        {
            Debug.Log($"{logPrefix} {message}", this.GameObject);
        }
    }
}