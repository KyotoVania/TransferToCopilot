using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using ScriptableObjects;

/// <summary>
/// Item UI pour la sélection de niveau - Version simplifiée basée sur AvailableCharacterListItemUI
/// </summary>
public class LevelSelectItemUI : MonoBehaviour
{
    [Header("Common References")]
    [SerializeField] private TextMeshProUGUI levelNumberText;
    [SerializeField] private Button selectButton;

    [Header("State Visuals")]
    [SerializeField] private GameObject starsContainer;
    [SerializeField] private List<Image> starImages;
    
    [Header("Effets Visuels")]
    [SerializeField] private GameObject focusEffectVisual; // Effet de focus (hover ou navigation)
    [SerializeField] private GameObject selectedEffectVisual; // Effet de sélection (après validation)
    
    [Header("Animation Settings")]
    [SerializeField] private float scaleOnFocus = 1.1f;
    [SerializeField] private float animationDuration = 0.2f;

    private LevelData_SO _levelData;
    private Action<LevelData_SO> _onSelectCallback;
    private bool _isFocused = false;
    private bool _isSelected = false;
    private Vector3 _originalScale;

    private void Awake()
    {
        _originalScale = transform.localScale;
        
        // S'assurer que tous les effets sont désactivés au départ
        if (focusEffectVisual != null) focusEffectVisual.SetActive(false);
        if (selectedEffectVisual != null) selectedEffectVisual.SetActive(false);
    }

    private void OnEnable()
    {
        ResetVisualState();
    }

    /// <summary>
    /// Configure l'item avec les données du niveau
    /// </summary>
    public void Setup(LevelData_SO levelData, int starRating, Action<LevelData_SO> onSelectCallback)
    {
        _levelData = levelData;
        _onSelectCallback = onSelectCallback;
        
        if (levelNumberText != null)
        {
            levelNumberText.text = _levelData.OrderIndex.ToString();
        }

        // Configure stars
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

        // Réinitialiser les états
        SetFocused(false);
        SetSelected(false);
        
        Debug.Log($"[LevelSelectItemUI] Setup niveau {_levelData.OrderIndex} avec {starRating} étoiles");
    }
    
    /// <summary>
    /// Définit si cet item a le focus (hover souris ou navigation manette)
    /// </summary>
    public void SetFocused(bool isFocused)
    {
        if (_isFocused == isFocused) return;
        
        _isFocused = isFocused;
        UpdateVisualState();
    }
    
    /// <summary>
    /// Définit si cet item est sélectionné (après validation)
    /// </summary>
    public void SetSelected(bool isSelected)
    {
        if (_isSelected == isSelected) return;
        
        _isSelected = isSelected;
        UpdateVisualState();
    }

    /// <summary>
    /// Retourne les données du niveau
    /// </summary>
    public LevelData_SO GetLevelData()
    {
        return _levelData;
    }

    /// <summary>
    /// Retourne le GameObject principal pour la détection de focus
    /// </summary>
    public GameObject GetGameObject()
    {
        return gameObject;
    }

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
        
        // Retour à l'état avec focus
        UpdateVisualState();
    }

    /// <summary>
    /// Met à jour l'état visuel basé sur les flags focus et selected
    /// </summary>
    private void UpdateVisualState()
    {
        // Gestion des effets visuels
        if (focusEffectVisual != null)
        {
            focusEffectVisual.SetActive(_isFocused && !_isSelected);
        }
        
        if (selectedEffectVisual != null)
        {
            selectedEffectVisual.SetActive(_isSelected);
        }
        
        // Animation d'échelle
        Vector3 targetScale = (_isFocused || _isSelected) ? _originalScale * scaleOnFocus : _originalScale;
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, targetScale, animationDuration).setEase(LeanTweenType.easeOutBack);
        
        Debug.Log($"[LevelSelectItemUI] Niveau {_levelData?.OrderIndex ?? -1} - Focus: {_isFocused}, Selected: {_isSelected}");
    }

    private void ResetVisualState()
    {
        _isFocused = false;
        _isSelected = false;
        
        // Arrêter toutes les animations
        LeanTween.cancel(gameObject);
        
        // Réinitialiser les valeurs
        transform.localScale = _originalScale;
        
        // Désactiver tous les effets
        if (focusEffectVisual != null) focusEffectVisual.SetActive(false);
        if (selectedEffectVisual != null) selectedEffectVisual.SetActive(false);
    }
}