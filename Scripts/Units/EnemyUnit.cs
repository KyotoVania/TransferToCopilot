using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using ScriptableObjects;

/// <summary>
/// Represents an enemy unit controlled by AI behavior graphs.
/// Provides a virtual switch mechanism for derived classes (like Boss) to override AI behavior.
/// Supports both standard AI behavior trees and hardcoded behavior for specialized units.
/// </summary>
public class EnemyUnit : Unit
{
    /// <summary>
    /// Virtual switch that allows derived classes (like Boss) to specify they have their own logic
    /// and should not use the behavior tree system.
    /// </summary>
    protected virtual bool IsHardcoded => false;

    [Header("Behavior Graph")]
    /// <summary>
    /// The Behavior Graph Agent component for AI decision making.
    /// </summary>
    [Tooltip("Assigner le Behavior Graph Agent de cet GameObject ici.")]
    [SerializeField] private BehaviorGraphAgent m_Agent;

    [Header("Enemy Settings")]
    /// <summary>
    /// Enable verbose logging for debugging enemy behavior.
    /// </summary>
    [SerializeField] public bool enableVerboseLogging = true;
   

    /// <summary>
    /// Initial behavior mode for the enemy unit.
    /// </summary>
    [Tooltip("Mode de comportement initial de l'unité.")]
    [SerializeField] private CurrentBehaviorMode initialBehaviorMode = CurrentBehaviorMode.Defensive;

    // --- Blackboard Keys ---
    /// <summary>Blackboard key for self unit reference.</summary>
    public const string BB_SELF_UNIT = "SelfUnit";
    /// <summary>Blackboard key for current behavior mode.</summary>
    public const string BB_CURRENT_BEHAVIOR_MODE = "CurrentBehaviorMode";
    /// <summary>Blackboard key for objective building target.</summary>
    public const string BB_OBJECTIVE_BUILDING = "ObjectiveBuilding";
    /// <summary>Blackboard key for detected player unit.</summary>
    public const string BB_DETECTED_PLAYER_UNIT = "DetectedPlayerUnit";
    /// <summary>Blackboard key for detected targetable building.</summary>
    public const string BB_DETECTED_TARGETABLE_BUILDING = "DetectedTargetableBuilding";
    /// <summary>Blackboard key for selected action type.</summary>
    public const string BB_SELECTED_ACTION_TYPE = "SelectedActionType";
    /// <summary>Blackboard key for movement target position.</summary>
    public const string BB_MOVEMENT_TARGET_POSITION = "FinalDestinationPosition";
    /// <summary>Blackboard key for interaction target unit.</summary>
    public const string BB_INTERACTION_TARGET_UNIT = "InteractionTargetUnit";
    /// <summary>Blackboard key for interaction target building.</summary>
    public const string BB_INTERACTION_TARGET_BUILDING = "InteractionTargetBuilding";
    /// <summary>Blackboard key for moving state.</summary>
    public const string BB_IS_MOVING = "IsMoving";
    /// <summary>Blackboard key for attacking state.</summary>
    public const string BB_IS_ATTACKING = "IsAttacking";
    /// <summary>Blackboard key for capturing state.</summary>
    public const string BB_IS_CAPTURING = "IsCapturing";
    /// <summary>Blackboard key for objective completion status.</summary>
    public const string BB_IS_OBJECTIVE_COMPLETED = "IsObjectiveCompleted";
    /// <summary>Blackboard key for pathfinding failure status.</summary>
    public const string BB_PATHFINDING_FAILED = "PathfindingFailed";
    /// <summary>Blackboard key for current pathfinding path.</summary>
    public const string BB_CURRENT_PATH = "CurrentPath";

