// File: Scripts/Nodes/SelectTargetNode.cs
using UnityEngine;
using Unity.Behavior; // Main namespace for Behavior Tree base classes
using Unity.Behavior.GraphFramework; // For Blackboard interaction
using System; // For [Serializable]
using System.Collections.Generic; // Required for blackboard variable caching list
using Unity.Properties; // Required for [GeneratePropertyBag]

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Select Target",
    story: "Select Target",
    category: "My Actions", // Ou la catégorie que vous préférez
    id: "YOUR_UNIQUE_ID_SelectTarget_V3" // Conservez votre GUID ou mettez-le à jour si c'est une nouvelle version majeure
)]
public class SelectTargetNode : Unity.Behavior.Action
{
    // --- Constants for Blackboard variable names ---
    // Inputs
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string DETECTED_ENEMY_UNIT_VAR = "DetectedEnemyUnit";
    private const string DETECTED_BUILDING_VAR = "DetectedBuilding"; // Tactical scan result
    private const string HAS_INITIAL_OBJECTIVE_SET_VAR = "HasInitialObjectiveSet";
    private const string INITIAL_TARGET_BUILDING_VAR = "InitialTargetBuilding";
    private const string IS_ATTACKING_VAR = "IsAttacking";
    private const string IS_CAPTURING_VAR = "IsCapturing"; // Ajouté si vous avez un état de capture
    private const string IS_OBJECTIVE_COMPLETED_VAR = "IsObjectiveCompleted";

    // Outputs
    private const string SELECTED_ACTION_TYPE_VAR = "SelectedActionType";
    private const string MOVEMENT_TARGET_POS_VAR = "MovementTargetPosition";
    private const string INTERACTION_TARGET_UNIT_VAR = "InteractionTargetUnit";
    private const string INTERACTION_TARGET_BUILDING_VAR = "InteractionTargetBuilding";

    // --- Node State ---
    private bool blackboardVariablesCached = false;

    // --- Blackboard Variable Cache ---
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<Unit> bbDetectedEnemyUnit;
    private BlackboardVariable<Building> bbDetectedTacticalBuilding; // Renommé pour clarté
    private BlackboardVariable<bool> bbHasInitialObjectiveSet;
    private BlackboardVariable<Building> bbInitialTargetBuilding;
    private BlackboardVariable<bool> bbIsAttacking;
    private BlackboardVariable<bool> bbIsCapturing; // Cache pour IsCapturing
    private BlackboardVariable<bool> bbIsObjectiveCompleted;

    private BlackboardVariable<AIActionType> bbSelectedActionType;
    private BlackboardVariable<Vector2Int> bbMovementTargetPosition;
    private BlackboardVariable<Unit> bbInteractionTargetUnit;
    private BlackboardVariable<Building> bbInteractionTargetBuilding;

    protected override Node.Status OnStart()
    {
        if (!CacheBlackboardVariables())
        {
            return Node.Status.Failure;
        }
        // Ce nœud prend sa décision dans OnUpdate, donc OnStart retourne Running.
        return Node.Status.Running;
    }

