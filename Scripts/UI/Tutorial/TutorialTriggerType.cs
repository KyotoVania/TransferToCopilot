// Fichier: Scripts/Tutorials/TutorialTriggerType.cs
public enum TutorialTriggerType
{
    None,               // Le joueur doit cliquer pour avancer (non utilisé pour l'instant)
    BeatCount,          // Avancer après un certain nombre de battements
    PlayerInputs,       // Avancer après un certain nombre d'inputs (X, C, V)
    BannerPlacedOnBuilding, // Avancer quand la bannière est placée sur un bâtiment
    UnitSummoned,       // Avancer quand une unité est invoquée
    // Ajoutez d'autres triggers au besoin !
}