namespace Hub
{ 
    using UnityEngine;
    using UnityEngine.UI;
    using TMPro;
    using System.Collections.Generic;
    using System.Linq;
    using ScriptableObjects;

    public class EquipmentPanelUI : MonoBehaviour
    {
        [Header("Panel References")]
        [SerializeField] private GameObject teamManagementPanel;
        [SerializeField] private Button backButton;

        [Header("Character Display")]
        [SerializeField] private UI.HUB.Character3DPreview characterPreview;
        [SerializeField] private TextMeshProUGUI characterNameText;
        [SerializeField] private TextMeshProUGUI characterLevelText;
        [SerializeField] private Slider xpSlider;
        [SerializeField] private TextMeshProUGUI xpSliderText;
        [SerializeField] private TextMeshProUGUI statsText; 
        [Header("Equipment & Inventory")]
        [Tooltip("Le parent des slots d'équipement du personnage (ex: casque, arme...).")]
        [SerializeField] private Transform equipmentSlotsContainer;
        [Tooltip("Le parent de la grille d'items de l'inventaire.")]
        [SerializeField] private Transform inventoryGridContainer;
        [SerializeField] private GameObject equipmentItemPrefab;

        private CharacterData_SO _currentCharacter;
        private List<GameObject> _instantiatedInventoryItems = new List<GameObject>();
        private List<GameObject> _instantiatedEquippedItems = new List<GameObject>();

        private void Awake()
        {
            backButton?.onClick.AddListener(HidePanel);
        }

        public void ShowPanelFor(CharacterData_SO character)
        {
            _currentCharacter = character;
            if (_currentCharacter == null)
            {
                Debug.LogError("[EquipmentPanelUI] ShowPanelFor a été appelé avec un personnage null.");
                HidePanel();
                return;
            }
            
            gameObject.SetActive(true);
            RefreshAll();
        }

        private void RefreshAll()
        {
            if (_currentCharacter == null) return;

            // 1. Afficher les infos du personnage
            characterNameText.text = _currentCharacter.DisplayName;
            characterPreview.ShowCharacter(_currentCharacter.HubVisualPrefab);
            UpdateLevelAndXPDisplay();

            // 2. Peupler les slots et l'inventaire
            PopulateEquippedSlots();
            PopulateInventoryGrid();
            
            UpdateStatsDisplay();
        }

        private void UpdateLevelAndXPDisplay()
        {
            PlayerDataManager.Instance.Data.CharacterProgressData.TryGetValue(_currentCharacter.CharacterID, out CharacterProgress progress);
            if (progress == null) progress = new CharacterProgress();

            characterLevelText.text = $" {progress.CurrentLevel}";

            if (_currentCharacter.ProgressionData != null)
            {
                // Utilise la courbe de progression pour obtenir l'XP requise pour le NIVEAU SUIVANT
                int xpForNextLevel = _currentCharacter.ProgressionData.GetXPRequiredForLevel(progress.CurrentLevel + 1);
                int xpForCurrentLevel = _currentCharacter.ProgressionData.GetXPRequiredForLevel(progress.CurrentLevel);

                // Gérer le cas du niveau maximum où l'XP requise pourrait ne pas augmenter
                if (xpForNextLevel <= xpForCurrentLevel) 
                {
                    xpSlider.value = 1f;
                    if(xpSliderText != null) xpSliderText.text = "MAX";
                }
                else
                {
                    // Calculer l'XP gagnée depuis le début du niveau actuel
                    int xpSinceLastLevel = progress.CurrentXP - xpForCurrentLevel;
                    int xpNeededForThisLevel = xpForNextLevel - xpForCurrentLevel;

                    xpSlider.value = (float)xpSinceLastLevel / Mathf.Max(1, xpNeededForThisLevel);
                    if(xpSliderText != null) xpSliderText.text = $"{xpSinceLastLevel} / {xpNeededForThisLevel} XP";
                }
            }
            else // Fallback si pas de données de progression
            {
                xpSlider.value = 0;
                if(xpSliderText != null) xpSliderText.text = "N/A";
            }
        }

