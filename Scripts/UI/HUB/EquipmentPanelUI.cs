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
            // Récupère la progression du joueur pour ce personnage
            PlayerDataManager.Instance.Data.CharacterProgressData.TryGetValue(_currentCharacter.CharacterID, out CharacterProgress progress);
            if (progress == null) progress = new CharacterProgress();

            characterLevelText.text = $" {progress.CurrentLevel}";

            // On utilise maintenant _currentCharacter.Stats qui est le nouveau StatSheet_SO
            if (_currentCharacter.Stats != null)
            {
                // L'appel à GetXPRequiredForLevel se fait via le StatSheet_SO
                int xpForNextLevel = _currentCharacter.Stats.GetXPRequiredForLevel(progress.CurrentLevel + 1);
                int xpForCurrentLevel = _currentCharacter.Stats.GetXPRequiredForLevel(progress.CurrentLevel);

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

                    xpSlider.value = (float)xpSinceLastLevel / Mathf.Max(1, xpNeededForThisLevel);
                    if(xpSliderText != null) xpSliderText.text = $"{xpSinceLastLevel} / {xpNeededForThisLevel} XP";
                }
            }
            else // Fallback
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
            if (_currentCharacter?.Stats == null)
            {
                statHealthText.text = "HP: --";
                healthSlider.value = 0;
                statAttackText.text = "Attaque: --";
                attackSlider.value = 0;
                statDefenseText.text = "Défense: --";
                defenseSlider.value = 0;
                Debug.LogWarning($"[EquipmentPanelUI] Le personnage '{_currentCharacter?.DisplayName}' n'a pas de StatSheet_SO assigné.");
                return;
            }

            // --- 1. Récupération des données nécessaires pour le calcul ---
            int characterLevel = 1;
            if (PlayerDataManager.Instance.Data.CharacterProgressData.TryGetValue(_currentCharacter.CharacterID, out CharacterProgress progress))
            {
                characterLevel = progress.CurrentLevel;
            }

            // Charger les ScriptableObjects des items équipés
            List<EquipmentData_SO> equippedItems = new List<EquipmentData_SO>();
            if (PlayerDataManager.Instance.Data.EquippedItems.TryGetValue(_currentCharacter.CharacterID, out List<string> equippedIDs))
            {
                foreach (string id in equippedIDs)
                {
                    EquipmentData_SO itemData = Resources.Load<EquipmentData_SO>($"Data/Equipment/{id}");
                    if (itemData != null)
                    {
                        equippedItems.Add(itemData);
                    }
                }
            }

            // --- 2. Appel au calculateur central ---
            // C'est ici que la nouvelle architecture prend tout son sens.
            // On demande au StatsCalculator de nous fournir les stats finales.
            RuntimeStats finalStats = StatsCalculator.GetFinalStats(_currentCharacter, characterLevel, equippedItems);
            // --- 3. Mise à Jour de l'UI avec les stats calculées ---
            var statSheet = _currentCharacter.Stats;

            // Santé
            statHealthText.text = $"HP: {finalStats.MaxHealth}";
            healthSlider.minValue = 0;
            // La valeur max pour le slider est la valeur maximale de la courbe de progression
            healthSlider.maxValue = statSheet.HealthCurve.keys[statSheet.HealthCurve.length - 1].value;
            healthSlider.value = finalStats.MaxHealth;

            // Attaque
            statAttackText.text = $"Attaque: {finalStats.Attack}";
            attackSlider.minValue = 0;
            attackSlider.maxValue = statSheet.AttackCurve.keys[statSheet.AttackCurve.length - 1].value;
            attackSlider.value = finalStats.Attack;

            // Défense
            statDefenseText.text = $"Défense: {finalStats.Defense}";
            defenseSlider.minValue = 0;
            defenseSlider.maxValue = statSheet.DefenseCurve.keys[statSheet.DefenseCurve.length - 1].value;
            defenseSlider.value = finalStats.Defense;
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