    protected override Node.Status OnUpdate()
    {
        if (!blackboardVariablesCached) // Devrait avoir été mis en cache par OnStart
        {
            LogNodeMessage("Blackboard variables not cached in OnUpdate. Attempting recache.", true, true);
            if (!CacheBlackboardVariables()) // Tenter de recacher
            {
                 LogNodeMessage("Recache FAILED in OnUpdate. Node Failure.", true, true);
                 return Node.Status.Failure;
            }
        }

        var selfUnit = bbSelfUnit?.Value;
        if (selfUnit == null)
        {
            LogNodeMessage($"'{SELF_UNIT_VAR}' value is null in OnUpdate. Node Failure.", true, true);
            ResetOutputsToIdleDefaults(null); // Passer null car selfUnit est null
            return Node.Status.Failure;
        }

        // Lire toutes les entrées pertinentes du Blackboard
        Unit detectedEnemy = bbDetectedEnemyUnit?.Value;
        Building detectedTacticalBuildingInput = bbDetectedTacticalBuilding?.Value; // Renommé la variable locale
        bool hasInitialObjective = bbHasInitialObjectiveSet?.Value ?? false;
        Building initialObjectiveBuildingInput = bbInitialTargetBuilding?.Value; // Renommé la variable locale
        bool isCurrentlyAttacking = bbIsAttacking?.Value ?? false;
        bool isCurrentlyCapturing = bbIsCapturing?.Value ?? false; // Lire l'état de capture
        bool isObjectiveFlagCompleted = bbIsObjectiveCompleted?.Value ?? false;

        // Initialiser les variables de décision
        AIActionType finalSelectedAction = AIActionType.None;
        Vector2Int currentUnitPos = selfUnit.GetOccupiedTile() != null ?
                                    new Vector2Int(selfUnit.GetOccupiedTile().column, selfUnit.GetOccupiedTile().row) :
                                    new Vector2Int(-1,-1); // Position invalide si pas sur une tuile

        if (currentUnitPos.x == -1 && selfUnit.GetOccupiedTile() == null) {
            LogNodeMessage($"SelfUnit '{selfUnit.name}' is not on a valid tile. Cannot determine current position accurately.", true, true);
            // Peut-être retourner Failure si la position est cruciale et inconnue
        }

        Vector2Int finalMovementTargetPos = currentUnitPos; // Par défaut, rester sur place
        Unit finalInteractionUnit = null;
        Building finalInteractionBuilding = null;

        // --- Logique de Décision Séquentielle ---

        // Priorité -1: Si l'unité est déjà en train de capturer, elle doit continuer cette action.
        // Le nœud de capture gérera son propre état. Ce nœud ne devrait pas l'interrompre.
        if (isCurrentlyCapturing)
        {
            // Si l'unité capture, elle ne devrait pas faire autre chose.
            // On pourrait vouloir que ce nœud définisse l'action sur None pour éviter
            // que d'autres nœuds de mouvement ou d'attaque ne soient activés par erreur.
            // Ou, si le graph est bien structuré, le chemin menant à ce SelectTargetNode
            // ne devrait pas être pris si l'unité est en capture.
            // Pour la robustesse, si IsCapturing est vrai, on s'assure qu'aucune autre action n'est sélectionnée.
            finalSelectedAction = AIActionType.None; // Ou un AIActionType.Capturing si vous en avez un.
                                                    // Si c'est None, le MovementTarget sera la pos actuelle.
            finalMovementTargetPos = currentUnitPos;
            finalInteractionUnit = null;
            // On pourrait vouloir conserver la cible de capture dans bbInteractionTargetBuilding si le nœud de capture l'utilise.
            // Pour l'instant, on le laisse être défini par le nœud de capture lui-même.
            finalInteractionBuilding = bbInteractionTargetBuilding?.Value; // Conserver la cible de capture si déjà définie

        }
        // Priorité 0: Continuer l'attaque en cours si applicable (uniquement si pas en capture)
        else if (isCurrentlyAttacking)
        {
            // Si l'unité attaque, elle devrait continuer si sa cible est toujours valide.
            // ScanForNearbyTargets devrait mettre à jour bbDetectedEnemyUnit.
            if (detectedEnemy != null && selfUnit.IsValidUnitTarget(detectedEnemy))
            {
                Tile enemyTile = detectedEnemy.GetOccupiedTile();
                if (enemyTile != null)
                {
                    finalMovementTargetPos = new Vector2Int(enemyTile.column, enemyTile.row);
                    finalInteractionUnit = detectedEnemy;
                    finalInteractionBuilding = null;
                    finalSelectedAction = selfUnit.IsUnitInRange(detectedEnemy) ? AIActionType.AttackUnit : AIActionType.MoveToUnit;
                }
            }

        }

        // Priorité 1: Cheer and Despawn (SI aucune action d'attaque/capture n'est en cours ou décidée)
        // Et si l'objectif est complété.
        if (finalSelectedAction == AIActionType.None && isObjectiveFlagCompleted && !isCurrentlyAttacking && !isCurrentlyCapturing)
        {
            finalSelectedAction = AIActionType.CheerAndDespawn;
            finalMovementTargetPos = currentUnitPos; // EXPLICITEMENT rester sur place
            finalInteractionUnit = null;
            finalInteractionBuilding = null;
        }
        // Priorité 2: Engager un ennemi détecté (SI pas Cheer, et pas déjà en train d'attaquer/capturer)
        else if (finalSelectedAction == AIActionType.None && detectedEnemy != null && selfUnit.IsValidUnitTarget(detectedEnemy))
        {
            Tile enemyTile = detectedEnemy.GetOccupiedTile();
            if (enemyTile != null)
            {
                finalMovementTargetPos = new Vector2Int(enemyTile.column, enemyTile.row);
                finalInteractionUnit = detectedEnemy;
                finalInteractionBuilding = null;
                finalSelectedAction = selfUnit.IsUnitInRange(detectedEnemy) ? AIActionType.AttackUnit : AIActionType.MoveToUnit;
            }
        }
        // Priorité 3: Engager un bâtiment tactique ennemi détecté
        else if (finalSelectedAction == AIActionType.None && detectedTacticalBuildingInput != null &&
                 detectedTacticalBuildingInput.Team == TeamType.Enemy && selfUnit.IsValidBuildingTarget(detectedTacticalBuildingInput))
        {
            Tile tacticalBuildingTile = detectedTacticalBuildingInput.GetOccupiedTile();
            if (tacticalBuildingTile != null)
            {
                finalMovementTargetPos = new Vector2Int(tacticalBuildingTile.column, tacticalBuildingTile.row);
                finalInteractionBuilding = detectedTacticalBuildingInput;
                finalInteractionUnit = null;

                if (selfUnit.IsBuildingInRange(detectedTacticalBuildingInput))
                {
                    NeutralBuilding neutralVersion = detectedTacticalBuildingInput as NeutralBuilding;
                    if (neutralVersion != null && neutralVersion.IsRecapturable && selfUnit.IsBuildingInCaptureRange(neutralVersion)) {
                        finalSelectedAction = AIActionType.CaptureBuilding;
                    } else {
                        finalSelectedAction = AIActionType.AttackBuilding;
                    }
                }
                else
                {
                    finalSelectedAction = AIActionType.MoveToBuilding;
                }
            }
        }
        // Priorité 4: Poursuivre l'objectif initial du bâtiment (SI pas Cheer, pas d'ennemi/bâtiment tactique, ET objectif initial existe et pas complété)
        else if (finalSelectedAction == AIActionType.None && hasInitialObjective && initialObjectiveBuildingInput != null && !isObjectiveFlagCompleted)
        {
            // Vérifier si l'objectif initial est toujours valide (n'a pas été détruit ou capturé par nous entre-temps)
            if (initialObjectiveBuildingInput == null || initialObjectiveBuildingInput.CurrentHealth <= 0 ||
                (initialObjectiveBuildingInput.Team == TeamType.Player && !(initialObjectiveBuildingInput is NeutralBuilding && ((NeutralBuilding)initialObjectiveBuildingInput).IsRecapturable))) // Si c'est à nous et pas recapturable (par l'ennemi pour nous)
            {
                 if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = true; // Mettre à jour BB
                 finalSelectedAction = AIActionType.None; // Laisser Cheer être choisi au prochain tick
                 finalMovementTargetPos = currentUnitPos; // Rester sur place pour ce tick.
            }
            else
            {
                Tile initialBuildingTile = initialObjectiveBuildingInput.GetOccupiedTile();
                if (initialBuildingTile != null)
                {
                    finalMovementTargetPos = new Vector2Int(initialBuildingTile.column, initialBuildingTile.row);
                    finalInteractionBuilding = initialObjectiveBuildingInput;
                    finalInteractionUnit = null;

                    if (selfUnit.IsBuildingInRange(initialObjectiveBuildingInput))
                    {
                        switch (initialObjectiveBuildingInput.Team)
                        {
                            case TeamType.Enemy:
                                NeutralBuilding enemyRecapturable = initialObjectiveBuildingInput as NeutralBuilding;
                                if (enemyRecapturable != null && enemyRecapturable.IsRecapturable && selfUnit.IsBuildingInCaptureRange(enemyRecapturable)) {
                                    finalSelectedAction = AIActionType.CaptureBuilding; // Recapturer un bâtiment ennemi (s'il est de type NeutralBuilding)
                                } else {
                                    finalSelectedAction = AIActionType.AttackBuilding;
                                }
                                break;
                            case TeamType.Player: // Normalement, on ne devrait pas avoir un bâtiment joueur comme objectif initial à "poursuivre" agressivement
                                finalSelectedAction = AIActionType.None;
                                break;
                            case TeamType.Neutral:
                                 NeutralBuilding neutralRef = initialObjectiveBuildingInput as NeutralBuilding;
                                 if (neutralRef != null && neutralRef.IsRecapturable && selfUnit.IsBuildingInCaptureRange(neutralRef)) {
                                    finalSelectedAction = AIActionType.CaptureBuilding;
                                 } else {
                                    // Si neutre mais pas capturable en portée, ou pas un NeutralBuilding, juste se déplacer vers lui.
                                    finalSelectedAction = AIActionType.MoveToBuilding;
                                 }
                                break;
                            default:
                                finalSelectedAction = AIActionType.MoveToBuilding;
                                break;
                        }
                    }
                    else // Pas à portée de l'objectif initial
                    {
                        finalSelectedAction = AIActionType.MoveToBuilding;
                    }
                }
                else // Tuile du bâtiment initial est null (bâtiment détruit pendant ce tick?)
                {
                    if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = true;
                    finalSelectedAction = AIActionType.None;
                    finalMovementTargetPos = currentUnitPos;
                }
            }
        }
        // Priorité 5: Fallback to Idle (None) si aucune autre action n'a été déterminée
        else if (finalSelectedAction == AIActionType.None)
        {
            finalMovementTargetPos = currentUnitPos; // EXPLICITEMENT rester sur place
            finalInteractionUnit = null;
            finalInteractionBuilding = null;
            // finalSelectedAction est déjà AIActionType.None
        }

        // --- Write Outputs to Blackboard ---
        if (bbSelectedActionType != null) bbSelectedActionType.Value = finalSelectedAction;
        else LogNodeMessage("bbSelectedActionType is null, cannot write to Blackboard.", true, true);

        if (bbMovementTargetPosition != null) bbMovementTargetPosition.Value = finalMovementTargetPos;
        else LogNodeMessage("bbMovementTargetPosition is null, cannot write to Blackboard.", true, true);

        if (bbInteractionTargetUnit != null) bbInteractionTargetUnit.Value = finalInteractionUnit;
        else LogNodeMessage("bbInteractionTargetUnit is null, cannot write to Blackboard.", true, true);

        if (bbInteractionTargetBuilding != null) bbInteractionTargetBuilding.Value = finalInteractionBuilding;
        else LogNodeMessage("bbInteractionTargetBuilding is null, cannot write to Blackboard.", true, true);

        return Node.Status.Success; // La décision est prise et écrite.
    }

