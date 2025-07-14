using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Game.Observers;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using Gameplay;

/// <summary>
/// Represents an allied unit controlled by the player.
/// Uses Unity's Behavior Graph system for AI-driven behavior and responds to banner commands.
/// Supports defensive positioning at player buildings and objective-based gameplay.
/// </summary>
public class AllyUnit : Unit, IBannerObserver
{
    // --- Behavior Graph Agent Reference ---
    [Header("Behavior Graph")]
    /// <summary>
    /// The Behavior Graph Agent component for AI decision making.
    /// </summary>
    [Tooltip("Assign the Behavior Graph Agent component from this GameObject here.")]
    [SerializeField] private BehaviorGraphAgent m_Agent;
    
    /// <summary>
    /// Gets the blackboard reference for AI communication.
    /// </summary>
    public BlackboardReference Blackboard => m_Agent?.BlackboardReference;

    // --- Blackboard Variable Cache ---
    // Blackboard Keys
    /// <summary>Blackboard key for banner target availability.</summary>
    private const string BB_HAS_BANNER_TARGET = "HasBannerTarget";
    /// <summary>Blackboard key for banner target position.</summary>
    private const string BB_BANNER_TARGET_POSITION = "BannerTargetPosition";
    /// <summary>Blackboard key for final destination position used by pathfinding.</summary>
    private const string BB_FINAL_DESTINATION_POSITION = "FinalDestinationPosition";
    /// <summary>Blackboard key for initial target building.</summary>
    private const string BB_INITIAL_TARGET_BUILDING = "InitialTargetBuilding";
    /// <summary>Blackboard key for initial objective set status.</summary>
    private const string BB_HAS_INITIAL_OBJECTIVE_SET = "HasInitialObjectiveSet";
    /// <summary>Blackboard key for attacking state.</summary>
    private const string BB_IS_ATTACKING = "IsAttacking";
    /// <summary>Blackboard key for capturing state.</summary>
    private const string BB_IS_CAPTURING = "IsCapturing";
    /// <summary>Blackboard key for defending state.</summary>
    private const string BB_IS_DEFENDING = "IsDefending";
    /// <summary>Blackboard key for objective completion status.</summary>
    private const string BB_IS_OBJECTIVE_COMPLETED = "IsObjectiveCompleted";

    // Cached Blackboard Variables
    /// <summary>Cached blackboard variable for banner target availability.</summary>
    private BlackboardVariable<bool> bbHasBannerTarget;
    /// <summary>Cached blackboard variable for banner target position.</summary>
    private BlackboardVariable<Vector2Int> bbBannerTargetPosition;
    /// <summary>Cached blackboard variable for final destination position.</summary>
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    /// <summary>Cached blackboard variable for initial target building.</summary>
    private BlackboardVariable<Building> bbInitialTargetBuilding;
    /// <summary>Cached blackboard variable for initial objective set status.</summary>
    private BlackboardVariable<bool> bbHasInitialObjectiveSet;
    /// <summary>Cached blackboard variable for attacking state.</summary>
    private BlackboardVariable<bool> bbIsAttacking;
    /// <summary>Cached blackboard variable for capturing state.</summary>
    private BlackboardVariable<bool> bbIsCapturing;
    /// <summary>Cached blackboard variable for objective completion status.</summary>
    private BlackboardVariable<bool> bbIsObjectiveCompleted;
    /// <summary>Cached blackboard variable for defending state.</summary>
    private BlackboardVariable<bool> bbIsDefending;
    /// <summary>Cached blackboard variable for defended building attack status.</summary>
    private BlackboardVariable<bool> bbDefendedBuildingIsUnderAttack;
    /// <summary>Cached blackboard variable for detected enemy unit.</summary>
    private BlackboardVariable<Unit> bbDetectedEnemyUnit;

    [Header("Ally Settings")]
    /// <summary>
    /// Enable verbose logging for debugging. Public so nodes can check this value.
    /// </summary>
    [SerializeField] public bool enableVerboseLogging = true;

