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
    // Input variables du Blackboard - utilise les mêmes noms que l'ancien système
    [SerializeReference] BlackboardVariable<bool> bbHasBannerTarget;
    [SerializeReference] BlackboardVariable<Vector2Int> bbBannerTargetPosition;
    [SerializeReference] BlackboardVariable<bool> bbHasInitialObjectiveSet;
    [SerializeReference] BlackboardVariable<Building> bbInitialTargetBuilding;
    [SerializeReference] BlackboardVariable<bool> bbIsObjectiveCompleted;

    // Output variables du Blackboard - IMPORTANT: utilise les mêmes noms que l'ancien système
    [SerializeReference] BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    [SerializeReference] BlackboardVariable<AIActionType> bbSelectedActionType; // Changé de bbSelectedAction
    [SerializeReference] BlackboardVariable<Unit> bbInteractionTargetUnit;
    [SerializeReference] BlackboardVariable<Building> bbInteractionTargetBuilding;
    [SerializeReference] BlackboardVariable<bool> bbIsInDefensiveMode;

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
        if (selfUnit == null)
        {
            LogMessage("ERROR: selfUnit is null in OnUpdate", true);
            return Status.Failure;
        }

        if (!blackboardVariablesCached)
        {
            LogMessage("ERROR: Blackboard variables not cached in OnUpdate", true);
            return Status.Failure;
        }

        // Reset des outputs
        ClearOutputs();

        // 1. Si l'objectif initial est terminé, chercher une nouvelle cible
        if (bbIsObjectiveCompleted?.Value == true)
        {
            LogMessage("Objective completed, looking for new target");
            return HandleObjectiveCompleted();
        }

        // 2. Si on a une bannière active, traiter la logique de bannière
        if (bbHasBannerTarget?.Value == true && bbBannerTargetPosition != null)
        {
            return HandleBannerTarget();
        }

        // 3. Comportement par défaut : chercher des ennemis à proximité
        return HandleDefaultBehavior();
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
            Tile availableReserveTile = allyBuildingWithReserves.GetAvailableReserveTile();
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
        if (bbSelectedActionType != null) bbSelectedActionType.Value = AIActionType.None;
        if (bbInteractionTargetUnit != null) bbInteractionTargetUnit.Value = null;
        if (bbInteractionTargetBuilding != null) bbInteractionTargetBuilding.Value = null;
        if (bbIsInDefensiveMode != null) bbIsInDefensiveMode.Value = false;
    }

    private void LogMessage(string message, bool isError = false)
    {
        if (!debugLogging && !isError) return;
        
        string logMessage = $"[{selfUnit?.name ?? "Unknown"}] SelectTargetNode_Ally: {message}";
        if (isError) Debug.LogError(logMessage);
        else Debug.Log(logMessage);
    }
}