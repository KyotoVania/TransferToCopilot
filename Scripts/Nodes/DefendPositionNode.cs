
using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using System;
using Unity.Properties;

[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Defend Position",
    story: "Unit holds its current position, typically a reserve tile, and scans for threats.",
    category: "Ally Actions",
    id: "AllyAction_DefendPosition_v1"
)]
public class DefendPositionNode : Unity.Behavior.Action
{
    // Blackboard variable Noms (entrées lues)
    private const string SELF_UNIT_VAR = "SelfUnit";
    private const string IS_IN_DEFENSIVE_MODE_VAR = "IsInDefensiveMode"; // Pour vérifier si on doit vraiment défendre
    private const string FINAL_DESTINATION_POS_VAR = "FinalDestinationPosition"; // La case de réserve attendue
    private const string DETECTED_ENEMY_UNIT_VAR = "DetectedEnemyUnit"; // Pour la logique d'engagement future

    // Blackboard variable Noms (sorties écrites)
    private const string IS_DEFENDING_VAR = "IsDefending"; // Le flag que ce noeud gère

    // Cache des variables Blackboard
    private BlackboardVariable<Unit> bbSelfUnit;
    private BlackboardVariable<bool> bbIsInDefensiveMode;
    private BlackboardVariable<Vector2Int> bbFinalDestinationPosition;
    private BlackboardVariable<Unit> bbDetectedEnemyUnit;
    private BlackboardVariable<bool> bbIsDefending;

    private bool blackboardVariablesCached = false;
    private AllyUnit selfUnitInstance;
    private BehaviorGraphAgent agent;
    private string nodeInstanceId; // Pour des logs plus clairs

    protected override Status OnStart()
    {
        nodeInstanceId = Guid.NewGuid().ToString("N").Substring(0, 6);
        if (GameObject != null) agent = GameObject.GetComponent<BehaviorGraphAgent>();

        LogNodeMessage("OnStart BEGIN", false, true);

        if (!CacheBlackboardVariables())
        {
            LogNodeMessage("CRITICAL: Failed to cache Blackboard variables. Node Failure.", true, true);
            if (bbIsDefending != null) bbIsDefending.Value = false; // Assurer le nettoyage
            return Status.Failure;
        }

        selfUnitInstance = bbSelfUnit?.Value as AllyUnit;
        if (selfUnitInstance == null)
        {
            LogNodeMessage($"'{SELF_UNIT_VAR}' est null ou n'est pas un AllyUnit. Node Failure.", true, true);
            if (bbIsDefending != null) bbIsDefending.Value = false;
            return Status.Failure;
        }

        bool isInDefensiveModeAccordingToBB = bbIsInDefensiveMode?.Value ?? false;
        if (!isInDefensiveModeAccordingToBB)
        {
            LogNodeMessage("BB 'IsInDefensiveMode' est false. Ce noeud ne devrait pas être actif. Retourne Success pour permettre réévaluation.", false, true);
            if (bbIsDefending != null) bbIsDefending.Value = false;
            return Status.Success; // Le mode a changé, l'arbre doit réévaluer
        }

        Tile currentTile = selfUnitInstance.GetOccupiedTile();
        Vector2Int expectedReservePos = bbFinalDestinationPosition?.Value ?? new Vector2Int(-1,-1);

        if (currentTile == null || expectedReservePos.x == -1 ||
            currentTile.column != expectedReservePos.x || currentTile.row != expectedReservePos.y)
        {
            LogNodeMessage($"Unité n'est pas sur sa tuile de réserve attendue ({expectedReservePos.x},{expectedReservePos.y}). Actuelle: ({(currentTile?.column ?? -99)},{(currentTile?.row ?? -99)}). Node Failure.", false, true);
             if (bbIsDefending != null) bbIsDefending.Value = false;
            return Status.Failure; // Ne devrait pas arriver ici si SelectTargetNode a bien fait son travail
        }

        // Si on arrive ici, on est en mode défense et sur la bonne case.
        LogNodeMessage($"En position de défense sur la tuile ({currentTile.column},{currentTile.row}). Maintien de la position.", false, true);
        if (bbIsDefending != null) bbIsDefending.Value = true; // Indiquer que cette action de défense est active
        else LogNodeMessage("bbIsDefending est null, impossible de mettre à jour le flag!", true);

        return Status.Running; // Maintenir la position et surveiller
    }

