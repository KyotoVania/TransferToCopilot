using UnityEngine;

/// <summary>
/// Activateur conditionnel qui peut contrôler les Colliders et/ou l'état IsTargetable d'un bâtiment.
/// Remplace ConditionalColliderActivator avec plus de fonctionnalités.
/// </summary>
public enum ActivatorMode
{
    ColliderOnly,      // Contrôle uniquement le collider
    TargetableOnly,    // Contrôle uniquement l'état IsTargetable
    Both              // Contrôle les deux
}

public class ConditionalActivator : MonoBehaviour, IScenarioTriggerable
{
    [Header("Settings")]
    [Tooltip("Mode de fonctionnement de l'activateur")]
    [SerializeField] private ActivatorMode mode = ActivatorMode.ColliderOnly;

    [Tooltip("Le Collider à activer ou désactiver (requis si mode = ColliderOnly ou Both)")]
    [SerializeField] private Collider targetCollider;

    [Tooltip("Le Building dont on veut contrôler l'état IsTargetable (requis si mode = TargetableOnly ou Both)")]
    [SerializeField] private Building targetBuilding;

    [Tooltip("L'état dans lequel mettre la cible lorsque l'action est déclenchée")]
    [SerializeField] private bool shouldBeEnabled = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    private void Awake()
    {
        // Validation et auto-détection
        if (mode == ActivatorMode.ColliderOnly || mode == ActivatorMode.Both)
        {
            if (targetCollider == null)
            {
                targetCollider = GetComponent<Collider>();
                if (targetCollider == null && debugMode)
                {
                    Debug.LogWarning($"[ConditionalActivator] sur {gameObject.name}: Aucun Collider assigné et aucun trouvé sur cet objet.", this);
                }
            }
        }

        if (mode == ActivatorMode.TargetableOnly || mode == ActivatorMode.Both)
        {
            if (targetBuilding == null)
            {
                targetBuilding = GetComponent<Building>();
                if (targetBuilding == null && debugMode)
                {
                    Debug.LogWarning($"[ConditionalActivator] sur {gameObject.name}: Aucun Building assigné et aucun trouvé sur cet objet.", this);
                }
            }
        }
    }

    /// <summary>
    /// Exécute l'action de changement d'état selon le mode configuré.
    /// </summary>
    public void TriggerAction()
    {
        switch (mode)
        {
            case ActivatorMode.ColliderOnly:
                SetColliderState();
                break;

            case ActivatorMode.TargetableOnly:
                SetTargetableState();
                break;

            case ActivatorMode.Both:
                SetColliderState();
                SetTargetableState();
                break;
        }
    }

    private void SetColliderState()
    {
        if (targetCollider != null)
        {
            targetCollider.enabled = shouldBeEnabled;
            if (debugMode)
            {
                Debug.Log($"[ConditionalActivator] sur {gameObject.name}: Collider mis à l'état 'enabled = {shouldBeEnabled}'.", this);
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning($"[ConditionalActivator] sur {gameObject.name}: Impossible de modifier le Collider car targetCollider est null.", this);
        }
    }

    private void SetTargetableState()
    {
        if (targetBuilding != null)
        {
            targetBuilding.SetTargetable(shouldBeEnabled);
            if (debugMode)
            {
                Debug.Log($"[ConditionalActivator] sur {gameObject.name}: Building IsTargetable mis à l'état '{shouldBeEnabled}'.", this);
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning($"[ConditionalActivator] sur {gameObject.name}: Impossible de modifier IsTargetable car targetBuilding est null.", this);
        }
    }

    /// <summary>
    /// Méthode utilitaire pour inverser l'état actuel
    /// </summary>
    public void ToggleState()
    {
        shouldBeEnabled = !shouldBeEnabled;
        TriggerAction();
    }

    /// <summary>
    /// Méthode pour forcer un état spécifique via code
    /// </summary>
    public void SetState(bool enabled)
    {
        shouldBeEnabled = enabled;
        TriggerAction();
    }
}