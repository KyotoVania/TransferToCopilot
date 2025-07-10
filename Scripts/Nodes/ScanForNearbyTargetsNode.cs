// Fichier: Scripts/Nodes/ScanForNearbyTargetsNode.cs
using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System.Collections.Generic;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Scan For Nearby Targets (Ally)", // Nom spécifique pour clarté si tu as plusieurs versions
    story: "Scan For Nearby Targets (Ally)",
    category: "Ally Actions", // Ou ta catégorie existante
    id: "AllyAction_ScanForNearbyTargets_v2" // Nouvel ID si la logique change significativement
)]
public class ScanForNearbyTargetsNode : Unity.Behavior.Action // Nom de classe générique gardé pour l'instant
{
    // Constants for Blackboard variable names (doivent correspondre à celles utilisées dans AllyUnit.cs et son BB)
    private const string SELF_UNIT_VAR = "SelfUnit"; // Sur le BB, cette variable est de type Unit
    private const string DETECTED_ENEMY_UNIT_VAR = "DetectedEnemyUnit";
    private const string DETECTED_BUILDING_VAR = "DetectedBuilding"; // Renommé pour clarté: Bâtiments ciblables par l'Allié

    // Optionnel: flags booléens
    // private const string HAS_ENEMY_TARGET_VAR = "HasEnemyTarget";
    // private const string HAS_BUILDING_TARGET_VAR = "HasBuildingTarget";

    // Cache blackboard variable references
    private BlackboardVariable<Unit> bbSelfUnit; // Variable du BB est de type Unit
    private BlackboardVariable<Unit> bbDetectedEnemyUnit; // Pour stocker l'EnemyUnit trouvé
    private BlackboardVariable<Building> bbDetectedBuilding;
    // private BlackboardVariable<bool> bbHasEnemyTarget;
    // private BlackboardVariable<bool> bbHasBuildingTarget;

    private bool blackboardVariablesCached = false;
    private AllyUnit selfAllyUnitCache; // Cache local de type AllyUnit pour l'exécution
    private BehaviorGraphAgent agent;
    private string nodeInstanceId;

    protected override Status OnStart()
    {
        nodeInstanceId = Guid.NewGuid().ToString("N").Substring(0, 6);
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();

        if (!CacheBlackboardVariables())
        {
            LogNodeMessage("CRITICAL: Failed to cache Blackboard variables. Node Failure.", true, true);
            return Status.Failure;
        }

        // Essayer de caster SelfUnit en AllyUnit ici pour selfAllyUnitCache
        if (bbSelfUnit?.Value != null)
        {
            selfAllyUnitCache = bbSelfUnit.Value as AllyUnit;
            if (selfAllyUnitCache == null)
            {
                LogNodeMessage($"'{SELF_UNIT_VAR}' from Blackboard (type: {bbSelfUnit.Value.GetType().Name}) is not an AllyUnit. Node Failure.", true, true);
                return Status.Failure;
            }
        }
        else
        {
            LogNodeMessage($"'{SELF_UNIT_VAR}' value is NULL. Node Failure.", true, true);
            return Status.Failure;
        }

        // Le scan lui-même est instantané et se produit dans OnUpdate pour cet exemple,
        // donc OnStart retourne Running pour permettre à OnUpdate de s'exécuter.
        // Si le scan était une opération longue, OnStart pourrait retourner Running.
        // Ici, on pourrait aussi faire le scan directement dans OnStart et retourner Success/Failure.
        // Pour rester cohérent avec la structure précédente, faisons le scan dans OnUpdate.
        return Status.Running;
    }

    private void LogNodeMessage(string message, bool isError = false, bool forceLog = false)
    {
        
        // Utilise selfAllyUnitCache pour le nom et le flag de log
        string unitName = selfAllyUnitCache != null ? selfAllyUnitCache.name : (GameObject != null ? GameObject.name : "ScanNode");
        bool enableLogging = selfAllyUnitCache != null ? selfAllyUnitCache.enableVerboseLogging : false;

        string logPrefix = $"[{nodeInstanceId} | {unitName} | ScanForTargetsNode(Ally)]";

        if (isError) Debug.LogError($"{logPrefix} {message}", GameObject);
        else if (forceLog || enableLogging) Debug.Log($"{logPrefix} {message}", GameObject);
        
    }


    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        if (agent == null || agent.BlackboardReference == null)
        {
            if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();
            if (agent == null || agent.BlackboardReference == null) {
                Debug.LogError($"[ScanForNearbyTargetsNode(Ally)] CacheBB: Agent or BlackboardRef missing on {GameObject?.name}.", GameObject);
                return false;
            }
        }
        var blackboard = agent.BlackboardReference;
        bool allEssentialFound = true;