        private void PopulateEquippedSlots()
        {
            // Nettoyer les anciens items
            foreach (var item in _instantiatedEquippedItems) Destroy(item);
            _instantiatedEquippedItems.Clear();

            // Vérifier si le personnage a des items équipés
            if (PlayerDataManager.Instance.Data.EquippedItems.TryGetValue(_currentCharacter.CharacterID, out List<string> equippedIDs))
            {
                foreach (string equipmentID in equippedIDs)
                {
                    EquipmentData_SO itemData = Resources.Load<EquipmentData_SO>($"Data/Equipment/{equipmentID}");
                    if (itemData != null)
                    {
                        GameObject itemGO = Instantiate(equipmentItemPrefab, equipmentSlotsContainer);
                        itemGO.GetComponent<EquipmentItemUI>().Setup(itemData, OnUnequipItem);
                        _instantiatedEquippedItems.Add(itemGO);
                    }
                }
            }
            // TODO: Gérer l'affichage de slots vides si nécessaire.
        }

        private void PopulateInventoryGrid()
        {
            // Nettoyer l'ancien inventaire
            foreach (var item in _instantiatedInventoryItems) Destroy(item);
            _instantiatedInventoryItems.Clear();

            List<string> allEquippedIds = PlayerDataManager.Instance.Data.EquippedItems.Values.SelectMany(list => list).ToList();
            List<string> unlockedIds = PlayerDataManager.Instance.Data.UnlockedEquipmentIDs;

            // On ne montre que les items débloqués qui ne sont équipés par PERSONNE
            List<string> inventoryIds = unlockedIds.Except(allEquippedIds).ToList();

            foreach (string equipmentID in inventoryIds)
            {
                EquipmentData_SO itemData = Resources.Load<EquipmentData_SO>($"Data/Equipment/{equipmentID}");
                if (itemData != null)
                {
                    GameObject itemGO = Instantiate(equipmentItemPrefab, inventoryGridContainer);
                    itemGO.GetComponent<EquipmentItemUI>().Setup(itemData, OnEquipItem);
                    _instantiatedInventoryItems.Add(itemGO);
                }
            }
        }

        private void UpdateStatsDisplay()
        {
            UnitStats_SO baseStats = _currentCharacter.BaseStats;
            if (baseStats == null)
            {
                statsText.text = "Stats de base non définies.";
                return;
            }

            // Commencer avec les stats de base
            int characterLevel = 1;
            if (PlayerDataManager.Instance.Data.CharacterProgressData.TryGetValue(_currentCharacter.CharacterID, out CharacterProgress progress))
            {
                characterLevel = progress.CurrentLevel;
            }

            // 1. Obtenir les stats calculées pour le niveau actuel
            UnitStats_SO statsForLevel = _currentCharacter.ProgressionData.GetStatsForLevel(_currentCharacter.BaseStats, characterLevel);

            // 2. Commencer avec ces stats
            Dictionary<StatType, int> finalStats = new Dictionary<StatType, int>
            {
                { StatType.Health, statsForLevel.Health },
                { StatType.Attack, statsForLevel.Attack },
                { StatType.Defense, statsForLevel.Defense }
            };


            // Ajouter les bonus des items équipés
            if (PlayerDataManager.Instance.Data.EquippedItems.TryGetValue(_currentCharacter.CharacterID, out List<string> equippedIDs))
            {
                foreach (string id in equippedIDs)
                {
                    EquipmentData_SO itemData = Resources.Load<EquipmentData_SO>($"Data/Equipment/{id}");
                    if (itemData != null)
                    {
                        foreach (StatModifier mod in itemData.Modifiers)
                        {
                            if (finalStats.ContainsKey(mod.StatToModify))
                            {
                                finalStats[mod.StatToModify] += mod.Value;
                            }
                        }
                    }
                }
            }

            // Construire et afficher la chaîne de caractères
            statsText.text = $"HP: {finalStats[StatType.Health]}\n" +
                             $"Attaque: {finalStats[StatType.Attack]}\n" +
                             $"Défense: {finalStats[StatType.Defense]}";
        }

        private void OnEquipItem(EquipmentData_SO itemToEquip)
        {
            Debug.Log($"Tentative d'équipement: {itemToEquip.DisplayName} sur {_currentCharacter.DisplayName}");
            PlayerDataManager.Instance.EquipItemOnCharacter(_currentCharacter.CharacterID, itemToEquip.EquipmentID);
            RefreshAll();
        }

        private void OnUnequipItem(EquipmentData_SO itemToUnequip)
        {
            Debug.Log($"Tentative de déséquipement: {itemToUnequip.DisplayName} de {_currentCharacter.DisplayName}");
            PlayerDataManager.Instance.UnequipItemFromCharacter(_currentCharacter.CharacterID, itemToUnequip.EquipmentID);
            RefreshAll();
        }

        public void HidePanel()
        {
            gameObject.SetActive(false);
            if (teamManagementPanel != null)
            {
                teamManagementPanel.SetActive(true);
            }
        }
    }   
}