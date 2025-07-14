using UnityEngine;

// Cette classe est un Singleton simple qui écoute les événements de la bannière
// et déclenche son propre événement pour le TutorialManager.
public class TutorialBannerObserver : MonoBehaviour, Game.Observers.IBannerObserver
{
    public static TutorialBannerObserver Instance { get; private set; }
    
    public event System.Action OnBannerPlacedOnBuilding;

    private bool hasBeenPlacedOnce = false;

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
        
        // --- LOGIQUE DU ZAP APPLIQUÉE ICI ---
        // On vérifie si l'équipe du bâtiment est Ennemie ou Neutre.
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

    // Méthode pour réinitialiser le statut si un nouveau tutoriel commence
    public void ResetStatus()
    {
        hasBeenPlacedOnce = false;
    }
}