        // SELF_UNIT_VAR est de type Unit sur le Blackboard
        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit))
        { LogNodeMessage($"BBVar '{SELF_UNIT_VAR}' (type Unit) missing.", true); allEssentialFound = false; }

        // Pour un ScanNode utilisé par un AllyUnit, DETECTED_ENEMY_UNIT_VAR stockera un EnemyUnit (qui est un Unit)
        if (!blackboard.GetVariable(DETECTED_ENEMY_UNIT_VAR, out bbDetectedEnemyUnit))
        { LogNodeMessage($"BBVar Output '{DETECTED_ENEMY_UNIT_VAR}' (type Unit) missing.", true); allEssentialFound = false; }

        if (!blackboard.GetVariable(DETECTED_BUILDING_VAR, out bbDetectedBuilding))
        { LogNodeMessage($"BBVar Output '{DETECTED_BUILDING_VAR}' (type Building) missing.", true); allEssentialFound = false; }

        // blackboard.GetVariable(HAS_ENEMY_TARGET_VAR, out bbHasEnemyTarget); // Optionnel
        // blackboard.GetVariable(HAS_BUILDING_TARGET_VAR, out bbHasBuildingTarget); // Optionnel

        blackboardVariablesCached = allEssentialFound;
        if (!allEssentialFound)
        {
            LogNodeMessage("CacheBB: CRITICAL - Failed to cache one or more ESSENTIAL Blackboard variables.", true, true);
        }
        return blackboardVariablesCached;
    }

    protected override Status OnUpdate()
    {
        if (!blackboardVariablesCached) // Protection si OnStart n'a pas pu cacher
        {
             LogNodeMessage("BB vars not cached in OnUpdate. Attempting recache.", true, true);
             if(!CacheBlackboardVariables()) {
                 ClearDetectedTargetsOnBB(); return Status.Failure;
             }
        }

        // S'assurer que selfAllyUnitCache est valide (il aurait dû être setté dans OnStart ou lors d'un recache réussi)
        if (bbSelfUnit?.Value != null && selfAllyUnitCache == null) // Peut arriver si OnStart a échoué puis OnUpdate a recaché bbSelfUnit
        {
            selfAllyUnitCache = bbSelfUnit.Value as AllyUnit;
        }

        if (selfAllyUnitCache == null)
        {
            LogNodeMessage($"SelfUnit (casted to AllyUnit) is NULL. Cannot perform scan.", true, true);
            ClearDetectedTargetsOnBB();
            return Status.Failure;
        }

        Tile currentTile = selfAllyUnitCache.GetOccupiedTile(); //
        if (currentTile == null)
        {
            LogNodeMessage($"Unit '{selfAllyUnitCache.name}' is not on a valid tile. Scan cannot be performed.", false, true);
            ClearDetectedTargetsOnBB();
            return Status.Success; // Scan "réussi" car rien à scanner depuis une position non valide
        }

        int detectionRange = selfAllyUnitCache.DetectionRange; //
        if (HexGridManager.Instance == null)
        {
            LogNodeMessage("HexGridManager instance not found.", true, true);
            ClearDetectedTargetsOnBB();
            return Status.Failure;
        }
        List<Tile> tilesInRange = HexGridManager.Instance.GetTilesWithinRange(currentTile.column, currentTile.row, detectionRange); //

        Unit closestEnemyOfTypeUnit = null; // Stocke un EnemyUnit, mais la variable BB est Unit
        Building closestTargetableBuildingByAlly = null;
        float minUnitDistSq = float.MaxValue;
        float minBuildingDistSq = float.MaxValue;
        Vector3 selfPosition = selfAllyUnitCache.transform.position;

        if (tilesInRange != null)
        {
            // Vérifier si l'unité a un objectif de capture de bâtiment actif
            bool hasCaptureBuildingObjective = HasActiveBuildingCaptureObjective();
            
            foreach (Tile tile in tilesInRange)
            {
                if (tile == null) continue;

                // Scan pour Unités Ennemies (EnemyUnit)
                if (tile.currentUnit != null && tile.currentUnit is EnemyUnit && selfAllyUnitCache.IsValidUnitTarget(tile.currentUnit)) //
                {
                    // Ignorer les boss si l'objectif principal est la capture d'un bâtiment
                    if (hasCaptureBuildingObjective && tile.currentUnit is BossUnit)
                    {
                        LogNodeMessage($"Boss {tile.currentUnit.name} ignoré car objectif de capture de bâtiment actif.", false, true);
                        continue;
                    }
                    
                    float distSq = (tile.currentUnit.transform.position - selfPosition).sqrMagnitude;
                    if (distSq < minUnitDistSq)
                    {
                        minUnitDistSq = distSq;
                        closestEnemyOfTypeUnit = tile.currentUnit; // C'est un EnemyUnit
                    }
                }

                // Scan pour Bâtiments ciblables par un Allié (Enemy ou Neutral si IsValidBuildingTarget le permet)
                if (tile.currentBuilding != null && selfAllyUnitCache.IsValidBuildingTarget(tile.currentBuilding)) //
                {
                    // AllyUnit.IsValidBuildingTarget devrait vérifier si c'est TeamType.Enemy ou TeamType.Neutral (pour capture)
                    float distSq = (tile.currentBuilding.transform.position - selfPosition).sqrMagnitude;
                    if (distSq < minBuildingDistSq)
                    {
                        minBuildingDistSq = distSq;
                        closestTargetableBuildingByAlly = tile.currentBuilding;
                    }
                }
            }
        }

        // Mise à jour du Blackboard
        if (bbDetectedEnemyUnit != null) bbDetectedEnemyUnit.Value = closestEnemyOfTypeUnit; // Assigne EnemyUnit (qui est un Unit) à la var BB de type Unit
        else LogNodeMessage($"bbDetectedEnemyUnit reference is null. Cannot write to BB.", true);

        if (bbDetectedBuilding != null) bbDetectedBuilding.Value = closestTargetableBuildingByAlly;
        else LogNodeMessage($"bbDetectedBuilding reference is null. Cannot write to BB.", true);

        LogNodeMessage($"Scan results - EnemyUnit: {(closestEnemyOfTypeUnit ? closestEnemyOfTypeUnit.name : "None")}, TargetableBuilding: {(closestTargetableBuildingByAlly ? closestTargetableBuildingByAlly.name : "None")}", false, true);

        return Status.Success; // Le scan est considéré comme une action instantanée qui réussit toujours.
    }

    private void ClearDetectedTargetsOnBB() // Méthode pour nettoyer le BB en cas d'erreur
    {
        if (bbDetectedEnemyUnit != null) bbDetectedEnemyUnit.Value = null;
        if (bbDetectedBuilding != null) bbDetectedBuilding.Value = null;
    }

    /// <summary>
    /// Vérifie si l'unité a un objectif actif de capture de bâtiment
    /// </summary>
    private bool HasActiveBuildingCaptureObjective()
    {
        if (agent?.BlackboardReference == null) return false;
        
        // Vérifier si l'objectif initial est un bâtiment ennemi/neutre non complété
        if (agent.BlackboardReference.GetVariable("HasInitialObjectiveSet", out BlackboardVariable<bool> hasObjective) &&
            agent.BlackboardReference.GetVariable("IsObjectiveCompleted", out BlackboardVariable<bool> isCompleted) &&
            agent.BlackboardReference.GetVariable("InitialTargetBuilding", out BlackboardVariable<Building> targetBuilding))
        {
            if (hasObjective.Value && !isCompleted.Value && targetBuilding.Value != null)
            {
                // Si le bâtiment cible est ennemi ou neutre, c'est un objectif de capture
                return targetBuilding.Value.Team == TeamType.Enemy || targetBuilding.Value.Team == TeamType.Neutral;
            }
        }
        
        // Vérifier si la bannière pointe vers un bâtiment ennemi/neutre
        if (agent.BlackboardReference.GetVariable("HasBannerTarget", out BlackboardVariable<bool> hasBanner) &&
            agent.BlackboardReference.GetVariable("BannerTargetPosition", out BlackboardVariable<Vector2Int> bannerPos))
        {
            if (hasBanner.Value && selfAllyUnitCache != null)
            {
                Building bannerBuilding = selfAllyUnitCache.FindBuildingAtPosition(bannerPos.Value);
                if (bannerBuilding != null)
                {
                    return bannerBuilding.Team == TeamType.Enemy || bannerBuilding.Team == TeamType.Neutral;
                }
            }
        }
        
        return false;
    }

    protected override void OnEnd()
    {
        blackboardVariablesCached = false; // Permet de recacher si le nœud est réexécuté
        selfAllyUnitCache = null; // Nettoyer le cache
        // Les références bbSelfUnit etc. seront nullifiées par le prochain CacheBlackboardVariables si le cache est reset,
        // ou peuvent être explicitement mises à null ici si on veut être très propre.
        bbSelfUnit = null;
        bbDetectedEnemyUnit = null;
        bbDetectedBuilding = null;
    }
}