    /// <summary>
    /// Direct reference to the initial objective building instance.
    /// </summary>
    private Building initialObjectiveBuildingInstance;
    
    /// <summary>
    /// Whether the initial objective has been set during this unit's lifetime.
    /// </summary>
    private bool hasInitialObjectiveBeenSetThisLife = false;
    
    /// <summary>
    /// Component for handling unit spawn feedback.
    /// </summary>
    private UnitSpawnFeedback spawnFeedbackPlayer;
    
    /// <summary>
    /// The player building this unit is currently defending.
    /// </summary>
    public PlayerBuilding currentReserveBuilding;
    
    /// <summary>
    /// The tile this unit is defending at the player building.
    /// </summary>
    public Tile currentReserveTile;

    /// <summary>
    /// Amount of momentum gained when this unit completes its objective.
    /// </summary>
    public float MomentumGainOnObjectiveComplete; 

    protected override IEnumerator Start()
    {
        yield return StartCoroutine(base.Start());

        if (m_Agent == null) m_Agent = GetComponent<BehaviorGraphAgent>();
        spawnFeedbackPlayer = GetComponent<UnitSpawnFeedback>();

        if (m_Agent == null)
        {
            Debug.LogError($"[{name}] BehaviorGraphAgent component not found on {gameObject.name}! AI will not run.", gameObject);
            SetSpawningState(false);
            yield break;
        }


        if (spawnFeedbackPlayer != null)
        {
            LogAlly($"SpawnFeedbackPlayer trouvé. Désactivation de l'agent '{m_Agent.name}' pour la séquence de spawn.");
            m_Agent.enabled = false; 

            bool localSpawnCompletedSignal = false; // Flag local pour la coroutine Start
            spawnFeedbackPlayer.OnSpawnCompleted += () => {
                localSpawnCompletedSignal = true;
                LogAlly("Signal OnSpawnCompleted reçu de UnitSpawnFeedback.");
            };

            LogAlly("Lancement de PlaySpawnFeedback...");
            spawnFeedbackPlayer.PlaySpawnFeedback(); // Lance la séquence de feedback

            LogAlly("En attente de la fin de la séquence de spawn (WaitUntil)...");
            yield return new WaitUntil(() => localSpawnCompletedSignal); // Attend que le feedback soit terminé
            LogAlly("Fin de la séquence de spawn signalée.");
        }
        else
        {
            LogAlly("UnitSpawnFeedback non trouvé. Passage direct à l'initialisation de l'IA.", true); // isWarning = true
            SetSpawningState(false); 
        }
        
        LogAlly($"Réactivation de l'agent '{m_Agent.name}'.");
        m_Agent.enabled = true;

        if (m_Agent.BlackboardReference == null)
        {
            Debug.LogError($"[{name}] BlackboardReference est null sur BehaviorGraphAgent APRES l'avoir réactivé! AI pourrait mal fonctionner.", gameObject);
            // Pas de yield break ici, car le reste de l'initialisation pourrait être utile
        }
        else
        {
            LogAlly("Mise en cache des variables Blackboard...");
            CacheBlackboardVariables();
            InitializeBlackboardFlags();
            Building.OnBuildingAttackedByUnit += HandleBuildingAttackedByUnit;
        }

        if (AllyUnitRegistry.Instance != null)
        {
            AllyUnitRegistry.Instance.RegisterUnit(this);
            LogAlly($"Enregistré auprès de AllyUnitRegistry.");
        }
        else
        {
            Debug.LogWarning($"[{name}] AllyUnitRegistry.Instance non trouvé. Impossible de s'enregistrer.");
        }

        if (BannerController.Exists)
        {
            BannerController.Instance.AddObserver(this);
            LogAlly("Abonné à BannerController.");
            if (BannerController.Instance.HasActiveBanner && !hasInitialObjectiveBeenSetThisLife)
            {
                LogAlly("Bannière active détectée au spawn, définition de l'objectif initial.");
            }
        }
        else
        {
            LogAlly("BannerController n'existe pas. Pas d'abonnement ni d'objectif initial via bannière.", true);
        }
        LogAlly("Initialisation de AllyUnit terminée. L'IA devrait prendre le relais.");
    }
    
