using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UI.HUB;
using System.Collections;
using ScriptableObjects;

public class CharacterSelectionUI : MonoBehaviour
{
    [Header("Panels & Conteneurs")]
    [Tooltip("Panel principal de gestion d'équipe (celui avec les 4 slots)")]
    [SerializeField] private GameObject teamManagementPanel;
    [Tooltip("Conteneur pour la liste des personnages disponibles (le ScrollRect)")]
    [SerializeField] private Transform availableCharactersContainer;
    [Tooltip("Le panel de droite affichant les détails")]
    [SerializeField] private GameObject detailPanel;

    [Header("Prefab de l'item de liste")]
    [SerializeField] private GameObject availableCharacterItemPrefab;

    [Header("Éléments du Panel de Détails")]
    [SerializeField] private TextMeshProUGUI detailCharacterNameText;
    [SerializeField] private TextMeshProUGUI detailCharacterDescriptionText;
       // Références vers les 4 textes dans la grille
    [SerializeField] private TextMeshProUGUI statHealthText;
    [SerializeField] private TextMeshProUGUI statAttackText;
    [SerializeField] private TextMeshProUGUI statDefenseText;
    [SerializeField] private TextMeshProUGUI statCostText;
    [Header("Boutons")]
    [SerializeField] private Button addToTeamButton;
    [SerializeField] private Button backButton;
    [Header("Prévisualisation 3D")]
    [SerializeField] private Character3DPreview characterPreview; // Référence à notre nouveau script
    private readonly List<AvailableCharacterListItemUI> _instantiatedListItems = new List<AvailableCharacterListItemUI>();

    // Références aux managers
    private TeamManager _teamManager;

    private CharacterData_SO _selectedCharacter;

    
    
    void Awake()
    {
        _teamManager = TeamManager.Instance;
        if (_teamManager == null) Debug.LogError("[CharacterSelectionUI] TeamManager non trouvé !");

        addToTeamButton?.onClick.AddListener(OnAddToTeamClicked);
        backButton?.onClick.AddListener(OnBackClicked);
    }

    private void OnEnable()
    {
        // Quand le panel est activé, on rafraîchit la liste
        PopulateAvailableCharactersList();
        // On désélectionne par défaut
        SelectCharacter(null);
    }

    private void PopulateAvailableCharactersList()
    {
        // Nettoyer l'ancienne liste
        foreach (var item in _instantiatedListItems)
        {
            if (item != null) Destroy(item.gameObject);
        }
        _instantiatedListItems.Clear();

        // Obtenir les personnages disponibles qui ne sont PAS déjà dans l'équipe
        var available = _teamManager.AvailableCharacters;
        var activeTeam = _teamManager.ActiveTeam;
        var charactersToShow = available.Except(activeTeam).ToList();

        // Instancier un item pour chaque personnage à afficher
        foreach (var character in charactersToShow)
        {
            if (character == null) continue;

            GameObject itemGO = Instantiate(availableCharacterItemPrefab, availableCharactersContainer);
            var itemUI = itemGO.GetComponent<AvailableCharacterListItemUI>();
            if (itemUI != null)
            {
                int characterLevel = 1;
                if (TeamManager.Instance != null &&
                    PlayerDataManager.Instance.Data.CharacterProgressData.ContainsKey(character.CharacterID))
                {
                    characterLevel = PlayerDataManager.Instance.Data.CharacterProgressData[character.CharacterID]
                        .CurrentLevel;
                }

                // Passe le niveau à l'item UI
                itemUI.Setup(character, SelectCharacter, characterLevel);

                _instantiatedListItems.Add(itemUI);
            }
        }   
    }

