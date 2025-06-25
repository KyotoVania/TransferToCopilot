using UnityEngine;
using UnityEngine.UI; // Important pour avoir accès au type Slider
using System.Collections.Generic;

public class MomentumDisplay : MonoBehaviour
{
    [Header("Références UI")]
    [Tooltip("Le Slider qui représente la jauge de Momentum. Le script le trouvera automatiquement sur cet objet si non assigné.")]
    [SerializeField] private Slider momentumSlider;

    [Tooltip("Liste des GameObjects des 3 icônes de charge. Elles seront activées/désactivées.")]
    [SerializeField] private List<GameObject> chargeIcons;

    private MomentumManager _momentumManager;

    // Awake est appelé avant Start. C'est le meilleur endroit pour récupérer les composants.
    void Awake()
    {
        // Si le Slider n'a pas été glissé dans l'inspecteur, on essaie de le trouver
        // sur le même GameObject que ce script.
        if (momentumSlider == null)
        {
            momentumSlider = GetComponent<Slider>();
        }
    }
    
    void Start()
    {
        // Validation des références. On vérifie maintenant momentumSlider au lieu de momentumFillImage.
        if (momentumSlider == null || chargeIcons == null || chargeIcons.Count != 3)
        {
            Debug.LogError("[MomentumDisplay] Référence au Slider ou aux icônes de charge manquante/incorrecte !", this);
            this.enabled = false;
            return;
        }

        // Configuration initiale du Slider
        momentumSlider.minValue = 0f;
        momentumSlider.maxValue = 3.0f; // Le Momentum a 3 charges max.
        momentumSlider.value = 0f;      // Assurer que la valeur de départ est 0.

        // Le reste de la logique d'abonnement est identique.
        _momentumManager = MomentumManager.Instance;
        if (_momentumManager != null)
        {
            _momentumManager.OnMomentumChanged += UpdateMomentumDisplay;
            UpdateMomentumDisplay(_momentumManager.CurrentCharges, _momentumManager.CurrentMomentumValue);
        }
        else
        {
            Debug.LogError("[MomentumDisplay] MomentumManager.Instance non trouvé ! Assurez-vous que la scène 'Core' est bien chargée.", this);
            this.enabled = false;
        }
    }
    
    private void OnDestroy()
    {
        // Toujours se désabonner pour éviter les fuites de mémoire.
        if (_momentumManager != null)
        {
            _momentumManager.OnMomentumChanged -= UpdateMomentumDisplay;
        }
    }

    /// <summary>
    /// Met à jour l'affichage du Momentum en pilotant le Slider et les icônes.
    /// </summary>
    private void UpdateMomentumDisplay(int charges, float momentumValue)
    {
        // Mettre à jour la valeur du Slider. C'est la ligne clé de la correction.
        if (momentumSlider != null)
        {
            // Le Slider se chargera lui-même de mettre à jour son image de remplissage.
            momentumSlider.value = momentumValue;
        }

        // La logique des icônes de charge reste la même.
        if (chargeIcons != null)
        {
            for (int i = 0; i < chargeIcons.Count; i++)
            {
                if (chargeIcons[i] != null)
                {
                    chargeIcons[i].SetActive(i < charges);
                }
            }
        }
    }
}