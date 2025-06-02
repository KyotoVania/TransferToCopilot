// Fichier: Scripts/Units/EnemyUnit.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Behavior; // Requis pour BehaviorGraphAgent
using Unity.Behavior.GraphFramework; // Requis pour Blackboard

public class EnemyUnit : Unit
{
    [Header("Behavior Graph")]
    [Tooltip("Assigner le Behavior Graph Agent de cet GameObject ici.")]
    [SerializeField] private BehaviorGraphAgent m_Agent;

    [Header("Enemy Settings")]
    [SerializeField] public bool enableVerboseLogging = true; // Public pour les logs des nœuds
    [Tooltip("Si true, l'unité attaquera les bâtiments joueurs en priorité.")]
    [SerializeField] private bool prioritizePlayerBuildings = true; // Peut être lu par un nœud de décision

    [Tooltip("Mode de comportement initial de l'unité.")]
    [SerializeField] private CurrentBehaviorMode initialBehaviorMode = CurrentBehaviorMode.Defensive;

    // --- Clés Blackboard (constantes pour éviter les typos) ---
    public const string BB_SELF_UNIT = "SelfUnit"; // Ajout pour référence claire
    public const string BB_CURRENT_BEHAVIOR_MODE = "CurrentBehaviorMode";
    public const string BB_OBJECTIVE_BUILDING = "ObjectiveBuilding";
    public const string BB_DETECTED_PLAYER_UNIT = "DetectedPlayerUnit";
    public const string BB_DETECTED_TARGETABLE_BUILDING = "DetectedTargetableBuilding";
    public const string BB_SELECTED_ACTION_TYPE = "SelectedActionType";
    public const string BB_MOVEMENT_TARGET_POSITION = "FinalDestinationPosition";
    public const string BB_INTERACTION_TARGET_UNIT = "InteractionTargetUnit";
    public const string BB_INTERACTION_TARGET_BUILDING = "InteractionTargetBuilding";
    public const string BB_IS_MOVING = "IsMoving"; // Ajouté pour cohérence
    public const string BB_IS_ATTACKING = "IsAttacking";
    public const string BB_IS_CAPTURING = "IsCapturing";
    public const string BB_IS_OBJECTIVE_COMPLETED = "IsObjectiveCompleted";
    public const string BB_PATHFINDING_FAILED = "PathfindingFailed"; // Ajouté
    public const string BB_CURRENT_PATH = "CurrentPath"; // Ajouté

