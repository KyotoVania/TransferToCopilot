using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class TeamSlotUI : MonoBehaviour
{
    [Header("Groupes d'affichage")]
    [SerializeField] private GameObject occupiedSlotVisuals;
    [SerializeField] private GameObject emptySlotVisuals;

    [Header("Éléments du Slot Occupé")]
    [SerializeField] private Image characterImage;
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private TextMeshProUGUI characterDescText;
    [SerializeField] private Button removeButton;

    [Header("Éléments du Slot Vide")]
    [SerializeField] private Button addButton;

    private CharacterData_SO _characterData;
    private int _slotIndex;
    private Action<CharacterData_SO> _onRemoveCallback;
    private Action<int> _onAddCallback;

    /// <summary>
    /// Configure le slot pour afficher soit un personnage, soit un bouton d'ajout.
    /// </summary>
    public void Setup(CharacterData_SO characterData, int slotIndex, Action<CharacterData_SO> onRemove, Action<int> onAdd)
    {
        _characterData = characterData;
        _slotIndex = slotIndex;
        _onRemoveCallback = onRemove;
        _onAddCallback = onAdd;

        if (_characterData != null)
        {
            // --- Slot Occupé ---
            occupiedSlotVisuals.SetActive(true);
            emptySlotVisuals.SetActive(false);

            if (characterImage != null)
            {
                characterImage.sprite = _characterData.Icon;
                characterImage.enabled = true;
            }
            if (characterNameText != null)
            {
                characterNameText.text = _characterData.DisplayName;
            }
            if (characterDescText != null)
            {
                // Exemple, vous pouvez adapter ceci avec les vraies données de CharacterData_SO si vous les ajoutez
                if (_characterData.BaseStats != null)
                {
                    characterDescText.text = $"Lvl ?? / {_characterData.BaseStats.Type}";
                }
                else
                {
                    characterDescText.text = "Stats N/A";
                }
            }

            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(HandleRemoveClick);
        }
        else
        {
            // --- Slot Vide ---
            occupiedSlotVisuals.SetActive(false);
            emptySlotVisuals.SetActive(true);

            addButton.onClick.RemoveAllListeners();
            addButton.onClick.AddListener(HandleAddClick);
        }
    }

    private void HandleRemoveClick()
    {
        _onRemoveCallback?.Invoke(_characterData);
    }

    private void HandleAddClick()
    {
        _onAddCallback?.Invoke(_slotIndex);
    }
}