    private void InitializeBlackboardFlags()
    {
        if(m_Agent == null || m_Agent.BlackboardReference == null) {
            LogAlly("Impossible d'initialiser les flags du Blackboard, agent ou référence null.", true);
            return;
        }
        LogAlly("Initialisation des flags du Blackboard...");
        if (bbHasBannerTarget != null) bbHasBannerTarget.Value = false;
        else LogAlly("bbHasBannerTarget non mis en cache.", true);

        if (bbHasInitialObjectiveSet != null) bbHasInitialObjectiveSet.Value = false;
        else LogAlly("bbHasInitialObjectiveSet non mis en cache.", true);

        if (bbIsAttacking != null) bbIsAttacking.Value = false;
        else LogAlly("bbIsAttacking non mis en cache.", true);

        if (bbIsCapturing != null) bbIsCapturing.Value = false;
        else LogAlly("bbIsCapturing non mis en cache.", true);

        if (bbIsDefending != null) bbIsDefending.Value = false; // Ajouté
        else LogAlly("bbIsDefending non mis en cache.", true); // Ajouté

        if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = false;
        else LogAlly("bbIsObjectiveCompleted non mis en cache.", true);
        LogAlly("Flags du Blackboard initialisés.");
    }

    private void LogAlly(string message, bool isWarning = false)
    {
        if (enableVerboseLogging || isWarning)
        {
            string logMessage = $"[{name}] {message}";
            if (isWarning) Debug.LogWarning(logMessage, gameObject);
            else Debug.Log(logMessage, gameObject);
        }
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
        if (!blackboardRef.GetVariable(BB_IS_DEFENDING, out bbIsDefending))
        {
            LogAlly($"La variable Blackboard '{BB_IS_DEFENDING}' est manquante. La logique de défense pourrait ne pas fonctionner.", true);
        }
        // Nouvelle variable Blackboard pour la défense du bâtiment
        if (!blackboardRef.GetVariable("DefendedBuildingIsUnderAttack", out bbDefendedBuildingIsUnderAttack))
        {
            LogAlly("Blackboard variable 'DefendedBuildingIsUnderAttack' is missing. Defensive alert logic may not work.", true);
        }
        // Nouvelle variable Blackboard pour l'ennemi détecté
        if (!blackboardRef.GetVariable("DetectedEnemyUnit", out bbDetectedEnemyUnit))
        {
            LogAlly("Blackboard variable 'DetectedEnemyUnit' is missing. Enemy detection logic may not work.", true);
        }
        // Variables d'état de l'unité
        blackboardRef.GetVariable(BB_IS_ATTACKING, out bbIsAttacking);
        blackboardRef.GetVariable(BB_IS_CAPTURING, out bbIsCapturing);
        blackboardRef.GetVariable(BB_IS_OBJECTIVE_COMPLETED, out bbIsObjectiveCompleted);
        
        
    }

    /// <summary>
    /// Gets the target position for this unit's movement.
    /// Returns null if the unit is currently attacking or capturing.
    /// </summary>
    protected override Vector2Int? TargetPosition
    {
        get
        {
            // If the unit is attacking or capturing, it shouldn't have an active movement target.
            // The Behavior Graph handles this.
            bool isCurrentlyAttacking = bbIsAttacking?.Value ?? false;
            bool isCurrentlyCapturing = bbIsCapturing?.Value ?? false;

            if (isCurrentlyAttacking || isCurrentlyCapturing)
            {
                // If an action is in progress, the movement target is not relevant via this property.
                // The specific action node (Attack, Capture) handles the unit's position.
                return null;
            }

            return bbFinalDestinationPosition.Value;
        }
    }

