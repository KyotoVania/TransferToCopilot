using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Gère les animations visuelles (échelle et teinte) d'une carte de personnage.
/// DOIT être placé sur le GameObject RACINE du prefab de la carte.
/// Il gère à la fois la sélection via les boutons (manette) et le survol (souris).
/// </summary>
public class CardVisualController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Références Visuelles")]
    [Tooltip("L'Image de fond de la carte (sur la racine) qui sera teintée.")]
    [SerializeField] private Image backgroundImage;

    [Header("Références des Boutons Interactifs")]
    [Tooltip("Le bouton d'ajout quand la carte est vide.")]
    [SerializeField] private Button addButton; // Le bouton de l'état "Empty"

    [Tooltip("Le bouton principal quand un personnage est affiché.")]
    [SerializeField] private Button mainButton; // Le bouton de l'état "Character"
    
    [Tooltip("Le bouton poubelle quand un personnage est affiché.")]
    [SerializeField] private Button trashButton; // Le bouton de l'état "Character"

    [Header("Paramètres d'Animation")]
    [SerializeField] private float scaleAmount = 1.2f;
    [SerializeField] private float animationDuration = 0.2f;
    [SerializeField] private Color deselectedTint = new Color(0.8f, 0.8f, 0.8f, 1f);

    void Awake()
    {
        // On vérifie que toutes nos références sont bien là.
        if (backgroundImage == null || addButton == null ||  trashButton == null)
        {
            Debug.LogError($"[CardVisualController] Toutes les références (Image et Boutons) doivent être assignées sur {gameObject.name}!", this);
            enabled = false;
        }
    }
    
    // --- Méthodes publiques appelées par les Event Triggers ---

    public void AnimateToSelectedState()
    {
        LeanTween.cancel(gameObject);
        LeanTween.cancel(backgroundImage.rectTransform);
        LeanTween.scale(gameObject, Vector3.one * scaleAmount, animationDuration).setEase(LeanTweenType.easeOutBack);
        LeanTween.color(backgroundImage.rectTransform, Color.white, animationDuration);
    }

    public void AnimateToDeselectedState()
    {
        LeanTween.cancel(gameObject);
        LeanTween.cancel(backgroundImage.rectTransform);
        LeanTween.scale(gameObject, Vector3.one, animationDuration).setEase(LeanTweenType.easeOutBack);
        LeanTween.color(backgroundImage.rectTransform, deselectedTint, animationDuration);
    }
    
    // --- Gestion de la Souris (Hover) ---

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Quand la souris entre sur la carte, on l'agrandit, peu importe son état.
        AnimateToSelectedState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

        // Si la souris quitte la carte, on vérifie si l'un de nos boutons est
        // actuellement sélectionné par la manette/clavier.
        if (currentSelected == addButton.gameObject || currentSelected == trashButton.gameObject)
        {
            // Si oui, on ne fait rien ! La carte doit rester agrandie.
            return;
        }

        // Sinon, aucun bouton n'est sélectionné, on peut revenir à l'état initial.
        AnimateToDeselectedState();
    }
}