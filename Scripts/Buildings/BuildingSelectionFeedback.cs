using UnityEngine;

// Assurez-vous que ce script est sur le même GameObject que le Renderer que vous voulez modifier.
public class BuildingSelectionFeedback : MonoBehaviour
{
    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;
    private bool _isOutlineActive = false;

    // Variables pour stocker les valeurs d'origine de l'outline
    private Color _originalOutlineColor;
    private float _originalOutlineSize;

    // Références aux noms des propriétés du shader pour éviter les erreurs de frappe
    private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineSizeID = Shader.PropertyToID("_OutlineSize");

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();

        if (_renderer == null)
        {
            Debug.LogError("Aucun composant Renderer trouvé sur cet objet. L'outline ne fonctionnera pas.", this);
            enabled = false;
            return; // Important de sortir ici pour éviter d'autres erreurs
        }

        // --- CORRECTION MAJEURE ---
        // On lit et stocke les valeurs initiales ICI, depuis le matériau de base.
        // On utilise sharedMaterial pour ne pas créer d'instance inutilement.
        if (_renderer.sharedMaterial.HasProperty(OutlineColorID))
        {
            _originalOutlineColor = _renderer.sharedMaterial.GetColor(OutlineColorID);
            _originalOutlineSize = _renderer.sharedMaterial.GetFloat(OutlineSizeID);
        }
        else
        {
            // Si le matériau n'a même pas ces propriétés, on met des valeurs par défaut sûres.
            Debug.LogWarning($"Le matériau sur {gameObject.name} ne semble pas avoir les propriétés d'outline attendues.", this);
            _originalOutlineColor = Color.black;
            _originalOutlineSize = 0f;
        }
    }

    // Méthode pour activer l'outline de sélection
    public void ShowSelectionOutline()
    {
        if (_renderer == null || _isOutlineActive) return;

        // On n'a plus besoin de sauvegarder quoi que ce soit ici.
        // On récupère le bloc de propriétés pour le modifier.
        _renderer.GetPropertyBlock(_propertyBlock);

        // Définir les nouvelles valeurs pour la couleur et la largeur de sélection
        _propertyBlock.SetColor(OutlineColorID, Color.white);
        _propertyBlock.SetFloat(OutlineSizeID, 20f);

        // Appliquer le bloc de propriétés modifié au renderer
        _renderer.SetPropertyBlock(_propertyBlock);
        
        _isOutlineActive = true;
    }

    // Méthode pour désactiver et RESTAURER l'outline
    public void HideSelectionOutline()
    {
        if (_renderer == null || !_isOutlineActive) return;

        // On récupère le bloc de propriétés actuel pour être sûr de ne pas écraser d'autres changements.
        _renderer.GetPropertyBlock(_propertyBlock);
        
        // On restaure les valeurs sauvegardées au démarrage.
        _propertyBlock.SetColor(OutlineColorID, _originalOutlineColor);
        _propertyBlock.SetFloat(OutlineSizeID, _originalOutlineSize);

        // On applique le bloc de propriétés restauré
        _renderer.SetPropertyBlock(_propertyBlock);
        
        _isOutlineActive = false;
    }
}