using UnityEngine;

/// <summary>
/// Singleton observer class that monitors banner placement events and triggers tutorial progression.
/// Implements the IBannerObserver interface to listen for banner placement actions during tutorial sequences.
/// Ensures that banner placement on valid enemy or neutral buildings advances the tutorial state.
/// </summary>
public class TutorialBannerObserver : MonoBehaviour, Game.Observers.IBannerObserver
{
    /// <summary>
    /// Singleton instance for global access to the tutorial banner observer.
    /// Ensures only one instance exists throughout the application lifecycle.
    /// </summary>
    public static TutorialBannerObserver Instance { get; private set; }
    
    /// <summary>
    /// Event triggered when the player successfully places a banner on a valid building during tutorial.
    /// Tutorial systems can subscribe to this event to advance tutorial progression.
    /// </summary>
    public event System.Action OnBannerPlacedOnBuilding;

    /// <summary>
    /// Flag to track whether the banner has been placed once during the tutorial.
    /// Prevents multiple triggers of the tutorial advancement event.
    /// </summary>
    private bool hasBeenPlacedOnce = false;

    /// <summary>
    /// Initializes the singleton instance and ensures only one TutorialBannerObserver exists.
    /// Destroys duplicate instances to maintain singleton pattern integrity.
    /// </summary>
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Handles banner placement events from the banner system.
    /// Validates that the banner is placed on appropriate tutorial targets (enemy/neutral buildings).
    /// Triggers tutorial progression event when valid placement occurs.
    /// </summary>
    /// <param name="column">The grid column where the banner was placed.</param>
    /// <param name="row">The grid row where the banner was placed.</param>
    public void OnBannerPlaced(int column, int row)
    {
        // On récupère le bâtiment ciblé directement depuis le singleton BannerController.
        if (hasBeenPlacedOnce)
        {
            Debug.LogWarning("[TutorialBannerObserver] OnBannerPlaced a été appelé, mais la bannière a déjà été placée une fois. On ignore cet appel.");
            return;
        }
        Building targetBuilding = BannerController.Instance.CurrentBuilding;
        
        // Si aucune bannière n'est active ou si la cible est nulle, on arrête.
        if (!BannerController.Instance.HasActiveBanner || targetBuilding == null)
        {
            Debug.LogWarning("[TutorialBannerObserver] OnBannerPlaced a été appelé, mais il n'y a pas de bâtiment cible valide.");
            return;
        }
        
        if (targetBuilding.Team == TeamType.Enemy || targetBuilding.Team == TeamType.Neutral || targetBuilding.Team == TeamType.NeutralEnemy)
        {
            Debug.Log($"[TutorialBannerObserver] Le joueur a placé la bannière sur une cible valide : {targetBuilding.name} (Équipe: {targetBuilding.Team}). L'étape du tutoriel est validée !");
            
            // On déclenche l'événement pour faire avancer le tutoriel.
            OnBannerPlacedOnBuilding?.Invoke();
            hasBeenPlacedOnce = true;
        }
        else
        {
            // Le placement initial sur le bâtiment allié sera ignoré ici.
            Debug.Log($"[TutorialBannerObserver] La bannière a été placée sur une cible non valide pour le tutoriel ({targetBuilding.name}, Équipe: {targetBuilding.Team}). On ignore.");
        }
    }

    public void ResetStatus()
    {
        hasBeenPlacedOnce = false;
    }
}