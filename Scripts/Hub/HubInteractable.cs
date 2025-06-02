using UnityEngine;

public class HubInteractable : MonoBehaviour
{
    public enum HubSection { LevelSelection, TeamManagement }
    [SerializeField] private HubSection sectionToOpen;

    // Référence au HubManager de la scène, à assigner dans l'Inspecteur
    [SerializeField] private HubManager hubManagerInstance;

    private void Start()
    {
        if (hubManagerInstance == null)
        {
            hubManagerInstance = FindFirstObjectByType<HubManager>(); // Trouve la première instance active de HubManager
            if (hubManagerInstance == null)
            {
                Debug.LogError($"[HubInteractable] HubManager non trouvé dans la scène pour l'objet {gameObject.name}. Assurez-vous qu'il existe et qu'il est actif, ou assignez-le dans l'inspecteur.");
                enabled = false; // Désactiver ce script s'il ne peut pas fonctionner
            }
        }
    }

    private void OnMouseDown() // Fonctionne si l'objet a un Collider
    {
        if (hubManagerInstance != null) // Utiliser la référence assignée
        {
            switch (sectionToOpen)
            {
                case HubSection.LevelSelection:
                    break;
                case HubSection.TeamManagement:
                    break;
            }
        }
        else
        {
            Debug.LogError($"[HubInteractable] hubManagerInstance n'est pas assigné sur {gameObject.name} et n'a pas pu être trouvé ! Impossible d'ouvrir la section.");
        }
    }
}