using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Select Target (Enemy)",
    story: "Select Target (Enemy)",
    category: "Enemy Actions",
    id: "EnemyAction_SelectTarget_v2"
)]
public partial class SelectTargetNode_Enemy : Unity.Behavior.Action
{
    // --- Entrées (lues depuis le Blackboard) ---
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<CurrentBehaviorMode> bbCurrentBehaviorMode;
    private BlackboardVariable<Building> bbObjectiveBuilding;
    private BlackboardVariable<AllyUnit> bbDetectedPlayerUnit;
    private BlackboardVariable<Building> bbDetectedTargetableBuilding;
    private BlackboardVariable<bool> bbIsObjectiveCompleted;
    private BlackboardVariable<bool> bbIsMoving;
    private BlackboardVariable<bool> bbIsAttacking;
    private BlackboardVariable<bool> bbIsCapturing;

    // --- Sorties (écrites sur le Blackboard) ---
    private BlackboardVariable<AIActionType> bbSelectedActionType;
    // MODIFICATION : Changer le nom de la variable membre et la clé Blackboard cible
    // Anciennement: private BlackboardVariable<Vector2Int> bbMovementTargetPosition;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPositionOutput; // Nouvelle variable membre pour la clarté
    // NOUVELLE CONSTANTE pour correspondre à ce que FindSmartStepNode lit
    private const string OUTPUT_FINAL_DESTINATION_KEY = "FinalDestinationPosition";

    private BlackboardVariable<Unit> bbInteractionTargetUnit;
    private BlackboardVariable<Building> bbInteractionTargetBuilding;

    private bool blackboardVariablesCached = false;
    private EnemyUnit selfUnitCache;
    private BehaviorGraphAgent agent;
    private string nodeInstanceId;

    protected override Status OnStart()
    {
        nodeInstanceId = Guid.NewGuid().ToString("N").Substring(0, 6);
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (!CacheBlackboardVariables())
        {
            LogNodeMessage("Failed to cache BB variables in OnStart.", true, true);
            ClearOutputs();
            return Status.Failure;
        }
        return Status.Running;
    }