    // Variables Blackboard mises en cache
    private BlackboardVariable<Unit> bbSelfUnit; // Ajouté pour que EnemyUnit puisse se référer à lui-même si besoin
    private BlackboardVariable<CurrentBehaviorMode> bbCurrentBehaviorMode;
    private BlackboardVariable<Building> bbObjectiveBuilding;
    private BlackboardVariable<Unit> bbDetectedPlayerUnit; // Typé pour être plus spécifique
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

protected override IEnumerator Start()
{
    // 1. Récupérer l'agent et vérifier les composants critiques
    if (m_Agent == null) m_Agent = GetComponent<BehaviorGraphAgent>();

    if (m_Agent == null)
    {
        Debug.LogError($"[{name}] EnemyUnit.Start: BehaviorGraphAgent component not found! AI will not run.", gameObject);
        yield break;
    }
    if (m_Agent.BlackboardReference == null)
    {
        Debug.LogError($"[{name}] EnemyUnit.Start: BlackboardReference is null on BehaviorGraphAgent! AI may not function correctly. Assurez-vous qu'un asset Blackboard est assigné.", gameObject);
        // Pas de yield break ici, car base.Start() pourrait encore fonctionner sans BB pour l'attachement.
    }

    // PAS DE m_Agent.enabled = false; ICI.
    // On suppose que l'agent est activé par défaut dans l'inspecteur, comme pour AllyUnit.
    // EnemyUnitBlackboardInitializer.cs (dans Awake) devrait déjà avoir initialisé SelfUnit.

    if (enableVerboseLogging) Debug.Log($"[{name}] EnemyUnit.Start: Début du processus d'initialisation. Agent Actif: {m_Agent.enabled}");

    // 2. Appel à Unit.Start() pour l'attachement à la tuile et autres initialisations de base.
    // C'est une coroutine, donc on attend sa complétion.
    if (enableVerboseLogging) Debug.Log($"[{name}] EnemyUnit.Start: Appel de base.Start() (Unit.Start) pour l'attachement à la tuile.");
    yield return StartCoroutine(base.Start()); // Attend que Unit.Start() et donc AttachToNearestTile() soient finis.

    // 3. Vérifier si l'attachement a réussi (this.occupiedTile et this.isAttached sont dans Unit.cs)
    if (this.occupiedTile != null && this.isAttached)
    {
        if (enableVerboseLogging) Debug.Log($"[{name}] EnemyUnit.Start: Unité attachée à la tuile ({this.occupiedTile.column}, {this.occupiedTile.row}). Initialisation du Blackboard spécifique à EnemyUnit.");

        // 4. Initialiser les variables Blackboard APRÈS l'attachement et l'initialisation de base.
        //    CacheBlackboardVariables() ici peuple principalement les champs bb... pour ce script.
        CacheBlackboardVariables();

        // Définir les valeurs initiales pour les variables Blackboard spécifiques à EnemyUnit
        if (bbCurrentBehaviorMode != null)
        {
            bbCurrentBehaviorMode.Value = initialBehaviorMode;
        } else {
            // Cette erreur est critique si la variable n'a pas pu être mise en cache.
            Debug.LogError($"[{name}] EnemyUnit.Start: bbCurrentBehaviorMode est null APRÈS CacheBlackboardVariables. Impossible de définir le mode de comportement initial.", gameObject);
        }

        if (bbIsObjectiveCompleted != null) {
            bbIsObjectiveCompleted.Value = false; // Un nouvel objectif n'est pas complété
        } else {
             Debug.LogError($"[{name}] EnemyUnit.Start: bbIsObjectiveCompleted est null APRÈS CacheBlackboardVariables. Impossible de définir l'état initial de l'objectif.", gameObject);
        }

        // Initialiser les autres flags d'état à false pour un état de départ propre
        if (bbIsMoving != null) bbIsMoving.Value = false;
        else Debug.LogWarning($"[{name}] EnemyUnit.Start: bbIsMoving (Blackboard Variable) est null. Vérifiez la configuration du Blackboard et la méthode CacheBlackboardVariables.", gameObject);

        if (bbIsAttacking != null) bbIsAttacking.Value = false;
        else Debug.LogWarning($"[{name}] EnemyUnit.Start: bbIsAttacking (Blackboard Variable) est null.", gameObject);

        if (bbIsCapturing != null) bbIsCapturing.Value = false;
        else Debug.LogWarning($"[{name}] EnemyUnit.Start: bbIsCapturing (Blackboard Variable) est null.", gameObject);

        if (bbPathfindingFailed != null) bbPathfindingFailed.Value = false;
        else Debug.LogWarning($"[{name}] EnemyUnit.Start: bbPathfindingFailed (Blackboard Variable) est null.", gameObject);

        if (bbCurrentPath != null) bbCurrentPath.Value = new List<Vector2Int>();
        else Debug.LogWarning($"[{name}] EnemyUnit.Start: bbCurrentPath (Blackboard Variable) est null.", gameObject);

        // À CE STADE :
        // - EnemyUnitBlackboardInitializer.Awake() a dû définir "SelfUnit".
        // - base.Start() a terminé, donc this.occupiedTile et this.isAttached sont définis.
        // - Les variables Blackboard spécifiques à EnemyUnit sont initialisées.
        // - L'agent (m_Agent) n'a pas été désactivé/réactivé par ce script. S'il est activé par défaut,
        //   son Behavior Graph commencera à s'exécuter selon le cycle de vie d'Unity.
    }
    else
    {
        // Ce log est important si l'attachement échoue.
        Debug.LogError($"[{name}] EnemyUnit.Start: ÉCHEC de l'attachement à une tuile après base.Start(). Le BehaviorGraphAgent (s'il est actif par défaut) pourrait opérer sur des données invalides. Tuile Occupée est {(this.occupiedTile == null ? "NULL" : this.occupiedTile.name)}, flag isAttached de Unit: {this.isAttached}", gameObject);
        // Si l'attachement échoue, l'agent (s'il était activé par défaut) pourrait mal fonctionner.
        // Vous pourriez envisager de le désactiver explicitement ici en cas d'échec d'attachement :
        // if (m_Agent != null) m_Agent.enabled = false;
    }

    if (enableVerboseLogging)
        Debug.Log($"[{name}] EnemyUnit.Start: Processus d'initialisation terminé. Agent Actif: {(m_Agent != null && m_Agent.enabled ? "OUI" : "NON ou Agent NULL")}. Mode initial depuis BB: {(bbCurrentBehaviorMode?.Value.ToString() ?? "NON DÉFINI/TROUVÉ")}. Graph: {(m_Agent?.Graph != null ? m_Agent.Graph.name : "PAS D'AGENT/GRAPH")}");
}

private void CacheBlackboardVariables()
{
    if (m_Agent == null || m_Agent.BlackboardReference == null)
    {
        // Ce cas est normalement déjà géré dans Start() avant l'appel à CacheBlackboardVariables,
        // mais c'est une double sécurité.
        Debug.LogError($"[{name}] CacheBlackboardVariables: BehaviorGraphAgent or its BlackboardReference is null. Cannot cache variables.", gameObject);
        return;
    }
    var blackboard = m_Agent.BlackboardReference;

    // Variable pour se référer à soi-même (initialisée par EnemyUnitBlackboardInitializer)
    // Note : bbSelfUnit est déjà déclaré comme variable membre de la classe EnemyUnit
    if (!blackboard.GetVariable(BB_SELF_UNIT, out bbSelfUnit)) // Utilise la constante de classe
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_SELF_UNIT}' (type Unit) not found. Ensure EnemyUnitBlackboardInitializer is working correctly and the variable is defined in the Blackboard Asset.", gameObject);

    // Variables de Configuration et d'État de Haut Niveau
    if (!blackboard.GetVariable(BB_CURRENT_BEHAVIOR_MODE, out bbCurrentBehaviorMode))
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_CURRENT_BEHAVIOR_MODE}' (type EnemyBehaviorMode) not found. Ensure it's defined in the Blackboard Asset.", gameObject);