    // Cached Blackboard Variables
    /// <summary>Cached blackboard variable for self unit reference.</summary>
    private BlackboardVariable<Unit> bbSelfUnit;
    /// <summary>Cached blackboard variable for current behavior mode.</summary>
    private BlackboardVariable<CurrentBehaviorMode> bbCurrentBehaviorMode;
    /// <summary>Cached blackboard variable for objective building target.</summary>
    private BlackboardVariable<Building> bbObjectiveBuilding;
    /// <summary>Cached blackboard variable for detected player unit.</summary>
    private BlackboardVariable<Unit> bbDetectedPlayerUnit;
    /// <summary>Cached blackboard variable for detected targetable building.</summary>
    private BlackboardVariable<Building> bbDetectedTargetableBuilding;
    /// <summary>Cached blackboard variable for selected action type.</summary>
    private BlackboardVariable<AIActionType> bbSelectedActionType;
    /// <summary>Cached blackboard variable for movement target position.</summary>
    private BlackboardVariable<Vector2Int> bbMovementTargetPosition;
    /// <summary>Cached blackboard variable for interaction target unit.</summary>
    private BlackboardVariable<Unit> bbInteractionTargetUnit;
    /// <summary>Cached blackboard variable for interaction target building.</summary>
    private BlackboardVariable<Building> bbInteractionTargetBuilding;
    /// <summary>Cached blackboard variable for moving state.</summary>
    private BlackboardVariable<bool> bbIsMoving;
    /// <summary>Cached blackboard variable for attacking state.</summary>
    private BlackboardVariable<bool> bbIsAttacking;
    /// <summary>Cached blackboard variable for capturing state.</summary>
    private BlackboardVariable<bool> bbIsCapturing;
    /// <summary>Cached blackboard variable for objective completion status.</summary>
    private BlackboardVariable<bool> bbIsObjectiveCompleted;
    /// <summary>Cached blackboard variable for pathfinding failure status.</summary>
    private BlackboardVariable<bool> bbPathfindingFailed;
    /// <summary>Cached blackboard variable for current pathfinding path.</summary>
    private BlackboardVariable<List<Vector2Int>> bbCurrentPath;

    /// <summary>
    /// Called when the unit is enabled. Activates AI behavior only if the unit is not hardcoded.
    /// </summary>
    protected override void OnEnable()
    {
        // On n'active la logique de l'IA (Behavior Graph) que si l'unité N'EST PAS hardcodée.
        if (!IsHardcoded)
        {
            // C'est la logique originale de la classe Unit pour s'abonner au rythme.
            // On ne l'appelle que pour les ennemis normaux. Le boss gèrera son propre abonnement.
            base.OnEnable();

            // Logique spécifique pour activer l'agent de l'IA.
            if (m_Agent != null && isAttached)
            {
                m_Agent.enabled = true;
                if (enableVerboseLogging) Debug.Log($"[{name}] Agent de comportement activé car IsHardcoded est false.");
            }
        }
        else
        {
             if (enableVerboseLogging) Debug.Log($"[{name}] Agent de comportement NON activé car IsHardcoded est true.");
        }
    }
    /// <summary>
    /// Initializes the enemy unit and registers it with the EnemyRegistry.
    /// </summary>
    protected override void Awake()
  	{
      if (EnemyRegistry.Instance != null)
      {
          EnemyRegistry.Instance.Register(this);
          if (enableVerboseLogging) Debug.Log($"[{name}] EnemyUnit.Awake: Enregistrement immédiat dans EnemyRegistry.");
      }

      base.Awake();
  	}
    /// <summary>
    /// Initializes the enemy unit components and blackboard variables.
    /// </summary>
    /// <returns>Coroutine for initialization process.</returns>
    protected override IEnumerator Start()
    {
        if (m_Agent == null) m_Agent = GetComponent<BehaviorGraphAgent>();
		Debug.Log($"[{name}] EnemyUnit.Start: Composant BehaviorGraphAgent récupéré");
        // Si l'unité EST hardcodée, on désactive l'agent pour être absolument sûr qu'il ne s'exécute pas.
        if(IsHardcoded && m_Agent != null)
        {
            m_Agent.enabled = false;
        }

        if (m_Agent == null && !IsHardcoded)
        {
            Debug.LogError($"[{name}] EnemyUnit.Start: BehaviorGraphAgent component not found! AI will not run.", gameObject);
            yield break;
        }

        if (m_Agent != null && m_Agent.BlackboardReference == null && !IsHardcoded)
        {
            Debug.LogError($"[{name}] EnemyUnit.Start: BlackboardReference is null on BehaviorGraphAgent! AI may not function correctly.", gameObject);
        }

        if (enableVerboseLogging) Debug.Log($"[{name}] EnemyUnit.Start: Début du processus d'initialisation. IsHardcoded: {IsHardcoded}");

        yield return StartCoroutine(base.Start());
	
		Debug.Log($"[{name}] EnemyUnit.Start: Processus de base terminé. devrait etre entrain de start les CharacterStatSheets.");

        if (CharacterStatSheets != null)
        {
            InitializeFromCharacterStatsSheets(CharacterStatSheets);
			Debug.Log($"[{name}] EnemyUnit.Start: Initialisation des CharacterStatSheets terminée.");
        }
        else
        {
            Debug.LogError($"[{name}] EnemyUnit.Start: Aucun CharacterStatSheets n'est assigné !", gameObject);
            yield break;
        }	

        if (this.isAttached)
        {
            // On initialise le blackboard uniquement pour les unités non-hardcodées.
            if (!IsHardcoded)
            {
                 if (enableVerboseLogging) Debug.Log($"[{name}] EnemyUnit.Start: Unité attachée, initialisation du Blackboard.");
                 CacheBlackboardVariables();
                 InitializeBlackboardValues();
            }
        }
        else
        {
            Debug.LogError($"[{name}] EnemyUnit.Start: ÉCHEC de l'attachement à une tuile.", gameObject);
        }


        if (enableVerboseLogging)
            Debug.Log($"[{name}] EnemyUnit.Start: Processus d'initialisation terminé.");
    }

