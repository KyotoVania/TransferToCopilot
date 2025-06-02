using UnityEngine;

/// <summary>
/// Singleton persistant générique pour les Managers Globaux.
/// </summary>
/// <typeparam name="T">Le type du manager.</typeparam>
public abstract class SingletonPersistent<T> : MonoBehaviour where T : Component
{
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {
        if (Instance == null)
        {
            Instance = this as T;
            // Assurer que l'objet n'est pas détruit au changement de scène
            // et qu'il est à la racine pour que DontDestroyOnLoad fonctionne correctement.
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