    protected override void OnEnd()
    {
        // Réinitialiser le cache pour la prochaine exécution.
        blackboardVariablesCached = false;
        bbSelfUnit = null;
        bbDetectedEnemyUnit = null;
        bbDetectedTacticalBuilding = null;
        bbHasInitialObjectiveSet = null;
        bbInitialTargetBuilding = null;
        bbIsAttacking = null;
        bbIsCapturing = null;
        bbIsObjectiveCompleted = null;
        bbSelectedActionType = null;
        bbMovementTargetPosition = null;
        bbInteractionTargetUnit = null;
        bbInteractionTargetBuilding = null;
    }

    // Appelé si selfUnit est null pour éviter des erreurs en aval.
    private void ResetOutputsToIdleDefaults(AllyUnit unitForCurrentPos)
    {
        if (bbSelectedActionType != null) bbSelectedActionType.Value = AIActionType.None;

        Vector2Int posToSet = Vector2Int.zero; // Valeur de secours
        if (unitForCurrentPos != null && unitForCurrentPos.GetOccupiedTile() != null)
        {
            var currentTile = unitForCurrentPos.GetOccupiedTile();
            posToSet = new Vector2Int(currentTile.column, currentTile.row);
        }
        if (bbMovementTargetPosition != null) bbMovementTargetPosition.Value = posToSet;

        if (bbInteractionTargetUnit != null) bbInteractionTargetUnit.Value = null;
        if (bbInteractionTargetBuilding != null) bbInteractionTargetBuilding.Value = null;
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null)
        {
            LogNodeMessage("BehaviorGraphAgent or BlackboardReference not found on GameObject.", true, true);
            return false;
        }
        var blackboard = agent.BlackboardReference;
        bool allFound = true; // Supposer que tout est trouvé initialement

