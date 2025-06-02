using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Game.Observers;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;

public class AllyUnit : Unit, IBannerObserver
{
    // --- Behavior Graph Agent Reference ---
    [Header("Behavior Graph")]
    [Tooltip("Assign the Behavior Graph Agent component from this GameObject here.")]
    [SerializeField] private BehaviorGraphAgent m_Agent;

    // --- Blackboard Variable Cache ---
    // Clés pour le Blackboard
    private const string BB_HAS_BANNER_TARGET = "HasBannerTarget";
    private const string BB_BANNER_TARGET_POSITION = "BannerTargetPosition";
    private const string BB_FINAL_DESTINATION_POSITION = "FinalDestinationPosition"; // Utilisé par FindSmartStepNode
    private const string BB_INITIAL_TARGET_BUILDING = "InitialTargetBuilding";
    private const string BB_HAS_INITIAL_OBJECTIVE_SET = "HasInitialObjectiveSet";
    private const string BB_IS_ATTACKING = "IsAttacking";
    private const string BB_IS_CAPTURING = "IsCapturing";
    private const string BB_IS_OBJECTIVE_COMPLETED = "IsObjectiveCompleted"; // Pour signaler la fin de l'objectif

    // Variables Blackboard mises en cache
    private BlackboardVariable<bool> bbHasBannerTarget;
    private BlackboardVariable<Vector2Int> bbBannerTargetPosition;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    private BlackboardVariable<Building> bbInitialTargetBuilding;
    private BlackboardVariable<bool> bbHasInitialObjectiveSet;
    private BlackboardVariable<bool> bbIsAttacking;
    private BlackboardVariable<bool> bbIsCapturing;
    private BlackboardVariable<bool> bbIsObjectiveCompleted;

    [Header("Ally Settings")]
    [SerializeField] public bool enableVerboseLogging = true; // Public pour que les nœuds puissent vérifier
    // ... (autres variables comme prioritizeEnemyBuildings, healAmount etc.)

    // Stockage direct de l'instance du bâtiment objectif initial pour s'abonner/désabonner aux événements
    private Building initialObjectiveBuildingInstance;
    private bool hasInitialObjectiveBeenSetThisLife = false;

    //FEEDBACKS
    private UnitSpawnFeedback spawnFeedbackPlayer; // Référence au composant de feedback


    protected override IEnumerator Start()
    {
        yield return StartCoroutine(base.Start()); // base.Start() gère l'attachement à la tuile, etc.

        // Enregistrement auprès du AllyUnitRegistry APRÈS que l'unité soit initialisée et potentiellement attachée
        if (AllyUnitRegistry.Instance != null)
        {
            AllyUnitRegistry.Instance.RegisterUnit(this);
            if (enableVerboseLogging) Debug.Log($"[{name}] s'est enregistré auprès de AllyUnitRegistry.");
        }
        else
        {
            Debug.LogWarning($"[{name}] AllyUnitRegistry.Instance non trouvé. Impossible de s'enregistrer.");
        }
        // S'assurer que m_Agent est assigné (crucial avant d'accéder au Blackboard)
        if (m_Agent == null) m_Agent = GetComponent<BehaviorGraphAgent>();
        if (m_Agent == null)
        {
            Debug.LogError($"[{name}] BehaviorGraphAgent component not found on {gameObject.name}!", gameObject);
            yield break; // Arrêter si l'agent n'est pas là
        }
        if (m_Agent.BlackboardReference == null)
        {
            Debug.LogError($"[{name}] BlackboardReference is null on BehaviorGraphAgent for {gameObject.name}!", gameObject);
            yield break; // Arrêter si le blackboard n'est pas là
        }

        // Mettre en cache toutes les variables Blackboard nécessaires
        CacheBlackboardVariables();

        // Initialiser les flags du Blackboard au démarrage de l'unité
        if (bbHasBannerTarget != null) bbHasBannerTarget.Value = false;
        if (bbHasInitialObjectiveSet != null) bbHasInitialObjectiveSet.Value = false;
        if (bbIsAttacking != null) bbIsAttacking.Value = false;
        if (bbIsCapturing != null) bbIsCapturing.Value = false;
        if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = false;
        // Laisser FinalDestinationPosition non initialisée ici, ou la mettre à une position "nulle"
        // si nécessaire (par exemple, (-1,-1) si 0,0 est une position valide).
        // Elle sera définie par SetInitialObjectiveFromPosition ou plus tard par le graph.

        // S'abonner aux événements de la bannière
        if (BannerController.Exists)
        {
            BannerController.Instance.AddObserver(this);
            // Si une bannière est déjà active au spawn de l'unité, la prendre comme objectif initial
            if (BannerController.Instance.HasActiveBanner && !hasInitialObjectiveBeenSetThisLife)
                SetInitialObjectiveFromPosition(BannerController.Instance.CurrentBannerPosition);
        }
        spawnFeedbackPlayer = GetComponent<UnitSpawnFeedback>();

        if (spawnFeedbackPlayer != null)
        {
            spawnFeedbackPlayer.PlaySpawnFeedback();
        }
        else
        {
            // Optionnel : Log si le composant de feedback est attendu mais non trouvé
            Debug.LogWarning($"[{name}] UnitSpawnFeedback component non trouvé. Aucun feedback de spawn ne sera joué.");
        }

        // Appeler la méthode Start de la classe de base (Unit)
        // Ceci gère l'attachement à la tuile et l'abonnement à RhythmManager.OnBeat
        //yield return StartCoroutine(base.Start()); // base.Start() devrait appeler OnRhythmBeatInternal qui appelle OnRhythmBeat
    }