    /// <summary>
    /// Initializes blackboard variables with default values.
    /// </summary>
    private void InitializeBlackboardValues()
    {
        if (bbCurrentBehaviorMode != null) bbCurrentBehaviorMode.Value = initialBehaviorMode;
        if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = false;
        if (bbIsMoving != null) bbIsMoving.Value = false;
        if (bbIsAttacking != null) bbIsAttacking.Value = false;
        if (bbIsCapturing != null) bbIsCapturing.Value = false;
        if (bbPathfindingFailed != null) bbPathfindingFailed.Value = false;
        if (bbCurrentPath != null) bbCurrentPath.Value = new List<Vector2Int>();
    }

    #region Blackboard and AI Methods
    /// <summary>
    /// Caches blackboard variables for improved performance during gameplay.
    /// </summary>
    private void CacheBlackboardVariables()
    {
        if (m_Agent == null || m_Agent.BlackboardReference == null)
        {
            Debug.LogError($"[{name}] CacheBlackboardVariables: BehaviorGraphAgent or its BlackboardReference is null. Cannot cache variables.", gameObject);
            return;
        }
        var blackboard = m_Agent.BlackboardReference;
        if (!blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_SELF_UNIT}' not found.", gameObject);
        if (!blackboard.GetVariable(BB_CURRENT_BEHAVIOR_MODE, out bbCurrentBehaviorMode))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_CURRENT_BEHAVIOR_MODE}' not found.", gameObject);
        if (!blackboard.GetVariable(BB_OBJECTIVE_BUILDING, out bbObjectiveBuilding))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_OBJECTIVE_BUILDING}' not found.", gameObject);
        if (!blackboard.GetVariable(BB_IS_OBJECTIVE_COMPLETED, out bbIsObjectiveCompleted))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_IS_OBJECTIVE_COMPLETED}' not found.", gameObject);
        if (!blackboard.GetVariable(BB_DETECTED_PLAYER_UNIT, out bbDetectedPlayerUnit))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_DETECTED_PLAYER_UNIT}' not found.", gameObject);
        if (!blackboard.GetVariable(BB_DETECTED_TARGETABLE_BUILDING, out bbDetectedTargetableBuilding))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_DETECTED_TARGETABLE_BUILDING}' not found.", gameObject);
        if (!blackboard.GetVariable(BB_SELECTED_ACTION_TYPE, out bbSelectedActionType))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_SELECTED_ACTION_TYPE}' not found.", gameObject);
        if (!blackboard.GetVariable(BB_MOVEMENT_TARGET_POSITION, out bbMovementTargetPosition))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_MOVEMENT_TARGET_POSITION}' not found.", gameObject);
        if (!blackboard.GetVariable(BB_INTERACTION_TARGET_UNIT, out bbInteractionTargetUnit))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_INTERACTION_TARGET_UNIT}' not found.", gameObject);
        if (!blackboard.GetVariable(BB_INTERACTION_TARGET_BUILDING, out bbInteractionTargetBuilding))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_INTERACTION_TARGET_BUILDING}' not found.", gameObject);
        if (!blackboard.GetVariable(BB_IS_MOVING, out bbIsMoving))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_IS_MOVING}' not found.", gameObject);
        if (!blackboard.GetVariable(BB_IS_ATTACKING, out bbIsAttacking))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_IS_ATTACKING}' not found.", gameObject);
        if (!blackboard.GetVariable(BB_IS_CAPTURING, out bbIsCapturing))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_IS_CAPTURING}' not found.", gameObject);
        if (!blackboard.GetVariable(BB_PATHFINDING_FAILED, out bbPathfindingFailed))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_PATHFINDING_FAILED}' not found.", gameObject);
        if (!blackboard.GetVariable(BB_CURRENT_PATH, out bbCurrentPath))
            Debug.LogWarning($"[{name}] Blackboard variable '{BB_CURRENT_PATH}' not found.", gameObject);
    }

    /// <summary>
    /// Update method for enemy unit behavior.
    /// </summary>
    private void Update() { }

    /// <summary>
    /// Gets the target position for enemy unit movement from blackboard.
    /// </summary>
    protected override Vector2Int? TargetPosition
    {
        get
        {
            if (m_Agent != null && m_Agent.BlackboardReference != null)
            {
                BlackboardVariable<Vector2Int> bbMoveTarget;
                if (m_Agent.BlackboardReference.GetVariable(BB_MOVEMENT_TARGET_POSITION, out bbMoveTarget))
                {
                    if (bbMoveTarget.Value.x < 0 || bbMoveTarget.Value.y < 0) return null;
                    return bbMoveTarget.Value;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Determines if another unit is a valid target for this enemy unit.
    /// </summary>
    /// <param name="otherUnit">The unit to check.</param>
    /// <returns>True if the unit is a valid target (AllyUnit).</returns>
    public override bool IsValidUnitTarget(Unit otherUnit)
    {
        return otherUnit is AllyUnit;
    }

 /// <summary>
 /// Determines if a building is a valid target for attack by this enemy unit.
 /// Enemy units can only attack player buildings, not neutral buildings.
 /// </summary>
 /// <param name="building">The building to check.</param>
 /// <returns>True if the building is a valid attack target.</returns>
 public override bool IsValidBuildingTarget(Building building)
  {
      if (building == null || !building.IsTargetable) return false;

      // Enemy units can only attack player buildings
      // Neutral buildings must be captured, not attacked
      return building.Team == TeamType.Player;
  }
/// <summary>
/// Determines if a building is a valid target for capture by this enemy unit.
/// </summary>
/// <param name="building">The building to check.</param>
/// <returns>True if the building can be captured.</returns>
public bool IsValidCaptureTarget(Building building)
  {
      if (building == null) return false;

      NeutralBuilding neutralBuilding = building as NeutralBuilding;
      if (neutralBuilding == null) return false;

      return neutralBuilding.IsRecapturable &&
             (neutralBuilding.Team == TeamType.Neutral || neutralBuilding.Team == TeamType.Player);
  }

    /// <summary>
    /// Attempts to capture a neutral building for the enemy team.
    /// </summary>
    /// <param name="buildingToCapture">The building to capture.</param>
    /// <returns>True if capture was successfully initiated.</returns>
    public bool PerformCaptureEnemy(Building buildingToCapture)
    {
        NeutralBuilding neutralBuilding = buildingToCapture as NeutralBuilding;
        if (neutralBuilding == null || !neutralBuilding.IsRecapturable)
        {
            if (enableVerboseLogging) Debug.LogWarning($"[{name}] Cannot capture '{buildingToCapture.name}'.");
            return false;
        }
        if (neutralBuilding.Team == TeamType.Enemy)
        {
             if (enableVerboseLogging) Debug.Log($"[{name}] Building '{buildingToCapture.name}' already belongs to Enemy team.");
            return false;
        }
        if (!IsBuildingInCaptureRange(neutralBuilding))
        {
            if (enableVerboseLogging) Debug.LogWarning($"[{name}] Cannot capture '{neutralBuilding.name}': out of range.");
            return false;
        }
        SetState(UnitState.Capturing);
        FaceBuildingTarget(buildingToCapture);
        bool captureInitiated = neutralBuilding.StartCapture(TeamType.Enemy, this);
        if (captureInitiated)
        {
            this.buildingBeingCaptured = neutralBuilding;
            this.beatsSpentCapturing = 0;
            if (enableVerboseLogging) Debug.Log($"[{name}] Initiated capture of '{neutralBuilding.name}'.");
            return true;
        }
        else
        {
            if (enableVerboseLogging) Debug.LogWarning($"[{name}] Failed to initiate capture of '{neutralBuilding.name}'.");
            SetState(UnitState.Idle);
            return false;
        }
    }

    /// <summary>
    /// Cleans up resources when the enemy unit is destroyed.
    /// Unregisters from the EnemyRegistry.
    /// </summary>
    public override void OnDestroy()
    {
        if (EnemyRegistry.Instance != null)
        {
            EnemyRegistry.Instance.Unregister(this);
        }
        base.OnDestroy();
    }
    #endregion
    
    /// <summary>
    /// Determines if this enemy unit can be targeted.
    /// Only Boss type units are targetable.
    /// </summary>
    public override bool IsTargetable => GetUnitType() == UnitType.Boss;
}