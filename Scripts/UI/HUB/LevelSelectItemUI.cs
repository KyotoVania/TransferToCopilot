using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using ScriptableObjects;

/// <summary>
/// Item UI pour la sélection de niveau avec support navigation manette et feedback visuel
/// </summary>
public class LevelSelectItemUI : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Common References")]
    [SerializeField] private TextMeshProUGUI levelNumberText;
    [SerializeField] private Button selectButton;

    [Header("State Visuals")]
    [Tooltip("Parent GameObject for star icons.")]
    [SerializeField] private GameObject starsContainer;
    [Tooltip("Image references for the 3 stars.")]
    [SerializeField] private List<Image> starImages;
    [Tooltip("The highlight border to show when this item is selected.")]
    [SerializeField] private GameObject selectionHighlight;

    [Header("Navigation Feedback")]
    [Tooltip("Effet visuel pour le focus manette (différent de la sélection)")]
    [SerializeField] private GameObject focusHighlight;
    [Tooltip("Effet de survol souris")]
    [SerializeField] private GameObject hoverEffect;
    
    [Header("Animation Settings")]
    [SerializeField] private float scaleOnFocus = 1.1f;
    [SerializeField] private float animationDuration = 0.2f;
    [SerializeField] private Color normalTint = Color.white;
    [SerializeField] private Color focusedTint = new Color(1.2f, 1.2f, 1.2f, 1f);

    private LevelData_SO _levelData;
    private Action<LevelData_SO> _onSelectCallback;
    private bool _isSelected = false; // Sélectionné logiquement (pour le jeu)
    private bool _isFocused = false;  // Focus manette/clavier
    private bool _isHovered = false;  // Survol souris
    private Vector3 _originalScale;

    #region Cycle de Vie Unity

    private void Awake()
    {
        _originalScale = transform.localScale;
        
        // S'assurer que tous les effets visuels sont désactivés au départ
        if (selectionHighlight != null) selectionHighlight.SetActive(false);
        if (focusHighlight != null) focusHighlight.SetActive(false);
        if (hoverEffect != null) hoverEffect.SetActive(false);
    }

    private void OnEnable()
    {
        ResetVisualState();
    }

    #endregion

    #region API Publique

    public void Setup(LevelData_SO levelData, int starRating, Action<LevelData_SO> onSelectCallback)
    {
        _levelData = levelData;
        _onSelectCallback = onSelectCallback;
        
        if (levelNumberText != null)
        {
            levelNumberText.text = _levelData.OrderIndex.ToString();
        }

        // Configure stars for completed levels. Shows stars if rating > 0.
        if (starsContainer != null && starImages != null)
        {
            bool isCompleted = starRating > 0;
            starsContainer.SetActive(isCompleted);
            if(isCompleted)
            {
                for (int i = 0; i < starImages.Count; i++)
                {
                    starImages[i].gameObject.SetActive(i < starRating);
                }
            }
        }

        // Configuration du bouton
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(HandleClick);
        }

        // Réinitialiser les états visuels
        _isSelected = false;
        ResetVisualState();
        
        Debug.Log($"[LevelSelectItemUI] Setup niveau {_levelData.OrderIndex} avec {starRating} étoiles");
    }
    
    public void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;
        UpdateVisualState();
    }

    public LevelData_SO GetLevelData()
    {
        return _levelData;
    }

    #endregion

    #region Gestion des Événements

    private void HandleClick()
    {
        Debug.Log($"[LevelSelectItemUI] Clic sur niveau {_levelData?.OrderIndex ?? -1}");
        
        // Animation de clic
        StartCoroutine(ClickAnimation());
        
        // Déclencher le callback
        _onSelectCallback?.Invoke(_levelData);
    }

    private System.Collections.IEnumerator ClickAnimation()
    {
        // Animation rapide de "press"
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, _originalScale * 0.95f, 0.1f).setEase(LeanTweenType.easeOutQuad);
        
        yield return new WaitForSeconds(0.1f);
        
        // Retour à l'état normal
        UpdateVisualState();
        LeanTween.scale(gameObject, GetTargetScale(), 0.1f).setEase(LeanTweenType.easeOutQuad);
    }

    #endregion

    #region Interfaces d'Événements Unity

    /// <summary>
    /// Appelé quand l'item reçoit le focus (navigation manette/clavier)
    /// </summary>
    public void OnSelect(BaseEventData eventData)
    {
        _isFocused = true;
        UpdateVisualState();
        Debug.Log($"[LevelSelectItemUI] Focus reçu : Niveau {_levelData?.OrderIndex ?? -1}");
    }

    /// <summary>
    /// Appelé quand l'item perd le focus (navigation manette/clavier)
    /// </summary>
    public void OnDeselect(BaseEventData eventData)
    {
        _isFocused = false;
        UpdateVisualState();
    }

    /// <summary>
    /// Appelé quand la souris entre sur l'item
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        
        // Si on utilise la souris, prendre le focus automatiquement
        if (EventSystem.current.currentSelectedGameObject != gameObject)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
        
        UpdateVisualState();
    }

    /// <summary>
    /// Appelé quand la souris sort de l'item
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        UpdateVisualState();
    }

    #endregion

    #region Gestion des États Visuels

    /// <summary>
    /// Met à jour l'apparence selon l'état actuel
    /// </summary>
    private void UpdateVisualState()
    {
        // Hiérarchie des effets visuels :
        // 1. Sélection logique (rouge/vert) - le niveau choisi pour jouer
        // 2. Focus manette (bleu) - le niveau actuellement fokusé
        // 3. Hover souris (jaune) - le niveau survolé par la souris
        
        // Gestion des effets visuels
        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(_isSelected);
        }
        
        if (focusHighlight != null)
        {
            focusHighlight.SetActive(_isFocused && !_isSelected);
        }
        
        if (hoverEffect != null)
        {
            hoverEffect.SetActive(_isHovered && !_isFocused && !_isSelected);
        }
        
        // Animation de couleur du texte de niveau
        if (levelNumberText != null)
        {
            Color targetColor = (_isFocused || _isHovered) ? focusedTint : normalTint;
            LeanTween.cancel(levelNumberText.gameObject);
            LeanTween.color(levelNumberText.rectTransform, targetColor, animationDuration);
        }
        
        // Animation d'échelle
        Vector3 targetScale = GetTargetScale();
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, targetScale, animationDuration).setEase(LeanTweenType.easeOutBack);
    }

    private Vector3 GetTargetScale()
    {
        // Agrandir légèrement quand fokusé ou survolé (mais pas sélectionné)
        bool shouldBeEnlarged = (_isFocused || _isHovered) && !_isSelected;
        return shouldBeEnlarged ? _originalScale * scaleOnFocus : _originalScale;
    }

    /// <summary>
    /// Remet l'item dans son état visuel par défaut
    /// </summary>
    private void ResetVisualState()
    {
        _isFocused = false;
        _isHovered = false;
        
        // Arrêter toutes les animations en cours
        LeanTween.cancel(gameObject);
        if (levelNumberText != null)
        {
            LeanTween.cancel(levelNumberText.gameObject);
        }
        
        // Réinitialiser les valeurs
        transform.localScale = _originalScale;
        
        if (levelNumberText != null)
        {
            levelNumberText.color = normalTint;
        }
        
        // Désactiver tous les effets sauf la sélection
        if (focusHighlight != null) focusHighlight.SetActive(false);
        if (hoverEffect != null) hoverEffect.SetActive(false);
        // Note: on garde selectionHighlight selon _isSelected
        
        UpdateVisualState(); // Pour appliquer l'état de sélection si nécessaire
    }

    #endregion

    #region Méthodes Utilitaires

    /// <summary>
    /// Force la mise à jour de l'état visuel
    /// </summary>
    public void RefreshVisualState()
    {
        UpdateVisualState();
    }

    /// <summary>
    /// Simule un clic sur ce niveau
    /// </summary>
    public void SimulateClick()
    {
        if (selectButton != null && selectButton.interactable)
        {
            HandleClick();
        }
    }

    #endregion

    #region Debug

    public override string ToString()
    {
        return $"LevelSelectItemUI: Niveau {_levelData?.OrderIndex ?? -1} " +
               $"(Selected: {_isSelected}, Focused: {_isFocused}, Hovered: {_isHovered})";
    }

    #endregion
}