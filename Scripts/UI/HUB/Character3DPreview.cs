namespace UI.HUB
{
   using UnityEngine;
using UnityEngine.EventSystems; // NÉCESSAIRE pour les interfaces de la souris

// On ajoute les interfaces IPointerDownHandler, etc.
public class Character3DPreview : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Références")]
    [SerializeField] private Transform characterSpawnPoint;
    [SerializeField] private string previewLayerName = "CharacterPreview";

    [Header("Animation")]
    [Tooltip("Vitesse de rotation automatique du modèle.")]
    [SerializeField] private float autoRotationSpeed = 20f;
    [Tooltip("Vitesse de rotation avec la souris.")]
    [SerializeField] private float mouseRotationSpeed = 10f; // Nouvelle variable

    private GameObject _currentCharacterInstance;
    private int _previewLayer;
    private bool _isDragging = false; // Pour savoir si on est en train de glisser

    void Awake()
    {
        _previewLayer = LayerMask.NameToLayer(previewLayerName);
        if (_previewLayer == -1)
        {
            Debug.LogError($"[Character3DPreview] Le layer '{previewLayerName}' n'existe pas.");
            enabled = false;
        }
    }

    void Update()
    {
        // Fait tourner le personnage sur lui-même SEULEMENT si on ne glisse pas la souris
        if (_currentCharacterInstance != null && !_isDragging)
        {
            _currentCharacterInstance.transform.Rotate(Vector3.up, autoRotationSpeed * Time.deltaTime);
        }
    }

    // --- NOUVELLES MÉTHODES DE GESTION DE LA SOURIS ---

    public void OnPointerDown(PointerEventData eventData)
    {
        // Le clic commence
        _isDragging = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Le clic est relâché
        _isDragging = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Pendant le glissement
        if (_isDragging && _currentCharacterInstance != null)
        {
            // On fait tourner le modèle en fonction du mouvement horizontal de la souris (eventData.delta.x)
            _currentCharacterInstance.transform.Rotate(Vector3.up, -eventData.delta.x * mouseRotationSpeed * Time.deltaTime, Space.World);
        }
    }

        public void ShowCharacter(GameObject characterPrefab)
    {
        ClearPreview();
        if (characterPrefab == null) return;
        _currentCharacterInstance = Instantiate(characterPrefab, characterSpawnPoint.position, characterSpawnPoint.rotation, characterSpawnPoint);
        SetLayerRecursively(_currentCharacterInstance, _previewLayer);
    }
    public void ClearPreview()
    {
        if (_currentCharacterInstance != null)
        {
            Destroy(_currentCharacterInstance);
            _currentCharacterInstance = null;
        }
    }
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
}