    private void CacheBlackboardVariables()
    {
        var blackboardRef = m_Agent.BlackboardReference;
        if (blackboardRef == null) return; // Déjà géré dans Start, mais sécurité

        // Variables liées à la bannière et à l'objectif initial
        blackboardRef.GetVariable(BB_HAS_BANNER_TARGET, out bbHasBannerTarget);
        blackboardRef.GetVariable(BB_BANNER_TARGET_POSITION, out bbBannerTargetPosition);
        blackboardRef.GetVariable(BB_FINAL_DESTINATION_POSITION, out bbFinalDestinationPosition);
        blackboardRef.GetVariable(BB_INITIAL_TARGET_BUILDING, out bbInitialTargetBuilding);
        blackboardRef.GetVariable(BB_HAS_INITIAL_OBJECTIVE_SET, out bbHasInitialObjectiveSet);

        // Variables d'état de l'unité
        blackboardRef.GetVariable(BB_IS_ATTACKING, out bbIsAttacking);
        blackboardRef.GetVariable(BB_IS_CAPTURING, out bbIsCapturing);
        blackboardRef.GetVariable(BB_IS_OBJECTIVE_COMPLETED, out bbIsObjectiveCompleted);

    }

    protected override Vector2Int? TargetPosition
    {
        get
        {
            // Si l'unité est en train d'attaquer ou de capturer, elle ne devrait pas avoir de
            // cible de mouvement active via cette propriété. Le Behavior Graph gère cela.
            bool isCurrentlyAttacking = bbIsAttacking?.Value ?? false;
            bool isCurrentlyCapturing = bbIsCapturing?.Value ?? false;

            if (isCurrentlyAttacking || isCurrentlyCapturing)
            {
                // Si une action est en cours, la cible de mouvement n'est pas pertinente via cette propriété.
                // Le nœud d'action spécifique (Attack, Capture) gère la position de l'unité.
                return null;
            }

            return bbFinalDestinationPosition.Value;

        }
    }

