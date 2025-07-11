using UnityEngine;
using UnityEngine.UI; // Important pour avoir accès au type Slider
using System.Collections;
using System.Collections.Generic;

public class MomentumDisplay : MonoBehaviour
{
    [Header("Références UI")]
    [Tooltip("Le Slider qui représente la jauge de Momentum. Le script le trouvera automatiquement sur cet objet si non assigné.")]
    [SerializeField] private Slider momentumSlider;

    [Tooltip("Liste des GameObjects des 3 icônes de charge. Elles seront activées/désactivées.")]
    [SerializeField] private List<GameObject> chargeIcons;

    [Header("Animation Settings")]
    [Tooltip("Durée de l'animation de progression du momentum en secondes")]
    [SerializeField] private float animationDuration = 0.3f;
    
    [Tooltip("Courbe d'animation pour la progression (optionnel)")]
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private MomentumManager _momentumManager;
    private Coroutine _currentAnimation;
    private float _targetValue;

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
    /// Met à jour l'affichage du Momentum en pilotant le Slider et les icônes avec animation.
    /// </summary>
    private void UpdateMomentumDisplay(int charges, float momentumValue)
    {
        // Animer la progression du Slider au lieu d'un changement instantané
        if (momentumSlider != null)
        {
            _targetValue = momentumValue;
            
            // Arrêter l'animation précédente si elle existe
            if (_currentAnimation != null)
            {
                StopCoroutine(_currentAnimation);
            }
            
            // Démarrer la nouvelle animation
            _currentAnimation = StartCoroutine(AnimateMomentumSlider());
        }

        // La logique des icônes de charge reste instantanée (plus naturel)
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

    /// <summary>
    /// Coroutine qui anime la progression du slider de momentum
    /// </summary>
    private IEnumerator AnimateMomentumSlider()
    {
        float startValue = momentumSlider.value;
        float elapsedTime = 0f;

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            
            // Utiliser la courbe d'animation si définie, sinon progression linéaire
            float curveValue = animationCurve != null ? animationCurve.Evaluate(progress) : progress;
            
            // Interpoler entre la valeur de départ et la valeur cible
            momentumSlider.value = Mathf.Lerp(startValue, _targetValue, curveValue);
            
            yield return null;
        }

        // S'assurer que la valeur finale est exacte
        momentumSlider.value = _targetValue;
        _currentAnimation = null;
    }
}