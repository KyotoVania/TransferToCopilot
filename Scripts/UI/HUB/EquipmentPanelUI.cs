namespace Hub
{ 
    using UnityEngine;
    using UnityEngine.UI;
    using TMPro;
    using System.Collections.Generic;
    using System.Linq;
    using System.Collections;
    using ScriptableObjects;
    using UnityEngine.EventSystems;
    using UnityEngine.InputSystem;

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
        [Tooltip("Les 3 slots d'équipement fixes (Buttons déjà placés dans l'UI)")]
        [SerializeField] private List<Button> equipmentSlotButtons = new List<Button>();
        [Tooltip("Images des icônes pour chaque slot d'équipement")]
        [SerializeField] private List<Image> equipmentSlotIcons = new List<Image>();
        [Tooltip("Le parent de la grille d'items de l'inventaire.")]
        [SerializeField] private Transform inventoryGridContainer;
        [SerializeField] private GameObject equipmentItemPrefab;

        [Header("Navigation Zones")]
        [Tooltip("ScrollRect pour la zone d'inventaire (optionnel)")]
        [SerializeField] private ScrollRect inventoryScrollRect;
        [SerializeField] private float scrollSpeed = 5f;

        private CharacterData_SO _currentCharacter;
        private List<GameObject> _instantiatedInventoryItems = new List<GameObject>();
        private List<EquipmentData_SO> _currentEquippedItems = new List<EquipmentData_SO>(); // Track equipped items
        
        // === SYSTÈME DE NAVIGATION MANETTE ===
        private enum NavigationZone
        {
            EquippedSlots,    // Slots d'équipement fixes (gauche)
            InventoryItems,   // Zone de l'inventaire (droite)
            BackButton        // Bouton de retour
        }
        
        private NavigationZone _currentZone = NavigationZone.EquippedSlots;
        private GameObject _lastSelectedObject;
        private bool _isTransitioningToSubPanel = false;

        #region Cycle de Vie Unity

        private void Awake()
        {
            backButton?.onClick.AddListener(HidePanel);
            
            if (inventoryScrollRect == null && inventoryGridContainer != null)
            {
                inventoryScrollRect = inventoryGridContainer.GetComponentInParent<ScrollRect>();
            }
            
            // Configurer les boutons des slots d'équipement
            SetupEquipmentSlotButtons();
        }
        
        /// <summary>
        /// Configure les callbacks des boutons de slots d'équipement
        /// </summary>
        private void SetupEquipmentSlotButtons()
        {
            for (int i = 0; i < equipmentSlotButtons.Count; i++)
            {
                if (equipmentSlotButtons[i] != null)
                {
                    int slotIndex = i; // Capture pour closure
                    equipmentSlotButtons[i].onClick.RemoveAllListeners();
                    equipmentSlotButtons[i].onClick.AddListener(() => OnEquipmentSlotClicked(slotIndex));
                }
            }
        }

        private void OnEnable()
        {
            Debug.Log("[EquipmentPanelUI] Panel activé - Prise de contrôle");
            
            // 🎯 CORRECTION ALPHA + PRISE DE CONTRÔLE
            CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
            
            TakeControlFromHub();
            
            // Setup initial de la navigation
            StartCoroutine(SetupInitialSelection());
        }

        private void OnDisable()
        {
            Debug.Log("[EquipmentPanelUI] Panel désactivé");
            
            _lastSelectedObject = EventSystem.current.currentSelectedGameObject;
            
            if (characterPreview != null)
            {
                characterPreview.ClearPreview();
            }
            
            // ⚠️ On ne rend le contrôle au Hub QUE si on ne va pas vers un sous-panel
            if (!_isTransitioningToSubPanel)
            {
                ReturnControlToHub();
            }
        }

        private void Update()
        {
            // Gérer l'action Cancel
            if (InputManager.Instance != null && InputManager.Instance.UIActions.Cancel.WasPressedThisFrame())
            {
                HidePanel();
            }
            
            // Navigation entre les zones avec les triggers/bumpers
            HandleZoneNavigation();
            
            // S'assurer qu'on a toujours quelque chose de sélectionné
            EnsureSelection();
            
            // Gérer le scroll automatique dans l'inventaire
            HandleInventoryScrolling();
        }

        #endregion

        #region Contrôle Hub

        private void TakeControlFromHub()
        {
            if (HubManager.Instance != null)
            {
                HubManager.Instance.DisableHubControls();
                Debug.Log("[EquipmentPanelUI] ✅ Contrôles du Hub désactivés");
            }
        }

        private void ReturnControlToHub()
        {
            if (HubManager.Instance != null)
            {
                HubManager.Instance.EnableHubControls();
                Debug.Log("[EquipmentPanelUI] ✅ Contrôles du Hub réactivés");
            }
        }

        #endregion

        #region Navigation Manette

        /// <summary>
        /// Gère la navigation entre les zones avec RB/LB (ou équivalent clavier)
        /// </summary>
        private void HandleZoneNavigation()
        {
            if (InputManager.Instance == null) return;
            
            // Utiliser les inputs de cycle (comme pour les cibles dans le gameplay)
            // ou définir de nouveaux inputs pour changer de zone
            Vector2 navigationInput = InputManager.Instance.UIActions.Navigate.ReadValue<Vector2>();
            
            // Navigation horizontale entre zones avec Left/Right + modifier (ex: Shift)
            bool shiftHeld = Keyboard.current?.leftShiftKey.isPressed ?? false;
            
            if (shiftHeld && Mathf.Abs(navigationInput.x) > 0.7f)
            {
                if (navigationInput.x > 0) // Droite
                {
                    SwitchToNextZone();
                }
                else // Gauche
                {
                    SwitchToPreviousZone();
                }
            }
        }

        private void SwitchToNextZone()
        {
            NavigationZone newZone = _currentZone;
            
            switch (_currentZone)
            {
                case NavigationZone.EquippedSlots:
                    newZone = NavigationZone.InventoryItems;
                    break;
                case NavigationZone.InventoryItems:
                    newZone = NavigationZone.BackButton;
                    break;
                case NavigationZone.BackButton:
                    newZone = NavigationZone.EquippedSlots;
                    break;
            }
            
            SwitchToZone(newZone);
        }

        private void SwitchToPreviousZone()
        {
            NavigationZone newZone = _currentZone;
            
            switch (_currentZone)
            {
                case NavigationZone.EquippedSlots:
                    newZone = NavigationZone.BackButton;
                    break;
                case NavigationZone.InventoryItems:
                    newZone = NavigationZone.EquippedSlots;
                    break;
                case NavigationZone.BackButton:
                    newZone = NavigationZone.InventoryItems;
                    break;
            }
            
            SwitchToZone(newZone);
        }

        private void SwitchToZone(NavigationZone targetZone)
        {
            if (targetZone == _currentZone) return;
            
            _currentZone = targetZone;
            
            GameObject targetObject = null;
            
            switch (targetZone)
            {
                case NavigationZone.EquippedSlots:
                    if (equipmentSlotButtons.Count > 0 && equipmentSlotButtons[0] != null)
                    {
                        targetObject = equipmentSlotButtons[0].gameObject;
                    }
                    Debug.Log("[EquipmentPanelUI] 🎯 Zone : Slots d'Équipement");
                    break;
                    
                case NavigationZone.InventoryItems:
                    if (_instantiatedInventoryItems.Count > 0)
                    {
                        targetObject = _instantiatedInventoryItems[0];
                    }
                    Debug.Log("[EquipmentPanelUI] 🎯 Zone : Inventaire");
                    break;
                    
                case NavigationZone.BackButton:
                    targetObject = backButton?.gameObject;
                    Debug.Log("[EquipmentPanelUI] 🎯 Zone : Bouton Retour");
                    break;
            }
            
            if (targetObject != null && targetObject.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(targetObject);
            }
        }

        private void HandleInventoryScrolling()
        {
            if (inventoryScrollRect == null || _currentZone != NavigationZone.InventoryItems) return;
            
            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
            if (currentSelected == null) return;
            
            // Vérifier si l'objet sélectionné est dans l'inventaire
            if (!_instantiatedInventoryItems.Contains(currentSelected)) return;
            
            RectTransform selectedRect = currentSelected.GetComponent<RectTransform>();
            if (selectedRect == null) return;

            // Calculer la position pour le scroll automatique
            RectTransform contentRect = inventoryScrollRect.content;
            RectTransform viewportRect = inventoryScrollRect.viewport;
            
            if (contentRect == null || viewportRect == null) return;
            
            // Calculer la position relative de l'item sélectionné dans le contenu
            Vector3[] contentCorners = new Vector3[4];
            contentRect.GetWorldCorners(contentCorners);
            
            Vector3[] itemCorners = new Vector3[4];
            selectedRect.GetWorldCorners(itemCorners);
            
            Vector3[] viewportCorners = new Vector3[4];
            viewportRect.GetWorldCorners(viewportCorners);
            
            // Vérifier si l'item est visible dans le viewport
            float itemTop = itemCorners[1].y;
            float itemBottom = itemCorners[0].y;
            float viewportTop = viewportCorners[1].y;
            float viewportBottom = viewportCorners[0].y;
            
            // Si l'item n'est pas entièrement visible, ajuster le scroll
            if (itemTop > viewportTop || itemBottom < viewportBottom)
            {
                float contentHeight = contentCorners[1].y - contentCorners[0].y;
                float viewportHeight = viewportTop - viewportBottom;
                
                if (contentHeight > viewportHeight)
                {
                    float itemCenterY = (itemTop + itemBottom) / 2f;
                    float contentCenterY = (contentCorners[1].y + contentCorners[0].y) / 2f;
                    
                    float relativePosition = (itemCenterY - contentCorners[0].y) / contentHeight;
                    float targetScroll = Mathf.Clamp01(1f - relativePosition);
                    
                    inventoryScrollRect.verticalNormalizedPosition = Mathf.Lerp(
                        inventoryScrollRect.verticalNormalizedPosition, 
                        targetScroll, 
                        Time.deltaTime * scrollSpeed
                    );
                }
            }
        }

        #endregion

        #region Setup et Sélection Initiale

        private IEnumerator SetupInitialSelection()
        {
            yield return null; // Attendre que tout soit initialisé
            
            GameObject targetObject = null;
            
            // Priorité: dernier objet sélectionné → premier slot équipement → premier item inventaire → bouton retour
            if (_lastSelectedObject != null && _lastSelectedObject.activeInHierarchy)
            {
                targetObject = _lastSelectedObject;
                DetermineZoneFromObject(targetObject);
            }
            else if (equipmentSlotButtons.Count > 0 && equipmentSlotButtons[0] != null)
            {
                targetObject = equipmentSlotButtons[0].gameObject;
                _currentZone = NavigationZone.EquippedSlots;
            }
            else if (_instantiatedInventoryItems.Count > 0)
            {
                targetObject = _instantiatedInventoryItems[0];
                _currentZone = NavigationZone.InventoryItems;
            }
            else if (backButton != null)
            {
                targetObject = backButton.gameObject;
                _currentZone = NavigationZone.BackButton;
            }
            
            if (targetObject != null)
            {
                EventSystem.current.SetSelectedGameObject(targetObject);
                Debug.Log($"[EquipmentPanelUI] ✅ Sélection initiale : {targetObject.name} (Zone: {_currentZone})");
            }
        }

        private void DetermineZoneFromObject(GameObject obj)
        {
            // Vérifier si c'est un slot d'équipement
            bool isEquipmentSlot = equipmentSlotButtons.Contains(obj.GetComponent<Button>());
            
            if (isEquipmentSlot)
            {
                _currentZone = NavigationZone.EquippedSlots;
            }
            else if (_instantiatedInventoryItems.Contains(obj))
            {
                _currentZone = NavigationZone.InventoryItems;
            }
            else if (obj == backButton?.gameObject)
            {
                _currentZone = NavigationZone.BackButton;
            }
        }

        private void EnsureSelection()
        {
            if (EventSystem.current.currentSelectedGameObject == null)
            {
                Vector2 navigationInput = InputManager.Instance?.UIActions.Navigate.ReadValue<Vector2>() ?? Vector2.zero;
                bool submitPressed = InputManager.Instance?.UIActions.Submit.WasPressedThisFrame() ?? false;
                
                if (navigationInput != Vector2.zero || submitPressed)
                {
                    StartCoroutine(SetupInitialSelection());
                }
            }
        }

        #endregion

        #region Gestion des Items et Navigation

        private void ConfigureItemNavigation()
        {
            // Navigation dans la zone des slots d'équipement fixes
            ConfigureEquipmentSlotsNavigation();
            
            // Navigation dans la zone de l'inventaire (GRID LAYOUT)
            ConfigureInventoryGridNavigation();
            
            // Navigation du bouton retour
            ConfigureBackButtonNavigation();
        }

        /// <summary>
        /// Configure la navigation pour les 3 slots d'équipement fixes (navigation verticale)
        /// </summary>
        private void ConfigureEquipmentSlotsNavigation()
        {
            for (int i = 0; i < equipmentSlotButtons.Count; i++)
            {
                if (equipmentSlotButtons[i] == null) continue;
                
                Button currentButton = equipmentSlotButtons[i];
                Navigation nav = currentButton.navigation;
                nav.mode = Navigation.Mode.Explicit;
                
                // Navigation verticale dans les slots
                if (i > 0 && equipmentSlotButtons[i - 1] != null)
                {
                    nav.selectOnUp = equipmentSlotButtons[i - 1];
                }
                
                if (i < equipmentSlotButtons.Count - 1 && equipmentSlotButtons[i + 1] != null)
                {
                    nav.selectOnDown = equipmentSlotButtons[i + 1];
                }
                
                // Navigation horizontale vers l'inventaire
                if (_instantiatedInventoryItems.Count > 0)
                {
                    // Connecter au premier item de l'inventaire
                    Button rightButton = _instantiatedInventoryItems[0].GetComponent<Button>();
                    nav.selectOnRight = rightButton;
                }
                
                currentButton.navigation = nav;
            }
        }

        /// <summary>
        /// Configure la navigation pour la grid d'inventaire (navigation en grille)
        /// </summary>
        private void ConfigureInventoryGridNavigation()
        {
            if (_instantiatedInventoryItems.Count == 0) return;
            
            // Déterminer le nombre de colonnes de la grid
            GridLayoutGroup gridLayout = inventoryGridContainer.GetComponent<GridLayoutGroup>();
            int columnsCount = 2; // Valeur par défaut
            
            if (gridLayout != null)
            {
                // Calculer le nombre de colonnes basé sur la largeur
                RectTransform containerRect = inventoryGridContainer.GetComponent<RectTransform>();
                if (containerRect != null)
                {
                    float containerWidth = containerRect.rect.width;
                    float cellWidth = gridLayout.cellSize.x + gridLayout.spacing.x;
                    columnsCount = Mathf.Max(1, Mathf.FloorToInt(containerWidth / cellWidth));
                }
            }
            
            Debug.Log($"[EquipmentPanelUI] Configuration grid navigation - {columnsCount} colonnes, {_instantiatedInventoryItems.Count} items");
            
            for (int i = 0; i < _instantiatedInventoryItems.Count; i++)
            {
                Button currentButton = _instantiatedInventoryItems[i].GetComponent<Button>();
                if (currentButton == null) continue;
                
                Navigation nav = currentButton.navigation;
                nav.mode = Navigation.Mode.Explicit;
                
                // Calculer la position dans la grille
                int row = i / columnsCount;
                int col = i % columnsCount;
                
                // Navigation horizontale (gauche/droite)
                if (col > 0) // Pas la première colonne
                {
                    int leftIndex = i - 1;
                    if (leftIndex >= 0)
                    {
                        Button leftButton = _instantiatedInventoryItems[leftIndex].GetComponent<Button>();
                        nav.selectOnLeft = leftButton;
                    }
                }
                else // Première colonne - connecter aux slots d'équipement
                {
                    if (equipmentSlotButtons.Count > 0 && equipmentSlotButtons[0] != null)
                    {
                        nav.selectOnLeft = equipmentSlotButtons[0];
                    }
                }
                
                if (col < columnsCount - 1) // Pas la dernière colonne
                {
                    int rightIndex = i + 1;
                    if (rightIndex < _instantiatedInventoryItems.Count)
                    {
                        Button rightButton = _instantiatedInventoryItems[rightIndex].GetComponent<Button>();
                        nav.selectOnRight = rightButton;
                    }
                }
                
                // Navigation verticale (haut/bas)
                if (row > 0) // Pas la première ligne
                {
                    int upIndex = i - columnsCount;
                    if (upIndex >= 0)
                    {
                        Button upButton = _instantiatedInventoryItems[upIndex].GetComponent<Button>();
                        nav.selectOnUp = upButton;
                    }
                }
                
                if (row < (_instantiatedInventoryItems.Count - 1) / columnsCount) // Pas la dernière ligne
                {
                    int downIndex = i + columnsCount;
                    if (downIndex < _instantiatedInventoryItems.Count)
                    {
                        Button downButton = _instantiatedInventoryItems[downIndex].GetComponent<Button>();
                        nav.selectOnDown = downButton;
                    }
                }
                
                currentButton.navigation = nav;
            }
        }

        private void ConfigureBackButtonNavigation()
        {
            if (backButton == null) return;
            
            Navigation backNav = backButton.navigation;
            backNav.mode = Navigation.Mode.Explicit;
            
            // Connexions avec les autres zones
            if (equipmentSlotButtons.Count > 0 && equipmentSlotButtons[equipmentSlotButtons.Count - 1] != null)
            {
                backNav.selectOnLeft = equipmentSlotButtons[equipmentSlotButtons.Count - 1];
            }
            
            if (_instantiatedInventoryItems.Count > 0)
            {
                Button lastInventoryButton = _instantiatedInventoryItems[_instantiatedInventoryItems.Count - 1].GetComponent<Button>();
                if (lastInventoryButton != null)
                {
                    backNav.selectOnUp = lastInventoryButton;
                }
            }
            
            backButton.navigation = backNav;
        }

        #endregion

        #region Méthodes Principales (API Publique)

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

            // 2. Peupler les slots fixes et l'inventaire
            PopulateEquipmentSlots();
            PopulateInventoryGrid();
            
            // 3. Configurer la navigation
            ConfigureItemNavigation();
            
            UpdateStatsDisplay();
        }

        private void HidePanel()
        {
            Debug.Log("[EquipmentPanelUI] Retour au TeamManagement");
            
            if (teamManagementPanel != null)
            {
                // ✨ RETOUR SIMPLE comme CharacterSelectionUI
                teamManagementPanel.SetActive(true);
                gameObject.SetActive(false);
                
                // Le TeamManagementUI va automatiquement restaurer le focus
            }
            else
            {
                Debug.LogError("[EquipmentPanelUI] Référence teamManagementPanel manquante !");
            }
        }

        #endregion

        #region Logique Existante (Adaptée)

        private void UpdateLevelAndXPDisplay()
        {
            PlayerDataManager.Instance.Data.CharacterProgressData.TryGetValue(_currentCharacter.CharacterID, out CharacterProgress progress);
            if (progress == null) progress = new CharacterProgress();

            characterLevelText.text = $" {progress.CurrentLevel}";

            if (_currentCharacter.Stats != null)
            {
                int xpForNextLevel = _currentCharacter.Stats.GetXPRequiredForLevel(progress.CurrentLevel + 1);
                int xpForCurrentLevel = _currentCharacter.Stats.GetXPRequiredForLevel(progress.CurrentLevel);

                xpSlider.minValue = 0f;
                xpSlider.maxValue = 1f;

                if (xpForNextLevel <= xpForCurrentLevel) 
                {
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
            else
            {
                xpSlider.value = 0;
                if(xpSliderText != null) xpSliderText.text = "N/A";
            }
        }

        /// <summary>
        /// Peuple les 3 slots d'équipement fixes avec les items équipés du personnage
        /// </summary>
        private void PopulateEquipmentSlots()
        {
            // Récupérer les équipements du personnage
            List<string> equippedIDs = new List<string>();
            if (PlayerDataManager.Instance.Data.EquippedItems.TryGetValue(_currentCharacter.CharacterID, out List<string> playerEquippedIDs))
            {
                equippedIDs = playerEquippedIDs;
            }
            
            // Clear current equipped items list
            _currentEquippedItems.Clear();
            
            Debug.Log($"[EquipmentPanelUI] Personnage {_currentCharacter.DisplayName} a {equippedIDs.Count} items équipés");
            
            // Peupler chaque slot (on a 3 slots mais le personnage peut avoir moins d'items)
            for (int i = 0; i < equipmentSlotButtons.Count && i < equipmentSlotIcons.Count; i++)
            {
                if (equipmentSlotButtons[i] == null || equipmentSlotIcons[i] == null) continue;
                
                EquipmentData_SO itemData = null;
                
                // Si il y a un équipement pour ce slot
                if (i < equippedIDs.Count)
                {
                    string equipmentID = equippedIDs[i];
                    itemData = Resources.Load<EquipmentData_SO>($"Data/Equipment/{equipmentID}");
                }
                
                // Add to our tracking list (null if empty slot)
                _currentEquippedItems.Add(itemData);
                
                // Configurer l'affichage du slot
                UpdateEquipmentSlotDisplay(i, itemData);
                
                Debug.Log($"[EquipmentPanelUI] Slot {i} : {(itemData?.DisplayName ?? "Vide")}");
            }
        }
        
        /// <summary>
        /// Met à jour l'affichage d'un slot d'équipement
        /// </summary>
        private void UpdateEquipmentSlotDisplay(int slotIndex, EquipmentData_SO itemData)
        {
            if (slotIndex >= equipmentSlotIcons.Count) return;
            
            Image slotIcon = equipmentSlotIcons[slotIndex];
            if (slotIcon == null) return;
            
            if (itemData != null && itemData.Icon != null)
            {
                // Slot avec item
                slotIcon.sprite = itemData.Icon;
                slotIcon.enabled = true;
                slotIcon.color = Color.white;
            }
            else
            {
                // Slot vide - on peut mettre une icône de placeholder ou désactiver
                slotIcon.enabled = false;
                // Ou garder un placeholder: slotIcon.sprite = placeholderSprite; slotIcon.color = Color.gray;
            }
        }
        
        /// <summary>
        /// Appelé quand on clique sur un slot d'équipement
        /// </summary>
        private void OnEquipmentSlotClicked(int slotIndex)
        {
            if (slotIndex >= _currentEquippedItems.Count) return;
            
            EquipmentData_SO equippedItem = _currentEquippedItems[slotIndex];
            
            if (equippedItem != null)
            {
                Debug.Log($"[EquipmentPanelUI] Déséquipement de {equippedItem.DisplayName} du slot {slotIndex}");
                OnUnequipItem(equippedItem);
            }
            else
            {
                Debug.Log($"[EquipmentPanelUI] Slot vide {slotIndex} cliqué - aucune action");
                // Ici on pourrait ouvrir une sélection d'items ou autre
            }
        }

        private void PopulateInventoryGrid()
        {
            foreach (var item in _instantiatedInventoryItems) Destroy(item);
            _instantiatedInventoryItems.Clear();

            List<string> allEquippedIds = PlayerDataManager.Instance.Data.EquippedItems.Values.SelectMany(list => list).ToList();
            List<string> unlockedIds = PlayerDataManager.Instance.Data.UnlockedEquipmentIDs;

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

            int characterLevel = 1;
            if (PlayerDataManager.Instance.Data.CharacterProgressData.TryGetValue(_currentCharacter.CharacterID, out CharacterProgress progress))
            {
                characterLevel = progress.CurrentLevel;
            }

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

            RuntimeStats finalStats = StatsCalculator.GetFinalStats(_currentCharacter, characterLevel, equippedItems);
            var statSheet = _currentCharacter.Stats;

            statHealthText.text = $"HP: {finalStats.MaxHealth}";
            healthSlider.minValue = 0;
            healthSlider.maxValue = statSheet.HealthCurve.keys[statSheet.HealthCurve.length - 1].value;
            healthSlider.value = finalStats.MaxHealth;

            statAttackText.text = $"Attaque: {finalStats.Attack}";
            attackSlider.minValue = 0;
            attackSlider.maxValue = statSheet.AttackCurve.keys[statSheet.AttackCurve.length - 1].value;
            attackSlider.value = finalStats.Attack;

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

        #endregion
    }   
}