    public void OnBannerPlaced(int column, int row)
    {
        if (m_Agent == null || bbHasBannerTarget == null || bbBannerTargetPosition == null)
        {
            return;
        }

        Vector2Int newBannerPosition = new Vector2Int(column, row);
        bbHasBannerTarget.Value = true;
        bbBannerTargetPosition.Value = newBannerPosition;

        // Si l'objectif initial n'a pas encore été fixé pour cette unité (durant sa vie actuelle)
        if (!hasInitialObjectiveBeenSetThisLife)
        {
            SetInitialObjectiveFromPosition(newBannerPosition);
        }
        // Si l'objectif initial a déjà été fixé, le Behavior Graph décidera s'il doit
        // abandonner l'objectif initial pour suivre la nouvelle position de la bannière.
        // Pour l'instant, FindSmartStepNode utilise toujours FinalDestinationPosition,
        // qui est lié à InitialTargetBuilding. On pourrait ajouter une logique ici ou dans le graph
        // pour redéfinir FinalDestinationPosition si la bannière bouge de manière significative
        // par rapport à l'objectif initial, ou si l'objectif initial est complété/détruit.
    }

    private void SetInitialObjectiveFromPosition(Vector2Int position)
    {
        if (hasInitialObjectiveBeenSetThisLife) // Ne pas redéfinir si déjà fait une fois.
        {
            return;
        }

        if (bbInitialTargetBuilding == null || bbHasInitialObjectiveSet == null || bbFinalDestinationPosition == null)
            return;

        Building buildingAtPos = FindBuildingAtPosition(position); // S'assure que la tuile et le bâtiment existent

        if (buildingAtPos != null)
        {
            UnsubscribeFromInitialBuildingEvents(); // Se désabonner de l'ancien si existant

            initialObjectiveBuildingInstance = buildingAtPos; // Garder une référence directe
            bbInitialTargetBuilding.Value = buildingAtPos;
            bbHasInitialObjectiveSet.Value = true;
            hasInitialObjectiveBeenSetThisLife = true; // Marquer comme défini pour cette vie

            // La destination finale pour le pathfinding devient la tuile de ce bâtiment initial
            Tile buildingTile = buildingAtPos.GetOccupiedTile();
            if (buildingTile != null)
            {
                bbFinalDestinationPosition.Value = new Vector2Int(buildingTile.column, buildingTile.row);
            }
            else
            {
                // Fallback: ne pas définir de FinalDestinationPosition ou la mettre à une position invalide
                bbHasInitialObjectiveSet.Value = false; // Marquer comme échec
                hasInitialObjectiveBeenSetThisLife = false;
                initialObjectiveBuildingInstance = null;
            }
            SubscribeToInitialBuildingEvents();
        }
        else
        {
            bbInitialTargetBuilding.Value = null;
            bbHasInitialObjectiveSet.Value = false;
            // Laisser FinalDestinationPosition telle quelle ou la mettre à la position actuelle de l'unité si pas d'autre cible.
            // Si bbFinalDestinationPosition n'a pas de valeur valide, FindSmartStepNode devrait échouer proprement.
        }
    }

    private void SubscribeToInitialBuildingEvents()
    {
        if (initialObjectiveBuildingInstance != null)
        {
            // S'abonner aux deux événements: destruction ET changement d'équipe
            Building.OnBuildingDestroyed += HandleInitialBuildingEvent;
            Building.OnBuildingTeamChangedGlobal += HandleInitialBuildingTeamChange; // NOUVEL ABONNEMENT
            if (enableVerboseLogging) Debug.Log($"[{name}] Subscribed to OnBuildingDestroyed AND OnBuildingTeamChangedGlobal for '{initialObjectiveBuildingInstance.name}'.");
        }
    }

    private void UnsubscribeFromInitialBuildingEvents()
    {
        if (initialObjectiveBuildingInstance != null) // Toujours vérifier avant de se désabonner
        {
            Building.OnBuildingDestroyed -= HandleInitialBuildingEvent;
            Building.OnBuildingTeamChangedGlobal -= HandleInitialBuildingTeamChange; // NOUVEAU DÉSABONNEMENT
            if (enableVerboseLogging) Debug.Log($"[{name}] Unsubscribed from OnBuildingDestroyed AND OnBuildingTeamChangedGlobal for '{initialObjectiveBuildingInstance.name}'.");
        }
    }

