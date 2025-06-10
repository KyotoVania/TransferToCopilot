// Modified file: kyotovania/transfertocopilot/KyotoVania-TransferToCopilot-c815ca4872be6703099c7d3724009d8a6568c3ba/Scripts/UI/HUB/LevelSelectItemUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class LevelSelectItemUI : MonoBehaviour
{
    [Header("Common References")]
    [SerializeField] private TextMeshProUGUI levelNumberText;
    [SerializeField] private Button selectButton;

    [Header("State Visuals")]
    [Tooltip("Parent GameObject for star icons.")]
    [SerializeField] private GameObject starsContainer;
    [Tooltip("Image references for the 3 stars.")]
    [SerializeField] private List<Image> starImages;
    [Tooltip("The highlight border to show when this item is selected.")]
    [SerializeField] private GameObject selectionHighlight;

    private LevelData_SO _levelData;

    public void Setup(LevelData_SO levelData, int starRating, Action<LevelData_SO> onSelectCallback)
    {
        _levelData = levelData;
        
        if (levelNumberText != null)
        {
            levelNumberText.text = _levelData.OrderIndex.ToString();
        }

        // Configure stars for completed levels. Shows stars if rating > 0.
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

        // The selection highlight is off by default.
        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(false);
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelectCallback?.Invoke(_levelData));
        }
    }
    
    public void SetSelected(bool isSelected)
    {
        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(isSelected);
        }
    }

    public LevelData_SO GetLevelData()
    {
        return _levelData;
    }
}