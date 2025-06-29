using UnityEngine;
using UnityEngine.InputSystem; 

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    // --- Actions ---
    // Référence à notre classe de contrôles générée automatiquement.
    private GameControls _playerControls;

    void Awake()
    {
        // --- Singleton Initialisation ---
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Pour le garder actif entre les scènes (ex: du MainMenu au niveau)

        // --- Initialisation des Contrôles ---
        _playerControls = new GameControls();
        Debug.Log("[InputManager] PlayerControls initialisés.");
    }

    private void OnEnable()
    {
        _playerControls.Enable(); // Active toutes les Action Maps
    }

    private void OnDisable()
    {
        _playerControls.Disable(); // Désactive tout
    }

    // --- Méthodes d'Accès Publiques ---
    // Les autres scripts n'accéderont pas directement à _playerControls.
    // Ils passeront par ces méthodes et propriétés, ce qui est plus propre.

    // Accès aux actions de Gameplay
    public GameControls.GameplayActions GameplayActions => _playerControls.Gameplay;

    // Accès aux actions de l'UI
    public GameControls.UIActions UIActions => _playerControls.UI;
    
    // Vous pouvez aussi exposer des valeurs spécifiques si nécessaire
    public Vector2 GetCameraMove()
    {
        // Ne lit la valeur que si l'action map Gameplay est activée
        if (_playerControls.Gameplay.enabled)
        {
            return _playerControls.Gameplay.CameraMove.ReadValue<Vector2>();
        }
        return Vector2.zero;
    }
    
    public Vector2 GetCameraPan()
    {
        if (_playerControls.Gameplay.enabled)
        {
           return _playerControls.Gameplay.CameraPan.ReadValue<Vector2>();
        }
        return Vector2.zero;
    }
    
}