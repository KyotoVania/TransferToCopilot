namespace Hub
{
    using UnityEngine;
    using UnityEngine.UI;
    using System;
    using ScriptableObjects;

    public class EquipmentItemUI : MonoBehaviour
    {
        [SerializeField] private Image itemIconImage;
        [SerializeField] private Button itemButton;

        private EquipmentData_SO _equipmentData;
        private Action<EquipmentData_SO> _onClickCallback;

        /// <summary>
        /// Configure l'item UI avec les données d'un équipement et une action à exécuter au clic.
        /// </summary>
        public void Setup(EquipmentData_SO data, Action<EquipmentData_SO> onClickAction)
        {
            _equipmentData = data;
            _onClickCallback = onClickAction;

            if (_equipmentData != null && _equipmentData.Icon != null)
            {
                itemIconImage.sprite = _equipmentData.Icon;
                itemIconImage.enabled = true;
            }
            else
            {
                // Cache l'icône si pas de data ou pas d'icône (pour les slots vides par ex.)
                itemIconImage.enabled = false;
            }

            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(HandleClick);
        }

        private void HandleClick()
        {
            // Quand on clique, on appelle le callback avec nos données.
            // C'est le panel principal qui décidera s'il faut équiper ou déséquiper.
            _onClickCallback?.Invoke(_equipmentData);
        }
    }
}