using UnityEngine;

/// <summary>
/// Un composant qui implémente IScenarioTriggerable pour activer ou désactiver un Collider.
/// Parfait pour être utilisé avec l'action TriggerGameObject du LevelScenarioManager.
/// </summary>
public class ConditionalColliderActivator : MonoBehaviour, IScenarioTriggerable
{
    [Header("Settings")]
    [Tooltip("Le Collider à activer ou désactiver.")]
    [SerializeField] private Collider targetCollider;

    [Tooltip("L'état dans lequel le Collider doit être mis lorsque l'action est déclenchée.")]
    [SerializeField] private bool shouldBeEnabled = true;

    private void Awake()
    {
        if (targetCollider == null)
        {
            // Essayez de trouver le collider sur le même GameObject si non assigné.
            targetCollider = GetComponent<Collider>();
            if (targetCollider == null)
            {
                Debug.LogError($"[ConditionalColliderActivator] sur {gameObject.name}: Aucun Collider n'est assigné et aucun n'a été trouvé sur cet objet.", this);
            }
        }
    }

    /// <summary>
    /// Exécute l'action de changement d'état du collider.
    /// </summary>
    public void TriggerAction()
    {
        if (targetCollider != null)
        {
            targetCollider.enabled = shouldBeEnabled;
            Debug.Log($"[ConditionalColliderActivator] sur {gameObject.name}: Collider mis à l'état 'enabled = {shouldBeEnabled}'.", this);
        }
        else
        {
            Debug.LogWarning($"[ConditionalColliderActivator] sur {gameObject.name}: Impossible d'exécuter l'action car targetCollider est null.", this);
        }
    }
}