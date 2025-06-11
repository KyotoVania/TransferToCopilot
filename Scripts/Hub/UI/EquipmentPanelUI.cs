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
        [Header("Character Stats Display")]
        [SerializeField] private TextMeshProUGUI statHealthText;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private TextMeshProUGUI statAttackText;
        [SerializeField] private Slider attackSlider;
        [SerializeField] private TextMeshProUGUI statDefenseText;
        [SerializeField] private Slider defenseSlider;
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
                int xpForNextLevel = _currentCharacter.ProgressionData.GetXPRequiredForLevel(progress.CurrentLevel + 1);
                int xpForCurrentLevel = _currentCharacter.ProgressionData.GetXPRequiredForLevel(progress.CurrentLevel);

                // Forcer les bornes du slider pour un affichage normalisé (0 à 1)
                xpSlider.minValue = 0f;
                xpSlider.maxValue = 1f;

                if (xpForNextLevel <= xpForCurrentLevel) 
                {
                    // Niveau maximum atteint
                    xpSlider.value = 1f;
                    if(xpSliderText != null) xpSliderText.text = "MAX";
                }
                else
                {
                    int xpSinceLastLevel = progress.CurrentXP - xpForCurrentLevel;
                    int xpNeededForThisLevel = xpForNextLevel - xpForCurrentLevel;

                    // Assigner la valeur normalisée au slider
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
            // --- Vérification initiale ---
            if (_currentCharacter?.ProgressionData == null)
            {
                statHealthText.text = "HP: --";
                healthSlider.value = 0;
                statAttackText.text = "Attaque: --";
                attackSlider.value = 0;
                statDefenseText.text = "Défense: --";
                defenseSlider.value = 0;
                Debug.LogWarning($"[EquipmentPanelUI] Le personnage '{_currentCharacter?.DisplayName}' n'a pas de ProgressionData assigné.");
                return;
            }

            // --- Calcul des Stats Finales (avec équipement) ---
            int characterLevel = 1;
            if (PlayerDataManager.Instance.Data.CharacterProgressData.TryGetValue(_currentCharacter.CharacterID, out CharacterProgress progress))
            {
                characterLevel = progress.CurrentLevel;
            }

            UnitStats_SO statsForLevel = _currentCharacter.ProgressionData.GetStatsForLevel(_currentCharacter.BaseStats, characterLevel);

            Dictionary<StatType, int> finalStats = new Dictionary<StatType, int>
            {
                { StatType.Health, statsForLevel.Health },
                { StatType.Attack, statsForLevel.Attack },
                { StatType.Defense, statsForLevel.Defense }
            };

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

            // --- Mise à Jour de l'UI avec Valeurs Max Dynamiques ---
            var progressionData = _currentCharacter.ProgressionData;

            // Santé
            int health = finalStats[StatType.Health];
            statHealthText.text = $"HP: {health}";
            healthSlider.minValue = 0;
            // La stat max est la valeur de la dernière clé de la courbe d'animation
            healthSlider.maxValue = progressionData.HealthCurve.keys[progressionData.HealthCurve.length - 1].value;
            healthSlider.value = health;

            // Attaque
            int attack = finalStats[StatType.Attack];
            statAttackText.text = $"Attaque: {attack}";
            attackSlider.minValue = 0;
            attackSlider.maxValue = progressionData.AttackCurve.keys[progressionData.AttackCurve.length - 1].value;
            attackSlider.value = attack;

            // Défense
            int defense = finalStats[StatType.Defense];
            statDefenseText.text = $"Défense: {defense}";
            defenseSlider.minValue = 0;
            defenseSlider.maxValue = progressionData.DefenseCurve.keys[progressionData.DefenseCurve.length - 1].value;
            defenseSlider.value = defense;
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

        private void HidePanel()
        {
            if (characterPreview != null)
            {
                characterPreview.ClearPreview();
            }
    
            if (teamManagementPanel != null)
            {
                teamManagementPanel.SetActive(true);
            }
    
            gameObject.SetActive(false);
        }
        private void OnDisable()
        {
            if (characterPreview != null)
            {
                characterPreview.ClearPreview();
            }
        }
    }   
}