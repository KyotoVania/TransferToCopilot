// Scripts/Hub/UI/AvailableCharacterListItemUI.cs

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using ScriptableObjects;

public class AvailableCharacterListItemUI : MonoBehaviour
{
    [Header("Références UI")]
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private Image characterIcon;
    [SerializeField] private TextMeshProUGUI characterLevelText;
    
    [Header("Effets Visuels")]
    [SerializeField] private GameObject focusEffectVisual;

    [Header("Interaction")]
    [SerializeField] private Button selectButton;

    private CharacterData_SO _characterData;
    private Action<CharacterData_SO> _onCharacterSelectedCallback;

    public void Setup(CharacterData_SO data, Action<CharacterData_SO> onSelect, int level)
    {
        _characterData = data;
        _onCharacterSelectedCallback = onSelect; // Le nom du paramètre est 'onSelect'
        SetSelected(false);

        if (_characterData == null)
        {
            gameObject.SetActive(false);
            return;
        }

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
        _onCharacterSelectedCallback?.Invoke(_characterData);
    }

    public CharacterData_SO GetCharacterData()
    {
        return _characterData;
    }
}