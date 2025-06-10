using UnityEngine;
using UnityEngine.UI;
using TMPro; // Si jamais tu veux ajouter un nom ou autre texte au slot
using System; // Pour Action
using ScriptableObjects; 
public class TeamSlotItemUI : MonoBehaviour
{
    [SerializeField] private Image characterIconImage;
    [SerializeField] private Button slotButton; // Le slot entier est cliquable
    [SerializeField] private GameObject emptySlotVisuals; // GameObject à afficher si le slot est vide

    private CharacterData_SO _characterData; // Null si le slot est vide
    private int _slotIndex;
    private Action<CharacterData_SO, int> _onClickAction; // Action pour retirer ou interagir avec le slot
    private Action<CharacterData_SO> _onShowDetailsAction; // Action pour afficher les détails

    public void Setup(CharacterData_SO data, int slotIndex, Action<CharacterData_SO, int> onClick, Action<CharacterData_SO> onShowDetails)
    {
        _characterData = data;
        _slotIndex = slotIndex;
        _onClickAction = onClick;
        _onShowDetailsAction = onShowDetails;

        if (characterIconImage != null)
        {
            if (_characterData != null && _characterData.Icon != null)
            {
                characterIconImage.sprite = _characterData.Icon;
                characterIconImage.enabled = true;
                if (emptySlotVisuals != null) emptySlotVisuals.SetActive(false);
            }
            else
            {
                characterIconImage.enabled = false;
                if (emptySlotVisuals != null) emptySlotVisuals.SetActive(true);
            }
        }

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        // Si le slot a un personnage, le clic le retire et affiche ses détails.
        // Si le slot est vide, le clic ne fait rien pour l'instant (ou pourrait sélectionner le slot pour un ajout).
        if (_characterData != null)
        {
            _onShowDetailsAction?.Invoke(_characterData); // Montre les détails d'abord
            _onClickAction?.Invoke(_characterData, _slotIndex); // Puis tente de retirer
        }
        else
        {
            // Logique future : sélectionner le slot vide pour y ajouter un personnage.
            // Pour l'instant, un clic sur un slot vide ne fait que potentiellement désélectionner
            // le panel de détails si rien n'est fourni à _onShowDetailsAction.
             _onShowDetailsAction?.Invoke(null); // Désélectionne le panel de détails
            Debug.Log($"[TeamSlotItemUI] Slot vide {_slotIndex} cliqué.");
        }
    }
}