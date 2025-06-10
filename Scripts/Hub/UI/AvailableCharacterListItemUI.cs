using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using ScriptableObjects;

public class AvailableCharacterListItemUI : MonoBehaviour
{
    [Header("Références UI")]
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private Image characterIcon; // Assurez-vous d'avoir une Image pour l'icône du perso
    [SerializeField] private TextMeshProUGUI characterLevelText; // Pour le "Lv47" etc.
    // Ajoutez d'autres références si nécessaire (ex: puissance, etc.)
    [Header("Effets Visuels")]
    [SerializeField] private GameObject focusEffectVisual; // Glissez ici votre objet de focus

    [Header("Interaction")]
    [SerializeField] private Button selectButton;

    private CharacterData_SO _characterData;
    private Action<CharacterData_SO> _onSelectCallback;

    public void Setup(CharacterData_SO data, Action<CharacterData_SO> onSelect, int level)
    {
        _characterData = data;
        _onSelectCallback = onSelect;
        SetSelected(false);

        if (_characterData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // Peupler les champs UI
        if (characterNameText != null)
            characterNameText.text = _characterData.DisplayName;

        if (characterIcon != null)
        {
            characterIcon.sprite = _characterData.Icon;
            characterIcon.enabled = (_characterData.Icon != null);
        }

        if (characterLevelText != null)
        {
            characterLevelText.text = $"Lv. {level}";
        }

        // Configurer le bouton
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(HandleClick);
        }
    }
    
    public void SetSelected(bool isSelected)
    {
        focusEffectVisual?.SetActive(isSelected);
    }

    private void HandleClick()
    {
        _onSelectCallback?.Invoke(_characterData);
    }
    public CharacterData_SO GetCharacterData()
    {
        return _characterData;
    }

    
}