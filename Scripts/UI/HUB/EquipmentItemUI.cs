namespace Hub
{
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.EventSystems;
    using System;
    using ScriptableObjects;

    /// <summary>
    /// Gère l'affichage et l'interaction avec un item d'équipement.
    /// Optimisé pour la navigation manette avec feedback visuel.
    /// </summary>
    public class EquipmentItemUI : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Composants UI")]
        [SerializeField] private Image itemIconImage;
        [SerializeField] private Button itemButton;
        
        [Header("Feedback Visuel")]
        [Tooltip("Bordure ou effet qui s'affiche quand l'item est sélectionné (focus manette)")]
        [SerializeField] private GameObject selectionHighlight;
        [Tooltip("Effet de survol souris (optionnel, différent du focus manette)")]
        [SerializeField] private GameObject hoverEffect;
        
        [Header("Paramètres Visuels")]
        [SerializeField] private Color normalTint = Color.white;
        [SerializeField] private Color selectedTint = new Color(1.2f, 1.2f, 1.2f, 1f);
        [SerializeField] private Color pressedTint = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private float scaleOnSelect = 1.1f;
        [SerializeField] private float animationDuration = 0.2f;

        private EquipmentData_SO _equipmentData;
        private Action<EquipmentData_SO> _onClickCallback;
        private bool _isSelected = false;
        private bool _isHovered = false;
        private Vector3 _originalScale;

        #region Cycle de Vie Unity

        private void Awake()
        {
            // Stocker l'échelle originale
            _originalScale = transform.localScale;
            
            // S'assurer que les effets visuels sont désactivés au départ
            if (selectionHighlight != null) selectionHighlight.SetActive(false);
            if (hoverEffect != null) hoverEffect.SetActive(false);
            
            // Vérifier les références critiques
            if (itemIconImage == null)
            {
                Debug.LogError($"[EquipmentItemUI] itemIconImage non assigné sur {gameObject.name}!");
            }
            
            if (itemButton == null)
            {
                Debug.LogError($"[EquipmentItemUI] itemButton non assigné sur {gameObject.name}!");
            }
        }

        private void OnEnable()
        {
            // Réinitialiser l'état visuel à l'activation
            ResetVisualState();
        }

        #endregion

        #region API Publique

        /// <summary>
        /// Configure l'item UI avec les données d'un équipement et une action à exécuter au clic.
        /// </summary>
        public void Setup(EquipmentData_SO data, Action<EquipmentData_SO> onClickAction)
        {
            _equipmentData = data;
            _onClickCallback = onClickAction;

            // Configuration de l'icône
            if (itemIconImage != null)
            {
                if (_equipmentData != null && _equipmentData.Icon != null)
                {
                    itemIconImage.sprite = _equipmentData.Icon;
                    itemIconImage.enabled = true;
                    itemIconImage.color = normalTint;
                }
                else
                {
                    // Cache l'icône si pas de data ou pas d'icône
                    itemIconImage.enabled = false;
                }
            }

            // Configuration du bouton
            if (itemButton != null)
            {
                itemButton.onClick.RemoveAllListeners();
                itemButton.onClick.AddListener(HandleClick);
                
                // S'assurer que le bouton est interactable
                itemButton.interactable = (_equipmentData != null);
            }

            // Réinitialiser l'état visuel
            ResetVisualState();
            
            Debug.Log($"[EquipmentItemUI] Setup : {(_equipmentData?.DisplayName ?? "Empty")}");
        }

        /// <summary>
        /// Retourne les données d'équipement de cet item
        /// </summary>
        public EquipmentData_SO GetEquipmentData()
        {
            return _equipmentData;
        }

        /// <summary>
        /// Indique si cet item contient des données valides
        /// </summary>
        public bool HasEquipment()
        {
            return _equipmentData != null;
        }

        #endregion

        #region Gestion des Événements UI

        private void HandleClick()
        {
            Debug.Log($"[EquipmentItemUI] Clic sur : {(_equipmentData?.DisplayName ?? "Empty")}");
            
            // Animation de clic
            if (_equipmentData != null)
            {
                StartCoroutine(ClickAnimation());
            }
            
            // Déclencher le callback
            _onClickCallback?.Invoke(_equipmentData);
        }

        private System.Collections.IEnumerator ClickAnimation()
        {
            // Animation rapide de "press"
            if (itemIconImage != null)
            {
                itemIconImage.color = pressedTint;
            }
            
            // Légère réduction d'échelle
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
            _isSelected = true;
            UpdateVisualState();
            Debug.Log($"[EquipmentItemUI] Focus reçu : {(_equipmentData?.DisplayName ?? "Empty")}");
        }

        /// <summary>
        /// Appelé quand l'item perd le focus (navigation manette/clavier)
        /// </summary>
        public void OnDeselect(BaseEventData eventData)
        {
            _isSelected = false;
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
        /// Met à jour l'apparence selon l'état actuel (sélectionné, survolé, etc.)
        /// </summary>
        private void UpdateVisualState()
        {
            bool shouldBeHighlighted = _isSelected || _isHovered;
            
            // Gestion des effets visuels
            if (selectionHighlight != null)
            {
                selectionHighlight.SetActive(_isSelected);
            }
            
            if (hoverEffect != null)
            {
                hoverEffect.SetActive(_isHovered && !_isSelected);
            }
            
            // Animation de l'icône
            if (itemIconImage != null && _equipmentData != null)
            {
                Color targetColor = shouldBeHighlighted ? selectedTint : normalTint;
                LeanTween.cancel(itemIconImage.gameObject);
                LeanTween.color(itemIconImage.rectTransform, targetColor, animationDuration);
            }
            
            // Animation d'échelle
            Vector3 targetScale = GetTargetScale();
            LeanTween.cancel(gameObject);
            LeanTween.scale(gameObject, targetScale, animationDuration).setEase(LeanTweenType.easeOutBack);
        }

        private Vector3 GetTargetScale()
        {
            bool shouldBeEnlarged = _isSelected || _isHovered;
            return shouldBeEnlarged ? _originalScale * scaleOnSelect : _originalScale;
        }

        /// <summary>
        /// Remet l'item dans son état visuel par défaut
        /// </summary>
        private void ResetVisualState()
        {
            _isSelected = false;
            _isHovered = false;
            
            // Arrêter toutes les animations en cours
            LeanTween.cancel(gameObject);
            if (itemIconImage != null)
            {
                LeanTween.cancel(itemIconImage.gameObject);
            }
            
            // Réinitialiser les valeurs
            transform.localScale = _originalScale;
            
            if (itemIconImage != null && _equipmentData != null)
            {
                itemIconImage.color = normalTint;
            }
            
            // Désactiver les effets
            if (selectionHighlight != null) selectionHighlight.SetActive(false);
            if (hoverEffect != null) hoverEffect.SetActive(false);
        }

        #endregion

        #region Méthodes Utilitaires

        /// <summary>
        /// Force la mise à jour de l'état visuel (utile après changement de données)
        /// </summary>
        public void RefreshVisualState()
        {
            UpdateVisualState();
        }

        /// <summary>
        /// Simule un clic sur cet item (pour tests ou actions programmatiques)
        /// </summary>
        public void SimulateClick()
        {
            if (itemButton != null && itemButton.interactable)
            {
                HandleClick();
            }
        }

        #endregion
        
        #region Debug et Informations

        /// <summary>
        /// Retourne une description de l'item pour le debug
        /// </summary>
        public override string ToString()
        {
            return $"EquipmentItemUI: {(_equipmentData?.DisplayName ?? "Empty")} " +
                   $"(Selected: {_isSelected}, Hovered: {_isHovered})";
        }

        #endregion
    }
}