    private void HandleInitialBuildingEvent(Building buildingEventSource)
    {
        // Vérifier si c'est bien notre bâtiment objectif initial ET qu'il a été détruit
        if (buildingEventSource == initialObjectiveBuildingInstance && buildingEventSource.CurrentHealth <= 0)
        {
            SignalObjectiveCompleted(); // Met bbIsObjectiveCompleted à true

            UnsubscribeFromInitialBuildingEvents();
            initialObjectiveBuildingInstance = null;
        }
        // Gérer aussi le cas de la capture par le joueur
        else if (buildingEventSource == initialObjectiveBuildingInstance && buildingEventSource.Team == TeamType.Player &&
                 (initialObjectiveBuildingInstance.Team == TeamType.Neutral || initialObjectiveBuildingInstance.Team == TeamType.Enemy)) // On vient de le capturer
        {
            SignalObjectiveCompleted();

            UnsubscribeFromInitialBuildingEvents();
            initialObjectiveBuildingInstance = null;
        }
    }

    private void HandleInitialBuildingTeamChange(Building buildingEventSource, TeamType oldTeam, TeamType newTeam)
    {
        if (buildingEventSource == initialObjectiveBuildingInstance)
        {
            if (newTeam == TeamType.Player && oldTeam != TeamType.Player) // Si le bâtiment devient allié (et ne l'était pas avant)
            {
                if (enableVerboseLogging) Debug.Log($"[{name}] Initial objective '{buildingEventSource.name}' was CAPTURED by Player (Team changed from {oldTeam} to {newTeam}).");
                SignalObjectiveCompleted();
            }
        }
    }

    private void CheckObjectiveCompletionAndSignal()
    {
        if (bbIsObjectiveCompleted != null && !(bbIsObjectiveCompleted.Value)) // Only signal once
        {
            if (enableVerboseLogging) Debug.Log($"[{name}] Signaling objective completed to Behavior Graph.");
            bbIsObjectiveCompleted.Value = true;
            // The SelectTargetNode will now pick up this flag.
        }
        // Clean up direct instance and subscriptions as objective is resolved
        UnsubscribeFromInitialBuildingEvents();
        initialObjectiveBuildingInstance = null;
        // bbHasInitialObjectiveSet remains true, but bbInitialTargetBuilding might be null or its state changed.
        // The graph needs bbIsObjectiveCompleted to take precedence.
    }

    // --- Helper Methods (Called by Custom Nodes) ---
    public Unit FindNearestEnemyUnit()
    {
        if (occupiedTile == null || HexGridManager.Instance == null) return null;
        var tilesInRange = HexGridManager.Instance.GetTilesWithinRange(occupiedTile.column, occupiedTile.row, DetectionRange);
        Unit nearestUnit = null;
        float nearestDistSq = float.MaxValue;
        Vector3 currentPos = transform.position;
        foreach (var tile in tilesInRange)
        {
            if (tile.currentUnit != null && IsValidUnitTarget(tile.currentUnit))
            {
                float distSq = (tile.currentUnit.transform.position - currentPos).sqrMagnitude;
                if (distSq < nearestDistSq) { nearestDistSq = distSq; nearestUnit = tile.currentUnit; }
            }
        }
        return nearestUnit;
    }

    protected override void OnRhythmBeat()
    {
    }

    public Building FindNearestEnemyBuilding()
    {
        if (occupiedTile == null || HexGridManager.Instance == null) return null;
        var tilesInRange = HexGridManager.Instance.GetTilesWithinRange(occupiedTile.column, occupiedTile.row, DetectionRange);
        Building nearestBuilding = null;
        float nearestDistSq = float.MaxValue;
        Vector3 currentPos = transform.position;
        foreach (var tile in tilesInRange)
        {
            if (tile.currentBuilding != null && tile.currentBuilding.Team == TeamType.Enemy && IsValidBuildingTarget(tile.currentBuilding))
            {
                float distSq = (tile.currentBuilding.transform.position - currentPos).sqrMagnitude;
                if (distSq < nearestDistSq) { nearestDistSq = distSq; nearestBuilding = tile.currentBuilding; }
            }
        }
        return nearestBuilding;
    }

