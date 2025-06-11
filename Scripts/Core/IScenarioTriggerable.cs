/// <summary>
/// Interface pour les objets qui peuvent être déclenchés par un événement du LevelScenarioManager.
/// </summary>
public interface IScenarioTriggerable
{
    /// <summary>
    /// Exécute l'action principale de l'objet.
    /// </summary>
    void TriggerAction();
}