using UnityEngine;
using UnityEngine.UI;

public class FeverDisplayUI : MonoBehaviour
{
    [Header("Références UI")]
    [Tooltip("Le layer à utiliser pour les VFX 3D affichés dans l'UI")]
    [SerializeField]
    private LayerMask uiVfxLayer;

    [Header("VFX de Fever")]
    [Tooltip("Prefabs de flammes par niveau d'intensité (index 0 = niveau 1, etc.)")]
    [SerializeField]
    private GameObject[] flameVfxPrefabs;

    [Tooltip("Position où instancier les VFX")]
    [SerializeField]
    private Transform vfxSpawnPoint;
    [Tooltip("La Render Texture cible qui doit être nettoyée.")]
    [SerializeField]
    private RenderTexture vfxRenderTexture; 
    // Reference to the current active VFX instance
    private GameObject _currentVfxInstance;
    [Header("Références UI")]
    [Tooltip("La caméra dédiée au rendu des VFX.")]
    [SerializeField]
    private Camera uiVfxCamera; 
    private void Start()
    {
        // Vérifier si les prefabs sont assignés
        if (flameVfxPrefabs == null || flameVfxPrefabs.Length == 0)
        {
            Debug.LogError("[FeverDisplayUI] Aucun prefab VFX n'est assigné!", this);
            enabled = false;
            return;
        }

        if (vfxSpawnPoint == null)
        {
            Debug.LogWarning("[FeverDisplayUI] Aucun point d'apparition VFX n'est défini. Utilisation de la position actuelle.", this);
            vfxSpawnPoint = transform;
        }

        // S'abonner aux événements du FeverManager
        if (FeverManager.Instance != null)
        {
            FeverManager.Instance.OnFeverStateChanged += HandleFeverStateChanged;
            FeverManager.Instance.OnFeverLevelChanged += HandleFeverLevelChanged;
        }
        else
        {
            Debug.LogError("[FeverDisplayUI] FeverManager.Instance est introuvable ! L'UI ne se mettra pas à jour.");
        }
    }

    private void OnDestroy()
    {
        // Se désabonner proprement
        if (FeverManager.Instance != null)
        {
            FeverManager.Instance.OnFeverStateChanged -= HandleFeverStateChanged;
            FeverManager.Instance.OnFeverLevelChanged -= HandleFeverLevelChanged;
        }
        
        // Détruire l'instance VFX si elle existe
        DestroyCurrentVfx();
    }

    private void HandleFeverStateChanged(bool isFeverActive)
    {
        if (!isFeverActive)
        {
            // Si le mode Fever est désactivé, détruire l'effet actuel
            DestroyCurrentVfx();
        }
        else if (_currentVfxInstance == null && FeverManager.Instance != null)
        {
            // Si le mode Fever vient d'être activé, créer le VFX approprié
            HandleFeverLevelChanged(FeverManager.Instance.CurrentFeverLevel);
        }
    }

    private void HandleFeverLevelChanged(int newFeverLevel)
    {
        // Si le Fever est désactivé (niveau 0), ne rien faire
        if (newFeverLevel <= 0)
        {
            DestroyCurrentVfx();
            return;
        }

        // Détruire l'instance actuelle s'il y en a une
        DestroyCurrentVfx();

        // Calculer l'index du prefab à utiliser (soustraire 1 car le niveau 1 correspond à l'index 0)
        int prefabIndex = Mathf.Clamp(newFeverLevel - 1, 0, flameVfxPrefabs.Length - 1);
        
        // Instancier le nouveau prefab VFX
        InstantiateVfx(prefabIndex);
    }
    
    private void InstantiateVfx(int prefabIndex)
    {
        if (prefabIndex < 0 || prefabIndex >= flameVfxPrefabs.Length || flameVfxPrefabs[prefabIndex] == null)
        {
            Debug.LogWarning($"[FeverDisplayUI] Prefab VFX non disponible pour l'index {prefabIndex}");
            return;
        }

        // Créer l'instance
        _currentVfxInstance = Instantiate(flameVfxPrefabs[prefabIndex], vfxSpawnPoint.position, vfxSpawnPoint.rotation);
        
        // Définir le parent
        _currentVfxInstance.transform.SetParent(vfxSpawnPoint);
        
        // Assigner le layer approprié pour le rendu par la caméra dédiée
        SetLayerRecursively(_currentVfxInstance, GetLayerFromMask(uiVfxLayer));
    }

    private void DestroyCurrentVfx()
    {
        if (_currentVfxInstance != null)
        {
            Destroy(_currentVfxInstance);
            _currentVfxInstance = null;
        }


        // On s'assure que la Render Texture est bien assignée avant de la nettoyer.
        if (vfxRenderTexture != null)
        {
            // 1. On dit au GPU que notre prochaine opération de rendu concerne CETTE texture.
            RenderTexture.active = vfxRenderTexture;
        
            // 2. On envoie la commande pour l'effacer (effacer la couleur et la profondeur) 
            //    avec une couleur totalement transparente.
            GL.Clear(true, true, Color.clear);
            // Note: GL.Clear efface la texture active, donc ici vfxRenderTexture.
            if (uiVfxCamera != null)
            {
                uiVfxCamera.Render();
            }
            Debug.Log("[FeverDisplayUI] RenderTexture nettoyée.");
            // 3. On relâche la texture pour que les opérations de rendu normales puissent reprendre.
            RenderTexture.active = null;
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        
        obj.layer = layer;
        
        foreach (Transform child in obj.transform)
        {
            if (child != null)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }

    private int GetLayerFromMask(LayerMask mask)
    {
        int layerNumber = 0;
        int layer = mask.value;
        while (layer > 0)
        {
            layer = layer >> 1;
            layerNumber++;
        }
        return layerNumber - 1;
    }
}