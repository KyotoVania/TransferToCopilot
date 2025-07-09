public enum TutorialTriggerType
{
    None,               // Le joueur doit cliquer pour avancer (non utilisé pour l'instant)
    BeatCount,          // Avancer après un certain nombre de battements
    PlayerInputs,       // Avancer après un certain nombre d'inputs (X, C, V)
    BannerPlacedOnBuilding, // Avancer quand la bannière est placée sur un bâtiment
    UnitSummoned,       // Avancer quand une unité est invoquée
    MomentumGained,   
    MomentumSpend,   
    FeverLevelReached,  // Nouveau trigger pour atteindre un niveau de fever
    ComboCountReached,  // Nouveau trigger pour atteindre un certain combo
    MomentumSpellCast,
    SequencePanelHUD, // Nouveau trigger pour afficher le HUD du panneau de séquence
    UnitObjectiveComplete  
}