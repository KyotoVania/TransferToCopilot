using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System; 
using ScriptableObjects;

public class AvailableCharacterItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private Image characterIconImage;
    [SerializeField] private Button selectButton; // Le bouton est l'item entier

    private CharacterData_SO _characterData;
    private Action<CharacterData_SO> _onSelectAction; // Action pour ajouter à l'équipe
    private Action<CharacterData_SO> _onShowDetailsAction; // Action pour afficher les détails

    public void Setup(CharacterData_SO data, Action<CharacterData_SO> onSelect, Action<CharacterData_SO> onShowDetails)
    {
        _characterData = data;
        _onSelectAction = onSelect;
        _onShowDetailsAction = onShowDetails;

        if (characterNameText != null) characterNameText.text = _characterData.DisplayName;
        if (characterIconImage != null)
        {
            characterIconImage.sprite = _characterData.Icon;
            characterIconImage.enabled = (_characterData.Icon != null);
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        // Au clic, on notifie d'abord pour afficher les détails,
        // puis on notifie pour tenter d'ajouter à l'équipe.
        // Tu pourrais choisir de ne faire qu'une seule action ici si le design le demande.
        _onShowDetailsAction?.Invoke(_characterData);
        _onSelectAction?.Invoke(_characterData);
    }
}