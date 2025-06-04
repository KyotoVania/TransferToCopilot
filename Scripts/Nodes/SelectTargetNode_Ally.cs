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
    name: "Select Target Ally",
    description: "Advanced target selection for ally units with defensive mode support",
    category: "Action"
)]
public partial class SelectTargetNode_Ally : Unity.Behavior.Action
{
   // Input variables du Blackboard
    private const string BB_HAS_BANNER_TARGET = "HasBannerTarget"; // Lu pour info, mais n'affecte plus l'objectif post-spawn
    private const string BB_BANNER_TARGET_POSITION = "BannerTargetPosition"; // Lu pour info
    private const string BB_HAS_INITIAL_OBJECTIVE_SET = "HasInitialObjectiveSet";
    private const string BB_INITIAL_TARGET_BUILDING = "InitialTargetBuilding";
    private const string BB_IS_OBJECTIVE_COMPLETED = "IsObjectiveCompleted";
    private const string BB_IS_ATTACKING = "IsAttacking"; // Doit être mis en cache
    private const string BB_IS_CAPTURING = "IsCapturing"; // Doit être mis en cache
    private const string BB_IS_MOVING = "IsMoving";       // Doit être mis en cache
    private const string BB_DETECTED_ENEMY_UNIT = "DetectedEnemyUnit"; // Doit être mis en cache
    private const string BB_IS_DEFENDING_INPUT = "IsDefending"; // Pour lire si on est déjà en train de défendre

    // Output variables du Blackboard
    private const string BB_FINAL_DESTINATION_POSITION = "FinalDestinationPosition";
    private const string BB_SELECTED_ACTION_TYPE = "SelectedActionType";
    private const string BB_INTERACTION_TARGET_UNIT = "InteractionTargetUnit";
    private const string BB_INTERACTION_TARGET_BUILDING = "InteractionTargetBuilding";
    private const string BB_IS_IN_DEFENSIVE_MODE = "IsInDefensiveMode";


    private BlackboardVariable<bool> bbHasBannerTarget;
    private BlackboardVariable<Vector2Int> bbBannerTargetPosition;
    private BlackboardVariable<bool> bbHasInitialObjectiveSet;
    private BlackboardVariable<Building> bbInitialTargetBuilding;
    private BlackboardVariable<bool> bbIsObjectiveCompleted;
    private BlackboardVariable<bool> bbIsAttacking;
    private BlackboardVariable<bool> bbIsCapturing;
    private BlackboardVariable<bool> bbIsMoving;
    private BlackboardVariable<Unit> bbDetectedEnemyUnit; // Pour lire l'ennemi détecté par ScanNode
    private BlackboardVariable<bool> bbIsDefending; // Pour lire l'état actuel
    
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    private BlackboardVariable<AIActionType> bbSelectedActionType;
    private BlackboardVariable<Unit> bbInteractionTargetUnit;
    private BlackboardVariable<Building> bbInteractionTargetBuilding;
    private BlackboardVariable<bool> bbIsInDefensiveMode;

    private AllyUnit selfUnit;
    private bool debugLogging = true;
    private bool blackboardVariablesCached = false;

    protected override Status OnStart()
    {
        // Cache la référence à l'unité
        selfUnit = GameObject.GetComponent<AllyUnit>();
        if (selfUnit == null)
        {
            LogMessage("ERROR: AllyUnit component not found!", true);
            return Status.Failure;
        }

        debugLogging = selfUnit.enableVerboseLogging;
        
        // Cache les variables blackboard
        if (!CacheBlackboardVariables())
        {
            LogMessage("ERROR: Failed to cache blackboard variables!", true);
            return Status.Failure;
        }

        return Status.Running;
    }

    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        var agent = GameObject.GetComponent<BehaviorGraphAgent>();
        if (agent == null || agent.BlackboardReference == null)
        {
            LogMessage("ERROR: Agent or BlackboardReference missing!", true);
            return false;
        }

        var blackboard = agent.BlackboardReference;
        bool success = true;

        // Cache input variables
        if (!blackboard.GetVariable("HasBannerTarget", out bbHasBannerTarget))
        {
            LogMessage("Warning: HasBannerTarget not found in blackboard", true);
            // Pas critique, continue
        }
        
        if (!blackboard.GetVariable("BannerTargetPosition", out bbBannerTargetPosition))
        {
            LogMessage("Warning: BannerTargetPosition not found in blackboard", true);
            // Pas critique, continue
        }
        if (!blackboard.GetVariable(BB_IS_DEFENDING_INPUT, out bbIsDefending)) { LogMessage("Warning: IsDefending (input) not found", false); /* Pas bloquant mais important pour la logique de défense*/ }

        if (!blackboard.GetVariable("HasInitialObjectiveSet", out bbHasInitialObjectiveSet))
        {
            LogMessage("Warning: HasInitialObjectiveSet not found in blackboard", true);
        }

