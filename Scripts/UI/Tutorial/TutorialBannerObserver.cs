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
        // On ne veut déclencher l'événement de tutoriel qu'une seule fois,
        // la première fois que la bannière est placée.
        if (!hasBeenPlacedOnce)
        {
            hasBeenPlacedOnce = true;
            OnBannerPlacedOnBuilding?.Invoke();
            Debug.Log("[TutorialBannerObserver] Banner placed for the first time, invoking OnBannerPlacedOnBuilding.");
        }
    }

    // Méthode pour réinitialiser le statut si un nouveau tutoriel commence
    public void ResetStatus()
    {
        hasBeenPlacedOnce = false;
    }
}