    protected override Status OnUpdate()
    {
        if (selfUnitInstance == null || !selfUnitInstance.gameObject.activeInHierarchy)
        {
            LogNodeMessage("SelfUnit est devenu null ou inactif. Node Failure.", true, true);
             if (bbIsDefending != null) bbIsDefending.Value = false;
            return Status.Failure;
        }

        // Vérifier si on doit toujours être en mode défense
        bool isInDefensiveModeAccordingToBB = bbIsInDefensiveMode?.Value ?? false;
        if (!isInDefensiveModeAccordingToBB)
        {
            LogNodeMessage("BB 'IsInDefensiveMode' est devenu false. Fin de la défense. Node Success.", false, true);
            // Le flag bbIsDefending sera mis à false dans OnEnd()
            return Status.Success;
        }

        // Logique future d'engagement si un ennemi menace le bâtiment défendu.
        // Pour l'instant, on ne fait que "tenir la position".
        // Si un ennemi est détecté A PORTEE par ScanForNearbyTargetsNode (Ally),
        // et que SelectTargetNode_Ally décide d'attaquer, il mettra IsInDefensiveMode à false
        // et choisira AttackUnit, ce qui interrompra ce noeud DefendPosition.

        Unit detectedEnemy = bbDetectedEnemyUnit?.Value;
        if (detectedEnemy != null && detectedEnemy.Health > 0 && selfUnitInstance.IsUnitInRange(detectedEnemy))
        {
            LogNodeMessage($"Ennemi '{detectedEnemy.name}' à portée pendant la défense. Le SelectTargetNode devrait gérer la transition vers l'attaque. Pour l'instant, DefendPosition retourne Success pour permettre cela.", false, true);
            // Si un ennemi est à portée, on veut potentiellement que l'arbre réévalue pour passer à AttackUnit.
            // En retournant Success, on force une réévaluation par SelectTargetNode_Ally.
            // SelectTargetNode_Ally devrait alors choisir AttackUnit.
            // bbIsDefending sera mis à false dans OnEnd().
            return Status.Success;
        }


        LogNodeMessage("Maintien de la position défensive.", false, false); // Optionnel: log verbeux pour chaque tick
        return Status.Running; // Rester en défense
    }

    protected override void OnEnd()
    {
        LogNodeMessage($"OnEnd appelé. Status: {CurrentStatus}. Nettoyage du flag IsDefending.", false, true);
        if (bbIsDefending != null)
        {
            bbIsDefending.Value = false; // Très important de nettoyer ce flag
        }

        // Réinitialiser le cache pour la prochaine exécution
        blackboardVariablesCached = false;
        bbSelfUnit = null;
        bbIsInDefensiveMode = null;
        bbFinalDestinationPosition = null;
        bbDetectedEnemyUnit = null;
        bbIsDefending = null;
        selfUnitInstance = null;
        agent = null;
    }


    private bool CacheBlackboardVariables()
    {
        if (blackboardVariablesCached) return true;

        if (agent == null || agent.BlackboardReference == null) {
             LogNodeMessage("Agent or BlackboardRef missing lors du cache.", true); return false;
        }
        var blackboard = agent.BlackboardReference;
        bool success = true;

        if (!blackboard.GetVariable(SELF_UNIT_VAR, out bbSelfUnit)) { LogNodeMessage($"BBVar IN '{SELF_UNIT_VAR}' missing.", true); success = false; }
        if (!blackboard.GetVariable(IS_IN_DEFENSIVE_MODE_VAR, out bbIsInDefensiveMode)) { LogNodeMessage($"BBVar IN '{IS_IN_DEFENSIVE_MODE_VAR}' missing.", true); success = false; }
        if (!blackboard.GetVariable(FINAL_DESTINATION_POS_VAR, out bbFinalDestinationPosition)) { LogNodeMessage($"BBVar IN '{FINAL_DESTINATION_POS_VAR}' missing.", true); success = false; }
        if (!blackboard.GetVariable(DETECTED_ENEMY_UNIT_VAR, out bbDetectedEnemyUnit)) { LogNodeMessage($"BBVar IN '{DETECTED_ENEMY_UNIT_VAR}' (optionnel) missing.", false); }


        if (!blackboard.GetVariable(IS_DEFENDING_VAR, out bbIsDefending)) { LogNodeMessage($"BBVar OUT '{IS_DEFENDING_VAR}' missing.", true); success = false; }


        blackboardVariablesCached = success;
        if (!success) Debug.LogError($"[{GameObject?.name} - DefendPositionNode] CRITICAL Blackboard variable(s) missing. Node WILL FAIL.", GameObject);
        return success;
    }

    private void LogNodeMessage(string message, bool isError = false, bool forceLog = false)
    {
        /*
        // Utilise selfUnitInstance pour le nom et le flag de log
        string unitName = selfUnitInstance != null ? selfUnitInstance.name : (GameObject != null ? GameObject.name : "DefendPosNode");
        bool enableLogging = (selfUnitInstance != null && selfUnitInstance.enableVerboseLogging) || forceLog;

        if (isError || enableLogging)
        {
            string logPrefix = $"[{nodeInstanceId} | {unitName} | DefendPositionNode]";
            if (isError) Debug.LogError($"{logPrefix} {message}", GameObject);
            else Debug.Log($"{logPrefix} {message}", GameObject);
        }
        */
    }
}