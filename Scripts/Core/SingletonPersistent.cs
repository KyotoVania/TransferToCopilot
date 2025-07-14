using UnityEngine;

/// <summary>
/// Generic persistent singleton base class for global managers.
/// Ensures only one instance exists across scene changes and persists throughout the game lifecycle.
/// </summary>
/// <typeparam name="T">The type of the manager that inherits from this singleton.</typeparam>
public abstract class SingletonPersistent<T> : MonoBehaviour where T : Component
{
    /// <summary>
    /// Gets the singleton instance of the manager.
    /// </summary>
    public static T Instance { get; private set; }

    /// <summary>
    /// Initializes the singleton instance and ensures persistence across scenes.
    /// Override this method in derived classes but always call base.Awake() first.
    /// </summary>
    protected virtual void Awake()
    {
        if (Instance == null)
        {
            Instance = this as T;
            // Ensure the object is not destroyed on scene change
            // and that it's at the root for DontDestroyOnLoad to work correctly.
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"Instance de {typeof(T).Name} déjà existante. Destruction du duplicata.");
            Destroy(gameObject);
        }
    }
} 