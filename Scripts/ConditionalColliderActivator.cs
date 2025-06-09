using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ConditionalColliderActivator : MonoBehaviour
{
    [Header("Configuration des Prérequis")]
    [Tooltip("Liste des bâtiments qui doivent être détruits ou capturés par le joueur pour activer le collider.")]
    public List<Building> prerequisiteBuildings = new List<Building>();

    [Header("Configuration du Collider")]
    [Tooltip("Si non assigné, le script essaiera de trouver le premier Collider sur cet objet.")]
    [SerializeField] private Collider colliderToToggle;

    private bool _conditionMet = false;
    private bool _isColliderInitiallyDisabled = false; // Pour s'assurer qu'on le désactive une seule fois au début
    private List<Building> _buildingsToMonitor = new List<Building>();

    void Awake()
    {
        // Essayer de trouver le collider si non assigné
        if (colliderToToggle == null)
        {
            colliderToToggle = GetComponent<Collider>();
        }

        if (colliderToToggle == null)
        {
            Debug.LogError($"[{gameObject.name}/ConditionalColliderActivator] Aucun Collider trouvé ou assigné. Le script ne pourra pas fonctionner.", this);
            enabled = false; // Désactiver le script s'il n'y a pas de collider à gérer
            return;
        }
    }

    void Start()
    {
        // Désactiver le collider au démarrage si ce n'est pas déjà fait
        if (colliderToToggle != null && colliderToToggle.enabled && !_isColliderInitiallyDisabled)
        {
            colliderToToggle.enabled = false;
            _isColliderInitiallyDisabled = true;
            Debug.Log($"[{gameObject.name}/ConditionalColliderActivator] Collider '{colliderToToggle.GetType().Name}' désactivé initialement.");
        }

        InitializeBuildingMonitoring();
        CheckAllConditionsMet(); // Vérifier si la condition est déjà remplie au démarrage
    }

    void InitializeBuildingMonitoring()
    {
        _buildingsToMonitor = prerequisiteBuildings.Where(b => b != null).ToList();

        if (_buildingsToMonitor.Count == 0 && prerequisiteBuildings.Count > 0)
        {
            Debug.LogWarning($"[{gameObject.name}/ConditionalColliderActivator] Tous les bâtiments dans prerequisiteBuildings sont null ou la liste est vide après filtrage.");
        }
        else if (prerequisiteBuildings.Count == 0)
        {
             Debug.LogWarning($"[{gameObject.name}/ConditionalColliderActivator] La liste prerequisiteBuildings est vide. La condition sera considérée comme remplie immédiatement.");
        }

        Building.OnBuildingDestroyed += HandleBuildingEvent;
        Building.OnBuildingTeamChangedGlobal += HandleBuildingTeamChangeEvent;
        Debug.Log($"[{gameObject.name}/ConditionalColliderActivator] Abonné aux événements des bâtiments. Surveillance de {_buildingsToMonitor.Count} bâtiments.");
    }

    void OnDestroy()
    {
        UnsubscribeFromBuildingEvents();
    }

    void UnsubscribeFromBuildingEvents()
    {
        Building.OnBuildingDestroyed -= HandleBuildingEvent;
        Building.OnBuildingTeamChangedGlobal -= HandleBuildingTeamChangeEvent;
    }

    private void HandleBuildingEvent(Building building)
    {
        if (!_conditionMet && _buildingsToMonitor.Contains(building))
        {
            CheckAllConditionsMet();
        }
    }

    private void HandleBuildingTeamChangeEvent(Building building, TeamType oldTeam, TeamType newTeam)
    {
        if (!_conditionMet && _buildingsToMonitor.Contains(building))
        {
            CheckAllConditionsMet();
        }
    }

    void CheckAllConditionsMet()
    {
        if (_conditionMet) // Si la condition est déjà remplie (et le collider activé), ne rien faire
        {
            return;
        }

        if (_buildingsToMonitor.Count == 0) // S'il n'y a aucun bâtiment à surveiller
        {
            Debug.Log($"[{gameObject.name}/ConditionalColliderActivator] Aucun bâtiment valide à surveiller. Condition considérée comme remplie pour l'activation du collider.");
            ActivateColliderAndFinalize();
            return;
        }

        bool allConditionsSatisfied = true;
        foreach (Building building in _buildingsToMonitor)
        {
            if (building == null) // Bâtiment détruit ou non assigné
            {
                continue;
            }

            bool currentBuildingConditionMet = false;
            if (building.CurrentHealth <= 0)
            {
                currentBuildingConditionMet = true;
            }
            else if (building is NeutralBuilding neutralBuilding)
            {
                if (neutralBuilding.Team == TeamType.Player) // Capturé par le joueur
                {
                    currentBuildingConditionMet = true;
                }
            }
            // Pour les EnemyBuilding, seule la destruction compte.
            // Pour les PlayerBuilding, ils ne devraient normalement pas être dans la liste des prérequis pour une activation positive.

            if (!currentBuildingConditionMet)
            {
                allConditionsSatisfied = false;
                break;
            }
        }

        if (allConditionsSatisfied)
        {
            Debug.Log($"[{gameObject.name}/ConditionalColliderActivator] Toutes les conditions des bâtiments prérequis sont remplies !");
            ActivateColliderAndFinalize();
        }
    }

    private void ActivateColliderAndFinalize()
    {
        if (colliderToToggle == null)
        {
            Debug.LogError($"[{gameObject.name}/ConditionalColliderActivator] Tentative d'activer un collider null !", this);
            return;
        }

        if (!_conditionMet) // Agir seulement si la condition n'a pas encore été remplie
        {
            if (!colliderToToggle.enabled)
            {
                colliderToToggle.enabled = true;
                Debug.Log($"[{gameObject.name}/ConditionalColliderActivator] Collider '{colliderToToggle.GetType().Name}' a été RÉACTIVÉ.");
            }
            _conditionMet = true;
            UnsubscribeFromBuildingEvents(); // Plus besoin de surveiller les bâtiments
            Debug.Log($"[{gameObject.name}/ConditionalColliderActivator] Condition remplie et collider activé. Désabonnement des événements.");
        }
    }

    [ContextMenu("Debug: Force Activate Collider")]
    public void Debug_ForceActivateCollider()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug_ForceActivateCollider ne peut être appelé qu'en mode Play.");
            return;
        }

        if (colliderToToggle == null)
        {
            Debug.LogError($"[{gameObject.name}/ConditionalColliderActivator] DEBUG: Collider non assigné/trouvé. Impossible de forcer l'activation.", this);
            return;
        }

        if (_conditionMet && colliderToToggle.enabled)
        {
             Debug.LogWarning($"[{gameObject.name}/ConditionalColliderActivator] DEBUG: Collider déjà activé et condition remplie.");
            return;
        }

        Debug.Log($"[{gameObject.name}/ConditionalColliderActivator] DEBUG: Forçage de l'activation du collider '{colliderToToggle.GetType().Name}' !");

        // Simuler que les conditions sont remplies et activer
        ActivateColliderAndFinalize();
    }
}