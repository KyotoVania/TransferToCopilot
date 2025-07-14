using UnityEngine;

/// <summary>
/// Defines the operating mode for the conditional activator.
/// </summary>
public enum ActivatorMode
{
    /// <summary>
    /// The activator will only control the enabled state of the Collider.
    /// </summary>
    ColliderOnly,

    /// <summary>
    /// The activator will only control the "targetable" state (IsTargetable) of the building.
    /// </summary>
    TargetableOnly,

    /// <summary>
    /// The activator will control both the Collider and the "targetable" state.
    /// </summary>
    Both
}

/// <summary>
/// Activates or deactivates components (Collider) or states (IsTargetable of a Building)
/// in response to a scenario event, implementing the IScenarioTriggerable interface.
/// </summary>
public class ConditionalColliderActivator : MonoBehaviour, IScenarioTriggerable
{
    [Header("Settings")]
    /// <summary>
    /// Determines which components or states this script should control.
    /// </summary>
    [Tooltip("Operating mode of the activator")]
    [SerializeField] private ActivatorMode mode = ActivatorMode.ColliderOnly;

    /// <summary>
    /// The Collider component to activate or deactivate. Required if the mode is `ColliderOnly` or `Both`.
    /// If not assigned, the script will try to find a Collider on the same GameObject.
    /// </summary>
    [Tooltip("The Collider to activate or deactivate (required if mode = ColliderOnly or Both)")]
    [SerializeField] private Collider targetCollider;

    /// <summary>
    /// The Building component whose `IsTargetable` state should be modified. Required if the mode is `TargetableOnly` or `Both`.
    /// If not assigned, the script will try to find a Building on the same GameObject.
    /// </summary>
    [Tooltip("The Building whose IsTargetable state is to be controlled (required if mode = TargetableOnly or Both)")]
    [SerializeField] private Building targetBuilding;

    /// <summary>
    /// The desired state for the target (Collider or Building) when the action is triggered.
    /// If `true`, the target will be enabled/targetable. If `false`, it will be disabled/non-targetable.
    /// </summary>
    [Tooltip("The state to set the target to when the action is triggered")]
    [SerializeField] private bool shouldBeEnabled = true;

    [Header("Debug")]
    /// <summary>
    /// Enables log messages for debugging.
    /// </summary>
    [SerializeField] private bool debugMode = false;

    /// <summary>
    /// Unity lifecycle method. Called on initialization.
    /// Validates references and tries to find them automatically if they are not assigned.
    /// </summary>
    private void Awake()
    {
        if (mode == ActivatorMode.ColliderOnly || mode == ActivatorMode.Both)
        {
            if (targetCollider == null)
            {
                targetCollider = GetComponent<Collider>();
                if (targetCollider == null && debugMode)
                {
                    Debug.LogWarning($"[ConditionalActivator] on {gameObject.name}: No Collider assigned and none found on this object.", this);
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
                    Debug.LogWarning($"[ConditionalActivator] on {gameObject.name}: No Building assigned and none found on this object.", this);
                }
            }
        }
    }

    /// <summary>
    /// Triggers the main action of the script based on the configured mode.
    /// This method is called by the LevelScenarioManager.
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

    /// <summary>
    /// Modifies the `enabled` state of the `targetCollider`.
    /// </summary>
    private void SetColliderState()
    {
        if (targetCollider != null)
        {
            targetCollider.enabled = shouldBeEnabled;
            if (debugMode)
            {
                Debug.Log($"[ConditionalActivator] on {gameObject.name}: Collider set to state 'enabled = {shouldBeEnabled}'.", this);
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning($"[ConditionalActivator] on {gameObject.name}: Cannot modify Collider because targetCollider is null.", this);
        }
    }

    /// <summary>
    /// Modifies the `IsTargetable` state of the `targetBuilding`.
    /// </summary>
    private void SetTargetableState()
    {
        if (targetBuilding != null)
        {
            targetBuilding.SetTargetable(shouldBeEnabled);
            if (debugMode)
            {
                Debug.Log($"[ConditionalActivator] on {gameObject.name}: Building IsTargetable set to state '{shouldBeEnabled}'.", this);
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning($"[ConditionalActivator] on {gameObject.name}: Cannot modify IsTargetable because targetBuilding is null.", this);
        }
    }

    /// <summary>
    /// Toggles the current state (`shouldBeEnabled`) and triggers the action.
    /// Useful for switches or reversible actions.
    /// </summary>
    public void ToggleState()
    {
        shouldBeEnabled = !shouldBeEnabled;
        TriggerAction();
    }

    /// <summary>
    /// Forces a specific state (enabled or disabled) and triggers the action.
    /// </summary>
    /// <param name="enabled">The state to apply. `true` to enable, `false` to disable.</param>
    public void SetState(bool enabled)
    {
        shouldBeEnabled = enabled;
        TriggerAction();
    }
}