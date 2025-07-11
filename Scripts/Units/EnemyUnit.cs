using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Behavior; // Requis pour BehaviorGraphAgent
using Unity.Behavior.GraphFramework; // Requis pour Blackboard
using ScriptableObjects; // Pour CharacterProgressionData_SO

public class EnemyUnit : Unit
{
    // --- L'INTERRUPTEUR VIRTUEL ---
    // Permet aux classes enfants (comme le Boss) de spécifier qu'elles ont leur propre logique
    // et ne doivent pas utiliser l'arbre de comportement.
    protected virtual bool IsHardcoded => false;

    [Header("Behavior Graph")]
    [Tooltip("Assigner le Behavior Graph Agent de cet GameObject ici.")]
    [SerializeField] private BehaviorGraphAgent m_Agent;

    [Header("Enemy Settings")]
    [SerializeField] public bool enableVerboseLogging = true;
   

    [Tooltip("Mode de comportement initial de l'unité.")]
    [SerializeField] private CurrentBehaviorMode initialBehaviorMode = CurrentBehaviorMode.Defensive;

    // --- Clés Blackboard (inchangées) ---
    public const string BB_SELF_UNIT = "SelfUnit";
    public const string BB_CURRENT_BEHAVIOR_MODE = "CurrentBehaviorMode";
    public const string BB_OBJECTIVE_BUILDING = "ObjectiveBuilding";
    public const string BB_DETECTED_PLAYER_UNIT = "DetectedPlayerUnit";
    public const string BB_DETECTED_TARGETABLE_BUILDING = "DetectedTargetableBuilding";
    public const string BB_SELECTED_ACTION_TYPE = "SelectedActionType";
    public const string BB_MOVEMENT_TARGET_POSITION = "FinalDestinationPosition";
    public const string BB_INTERACTION_TARGET_UNIT = "InteractionTargetUnit";
    public const string BB_INTERACTION_TARGET_BUILDING = "InteractionTargetBuilding";
    public const string BB_IS_MOVING = "IsMoving";
    public const string BB_IS_ATTACKING = "IsAttacking";
    public const string BB_IS_CAPTURING = "IsCapturing";
    public const string BB_IS_OBJECTIVE_COMPLETED = "IsObjectiveCompleted";
    public const string BB_PATHFINDING_FAILED = "PathfindingFailed";
    public const string BB_CURRENT_PATH = "CurrentPath";

    // Variables Blackboard mises en cache (inchangées)
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<CurrentBehaviorMode> bbCurrentBehaviorMode;
    private BlackboardVariable<Building> bbObjectiveBuilding;
    private BlackboardVariable<Unit> bbDetectedPlayerUnit;
    private BlackboardVariable<Building> bbDetectedTargetableBuilding;
    private BlackboardVariable<AIActionType> bbSelectedActionType;
    private BlackboardVariable<Vector2Int> bbMovementTargetPosition;
    private BlackboardVariable<Unit> bbInteractionTargetUnit;
    private BlackboardVariable<Building> bbInteractionTargetBuilding;
    private BlackboardVariable<bool> bbIsMoving;
    private BlackboardVariable<bool> bbIsAttacking;
    private BlackboardVariable<bool> bbIsCapturing;
    private BlackboardVariable<bool> bbIsObjectiveCompleted;
    private BlackboardVariable<bool> bbPathfindingFailed;
    private BlackboardVariable<List<Vector2Int>> bbCurrentPath;

    // OnEnable est appelé quand l'unité est activée. C'est ici que l'on va mettre la condition.
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
	protected override void Awake()
  	{
      if (EnemyRegistry.Instance != null)
      {
          EnemyRegistry.Instance.Register(this);
          if (enableVerboseLogging) Debug.Log($"[{name}] EnemyUnit.Awake: Enregistrement immédiat dans EnemyRegistry.");
      }

      base.Awake();
  	}
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

    // Nouvelle méthode pour centraliser l'initialisation du blackboard
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

    // Le reste du script (CacheBlackboardVariables, Update, TargetPosition, etc.) reste inchangé.
    #region Unchanged Methods
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

    private void Update() { }

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

    public override bool IsValidUnitTarget(Unit otherUnit)
    {
        return otherUnit is AllyUnit;
    }

 public override bool IsValidBuildingTarget(Building building)
  {
      if (building == null || !building.IsTargetable) return false;

      // Les unités ennemies ne peuvent attaquer que les bâtiments du joueur
      // Les bâtiments neutres doivent être capturés, pas attaqués
      return building.Team == TeamType.Player;
  }
public bool IsValidCaptureTarget(Building building)
  {
      if (building == null) return false;

      NeutralBuilding neutralBuilding = building as NeutralBuilding;
      if (neutralBuilding == null) return false;

      return neutralBuilding.IsRecapturable &&
             (neutralBuilding.Team == TeamType.Neutral || neutralBuilding.Team == TeamType.Player);
  }

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

    public override void OnDestroy()
    {
        if (EnemyRegistry.Instance != null)
        {
            EnemyRegistry.Instance.Unregister(this);
        }
        base.OnDestroy();
    }
    #endregion
    // Override de ITargetable pour les unités ennemies
    // Seules les unités de type Boss sont ciblables
    public override bool IsTargetable => GetUnitType() == UnitType.Boss;
}