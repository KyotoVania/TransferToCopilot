using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class LevelSelectItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelNameText;
    [SerializeField] private Button selectButton;
    // Ajoute d'autres références UI si besoin (icône, indicateur de complétion, etc.)

    private LevelData_SO _currentLevelData;
    private Action<LevelData_SO> _onSelectAction; // Action à appeler quand on clique

    public void Setup(LevelData_SO levelData, bool isUnlocked, Action<LevelData_SO> onSelectCallback)
    {
        _currentLevelData = levelData;
        _onSelectAction = onSelectCallback;

        if (levelNameText != null)
        {
            levelNameText.text = levelData.DisplayName;
        }

        if (selectButton != null)
        {
            selectButton.interactable = isUnlocked;
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnItemClicked);

            // Visuel pour l'état bloqué/débloqué
            var colors = selectButton.colors;
            if (!isUnlocked)
            {
                if (levelNameText != null) levelNameText.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
                 colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }
            else
            {
                 if (levelNameText != null) levelNameText.color = Color.white; // Ou ta couleur par défaut
            }
             selectButton.colors = colors;
        }
    }

    private void OnItemClicked()
    {
        _onSelectAction?.Invoke(_currentLevelData);
    }
}