        if (!blackboard.GetVariable("InitialTargetBuilding", out bbInitialTargetBuilding))
        {
            LogMessage("Warning: InitialTargetBuilding not found in blackboard", true);
        }

        if (!blackboard.GetVariable("IsObjectiveCompleted", out bbIsObjectiveCompleted))
        {
            LogMessage("Warning: IsObjectiveCompleted not found in blackboard", true);
        }

        // Cache output variables - ESSENTIELS
        if (!blackboard.GetVariable("FinalDestinationPosition", out bbFinalDestinationPosition))
        {
            LogMessage("ERROR: FinalDestinationPosition not found in blackboard!", true);
            success = false;
        }

        if (!blackboard.GetVariable("SelectedActionType", out bbSelectedActionType))
        {
            LogMessage("ERROR: SelectedActionType not found in blackboard!", true);
            success = false;
        }

        if (!blackboard.GetVariable("InteractionTargetUnit", out bbInteractionTargetUnit))
        {
            LogMessage("Warning: InteractionTargetUnit not found in blackboard", true);
        }

        if (!blackboard.GetVariable("InteractionTargetBuilding", out bbInteractionTargetBuilding))
        {
            LogMessage("Warning: InteractionTargetBuilding not found in blackboard", true);
        }

        if (!blackboard.GetVariable("IsInDefensiveMode", out bbIsInDefensiveMode))
        {
            LogMessage("Warning: IsInDefensiveMode not found in blackboard", true);
        }
        if (!blackboard.GetVariable("FinalDestinationPosition", out bbFinalDestinationPosition))
        {
            LogMessage("ERROR: FinalDestinationPosition not found in blackboard!", true);
            success = false;
        }
        blackboardVariablesCached = success;
        if (success)
        {
            LogMessage("Successfully cached all essential blackboard variables");
        }
        else
        {
            LogMessage("ERROR: Failed to cache essential blackboard variables!", true);
        }

        return success;
    }