    if (!blackboard.GetVariable(BB_OBJECTIVE_BUILDING, out bbObjectiveBuilding))
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_OBJECTIVE_BUILDING}' (type Building) not found. This is optional if not always used.", gameObject);

    if (!blackboard.GetVariable(BB_IS_OBJECTIVE_COMPLETED, out bbIsObjectiveCompleted))
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_IS_OBJECTIVE_COMPLETED}' (type bool) not found. Ensure it's defined.", gameObject);

    // Variables liées à la Détection (mises à jour par ScanForTargetsNode)
    if (!blackboard.GetVariable(BB_DETECTED_PLAYER_UNIT, out bbDetectedPlayerUnit))
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_DETECTED_PLAYER_UNIT}' (type AllyUnit or Unit) not found. Scan results cannot be stored.", gameObject);

    if (!blackboard.GetVariable(BB_DETECTED_TARGETABLE_BUILDING, out bbDetectedTargetableBuilding))
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_DETECTED_TARGETABLE_BUILDING}' (type Building) not found. Scan results cannot be stored.", gameObject);

    // Variables de Décision d'Action (mises à jour par SelectTargetNode)
    if (!blackboard.GetVariable(BB_SELECTED_ACTION_TYPE, out bbSelectedActionType))
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_SELECTED_ACTION_TYPE}' (type AIActionType) not found. Decision making will be impaired.", gameObject);

    if (!blackboard.GetVariable(BB_MOVEMENT_TARGET_POSITION, out bbMovementTargetPosition))
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_MOVEMENT_TARGET_POSITION}' (type Vector2Int) not found. Movement planning will fail.", gameObject);

    if (!blackboard.GetVariable(BB_INTERACTION_TARGET_UNIT, out bbInteractionTargetUnit))
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_INTERACTION_TARGET_UNIT}' (type Unit) not found. Unit interactions will fail.", gameObject);

    if (!blackboard.GetVariable(BB_INTERACTION_TARGET_BUILDING, out bbInteractionTargetBuilding))
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_INTERACTION_TARGET_BUILDING}' (type Building) not found. Building interactions will fail.", gameObject);

    // Flags d'État d'Action (mis à jour par les nœuds d'action respectifs)
    if (!blackboard.GetVariable(BB_IS_MOVING, out bbIsMoving))
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_IS_MOVING}' (type bool) not found. Tracking movement state will fail.", gameObject);

    if (!blackboard.GetVariable(BB_IS_ATTACKING, out bbIsAttacking))
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_IS_ATTACKING}' (type bool) not found. Tracking attack state will fail.", gameObject);

    if (!blackboard.GetVariable(BB_IS_CAPTURING, out bbIsCapturing))
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_IS_CAPTURING}' (type bool) not found. Tracking capture state will fail.", gameObject);

    // Variables liées au Pathfinding (mises à jour par FindSmartStepNode)
    if (!blackboard.GetVariable(BB_PATHFINDING_FAILED, out bbPathfindingFailed))
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_PATHFINDING_FAILED}' (type bool) not found. Pathfinding failure cannot be signaled.", gameObject);

    if (!blackboard.GetVariable(BB_CURRENT_PATH, out bbCurrentPath))
        Debug.LogWarning($"[{name}] Blackboard variable '{BB_CURRENT_PATH}' (type List<Vector2Int>) (optional) not found.", gameObject);

    if (enableVerboseLogging) Debug.Log($"[{name}] CacheBlackboardVariables attempt finished. Check warnings for any missing variables.", gameObject);
}

    private void Update()
    {
        // Potentiellement, vérifier ici si l'objectif principal (bbObjectiveBuilding) est détruit
        // et mettre à jour bbIsObjectiveCompleted, bien que cela puisse aussi être géré
        // par des observeurs ou des conditions dans le graph.
    }

    protected override Vector2Int? TargetPosition
    {
        get
        {
            // Cette propriété est maintenant moins centrale pour la prise de décision interne de EnemyUnit,
            // car le Behavior Graph définira directement BB_MOVEMENT_TARGET_POSITION.
            // Cependant, les nœuds de mouvement pourraient toujours la lire.
            // Il faut s'assurer que BB_MOVEMENT_TARGET_POSITION est bien la source de vérité.

            if (m_Agent != null && m_Agent.BlackboardReference != null)
            {
                BlackboardVariable<Vector2Int> bbMoveTarget;
                if (m_Agent.BlackboardReference.GetVariable(BB_MOVEMENT_TARGET_POSITION, out bbMoveTarget))
                {
                    // Si la cible de mouvement est invalide (ex: (-1,-1)), retourner null.
                    if (bbMoveTarget.Value.x < 0 || bbMoveTarget.Value.y < 0) return null;
                    return bbMoveTarget.Value;
                }
            }
            return null; // Pas de cible de mouvement définie sur le Blackboard
        }
    }

    // Les méthodes IsValidUnitTarget et IsValidBuildingTarget restent importantes
    // car elles peuvent être utilisées par des nœuds de condition ou de scan.
    public override bool IsValidUnitTarget(Unit otherUnit)
    {
        return otherUnit is AllyUnit; // Les ennemis ciblent les unités alliées
    }

    public override bool IsValidBuildingTarget(Building building)
    {
        if (building == null || !building.IsTargetable) return false;
        // Les ennemis ciblent les bâtiments joueurs ou neutres (pour capture/destruction)
        return building.Team == TeamType.Player || building.Team == TeamType.Neutral;
    }

    // Les méthodes d'action comme PerformAttackCoroutine, PerformAttackBuildingCoroutine, PerformCapture
    // sont conservées car elles seront appelées par les nœuds d'action du Behavior Graph.
    // Assurez-vous qu'elles ne contiennent plus de logique de décision de cible.

    // Exemple : PerformCapture (adapté de AllyUnit et simplifié pour EnemyUnit)
    public bool PerformCaptureEnemy(Building buildingToCapture)
    {
        NeutralBuilding neutralBuilding = buildingToCapture as NeutralBuilding;
        if (neutralBuilding == null || !neutralBuilding.IsRecapturable)
        {
            if (enableVerboseLogging) Debug.LogWarning($"[{name}] Cannot capture '{buildingToCapture.name}': not a recapturable NeutralBuilding.");
            return false;
        }
        // L'ennemi capture pour l'équipe Ennemie
        if (neutralBuilding.Team == TeamType.Enemy)
        {
             if (enableVerboseLogging) Debug.Log($"[{name}] Building '{buildingToCapture.name}' already belongs to Enemy team.");
            return false; // Déjà à l'équipe ennemie
        }

        if (!IsBuildingInCaptureRange(neutralBuilding))
        {
            if (enableVerboseLogging) Debug.LogWarning($"[{name}] Cannot capture '{neutralBuilding.name}': out of range.");
            return false;
        }

        SetState(UnitState.Capturing); // L'état interne de l'unité change
                                       // Le flag BB_IS_CAPTURING sera géré par le nœud d'action CaptureBuildingNode

        FaceBuildingTarget(buildingToCapture);
        bool captureInitiated = neutralBuilding.StartCapture(TeamType.Enemy, this);

        if (captureInitiated)
        {
            this.buildingBeingCaptured = neutralBuilding; // Utiliser la variable de la classe Unit
            this.beatsSpentCapturing = 0;         // Idem
            if (enableVerboseLogging) Debug.Log($"[{name}] Initiated capture of '{neutralBuilding.name}' for Enemy team.");
            return true;
        }
        else
        {
            if (enableVerboseLogging) Debug.LogWarning($"[{name}] Failed to initiate capture of '{neutralBuilding.name}'.");
            SetState(UnitState.Idle); // Retour à Idle si StartCapture échoue
            return false;
        }
    }

    // OnCaptureComplete et StopCapturing de Unit.cs devraient suffire.
    // La logique de mise à jour de bbIsObjectiveCompleted devra être gérée par le graph
    // ou par un système qui observe la capture des objectifs.

    // La méthode OnRhythmBeat est héritée de Unit.cs.
    // Le Behavior Graph utilisera probablement son propre nœud "WaitForBeat"
    // pour séquencer les actions au rythme.
    // La logique de HandleMovementOnBeat et HandleAttackOnBeat de Unit.cs
    // sera déclenchée par les nœuds d'action (MoveToTarget, AttackUnit/Building).

    public override void OnDestroy()
    {
        // Se désabonner des événements si nécessaire
        base.OnDestroy();
    }
}