    public Building FindBuildingAtPosition(Vector2Int pos)
    {
        if (HexGridManager.Instance == null)
        {
            Debug.LogError($"[{name}] HexGridManager.Instance is null in FindBuildingAtPosition. Cannot find building.");
            return null;
        }
        Tile targetTile = HexGridManager.Instance.GetTileAt(pos.x, pos.y);
        if (targetTile == null)
        {
            // if (enableVerboseLogging) Debug.LogWarning($"[{name}] No tile found at position ({pos.x},{pos.y}) in FindBuildingAtPosition.");
            return null;
        }
        return targetTile.currentBuilding;
    }

    public Unit FindUnitAtPosition(Vector2Int pos)
    {
        if (HexGridManager.Instance == null) return null;
        Tile targetTile = HexGridManager.Instance.GetTileAt(pos.x, pos.y);
        return targetTile?.currentUnit;
    }

    public override bool IsValidUnitTarget(Unit otherUnit) => otherUnit is EnemyUnit;
    public override bool IsValidBuildingTarget(Building building)
    {
        if (building == null || !building.IsTargetable) return false;
        return building.Team == TeamType.Enemy || building.Team == TeamType.Player || building.Team == TeamType.Neutral;
    }

    public bool PerformCapture(Building building)
    {
        NeutralBuilding neutralBuilding = building as NeutralBuilding;
        if (neutralBuilding == null || !neutralBuilding.IsRecapturable || (neutralBuilding.Team != TeamType.Neutral && neutralBuilding.Team != TeamType.Enemy))
        {
            if (enableVerboseLogging) Debug.Log($"[{name}] Cannot capture {building.name}. Not Neutral or Enemy Recapturable.");
            return false;
        }
        if (!IsBuildingInCaptureRange(neutralBuilding))
        {
            if (enableVerboseLogging) Debug.Log($"[{name}] Cannot capture {building.name}. Out of range.");
            return false;
        }
        currentState = UnitState.Capturing; // Mettre à jour l'état de l'unité

        FaceBuildingTarget(building);
        bool success = neutralBuilding.StartCapture(TeamType.Player, this);

        if (success)
        {
            buildingBeingCaptured = neutralBuilding;
            beatsSpentCapturing = 0;
            return true;
        }
        else
        {
            return false;
        }
    }

    public override void OnCaptureComplete() // Appelée par NeutralBuilding lorsque la capture est terminée par une unité.
{
    Building buildingJustCaptured = this.buildingBeingCaptured; // Récupérer la référence AVANT de la nullifier

    if (enableVerboseLogging) Debug.Log($"[{name}] Received OnCaptureComplete notification for building '{(buildingJustCaptured != null ? buildingJustCaptured.name : "Unknown (was null)")}'.");

    // Réinitialiser l'état de capture de l'unité
    this.buildingBeingCaptured = null;
    this.beatsSpentCapturing = 0;
    // Note: SetState(UnitState.Idle) sera probablement géré par le Behavior Graph
    // lorsque IsCapturing deviendra false et qu'aucune autre action n'est choisie.

    // Mettre à jour le flag IsCapturing sur le Blackboard
    if (bbIsCapturing != null) // Assurez-vous que bbIsCapturing est mis en cache dans AllyUnit
    {
        bbIsCapturing.Value = false;
        if (enableVerboseLogging) Debug.Log($"[{name}] Set Blackboard '{BB_IS_CAPTURING}' to false.");
    }
    else
    {
        Debug.LogWarning($"[{name}] bbIsCapturing not cached/found in OnCaptureComplete. Blackboard flag not updated.");
    }

    if (initialObjectiveBuildingInstance != null && buildingJustCaptured == initialObjectiveBuildingInstance)
    {
        if (enableVerboseLogging) Debug.Log($"[{name}] Unit that completed capture: '{buildingJustCaptured.name}' WAS the initial objective. Ensuring SignalObjectiveCompleted.");
        SignalObjectiveCompleted(); // S'assure que c'est signalé, même si l'event global le ferait aussi.
    }
    else
    {
        if (enableVerboseLogging && initialObjectiveBuildingInstance != null)
        {
            Debug.LogWarning($"[{name}] Captured building '{(buildingJustCaptured != null ? buildingJustCaptured.name : "N/A")}' was NOT the initial objective '{initialObjectiveBuildingInstance.name}'.");
        }
        else if (enableVerboseLogging && initialObjectiveBuildingInstance == null)
        {
             Debug.Log($"[{name}] Captured building '{(buildingJustCaptured != null ? buildingJustCaptured.name : "N/A")}'. No initial objective was set or it was already cleared.");
        }
    }

}