        // Inputs
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit)) { LogNodeMessage($"Input BBVar '{SELF_UNIT_VAR}' missing.", true, true); allFound = false; }
        if (!blackboard.GetVariable(DETECTED_ENEMY_UNIT_VAR, out bbDetectedEnemyUnit)) { LogNodeMessage($"Input BBVar '{DETECTED_ENEMY_UNIT_VAR}' missing (considered optional for some paths).", false, true); /* Pas bloquant pour le cache */ }
        if (!blackboard.GetVariable(DETECTED_BUILDING_VAR, out bbDetectedTacticalBuilding)) { LogNodeMessage($"Input BBVar '{DETECTED_BUILDING_VAR}' missing (considered optional for some paths).", false, true); /* Pas bloquant */ }
        if (!blackboard.GetVariable(HAS_INITIAL_OBJECTIVE_SET_VAR, out bbHasInitialObjectiveSet)) { LogNodeMessage($"Input BBVar '{HAS_INITIAL_OBJECTIVE_SET_VAR}' missing.", true, true); allFound = false; }
        if (!blackboard.GetVariable(INITIAL_TARGET_BUILDING_VAR, out bbInitialTargetBuilding)) { LogNodeMessage($"Input BBVar '{INITIAL_TARGET_BUILDING_VAR}' missing.", true, true); allFound = false; } // Important s'il y a un objectif initial
        if (!blackboard.GetVariable(IS_ATTACKING_VAR, out bbIsAttacking)) { LogNodeMessage($"Input BBVar '{IS_ATTACKING_VAR}' missing.", true, true); allFound = false; }
        if (!blackboard.GetVariable(IS_CAPTURING_VAR, out bbIsCapturing)) { LogNodeMessage($"Input BBVar '{IS_CAPTURING_VAR}' missing.", true, true); allFound = false; }
        if (!blackboard.GetVariable(IS_OBJECTIVE_COMPLETED_VAR, out bbIsObjectiveCompleted)) { LogNodeMessage($"Input BBVar '{IS_OBJECTIVE_COMPLETED_VAR}' missing.", true, true); allFound = false; }

        // Outputs
        if (!blackboard.GetVariable(SELECTED_ACTION_TYPE_VAR, out bbSelectedActionType)) { LogNodeMessage($"Output BBVar '{SELECTED_ACTION_TYPE_VAR}' missing.", true, true); allFound = false; }
        if (!blackboard.GetVariable(MOVEMENT_TARGET_POS_VAR, out bbMovementTargetPosition)) { LogNodeMessage($"Output BBVar '{MOVEMENT_TARGET_POS_VAR}' missing.", true, true); allFound = false; }
        if (!blackboard.GetVariable(INTERACTION_TARGET_UNIT_VAR, out bbInteractionTargetUnit)) { LogNodeMessage($"Output BBVar '{INTERACTION_TARGET_UNIT_VAR}' missing.", true, true); allFound = false; }
        if (!blackboard.GetVariable(INTERACTION_TARGET_BUILDING_VAR, out bbInteractionTargetBuilding)) { LogNodeMessage($"Output BBVar '{INTERACTION_TARGET_BUILDING_VAR}' missing.", true, true); allFound = false; }

        if (!allFound)
        {
             Debug.LogError($"[{GameObject?.name} - SelectTargetNode] CRITICAL Blackboard variable(s) (Inputs or Outputs) missing during CacheBlackboardVariables. Node WILL FAIL. Check console for details.");
        }

        blackboardVariablesCached = allFound; // Le cache n'est valide que si TOUTES les variables critiques sont trouvées
        return allFound;
    }

    // Wrapper pour LogFailure pour ajouter le nom du GameObject au message
    private void LogNodeMessage(string message, bool isError, bool forceLog = false)
    {
    }
}