    /// <summary>
    /// Called when a banner is placed by the player. Updates the unit's banner target.
    /// </summary>
    /// <param name="column">The column position of the banner.</param>
    /// <param name="row">The row position of the banner.</param>
    public void OnBannerPlaced(int column, int row)
    {
        if (m_Agent == null || bbHasBannerTarget == null || bbBannerTargetPosition == null)
        {
            return;
        }

        Vector2Int newBannerPosition = new Vector2Int(column, row);
        bbHasBannerTarget.Value = true;
        bbBannerTargetPosition.Value = newBannerPosition;

        // If the initial objective hasn't been set for this unit yet (during its current lifetime)
        if (!hasInitialObjectiveBeenSetThisLife)
    	{
        	LogAlly($"Définition de l'objectif initial à la position de la bannière: ({newBannerPosition.x},{newBannerPosition.y})");
    	}
    	else
		{
            LogAlly($"bbHasInitialObjectiveSet : {bbHasInitialObjectiveSet.Value}, bbInitialTargetBuilding : {bbInitialTargetBuilding.Value?.name ?? "null"}");
        	LogAlly($"Bannière déplacée vers ({newBannerPosition.x},{newBannerPosition.y}) mais objectif déjà fixé - ignoré.");
    	}
        // If the initial objective has already been set, the Behavior Graph will decide
        // whether to abandon the initial objective to follow the new banner position.
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
                Debug.LogWarning($"[{name}] Building '{buildingAtPos.name}' has no occupied tile. Setting FinalDestinationPosition to an invalid position.");
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
            LogAlly("Aucun bâtiment trouvé à la position de la bannière. Objectif initial non défini.", true);
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
    /// <summary>
    /// Finds the nearest enemy unit within detection range.
    /// </summary>
    /// <returns>The nearest enemy unit, or null if none found.</returns>
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

    protected override void OnRhythmBeat(float beatDuration)
    {
        // La logique de cette méthode était vide dans la classe AllyUnit, donc rien à changer à l'intérieur.
        // Seule la signature est importante pour la compilation.
    }

    /// <summary>
    /// Finds the nearest enemy building within detection range.
    /// </summary>
    /// <returns>The nearest enemy building, or null if none found.</returns>
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

    /// <summary>
    /// Determines if another unit is a valid target for this ally unit.
    /// </summary>
    /// <param name="otherUnit">The unit to check.</param>
    /// <returns>True if the unit is an enemy unit.</returns>
    public override bool IsValidUnitTarget(Unit otherUnit) => otherUnit is EnemyUnit;
    
    /// <summary>
    /// Determines if a building is a valid target for this ally unit.
    /// </summary>
    /// <param name="building">The building to check.</param>
    /// <returns>True if the building belongs to the enemy team.</returns>
    public override bool IsValidBuildingTarget(Building building)
    {
        if (building == null || !building.IsTargetable) return false;

        return building.Team == TeamType.Enemy;
    }
    /// <summary>
    /// Determines if a building is a valid capture target for this ally unit.
    /// </summary>
    /// <param name="building">The building to check.</param>
    /// <returns>True if the building can be captured by allied units.</returns>
    public bool IsValidCaptureTarget(Building building)
    {
        if (building == null) return false;

        NeutralBuilding neutralBuilding = building as NeutralBuilding;
        if (neutralBuilding == null) return false;

        return neutralBuilding.IsRecapturable &&
               (neutralBuilding.Team == TeamType.Neutral || neutralBuilding.Team == TeamType.NeutralEnemy);
    }
    public override  bool PerformCapture(Building building)
    {
        NeutralBuilding neutralBuilding = building as NeutralBuilding;
        if (neutralBuilding == null || !neutralBuilding.IsRecapturable || (neutralBuilding.Team != TeamType.Neutral && neutralBuilding.Team != TeamType.NeutralEnemy))
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

    // Handler pour l'événement d'attaque sur bâtiment
    private void HandleBuildingAttackedByUnit(Building building, Unit attacker)
    {
        // Si cette unité défend ce bâtiment, on met à jour la variable Blackboard
        if (currentReserveBuilding != null && building == currentReserveBuilding)
        {
            if (bbDefendedBuildingIsUnderAttack != null)
            {
                bbDefendedBuildingIsUnderAttack.Value = true;
                if (enableVerboseLogging)
                    Debug.Log($"[{name}] Defended building '{building.name}' is under attack! Blackboard flag set.");
            }
        }
    }

    /// <summary>
    /// Appelée directement par le PlayerBuilding lorsqu'il est attaqué, pour notifier cette unité si elle défend ce bâtiment.
    /// </summary>
    public void OnDefendedBuildingAttacked(Building building, Unit attacker)
    {
        if (currentReserveBuilding != null && building == currentReserveBuilding)
        {
            if (bbDefendedBuildingIsUnderAttack != null)
            {
                bbDefendedBuildingIsUnderAttack.Value = true;
                if (enableVerboseLogging)
                    Debug.Log($"[{name}] (Direct) Defended building '{building.name}' is under attack by '{attacker?.name ?? "Unknown"}'. Blackboard flag set.");
                //on va devoir aussi faire en sorte d'enregistrer l'attaquant dans le BB, on peut utiliser bbDetectedEnemyUnit 
                if (bbDetectedEnemyUnit != null && attacker != null && IsValidUnitTarget(attacker))
                {
                    bbDetectedEnemyUnit.Value = attacker; // Enregistrer l'attaquant dans le BB
                    if (enableVerboseLogging)
                        Debug.Log($"[{name}] Detected enemy unit '{attacker.name}' attacking defended building '{building.name}'.");
                }
            }
        }
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
        ClearCurrentReservePosition();
        initialObjectiveBuildingInstance = null;

        // Désabonnement de l'événement d'attaque sur bâtiment
        Building.OnBuildingAttackedByUnit -= HandleBuildingAttackedByUnit;

        base.OnDestroy();

    }
	
     public void SetReservePosition(PlayerBuilding building, Tile reserveTile)
    {
        // 1. Si on avait une ancienne position de réserve (différente ou sur un autre bâtiment), la libérer.
        if (currentReserveBuilding != null && currentReserveTile != null)
        {
            // Si le nouveau bâtiment est différent OU si la nouvelle tuile est différente dans le même bâtiment
            if (currentReserveBuilding != building || currentReserveTile != reserveTile)
            {
                currentReserveBuilding.ReleaseReserveTile(currentReserveTile, this);
                LogAlly($"Ancienne position de réserve ({currentReserveTile.column},{currentReserveTile.row}) chez {currentReserveBuilding.name} libérée.");
            }
        }

        // 2. Assigner la nouvelle position de réserve
        currentReserveBuilding = building;
        currentReserveTile = reserveTile;

        if (currentReserveBuilding != null && currentReserveTile != null)
        {
            // L'assignation effective sur le bâtiment est importante
            bool success = currentReserveBuilding.AssignUnitToReserveTile(this, currentReserveTile);
            if (success) {
                LogAlly($"Nouvelle position de réserve assignée : ({currentReserveTile.column},{currentReserveTile.row}) chez {currentReserveBuilding.name}.");
            } else {
                LogAlly($"ÉCHEC de l'assignation à la position de réserve ({currentReserveTile.column},{currentReserveTile.row}) chez {currentReserveBuilding.name}.", true);
                // Si l'assignation échoue, on devrait peut-être nullifier currentReserveTile/Building pour éviter des états incohérents
                this.currentReserveBuilding = null;
                this.currentReserveTile = null;
            }
        } else {
            LogAlly("Tentative de SetReservePosition avec building ou tile null.", true);
        }
    }

    public void ClearCurrentReservePosition()
    {
        if (currentReserveBuilding != null && currentReserveTile != null)
        {
            currentReserveBuilding.ReleaseReserveTile(currentReserveTile, this);
        }
        
        currentReserveBuilding = null;
        currentReserveTile = null;
    }

    // Override de OnMovementComplete pour gérer les réserves
    public override void OnMovementComplete()
    {
        base.OnMovementComplete();
        
        // Si on arrive sur une case de réserve, s'assurer qu'elle est bien assignée
        if (currentReserveTile != null && occupiedTile == currentReserveTile)
        {
            if (enableVerboseLogging) 
                Debug.Log($"[{name}] Arrived at reserve position ({currentReserveTile.column},{currentReserveTile.row})");
        }
    }

}
