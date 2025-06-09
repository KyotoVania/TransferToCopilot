using UnityEngine;
// Importer le namespace si votre script OutlineFx y est défini
using OutlineFx; // <--- Assurez-vous que ceci correspond au namespace de l'asset

[RequireComponent(typeof(Building))] // Toujours bien d'avoir cette dépendance
public class BuildingOutlineFeedback : MonoBehaviour
{
    private Building buildingComponent; // Toujours utile si vous voulez ajouter des conditions basées sur le bâtiment lui-même
    private OutlineFx.OutlineFx outlineEffectComponent;

    private bool isOutlineActive = false;

    void Awake()
    {
        buildingComponent = GetComponent<Building>(); // On le garde au cas où
        if (buildingComponent == null)
        {
            Debug.LogError($"[{gameObject.name}/BuildingOutlineFeedback] Composant Building non trouvé !", this);
            enabled = false;
            return;
        }

        outlineEffectComponent = GetComponent<OutlineFx.OutlineFx>();

        if (outlineEffectComponent == null)
        {
            Debug.LogWarning($"[{gameObject.name}/BuildingOutlineFeedback] Composant 'OutlineFx.OutlineFx' non trouvé. Tentative de l'ajouter...", this);
            outlineEffectComponent = gameObject.AddComponent<OutlineFx.OutlineFx>();
            if (outlineEffectComponent == null)
            {
                 Debug.LogError($"[{gameObject.name}/BuildingOutlineFeedback] Échec de l'ajout du composant 'OutlineFx.OutlineFx'. L'outline ne fonctionnera pas.", this);
                 enabled = false; // Désactiver ce script si l'outline ne peut pas être contrôlée
                 return;
            }
        }

        // Très important : s'assurer que l'outline est désactivée par défaut
        // et que c'est ce script qui contrôle son état visible/invisible.
        outlineEffectComponent.enabled = false;
        isOutlineActive = false;
    }

    public void ShowOutline()
    {
        if (outlineEffectComponent == null) return;

        // La couleur est CELLE DEJA CONFIGUREE sur outlineEffectComponent.
        // On s'assure juste qu'il est activé.
        if (!outlineEffectComponent.enabled)
        {
            outlineEffectComponent.enabled = true;
        }
        isOutlineActive = true;
        // Si OutlineFx a besoin qu'on lui redise d'appliquer ses propriétés après l'avoir activé :
        // outlineEffectComponent.ApplyModifiedProperties(); // Ou une méthode équivalente si elle existe
    }

    public void HideOutline()
    {
        // On vérifie isOutlineActive pour éviter de désactiver inutilement
        // si HideOutline est appelé plusieurs fois.
        if (!isOutlineActive || outlineEffectComponent == null)
        {
            return;
        }

        if (outlineEffectComponent.enabled)
        {
            outlineEffectComponent.enabled = false;
        }
        isOutlineActive = false;

        // Debug.Log($"[{gameObject.name}/BuildingOutlineFeedback] HideOutline");
    }

    // S'assurer que l'outline est cachée si cet objet (le bâtiment) est désactivé
    void OnDisable()
    {
        if (isOutlineActive && outlineEffectComponent != null)
        {
            outlineEffectComponent.enabled = false;
            isOutlineActive = false;
        }
    }
}