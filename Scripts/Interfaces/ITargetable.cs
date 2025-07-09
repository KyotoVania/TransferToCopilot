using UnityEngine;

/// <summary>
/// Interface pour les objets qui peuvent être ciblés par le système de caméra et d'UI.
/// Permet un traitement uniforme des bâtiments et des unités boss.
/// </summary>
public interface ITargetable
{
    /// <summary>
    /// Point de transformation pour le ciblage de la caméra et l'UI
    /// </summary>
    Transform TargetPoint { get; }

    /// <summary>
    /// Indique si cet objet peut actuellement être ciblé
    /// </summary>
    bool IsTargetable { get; }

    /// <summary>
    /// Le GameObject associé à cette cible (pour la compatibilité avec l'InputTargetingManager existant)
    /// </summary>
    GameObject GameObject { get; }
}
