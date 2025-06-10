using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ScriptableObjects;

/// <summary>
/// Composant optionnel pour les boutons de niveau dans la scène de debug
/// Peut être utilisé pour des fonctionnalités supplémentaires si nécessaire
/// </summary>
public class LevelDebugButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI levelNameText;
    [SerializeField] private Image backgroundImage;
    
    private LevelData_SO associatedLevel;

    /// <summary>
    /// Configure ce bouton avec les données de niveau
    /// </summary>
    public void Setup(LevelData_SO levelData, System.Action<LevelData_SO> onClickCallback)
    {
        associatedLevel = levelData;
        
        if (levelNameText != null)
        {
            levelNameText.text = levelData.DisplayName;
        }
        
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClickCallback?.Invoke(levelData));
        }
    }

    /// <summary>
    /// Met en surbrillance ce bouton (niveau sélectionné)
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = highlighted ? Color.yellow : Color.white;
        }
    }
}