    private void LogNodeMessage(string message, bool isError = false, bool forceLog = false)
    {
        /*
        Unit unitForLog = selfUnitCache ?? bbSelfUnit?.Value;
        string unitName = unitForLog != null ? unitForLog.name : (GameObject != null ? GameObject.name : "SelectTargetNode_Enemy");
        bool enableLogging = false;
        if (unitForLog is EnemyUnit enemyLog) enableLogging = enemyLog.enableVerboseLogging;


        string logPrefix = $"[{nodeInstanceId} | {unitName} | SelectTarget_Enemy]";

        if (isError) Debug.LogError($"{logPrefix} {message}", GameObject);
        else if (forceLog || enableLogging) Debug.Log($"{logPrefix} {message}", GameObject);
        */
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;
        if (agent == null || agent.BlackboardReference == null) {
            if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
            if (agent == null || agent.BlackboardReference == null) {
                LogNodeMessage("Agent or BlackboardRef missing.", true); return false;
            }
        }
        var blackboard = agent.BlackboardReference;
        bool success = true;

        if (!blackboard.GetVariable(EnemyUnit.BB_SELF_UNIT, out bbSelfUnit)) { LogNodeMessage($"BBVar '{EnemyUnit.BB_SELF_UNIT}' missing.", true); success = false; }
        if (success && bbSelfUnit.Value != null) {
            selfUnitCache = bbSelfUnit.Value as EnemyUnit;
            if (selfUnitCache == null) {
                LogNodeMessage($"BBVar '{EnemyUnit.BB_SELF_UNIT}' (type Unit) could not be cast to EnemyUnit.", true); success = false;
            }
        } else if (success && bbSelfUnit.Value == null) {
             LogNodeMessage($"BBVar '{EnemyUnit.BB_SELF_UNIT}' value is null. Ensure Initializer runs.", true); success = false;
        }

        if (!blackboard.GetVariable(EnemyUnit.BB_CURRENT_BEHAVIOR_MODE, out bbCurrentBehaviorMode)) { LogNodeMessage($"BBVar '{EnemyUnit.BB_CURRENT_BEHAVIOR_MODE}' missing.", true); success = false; }
        if (!blackboard.GetVariable(EnemyUnit.BB_OBJECTIVE_BUILDING, out bbObjectiveBuilding)) { LogNodeMessage($"BBVar '{EnemyUnit.BB_OBJECTIVE_BUILDING}' (optional) missing.", false); }
        if (!blackboard.GetVariable(EnemyUnit.BB_DETECTED_PLAYER_UNIT, out bbDetectedPlayerUnit)) { LogNodeMessage($"BBVar '{EnemyUnit.BB_DETECTED_PLAYER_UNIT}' (optional) missing.", false); }
        if (!blackboard.GetVariable(EnemyUnit.BB_DETECTED_TARGETABLE_BUILDING, out bbDetectedTargetableBuilding)) { LogNodeMessage($"BBVar '{EnemyUnit.BB_DETECTED_TARGETABLE_BUILDING}' (optional) missing.", false); }
        if (!blackboard.GetVariable(EnemyUnit.BB_IS_OBJECTIVE_COMPLETED, out bbIsObjectiveCompleted)) { LogNodeMessage($"BBVar '{EnemyUnit.BB_IS_OBJECTIVE_COMPLETED}' missing.", true); success = false; }
        if (!blackboard.GetVariable(EnemyUnit.BB_IS_MOVING, out bbIsMoving)) { LogNodeMessage($"BBVar '{EnemyUnit.BB_IS_MOVING}' missing.", true); success = false; }
        if (!blackboard.GetVariable(EnemyUnit.BB_IS_ATTACKING, out bbIsAttacking)) { LogNodeMessage($"BBVar '{EnemyUnit.BB_IS_ATTACKING}' missing.", true); success = false; }
        if (!blackboard.GetVariable(EnemyUnit.BB_IS_CAPTURING, out bbIsCapturing)) { LogNodeMessage($"BBVar '{EnemyUnit.BB_IS_CAPTURING}' missing.", true); success = false; }
        if (!blackboard.GetVariable(EnemyUnit.BB_SELECTED_ACTION_TYPE, out bbSelectedActionType)) { LogNodeMessage($"BBVar '{EnemyUnit.BB_SELECTED_ACTION_TYPE}' missing.", true); success = false; }

        // MODIFICATION : Utiliser la nouvelle clé pour la sortie de destination
        if (!blackboard.GetVariable(OUTPUT_FINAL_DESTINATION_KEY, out bbFinalDestinationPositionOutput)) { LogNodeMessage($"BBVar '{OUTPUT_FINAL_DESTINATION_KEY}' missing.", true); success = false; }

        if (!blackboard.GetVariable(EnemyUnit.BB_INTERACTION_TARGET_UNIT, out bbInteractionTargetUnit)) { LogNodeMessage($"BBVar '{EnemyUnit.BB_INTERACTION_TARGET_UNIT}' missing.", true); success = false; }
        if (!blackboard.GetVariable(EnemyUnit.BB_INTERACTION_TARGET_BUILDING, out bbInteractionTargetBuilding)) { LogNodeMessage($"BBVar '{EnemyUnit.BB_INTERACTION_TARGET_BUILDING}' missing.", true); success = false; }

        blackboardVariablesCached = success;
        return success;
    }

protected override Status OnUpdate()
{
    // Récupérer selfUnitCache si besoin (normalement fait dans OnStart ou CacheBlackboardVariables)
    if (selfUnitCache == null) {
        if (bbSelfUnit?.Value != null) selfUnitCache = bbSelfUnit.Value as EnemyUnit;
        if (selfUnitCache == null) {
            LogNodeMessage("SelfUnitCache is NULL in OnUpdate. Node Failure.", true, true);
            ClearOutputs(); return Status.Failure;
        }
    }

    // Vérification des références Blackboard critiques
    if (bbIsMoving == null || bbIsAttacking == null || bbIsCapturing == null ||
        bbCurrentBehaviorMode == null || bbIsObjectiveCompleted == null ||
        bbSelectedActionType == null || bbFinalDestinationPositionOutput == null || // Utilise bbFinalDestinationPositionOutput
        bbInteractionTargetUnit == null || bbInteractionTargetBuilding == null)
    {
        LogNodeMessage("Une ou plusieurs références Blackboard critiques sont nulles dans OnUpdate. Tentative de recache...", true, true);
        if (!CacheBlackboardVariables() || selfUnitCache == null) { // selfUnitCache est re-vérifié au cas où CacheBlackboardVariables l'affecterait
             LogNodeMessage("Recache ÉCHOUÉ ou SelfUnit toujours null dans OnUpdate. Node Failure.", true, true);
             ClearOutputs(); return Status.Failure;
        }
        // Revérifier après recache
        if (bbIsMoving == null || bbIsAttacking == null || bbIsCapturing == null || bbFinalDestinationPositionOutput == null || bbInteractionTargetBuilding == null) // Ajoutez toutes les vérifications nécessaires
        {
             LogNodeMessage("Toujours des BBVars critiques manquantes après recache dans OnUpdate. Node Failure.", true, true);
             ClearOutputs(); return Status.Failure;
        }
    }

    // Si une action majeure est déjà en cours (gérée par un autre nœud qui met ces flags BB),
    // ce nœud ne devrait pas prendre de nouvelle décision de *cible* pour le moment.
    if (bbIsMoving.Value || bbIsAttacking.Value || bbIsCapturing.Value)
    {
        LogNodeMessage($"Action déjà en cours (Move:{bbIsMoving.Value}, Attack:{bbIsAttacking.Value}, Capture:{bbIsCapturing.Value}). Aucune nouvelle décision de cible pour ce tick.", false, true);
        // Il est important que le nœud retourne quand même Success pour que le graph continue
        // et que les nœuds d'action en cours (Move, Attack, Capture) puissent s'exécuter.
        // Ne pas appeler ClearOutputs() ici car les sorties existantes sont peut-être toujours pertinentes pour l'action en cours.
        return Status.Success;
    }

    // Lecture des entrées du Blackboard
    CurrentBehaviorMode currentMode = bbCurrentBehaviorMode.Value;
    Building currentObjectiveBuilding = bbObjectiveBuilding?.Value; // Peut être null
    AllyUnit detectedPlayer = bbDetectedPlayerUnit?.Value;        // Peut être null
    Building detectedLocalBuilding = bbDetectedTargetableBuilding?.Value; // Peut être null
    bool isObjectiveFlagCompleted = bbIsObjectiveCompleted.Value;

    // Variables locales pour la décision
    AIActionType selectedAction = AIActionType.None;
    Tile selfTile = selfUnitCache.GetOccupiedTile();
    // Initialiser movementTargetPos à une position invalide ou à la position actuelle si valide.
    // Cela sera la "destination finale" que ce nœud décide.
    Vector2Int finalDestinationForPathfinding = selfTile != null ? new Vector2Int(selfTile.column, selfTile.row) : new Vector2Int(-1,-1);
    Unit interactionUnit = null;
    Building interactionBuilding = null;

    // --- Début de la Logique de Décision ---
    if (currentMode == CurrentBehaviorMode.ObjectiveFocused)
    {
        bool objectiveInvalidOrDone = isObjectiveFlagCompleted ||
                                      currentObjectiveBuilding == null ||
                                      currentObjectiveBuilding.CurrentHealth <= 0 ||
                                      (currentObjectiveBuilding.Team == TeamType.Enemy && !(currentObjectiveBuilding is NeutralBuilding && ((NeutralBuilding)currentObjectiveBuilding).IsRecapturable));

        if (objectiveInvalidOrDone)
        {
            LogNodeMessage("ObjectiveFocused: Objectif actuel invalide ou complété. Recherche d'un nouvel objectif sur la carte.", false, true);
            Building newMapObjective = FindNewObjectiveBuildingOnMap(selfUnitCache);
            if (newMapObjective != null)
            {
                LogNodeMessage($"Nouvel objectif trouvé sur la carte: {newMapObjective.name}. Mise à jour du Blackboard.", false, true);
                if(bbObjectiveBuilding != null) bbObjectiveBuilding.Value = newMapObjective;
                else LogNodeMessage("bbObjectiveBuilding est null, impossible d'écrire.", true);

                if(bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = false;
                else LogNodeMessage("bbIsObjectiveCompleted est null, impossible d'écrire.", true);

                currentObjectiveBuilding = newMapObjective; // Mettre à jour la variable locale pour la suite de cette frame
            }
            else
            {
                LogNodeMessage("ObjectiveFocused: Aucun nouvel objectif trouvé sur la carte. Passage en mode Défensif.", false, true);
                if(bbCurrentBehaviorMode != null) bbCurrentBehaviorMode.Value = CurrentBehaviorMode.Defensive;
                else LogNodeMessage("bbCurrentBehaviorMode est null, impossible d'écrire.", true);
                // Laisser selectedAction à None, le graph réévaluera en mode Defensive au prochain tick.
                // Mettre à jour les sorties avec "None" pour ce tick.
                UpdateBlackboardOutputs(AIActionType.None, finalDestinationForPathfinding, null, null);
                return Status.Success; // Permet au graph de changer de branche/réévaluer
            }
        }

        // Logique ObjectiveFocused (poursuite de l'objectif actuel ou du nouvel objectif)
        if (detectedPlayer != null && detectedPlayer.Health > 0 && IsPlayerThreatening(detectedPlayer))
        {
            interactionUnit = detectedPlayer;
            Tile playerTile = detectedPlayer.GetOccupiedTile();
            if (playerTile != null) {
                finalDestinationForPathfinding = new Vector2Int(playerTile.column, playerTile.row);
                selectedAction = selfUnitCache.IsUnitInRange(detectedPlayer) ? AIActionType.AttackUnit : AIActionType.MoveToUnit;
            } else { LogNodeMessage($"Joueur menaçant {detectedPlayer.name} n'a pas de tuile.", true); }
        }
        else if (currentObjectiveBuilding != null && currentObjectiveBuilding.CurrentHealth > 0)
        {
            interactionBuilding = currentObjectiveBuilding;
            Tile objectiveTile = currentObjectiveBuilding.GetOccupiedTile();
            if (objectiveTile != null) {
                finalDestinationForPathfinding = new Vector2Int(objectiveTile.column, objectiveTile.row);
                if (selfUnitCache.IsBuildingInRange(currentObjectiveBuilding))
                {
                    NeutralBuilding nb = currentObjectiveBuilding as NeutralBuilding;
                    if (nb != null && nb.IsRecapturable && nb.Team != TeamType.Enemy) // Peut capturer Joueur ou Neutre
                        selectedAction = AIActionType.CaptureBuilding;
                    else if (currentObjectiveBuilding.Team == TeamType.Player) // Attaquer bâtiment joueur non capturable
                        selectedAction = AIActionType.AttackBuilding;
                    else { // Objectif Ennemi (et non recapturable) ou situation anormale
                        LogNodeMessage($"Objectif '{currentObjectiveBuilding.name}' (Équipe: {currentObjectiveBuilding.Team}) est considéré comme complété ou cible non valide pour l'ennemi. Signalement de complétion.", false, true);
                        if(bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = true;
                        selectedAction = AIActionType.None; // L'objectif est "atteint"
                    }
                } else { selectedAction = AIActionType.MoveToBuilding; }
            } else { LogNodeMessage($"Objectif actuel {currentObjectiveBuilding.name} n'a pas de tuile.", true); selectedAction = AIActionType.None; }
        } else {
             LogNodeMessage("ObjectiveFocused: Pas d'objectif valide (même après recherche potentielle). Reste en Idle pour ce tick.", false, true);
             selectedAction = AIActionType.None; // S'il n'y a plus d'objectif après la recherche
        }
    }
    else if (currentMode == CurrentBehaviorMode.Defensive)
    {
        if (detectedPlayer != null && detectedPlayer.Health > 0)
        {
            interactionUnit = detectedPlayer;
            Tile playerTile = detectedPlayer.GetOccupiedTile();
            if (playerTile != null) {
                finalDestinationForPathfinding = new Vector2Int(playerTile.column, playerTile.row);
                selectedAction = selfUnitCache.IsUnitInRange(detectedPlayer) ? AIActionType.AttackUnit : AIActionType.MoveToUnit;
            } else { LogNodeMessage($"Défensif: Joueur Détecté {detectedPlayer.name} n'a pas de tuile.", true); }
        }
        else if (detectedLocalBuilding != null && detectedLocalBuilding.CurrentHealth > 0 &&
                 (detectedLocalBuilding.Team == TeamType.Player || detectedLocalBuilding.Team == TeamType.Neutral)) // L'ennemi attaque Joueur/Neutre en défense
        {
            interactionBuilding = detectedLocalBuilding; // Cible le bâtiment détecté localement
            Tile buildingTile = detectedLocalBuilding.GetOccupiedTile();
            if(buildingTile != null) {
                finalDestinationForPathfinding = new Vector2Int(buildingTile.column, buildingTile.row);
                if (selfUnitCache.IsBuildingInRange(detectedLocalBuilding)) {
                    NeutralBuilding nb = detectedLocalBuilding as NeutralBuilding;
                    if (nb != null && nb.IsRecapturable && nb.Team != TeamType.Enemy) // L'ennemi peut capturer un bâtiment Joueur/Neutre qui est recapturable
                        selectedAction = AIActionType.CaptureBuilding;
                    else if (detectedLocalBuilding.Team == TeamType.Player) // Attaquer bâtiment joueur non capturable
                        selectedAction = AIActionType.AttackBuilding;
                    // Si c'est un bâtiment Neutre non-NeutralBuilding ou non-recapturable, que faire ?
                    // Pour l'instant, on ne l'attaque pas, on pourrait juste se déplacer vers lui ou l'ignorer.
                    // Ici, on le laisse à None si aucune des conditions ci-dessus n'est remplie.
                    else selectedAction = AIActionType.None;
                } else { selectedAction = AIActionType.MoveToBuilding; }
            } else { LogNodeMessage($"Défensif: Bâtiment Détecté {detectedLocalBuilding.name} n'a pas de tuile.", true); }
        }
        // Si rien n'est détecté en mode défensif, selectedAction reste None, et finalDestinationForPathfinding reste la position actuelle (ou -1,-1).
    }

    // --- AJUSTEMENT FINAL de finalDestinationForPathfinding pour les actions immédiates ---
    if (selectedAction == AIActionType.AttackBuilding ||
        selectedAction == AIActionType.AttackUnit ||
        selectedAction == AIActionType.CaptureBuilding)
    {
        bool isInRangeForImmediateAction = false;
        if (selectedAction == AIActionType.AttackBuilding && interactionBuilding != null && selfUnitCache.IsBuildingInRange(interactionBuilding))
            isInRangeForImmediateAction = true;
        else if (selectedAction == AIActionType.AttackUnit && interactionUnit != null && selfUnitCache.IsUnitInRange(interactionUnit))
            isInRangeForImmediateAction = true;
        else if (selectedAction == AIActionType.CaptureBuilding && interactionBuilding != null && selfUnitCache.IsBuildingInCaptureRange(interactionBuilding))
            isInRangeForImmediateAction = true;

        if (isInRangeForImmediateAction)
        {
            if (selfTile != null)
            {
                finalDestinationForPathfinding = new Vector2Int(selfTile.column, selfTile.row);
                LogNodeMessage($"Action '{selectedAction}' depuis la position actuelle. FinalDestination réglée sur la tuile actuelle: ({selfTile.column},{selfTile.row}).", false, true);
            }
            else
            {
                LogNodeMessage("selfTile est null lors de la tentative de régler FinalDestination sur la position actuelle pour une action immédiate. C'est inattendu. Action pourrait échouer.", true, true);
                selectedAction = AIActionType.None; // Annuler l'action si on ne peut pas confirmer la position actuelle
            }
        }
        // Si ce n'est pas une action immédiate ou si l'unité n'est PAS à portée (par exemple, selectedAction est MoveTo...),
        // alors finalDestinationForPathfinding aura déjà été défini sur la tuile de la CIBLE par la logique de décision précédente.
    }
    else if (selectedAction == AIActionType.None) // Si aucune action, la destination est la position actuelle.
    {
        if (selfTile != null) {
            finalDestinationForPathfinding = new Vector2Int(selfTile.column, selfTile.row);
        } else {
            // Si selfTile est null et aucune action, on ne peut pas déterminer une destination "actuelle" valide.
            finalDestinationForPathfinding = new Vector2Int(-1,-1); // Indiquer une position invalide
        }
    }
    // --- Fin de l'Ajustement ---

    UpdateBlackboardOutputs(selectedAction, finalDestinationForPathfinding, interactionUnit, interactionBuilding);
    return Status.Success;
}

    private void UpdateBlackboardOutputs(AIActionType action, Vector2Int movePos, Unit iUnit, Building iBuilding)
    {
        if(bbSelectedActionType != null) bbSelectedActionType.Value = action;
        else LogNodeMessage("bbSelectedActionType is null, cannot write to Blackboard.", true, true);

        // MODIFICATION : Écrire dans bbFinalDestinationPositionOutput (qui utilise la clé "FinalDestinationPosition")
        if(bbFinalDestinationPositionOutput != null) bbFinalDestinationPositionOutput.Value = movePos;
        else LogNodeMessage($"bbFinalDestinationPositionOutput (pour clé '{OUTPUT_FINAL_DESTINATION_KEY}') is null, cannot write to Blackboard.", true, true);

        if(bbInteractionTargetUnit != null) bbInteractionTargetUnit.Value = iUnit;
        else LogNodeMessage("bbInteractionTargetUnit is null, cannot write to Blackboard.", true, true);

        if(bbInteractionTargetBuilding != null) bbInteractionTargetBuilding.Value = iBuilding;
        else LogNodeMessage("bbInteractionTargetBuilding is null, cannot write to Blackboard.", true, true);

        LogNodeMessage($"Decision Output: Action={action}, FinalDestOutput={movePos}, InteractUnit={(iUnit?.name ?? "None")}, InteractBuilding={(iBuilding?.name ?? "None")}", false, true);
    }

    // ... (FindNewObjectiveBuildingOnMap, IsPlayerThreatening, ClearOutputs, OnEnd restent majoritairement les mêmes) ...
    private Building FindNewObjectiveBuildingOnMap(EnemyUnit searchingUnit)
    {
        if (HexGridManager.Instance == null || searchingUnit == null) return null;

        Building closestSuitableBuilding = null;
        float minDistanceSq = float.MaxValue;
        Vector3 searcherPosition = searchingUnit.transform.position;

        for (int col = 0; col < HexGridManager.Instance.columns; col++)
        {
            for (int row = 0; row < HexGridManager.Instance.rows; row++)
            {
                Tile tile = HexGridManager.Instance.GetTileAt(col, row);
                if (tile == null || tile.currentBuilding == null) continue;

                Building building = tile.currentBuilding;

                if (searchingUnit.IsValidBuildingTarget(building) && building.CurrentHealth > 0)
                {
                    float distSq = (building.transform.position - searcherPosition).sqrMagnitude;
                    if (distSq < minDistanceSq)
                    {
                        minDistanceSq = distSq;
                        closestSuitableBuilding = building;
                    }
                }
            }
        }
        if (closestSuitableBuilding != null) LogNodeMessage($"FindNewObjective: Found '{closestSuitableBuilding.name}' (Team: {closestSuitableBuilding.Team}) as closest new objective.", false, true);
        else LogNodeMessage("FindNewObjective: No suitable new objective building found on map.", false, true);
        return closestSuitableBuilding;
    }


    private bool IsPlayerThreatening(AllyUnit playerUnit)
    {
        if (playerUnit == null || selfUnitCache == null) return false;
        Tile playerTile = playerUnit.GetOccupiedTile();
        Tile selfTile = selfUnitCache.GetOccupiedTile();
        if (playerTile == null || selfTile == null) return false;
        int distance = HexGridManager.Instance.HexDistance(selfTile.column, selfTile.row, playerTile.column, playerTile.row);
        return distance <= selfUnitCache.DetectionRange / 2 || distance <= 2;
    }

    private void ClearOutputs()
    {
        UpdateBlackboardOutputs(AIActionType.None, new Vector2Int(-1,-1), null, null);
    }

    protected override void OnEnd()
    {
        blackboardVariablesCached = false;
        selfUnitCache = null;
        // Les autres références bbXXX seront réinitialisées au prochain CacheBlackboardVariables
    }
}