    // Appelée par un item de la liste quand il est cliqué
    private void SelectCharacter(CharacterData_SO characterData)
    {
        _selectedCharacter = characterData;
        UpdateDetailPanel();
        foreach (var itemUI in _instantiatedListItems)
        {
            if (itemUI != null)
            {
                // L'item se met en mode "sélectionné" si son personnage est celui qui est actuellement sélectionné.
                itemUI.SetSelected(itemUI.GetCharacterData() == _selectedCharacter);
            }
        }
        
        // Mettre à jour la prévisualisation 3D
        
        if (characterPreview != null)
        {
            if (_selectedCharacter != null)
            {
                // On utilise HubVisualPrefab comme convenu
                characterPreview.ShowCharacter(_selectedCharacter.HubVisualPrefab);
            }
            else
            {
                characterPreview.ClearPreview();
            }
        }
    }


    private void UpdateDetailPanel()
    {
        if (_selectedCharacter != null)
        {
            detailPanel.SetActive(true);
            addToTeamButton.interactable = true;

            if (detailCharacterNameText != null)
                detailCharacterNameText.text = _selectedCharacter.DisplayName;

            if (detailCharacterDescriptionText != null)
                detailCharacterDescriptionText.text = _selectedCharacter.Description;

            if (_selectedCharacter.BaseStats != null)
            {
                var stats = _selectedCharacter.BaseStats;

                // On remplit chaque champ de texte individuellement
                statHealthText.text = $"HP: {stats.Health}";
                statAttackText.text = $"ACK: {stats.Attack}";
                statDefenseText.text = $"Def: {stats.Defense}";
                statCostText.text = $"Cost: {stats.AttackDelay} or";

                // Le Grid Layout Group s'occupe de les placer !
            }
        }
        else
        {
            // Cacher les détails et désactiver le bouton d'ajout si rien n'est sélectionné
            detailPanel.SetActive(false);
            addToTeamButton.interactable = false;
        }
    }

    private void OnAddToTeamClicked()
    {
        if (_selectedCharacter != null)
        {
            bool added = _teamManager.TryAddCharacterToActiveTeam(_selectedCharacter);
            if (added)
            {
                // Revenir à l'écran de gestion d'équipe
                StartCoroutine(TransitionToPanel(teamManagementPanel));
            }
            else
            {
                // Optionnel : Afficher un message "Équipe pleine"
                Debug.Log("Impossible d'ajouter le personnage, l'équipe est probablement pleine.");
            }
        }
    }

    private void OnBackClicked()
    {
        StartCoroutine(TransitionToPanel(teamManagementPanel));
    }

    private void ReturnToTeamManagement()
    {
        gameObject.SetActive(false);
        if (teamManagementPanel != null)
        {
            teamManagementPanel.SetActive(true);
        }
    }
    private IEnumerator TransitionToPanel(GameObject panelToShow)
    {
        CanvasGroup currentPanelCanvasGroup = GetComponent<CanvasGroup>();
        if (currentPanelCanvasGroup == null) currentPanelCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        CanvasGroup nextPanelCanvasGroup = panelToShow.GetComponent<CanvasGroup>();
        if (nextPanelCanvasGroup == null) nextPanelCanvasGroup = panelToShow.AddComponent<CanvasGroup>();

        float duration = 0.25f;
        float elapsedTime = 0f;

        // ÉTAPE 1 : Préparer le nouveau panel. On l'active, mais on le rend transparent.
        panelToShow.SetActive(true);
        nextPanelCanvasGroup.alpha = 0;

        // ÉTAPE 2 : Animer les deux fondus en même temps dans une seule boucle.
        while (elapsedTime < duration)
        {
            // Le panel actuel devient de plus en plus transparent.
            currentPanelCanvasGroup.alpha = 1f - (elapsedTime / duration);
        
            // Le nouveau panel devient de plus en plus opaque.
            nextPanelCanvasGroup.alpha = elapsedTime / duration;

            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }

        // ÉTAPE 3 : S'assurer que les états finaux sont parfaits.
        currentPanelCanvasGroup.alpha = 0;
        nextPanelCanvasGroup.alpha = 1;

        // C'est SEULEMENT MAINTENANT, à la toute fin, qu'on désactive l'ancien panel.
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