protected override Status OnUpdate()
{
    if (selfUnit == null || !blackboardVariablesCached)
    {
        LogMessage("ERROR: selfUnit is null or Blackboard variables not cached in OnUpdate. Attempting re-cache.", true);
        if (!CacheBlackboardVariables() || selfUnit == null) // selfUnit pourrait être initialisé dans CacheBlackboardVariables
        {
            LogMessage("ERROR: Re-cache failed or selfUnit still null. Node Failure.", true);
            ClearOutputs(); // S'assurer que les sorties sont propres en cas d'échec
            return Status.Failure;
        }
    }

    ClearOutputs(); // Réinitialiser les sorties au début de chaque évaluation

    // Lire les valeurs actuelles du Blackboard
    bool isCurrentlyAttacking = bbIsAttacking?.Value ?? false;
    bool isCurrentlyCapturing = bbIsCapturing?.Value ?? false;
    bool isCurrentlyMoving = bbIsMoving?.Value ?? false; // Supposant que vous avez ce flag issu de MoveToTargetNode
    bool isCurrentlyDefendingState = bbIsDefending?.Value ?? false; // Lire l'état de défense actuel

    // Si l'unité est déjà engagée dans une action gérée par un autre noeud (qui mettrait ces flags),
    // ce noeud ne devrait pas prendre de nouvelle décision de haut niveau pour l'instant.
    if (isCurrentlyAttacking || isCurrentlyCapturing || isCurrentlyMoving || isCurrentlyDefendingState)
    {
        LogMessage($"Action en cours (Attack:{isCurrentlyAttacking}, Capture:{isCurrentlyCapturing}, Move:{isCurrentlyMoving}, Defend:{isCurrentlyDefendingState}). SelectTargetNode_Ally ne prend pas de nouvelle décision ce tick.", false);
        // Si l'action est "DefendPosition", il faut s'assurer que la destination est bien la case de réserve.
        // Si c'est une autre action (Move, Attack, Capture), on ne touche pas aux outputs.
        if (isCurrentlyDefendingState && selfUnit.currentReserveTile != null)
        {
            if (bbFinalDestinationPosition != null) bbFinalDestinationPosition.Value = new Vector2Int(selfUnit.currentReserveTile.column, selfUnit.currentReserveTile.row);
            if (bbSelectedActionType != null) bbSelectedActionType.Value = AIActionType.DefendPosition; // Maintien de l'action de défense
        }
        // Si l'action est Move, Attack, Capture, les outputs sont déjà gérés par la décision précédente qui a mené à ces états.
        // On ne les écrase pas ici.
        return Status.Success;
    }


    bool hasInitialObjective = bbHasInitialObjectiveSet?.Value ?? false;
    bool isInitialObjectiveCompleted = bbIsObjectiveCompleted?.Value ?? false;
    Building initialObjectiveBuilding = hasInitialObjective ? bbInitialTargetBuilding?.Value : null;

    bool hasBanner = bbHasBannerTarget?.Value ?? false;
    Vector2Int bannerPos = hasBanner ? (bbBannerTargetPosition?.Value ?? new Vector2Int(-1, -1)) : new Vector2Int(-1, -1);
    Building buildingAtBanner = hasBanner ? selfUnit.FindBuildingAtPosition(bannerPos) : null;

    // --- Variables de décision pour ce tick ---
    AIActionType decidedAction = AIActionType.None;
    Tile currentUnitTileForPos = selfUnit.GetOccupiedTile();
    Vector2Int decidedDestination = currentUnitTileForPos != null ? new Vector2Int(currentUnitTileForPos.column, currentUnitTileForPos.row) : new Vector2Int(-1, -1);
    Building decidedInteractionBuilding = null;
    Unit decidedInteractionUnit = null;
    bool decidedDefensiveModeFlag = false; // Ce flag indique si le *mode* général est défensif

    // --- PRIORITÉ 1: Objectif Initial (si actif et non complété) ---
    if (hasInitialObjective && !isInitialObjectiveCompleted && initialObjectiveBuilding != null && initialObjectiveBuilding.CurrentHealth > 0)
    {
        LogMessage($"Poursuite de l'Objectif Initial: {initialObjectiveBuilding.name} (Team: {initialObjectiveBuilding.Team})", false);
        decidedInteractionBuilding = initialObjectiveBuilding;
        Tile objectiveTile = initialObjectiveBuilding.GetOccupiedTile();

        if (objectiveTile == null) { // Bâtiment objectif détruit ou invalide entre-temps
            LogMessage($"Objectif Initial '{initialObjectiveBuilding.name}' n'a plus de tuile valide. Marqué comme complété.", true);
            if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = true; // Mettre à jour le BB
            isInitialObjectiveCompleted = true; // Mettre à jour la variable locale pour la logique de ce tick
            decidedInteractionBuilding = null; // Plus de bâtiment cible
            // La logique tombera dans la section "Objectif Complété" ou "Bannière" ensuite
        } else {
            decidedDestination = new Vector2Int(objectiveTile.column, objectiveTile.row);

            if (initialObjectiveBuilding.Team == TeamType.Player) 
            {
                decidedDefensiveModeFlag = true; // Le mode est défensif
                PlayerBuilding pb = initialObjectiveBuilding as PlayerBuilding;
                Tile reserveTile = pb?.GetAvailableReserveTileForUnit(selfUnit);

                if (reserveTile != null) {
                    LogMessage($"Objectif Initial Défensif: {initialObjectiveBuilding.name}. Case de réserve disponible: ({reserveTile.column},{reserveTile.row})", false);
                    decidedDestination = new Vector2Int(reserveTile.column, reserveTile.row);
                    selfUnit.SetReservePosition(pb, reserveTile); // IMPORTANT: Marquer la réservation
                } else 
                {
                    LogMessage($"Objectif Initial Défensif: {initialObjectiveBuilding.name}. Pas de case de réserve dispo ou pas un PlayerBuilding. Cible bâtiment lui-même.", false);
                    // decidedDestination est déjà la tuile du bâtiment.
                }
                // Vérifier si l'unité est DÉJÀ à la destination défensive
                if (selfUnit.GetOccupiedTile() == reserveTile && reserveTile != null) // Si déjà sur SA case de réserve
                {
                    decidedAction = AIActionType.DefendPosition; // Nouvelle action
                    LogMessage($"En position défensive ({reserveTile.column},{reserveTile.row}) sur l'objectif initial. Action: DefendPosition.", false);
                } else if (selfUnit.GetOccupiedTile() == objectiveTile && reserveTile == null) { // Si sur le bâtiment lui-même (pas de case de réserve)
                    decidedAction = AIActionType.DefendPosition; // Nouvelle action
                    LogMessage($"En position défensive (sur bâtiment {objectiveTile.column},{objectiveTile.row}) sur l'objectif initial. Action: DefendPosition.", false);
                }
                else {
                    decidedAction = AIActionType.MoveToBuilding; // Se déplacer vers la (réserve ou bâtiment)
                    LogMessage($"Déplacement vers Objectif Initial défensif: {initialObjectiveBuilding.name} (Destination: {decidedDestination.x},{decidedDestination.y})", false);
                }
                // NE PAS METTRE bbIsObjectiveCompleted à true ici. L'objectif de défense est "en cours".
            }
            else // Objectif initial est Ennemi ou Neutre (offensif)
            {
                decidedDefensiveModeFlag = false;
                if (selfUnit.IsBuildingInRange(initialObjectiveBuilding)) {
                    decidedAction = DetermineOffensiveAction(initialObjectiveBuilding);
                    if (decidedAction != AIActionType.None) LogMessage($"Objectif Initial '{initialObjectiveBuilding.name}' à portée. Action: {decidedAction}", false);
                    else { // Ex: Bâtiment neutre non capturable ou allié (ne devrait pas arriver ici si la logique est bonne)
                        LogMessage($"Objectif Initial '{initialObjectiveBuilding.name}' à portée mais action indéterminée. Devient 'None'.", true);
                         if (bbIsObjectiveCompleted != null) bbIsObjectiveCompleted.Value = true; // On considère l'objectif "géré"
                    }
                } else {
                    decidedAction = AIActionType.MoveToBuilding;
                    LogMessage($"Déplacement vers Objectif Initial offensif: {initialObjectiveBuilding.name}", false);
                }
            }
        }
    }

    // --- PRIORITÉ 2: Cible de la Bannière (si pas d'objectif initial actif/valide, OU si l'objectif initial vient d'être complété) ---
    // Cette section s'exécute si la condition précédente (objectif initial actif) n'était pas remplie.
    if (decidedAction == AIActionType.None && decidedInteractionBuilding == null) // Si aucune action/cible n'a été définie par l'objectif initial
    {
        if (hasBanner && buildingAtBanner != null && buildingAtBanner.CurrentHealth > 0)
        {
            LogMessage($"Poursuite de la Cible de Bannière: {buildingAtBanner.name} (Team: {buildingAtBanner.Team})", false);
            decidedInteractionBuilding = buildingAtBanner;
            Tile bannerBuildingTile = buildingAtBanner.GetOccupiedTile();

             if (bannerBuildingTile == null) {
                LogMessage($"Cible de Bannière '{buildingAtBanner.name}' n'a plus de tuile valide.", true);
                decidedInteractionBuilding = null; // Cible invalide
                // Laisser la logique tomber vers le comportement par défaut / recherche d'ennemis
            } else {
                decidedDestination = new Vector2Int(bannerBuildingTile.column, bannerBuildingTile.row);

                if (buildingAtBanner.Team == TeamType.Player) // Bannière sur bâtiment allié -> Défense
                {
                    decidedDefensiveModeFlag = true;
                    PlayerBuilding pb = buildingAtBanner as PlayerBuilding;
                    Tile reserveTile = pb?.GetAvailableReserveTileForUnit(selfUnit);

                    if (reserveTile != null) {
                        LogMessage($"Cible Bannière Défensive: {buildingAtBanner.name}. Case de réserve dispo: ({reserveTile.column},{reserveTile.row})", false);
                        decidedDestination = new Vector2Int(reserveTile.column, reserveTile.row);
                        selfUnit.SetReservePosition(pb, reserveTile);
                    } else {
                         LogMessage($"Cible Bannière Défensive: {buildingAtBanner.name}. Pas de case de réserve dispo ou pas un PlayerBuilding. Cible bâtiment lui-même.", false);
                    }

                    if (selfUnit.GetOccupiedTile()?.column == decidedDestination.x && selfUnit.GetOccupiedTile()?.row == decidedDestination.y) {
                        decidedAction = AIActionType.None; // En position
                        LogMessage("En position défensive sur la cible de bannière.", false);
                    } else {
                        decidedAction = AIActionType.MoveToBuilding;
                    }
                }
                else // Bannière sur bâtiment Ennemi ou Neutre -> Offensif
                {
                    decidedDefensiveModeFlag = false;
                    if (selfUnit.IsBuildingInRange(buildingAtBanner)) {
                        decidedAction = DetermineOffensiveAction(buildingAtBanner);
                         if (decidedAction != AIActionType.None) LogMessage($"Cible de Bannière '{buildingAtBanner.name}' à portée. Action: {decidedAction}", false);
                         else {
                             LogMessage($"Cible de Bannière '{buildingAtBanner.name}' à portée mais action indéterminée. Devient 'None'.", true);
                             // Pas de bbIsObjectiveCompleted ici, car la bannière est une commande directe, pas un "objectif" au sens BG.
                         }
                    } else {
                        decidedAction = AIActionType.MoveToBuilding;
                        LogMessage($"Déplacement vers Cible de Bannière offensive: {buildingAtBanner.name}", false);
                    }
                }
            }
        }
    }

    // --- PRIORITÉ 3: Engagement d'Opportunité (si aucune cible de bâtiment prioritaire OU si en mode défense et menace proche) ---
      Unit detectedEnemy = bbDetectedEnemyUnit?.Value;

    bool canConsiderOpportunisticTarget = (decidedAction == AIActionType.None && decidedInteractionBuilding == null) || // Si aucune cible bâtiment prioritaire
                                          (decidedDefensiveModeFlag && decidedAction == AIActionType.DefendPosition);     // Ou si en mode défense et en position

    if (canConsiderOpportunisticTarget && detectedEnemy != null && detectedEnemy.Health > 0 && selfUnit.IsValidUnitTarget(detectedEnemy))
    {
        bool engageOpportunistic = true;
        if (decidedDefensiveModeFlag && decidedAction == AIActionType.DefendPosition) // Si on est en train de garder un poste défensif
        {
            // Ne rompre la position défensive que si l'ennemi est très proche ou à portée d'attaque
            // Condition à affiner : peut-être une "zone d'agression" autour du bâtiment défendu.
            int distToEnemy = (selfUnit.GetOccupiedTile() != null && detectedEnemy.GetOccupiedTile() != null) ?
                               HexGridManager.Instance.HexDistance(selfUnit.GetOccupiedTile().column, selfUnit.GetOccupiedTile().row, detectedEnemy.GetOccupiedTile().column, detectedEnemy.GetOccupiedTile().row)
                               : int.MaxValue;

            // Exemple: N'engage que si l'ennemi est à portée d'attaque OU à X tuiles du bâtiment défendu.
            // Pour l'instant, gardons la logique simple : si en défense et ennemi détecté, on évalue.
            // Plus tard, on ajoutera une condition "si l'ennemi menace le bâtiment défendu".
            // Pour l'instant, une unité en défense n'attaquera que si un ennemi est à portée directe de l'unité elle-même.
            if (!selfUnit.IsUnitInRange(detectedEnemy))
            {
                engageOpportunistic = false;
                LogMessage($"Ennemi {detectedEnemy.name} détecté, mais hors de portée d'attaque et l'unité est en mode 'DefendPosition'. Maintien de la position.", false);
                // L'action reste DefendPosition et la destination la case de réserve.
                decidedAction = AIActionType.DefendPosition;
                decidedDestination = selfUnit.currentReserveTile != null ?
                                     new Vector2Int(selfUnit.currentReserveTile.column, selfUnit.currentReserveTile.row) :
                                     decidedDestination; // Garde la destination actuelle si pas de case de réserve (ne devrait pas arriver si DefendPosition est actif)
                decidedInteractionUnit = null; // Pas d'engagement actif si hors de portée
                decidedInteractionBuilding = selfUnit.currentReserveBuilding; // La cible de "défense" reste le bâtiment.
            }
        }

        if (engageOpportunistic)
        {
            LogMessage($"Engagement d'opportunité : {detectedEnemy.name}", false);
            decidedInteractionUnit = detectedEnemy;
            Tile enemyTile = detectedEnemy.GetOccupiedTile();
            if (enemyTile != null) {
                decidedDestination = new Vector2Int(enemyTile.column, enemyTile.row); // Cible de mouvement est l'ennemi
                decidedAction = selfUnit.IsUnitInRange(detectedEnemy) ? AIActionType.AttackUnit : AIActionType.MoveToUnit;
                decidedInteractionBuilding = null;
                if (decidedDefensiveModeFlag) { // Si on quitte un poste défensif pour engager
                    selfUnit.ClearCurrentReservePosition();
                }
                decidedDefensiveModeFlag = false; // L'engagement d'unité est offensif
            } else {
            LogMessage($"Ennemi d'opportunité {detectedEnemy.name} n'a pas de tuile valide.", true);
            // L'action restera celle décidée précédemment (probablement None si on en est là)
            }
        }
    }

    // --- PRIORITÉ 4: Objectif Initial Complété ET Pas d'autre Ordre (Bannière ou Opportunité) -> Cheer ---
    // Note : isInitialObjectiveCompleted peut avoir été mis à true par ce noeud même, plus haut.
    if (isInitialObjectiveCompleted && decidedAction == AIActionType.None && decidedInteractionBuilding == null && decidedInteractionUnit == null && !hasBanner)
    {
        LogMessage("Objectif initial complété et aucune autre tâche (bannière/ennemi). Passage à CheerAndDespawn.", false);
        decidedAction = AIActionType.CheerAndDespawn;
        if (selfUnit.GetOccupiedTile() != null) {
             decidedDestination = new Vector2Int(selfUnit.GetOccupiedTile().column, selfUnit.GetOccupiedTile().row); // Rester sur place
        } else { decidedDestination = new Vector2Int(-1,-1); } // Sécurité
        decidedDefensiveModeFlag = false; // Pas en défense si on cheer
        if(selfUnit.currentReserveBuilding != null) {
            selfUnit.ClearCurrentReservePosition(); // Libérer la case de réserve si on cheer
        }
    }
    
    // --- Cas Final: Si aucune action du tout n'a été décidée (devrait être rare) ---
    if (decidedAction == AIActionType.None && decidedInteractionBuilding == null && decidedInteractionUnit == null && !decidedDefensiveModeFlag) {
        LogMessage("Aucune action décidée après toutes les priorités. L'unité restera inactive (AIActionType.None).", false);
        if (selfUnit.GetOccupiedTile() != null) {
             decidedDestination = new Vector2Int(selfUnit.GetOccupiedTile().column, selfUnit.GetOccupiedTile().row);
        } else { decidedDestination = new Vector2Int(-1,-1); }
    }


    // --- Écriture des sorties sur le Blackboard ---
    if (bbFinalDestinationPosition != null) bbFinalDestinationPosition.Value = decidedDestination;
    else LogMessage("CRITICAL: bbFinalDestinationPosition is null, cannot write to Blackboard!", true);

    if (bbSelectedActionType != null) bbSelectedActionType.Value = decidedAction;
    else LogMessage("CRITICAL: bbSelectedActionType is null, cannot write to Blackboard!", true);

    if (bbInteractionTargetUnit != null) bbInteractionTargetUnit.Value = decidedInteractionUnit;
    else LogMessage("bbInteractionTargetUnit is null (this might be intended if no unit target).", false);

    if (bbInteractionTargetBuilding != null) bbInteractionTargetBuilding.Value = decidedInteractionBuilding;
    else LogMessage("bbInteractionTargetBuilding is null (this might be intended if no building target).", false);
    if (bbIsInDefensiveMode != null) bbIsInDefensiveMode.Value = decidedDefensiveModeFlag; // C'est le flag général de mode
    else LogMessage("bbIsInDefensiveMode is null!", true);
    

    LogMessage($"Décision finale: Action={decidedAction}, Dest=({decidedDestination.x},{decidedDestination.y}), TargetBuilding='{decidedInteractionBuilding?.name ?? "None"}', TargetUnit='{decidedInteractionUnit?.name ?? "None"}', Defensive={decidedDefensiveModeFlag}", false);
    return Status.Success;
}