    private void SignalObjectiveCompleted()
    {
        if (bbIsObjectiveCompleted != null && !bbIsObjectiveCompleted.Value)
        {
            if (enableVerboseLogging) Debug.Log($"[{name}] Signaling objective completed to Behavior Graph (setting '{BB_IS_OBJECTIVE_COMPLETED}' to true).");
            bbIsObjectiveCompleted.Value = true;
            UnsubscribeFromInitialBuildingEvents(); // Important de le faire ici
            initialObjectiveBuildingInstance = null;
        }
        else if (bbIsObjectiveCompleted == null)
        {
            Debug.LogError($"[{name}] Cannot signal objective completion: bbIsObjectiveCompleted is not cached!");
        }
    }


// De même pour StopCapturing() dans AllyUnit.cs
public override void StopCapturing()
{
    if (currentState == UnitState.Capturing || buildingBeingCaptured != null)
    {
        if (enableVerboseLogging) Debug.Log($"[{name}] StopCapturing called. Was capturing: '{(buildingBeingCaptured != null ? buildingBeingCaptured.name : "N/A")}'");

        Building buildingWeWereCapturing = this.buildingBeingCaptured;

        if (buildingWeWereCapturing != null)
        {
            // Important: S'assurer que buildingWeWereCapturing est bien un NeutralBuilding avant de caster
            NeutralBuilding nb = buildingWeWereCapturing as NeutralBuilding;
            if (nb != null)
            {
                nb.StopCapturing(this); // Notifier le bâtiment
            }
            else
            {
                Debug.LogWarning($"[{name}] Attempted to stop capturing a building '{buildingWeWereCapturing.name}' that is not a NeutralBuilding type.");
            }
        }

        this.buildingBeingCaptured = null;
        this.beatsSpentCapturing = 0;
        // SetState(UnitState.Idle); // Laisser le graph décider

        if (bbIsCapturing != null)
        {
            bbIsCapturing.Value = false;
             if (enableVerboseLogging) Debug.Log($"[{name}] Set Blackboard '{BB_IS_CAPTURING}' to false via StopCapturing.");
        }
        else
        {
            Debug.LogWarning($"[{name}] bbIsCapturing not cached/found in StopCapturing. Blackboard flag not updated.");
        }
        // isInteractingWithBuilding = false; // Laisser le graph gérer cela aussi

    }
}

    public override void OnMovementComplete()
    {
        base.OnMovementComplete();
        if (enableVerboseLogging) Debug.Log($"[{name}] Movement Complete - Graph will now check interactions.");
    }

    public override void OnDestroy()
    {
        if (AllyUnitRegistry.Instance != null)
        {
            AllyUnitRegistry.Instance.UnregisterUnit(this);
            if (enableVerboseLogging && Application.isPlaying) // Vérifier Application.isPlaying pour éviter les logs en sortie d'éditeur
            {
                Debug.Log($"[{name}] s'est désenregistré de AllyUnitRegistry.");
            }
        }

        UnsubscribeFromInitialBuildingEvents(); // Ensure cleanup
        initialObjectiveBuildingInstance = null;
        base.OnDestroy();

    }

}
