using UnityEngine;

/// <summary>
/// Composant de feedback visuel pour les unités (notamment les boss).
/// Fonctionne de manière identique à BuildingSelectionFeedback.
/// </summary>
public class UnitSelectionFeedback : MonoBehaviour
{
    private OutlineState _currentState;
    public OutlineState CurrentState => _currentState;

    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;

    private Color _originalOutlineColor;
    private float _originalOutlineSize;

    private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineSizeID = Shader.PropertyToID("_OutlineSize");

    // Couleurs et valeurs pour les nouveaux états
    private static readonly Color HoverColor = Color.white;
    private static readonly Color SelectedColor = Color.red;
    private const float HighlightSize = 20f;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();
        _currentState = OutlineState.Default; // Initialiser l'état

        if (_renderer == null)
        {
            Debug.LogError("Aucun composant Renderer trouvé. L'outline ne fonctionnera pas.", this);
            enabled = false;
            return;
        }

        if (_renderer.sharedMaterial.HasProperty(OutlineColorID))
        {
            _originalOutlineColor = _renderer.sharedMaterial.GetColor(OutlineColorID);
            _originalOutlineSize = _renderer.sharedMaterial.GetFloat(OutlineSizeID);
        }
        else
        {
            Debug.LogWarning($"Le matériau sur {gameObject.name} n'a pas les propriétés d'outline.", this);
            _originalOutlineColor = Color.black;
            _originalOutlineSize = 0f;
        }
    }

    /// <summary>
    /// Méthode de contrôle unique pour changer l'état visuel de l'outline.
    /// C'est le seul point d'entrée pour modifier l'apparence depuis d'autres scripts.
    /// </summary>
    /// <param name="newState">Le nouvel état à appliquer.</param>
    public void SetOutlineState(OutlineState newState)
    {
        // Optimisation : ne rien faire si l'état demandé est déjà l'état actuel.
        if (newState == _currentState) return;

        _renderer.GetPropertyBlock(_propertyBlock);

        // Appliquer les bonnes propriétés en fonction de l'état demandé.
        switch (newState)
        {
            case OutlineState.Hover:
                _propertyBlock.SetColor(OutlineColorID, HoverColor);
                _propertyBlock.SetFloat(OutlineSizeID, HighlightSize);
                break;

            case OutlineState.Selected:
                _propertyBlock.SetColor(OutlineColorID, SelectedColor);
                _propertyBlock.SetFloat(OutlineSizeID, HighlightSize);
                break;

            case OutlineState.Default:
            default:
                _propertyBlock.SetColor(OutlineColorID, _originalOutlineColor);
                _propertyBlock.SetFloat(OutlineSizeID, _originalOutlineSize);
                break;
        }

        _renderer.SetPropertyBlock(_propertyBlock);
        _currentState = newState; // Mettre à jour l'état actuel
    }
}