// Helper pour déterminer l'action offensive sur un bâtiment
private AIActionType DetermineOffensiveAction(Building targetBuilding)
{
    if (targetBuilding is NeutralBuilding nb && nb.IsRecapturable && nb.Team != TeamType.Player) // Peut être Neutre ou Ennemi et recapturable
    {
        // Les alliés capturent les bâtiments Neutres ou Ennemis (s'ils sont de type NeutralBuilding et recapturable)
        if (nb.Team == TeamType.Neutral || (nb.Team == TeamType.Enemy && nb.IsRecapturable)) {
             LogMessage($"Offensive Action for {targetBuilding.name} (Team: {targetBuilding.Team}): CaptureBuilding", false);
            return AIActionType.CaptureBuilding;
        }
    }
    // Si ce n'est pas un NeutralBuilding recapturable, ou si c'est un PlayerBuilding (ne devrait pas être une cible offensive)
    // alors on attaque (si c'est un ennemi).
    if (targetBuilding.Team == TeamType.Enemy) {
        LogMessage($"Offensive Action for {targetBuilding.name} (Team: {targetBuilding.Team}): AttackBuilding", false);
        return AIActionType.AttackBuilding;
    }

    LogMessage($"Offensive Action for {targetBuilding.name} (Team: {targetBuilding.Team}): Action indéterminée, fallback vers None.", true);
    return AIActionType.None; // Fallback
}



    private Status HandleBannerTarget()
    {
        Vector2Int bannerPos = bbBannerTargetPosition.Value;
        Building buildingAtBanner = selfUnit.FindBuildingAtPosition(bannerPos);

        if (buildingAtBanner == null)
        {
            LogMessage($"No building found at banner position ({bannerPos.x},{bannerPos.y})");
            return HandleDefaultBehavior();
        }

        // Vérifier si c'est un bâtiment allié
        if (buildingAtBanner.Team == TeamType.Player)
        {
            return HandleAlliedBuildingTarget(buildingAtBanner);
        }
        // Sinon, c'est un bâtiment ennemi/neutre - comportement d'attaque normal
        else
        {
            return HandleEnemyBuildingTarget(buildingAtBanner, bannerPos);
        }
    }

    private Status HandleAlliedBuildingTarget(Building alliedBuilding)
    {
        LogMessage($"Banner on allied building: {alliedBuilding.name} - Checking defensive mode");

        // Essayer de caster vers PlayerBuilding pour accéder aux réserves
        PlayerBuilding allyBuildingWithReserves = alliedBuilding as PlayerBuilding;
        
        if (allyBuildingWithReserves == null)
        {
            LogMessage("Allied building doesn't have reserve system, moving to building position");
            return MoveToBuilding(alliedBuilding);
        }

        // Vérifier s'il y a des cases de réserves disponibles
        if (allyBuildingWithReserves.HasAvailableReserveTiles())
        {
            Tile availableReserveTile = allyBuildingWithReserves.GetAvailableReserveTileForUnit(selfUnit);
            if (availableReserveTile != null)
            {
                LogMessage($"Moving to defensive position on reserve tile ({availableReserveTile.column},{availableReserveTile.row})");
                
                // Assigner l'unité à cette case de réserve
                allyBuildingWithReserves.AssignUnitToReserveTile(selfUnit, availableReserveTile);
                
                // Configurer la destination et le mode défensif
                if (bbFinalDestinationPosition != null)
                    bbFinalDestinationPosition.Value = new Vector2Int(availableReserveTile.column, availableReserveTile.row);
                if (bbSelectedActionType != null)
                    bbSelectedActionType.Value = AIActionType.MoveToBuilding;
                if (bbIsInDefensiveMode != null)
                    bbIsInDefensiveMode.Value = true;
                
                return Status.Success;
            }
        }

        LogMessage("No available reserve tiles, moving to building position");
        return MoveToBuilding(alliedBuilding);
    }

    private Status HandleEnemyBuildingTarget(Building enemyBuilding, Vector2Int bannerPos)
    {
        LogMessage($"Banner on enemy/neutral building: {enemyBuilding.name} - Attack mode");
        
        if (bbFinalDestinationPosition != null)
            bbFinalDestinationPosition.Value = bannerPos;
        if (bbInteractionTargetBuilding != null)
            bbInteractionTargetBuilding.Value = enemyBuilding;
        if (bbIsInDefensiveMode != null)
            bbIsInDefensiveMode.Value = false;

        // Déterminer l'action en fonction de la distance et du type de bâtiment
        if (selfUnit.IsBuildingInRange(enemyBuilding))
        {
            if (enemyBuilding is NeutralBuilding && enemyBuilding.Team != TeamType.Player)
            {
                if (bbSelectedActionType != null)
                    bbSelectedActionType.Value = AIActionType.CaptureBuilding;
                LogMessage("In range for capture");
            }
            else
            {
                if (bbSelectedActionType != null)
                    bbSelectedActionType.Value = AIActionType.AttackBuilding;
                LogMessage("In range for attack");
            }
        }
        else
        {
            if (bbSelectedActionType != null)
                bbSelectedActionType.Value = AIActionType.MoveToBuilding;
            LogMessage("Moving towards enemy building");
        }

        return Status.Success;
    }

    private Status MoveToBuilding(Building building)
    {
        Tile buildingTile = building.GetOccupiedTile();
        if (buildingTile != null)
        {
            if (bbFinalDestinationPosition != null)
                bbFinalDestinationPosition.Value = new Vector2Int(buildingTile.column, buildingTile.row);
            if (bbSelectedActionType != null)
                bbSelectedActionType.Value = AIActionType.MoveToBuilding;
            if (bbIsInDefensiveMode != null)
                bbIsInDefensiveMode.Value = true; // Mode défensif même sans réserves
            return Status.Success;
        }
        return Status.Failure;
    }

    private Status HandleObjectiveCompleted()
    {
        // Chercher de nouveaux ennemis à proximité
        Unit nearestEnemy = selfUnit.FindNearestEnemyUnit();
        Building nearestEnemyBuilding = selfUnit.FindNearestEnemyBuilding();

        if (nearestEnemy != null)
        {
            LogMessage($"New objective: Attack enemy unit {nearestEnemy.name}");
            Tile enemyTile = nearestEnemy.GetOccupiedTile();
            if (enemyTile != null)
            {
                if (bbFinalDestinationPosition != null)
                    bbFinalDestinationPosition.Value = new Vector2Int(enemyTile.column, enemyTile.row);
                if (bbInteractionTargetUnit != null)
                    bbInteractionTargetUnit.Value = nearestEnemy;
                if (bbSelectedActionType != null)
                    bbSelectedActionType.Value = selfUnit.IsUnitInRange(nearestEnemy) ? AIActionType.AttackUnit : AIActionType.MoveToUnit;
                if (bbIsInDefensiveMode != null)
                    bbIsInDefensiveMode.Value = false;
                return Status.Success;
            }
        }

        if (nearestEnemyBuilding != null)
        {
            LogMessage($"New objective: Target enemy building {nearestEnemyBuilding.name}");
            Tile buildingTile = nearestEnemyBuilding.GetOccupiedTile();
            if (buildingTile != null)
            {
                if (bbFinalDestinationPosition != null)
                    bbFinalDestinationPosition.Value = new Vector2Int(buildingTile.column, buildingTile.row);
                if (bbInteractionTargetBuilding != null)
                    bbInteractionTargetBuilding.Value = nearestEnemyBuilding;
                
                if (selfUnit.IsBuildingInRange(nearestEnemyBuilding))
                {
                    if (bbSelectedActionType != null)
                        bbSelectedActionType.Value = AIActionType.AttackBuilding;
                }
                else
                {
                    if (bbSelectedActionType != null)
                        bbSelectedActionType.Value = AIActionType.MoveToBuilding;
                }
                if (bbIsInDefensiveMode != null)
                    bbIsInDefensiveMode.Value = false;
                return Status.Success;
            }
        }

        LogMessage("No new objectives found - staying idle");
        if (bbSelectedActionType != null)
            bbSelectedActionType.Value = AIActionType.None;
        return Status.Success;
    }

    private Status HandleDefaultBehavior()
    {
        LogMessage("No banner target, using default behavior");
        
        // Chercher des ennemis à proximité
        Unit nearestEnemy = selfUnit.FindNearestEnemyUnit();
        if (nearestEnemy != null)
        {
            Tile enemyTile = nearestEnemy.GetOccupiedTile();
            if (enemyTile != null)
            {
                if (bbFinalDestinationPosition != null)
                    bbFinalDestinationPosition.Value = new Vector2Int(enemyTile.column, enemyTile.row);
                if (bbInteractionTargetUnit != null)
                    bbInteractionTargetUnit.Value = nearestEnemy;
                if (bbSelectedActionType != null)
                    bbSelectedActionType.Value = selfUnit.IsUnitInRange(nearestEnemy) ? AIActionType.AttackUnit : AIActionType.MoveToUnit;
                if (bbIsInDefensiveMode != null)
                    bbIsInDefensiveMode.Value = false;
                return Status.Success;
            }
        }

        Building nearestEnemyBuilding = selfUnit.FindNearestEnemyBuilding();
        if (nearestEnemyBuilding != null)
        {
            Tile buildingTile = nearestEnemyBuilding.GetOccupiedTile();
            if (buildingTile != null)
            {
                if (bbFinalDestinationPosition != null)
                    bbFinalDestinationPosition.Value = new Vector2Int(buildingTile.column, buildingTile.row);
                if (bbInteractionTargetBuilding != null)
                    bbInteractionTargetBuilding.Value = nearestEnemyBuilding;
                if (bbSelectedActionType != null)
                    bbSelectedActionType.Value = selfUnit.IsBuildingInRange(nearestEnemyBuilding) ? AIActionType.AttackBuilding : AIActionType.MoveToBuilding;
                if (bbIsInDefensiveMode != null)
                    bbIsInDefensiveMode.Value = false;
                return Status.Success;
            }
        }

        // Aucune cible trouvée
        if (bbSelectedActionType != null)
            bbSelectedActionType.Value = AIActionType.None;
        return Status.Success;
    }

    private void ClearOutputs()
    {
        
        Tile currentTile = selfUnit?.GetOccupiedTile();
        Vector2Int currentPos = (currentTile != null) ? new Vector2Int(currentTile.column, currentTile.row) : new Vector2Int(-1,-1); // Position invalide si pas sur tuile

        if (bbSelectedActionType != null) bbSelectedActionType.Value = AIActionType.None;
        if (bbFinalDestinationPosition != null) bbFinalDestinationPosition.Value = currentPos; // Important
        if (bbInteractionTargetUnit != null) bbInteractionTargetUnit.Value = null;
        if (bbInteractionTargetBuilding != null) bbInteractionTargetBuilding.Value = null;
        if (bbIsInDefensiveMode != null) bbIsInDefensiveMode.Value = false; // Par défaut, pas en mode défensif
    }

    private void LogMessage(string message, bool isError = false)
    {
        if (!debugLogging && !isError) return;
        
        string logMessage = $"[{selfUnit?.name ?? "Unknown"}] SelectTargetNode_Ally: {message}";
        if (isError) Debug.LogError(logMessage);
        else Debug.Log(logMessage);
    }
    protected override void OnEnd()
    {
        blackboardVariablesCached = false;
        selfUnit = null; // Nettoyer la référence cachée de l'unité elle-même
        // Les références bbXXX seront réinitialisées au prochain CacheBlackboardVariables
        // ou lors d'une nouvelle initialisation du noeud.
        bbHasBannerTarget = null;
        bbBannerTargetPosition = null;
        bbHasInitialObjectiveSet = null;
        bbInitialTargetBuilding = null;
        bbIsObjectiveCompleted = null;
        bbIsAttacking = null;
        bbIsCapturing = null;
        bbIsMoving = null;
        bbDetectedEnemyUnit = null;
        bbIsDefending = null; // Nettoyer aussi ce cache

        bbFinalDestinationPosition = null;
        bbSelectedActionType = null;
        bbInteractionTargetUnit = null;
        bbInteractionTargetBuilding = null;
        bbIsInDefensiveMode = null;

        LogMessage("OnEnd: Node execution finished, cache cleared.", false);
    }

}