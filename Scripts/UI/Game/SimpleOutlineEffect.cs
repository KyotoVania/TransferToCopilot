// Fichier: SimpleOutlineEffect.cs
using UnityEngine;
using System.Collections.Generic;

public class SimpleOutlineEffect : MonoBehaviour
{
    // Propriétés publiques contrôlées par BuildingOutlineFeedback
    [Tooltip("Couleur de l'outline.")]
    public Color OutlineColor = Color.yellow; // Laisser la couleur par défaut ici

    [Tooltip("Largeur de l'outline.")]
    public float OutlineWidth = 0.02f;

    [Header("Configuration Interne")]
    [Tooltip("Matériel à utiliser pour l'outline. Doit utiliser un shader d'outline (ex: Custom/UnlitOutlineShader).")]
    [SerializeField] private Material outlineMaterialSource;

    private List<GameObject> outlineHolderObjects = new List<GameObject>();
    private List<Material> instancedOutlineMaterials = new List<Material>();
    private bool hasBeenInitialized = false;

    void Awake()
    {
        // L'initialisation principale se fera au premier OnEnable ou via un appel explicite.
        // Debug.Log($"[{gameObject.name}/SimpleOutlineEffect] Awake(). Component enabled: {this.enabled}");
    }

    // Appelé quand le composant MonoBehaviour est activé
    void OnEnable()
    {
        // Debug.Log($"[{gameObject.name}/SimpleOutlineEffect] OnEnable() - hasBeenInitialized: {hasBeenInitialized}");
        if (!hasBeenInitialized)
        {
            InitializeAndCreateObjects();
        }
        SetOutlineVisibility(true); // Rend les renderers visibles
        ApplyMaterialProperties();  // Applique couleur/largeur
    }

    // Appelé quand le composant MonoBehaviour est désactivé
    void OnDisable()
    {
        // Debug.Log($"[{gameObject.name}/SimpleOutlineEffect] OnDisable()");
        SetOutlineVisibility(false); // Cache les renderers
    }

    void OnDestroy()
    {
        ClearOutlineData();
    }

    private void InitializeAndCreateObjects()
    {
        if (hasBeenInitialized) return;

        if (outlineMaterialSource == null)
        {
            Shader unlitOutlineShader = Shader.Find("Custom/UnlitOutlineShader");
            if (unlitOutlineShader != null)
            {
                outlineMaterialSource = new Material(unlitOutlineShader);
            }
            else
            {
                Debug.LogError($"[{gameObject.name}/SimpleOutlineEffect] Shader 'Custom/UnlitOutlineShader' non trouvé et 'outlineMaterialSource' non assigné. Outline désactivé.", this);
                // Il est crucial de ne pas activer ce composant s'il ne peut pas fonctionner.
                // Si BuildingOutlineFeedback l'ajoute dynamiquement, il faut gérer ce cas.
                // Pour l'instant, on suppose qu'il est sur le prefab.
                this.enabled = false;
                return;
            }
        }

        CreateOutlineObjectsInternal();
        hasBeenInitialized = true;
    }

    private void CreateOutlineObjectsInternal()
    {
        ClearOutlineData(); // Nettoyer au cas où

        Renderer[] mainRenderers = GetComponentsInChildren<Renderer>(true); // true pour inclure les enfants inactifs
        if (mainRenderers.Length == 0) return;

        foreach (Renderer mainRenderer in mainRenderers)
        {
            if (mainRenderer == null || mainRenderer is ParticleSystemRenderer || mainRenderer is TrailRenderer || mainRenderer is LineRenderer)
                continue;

            // Éviter de créer un outline pour un outline déjà existant ou pour soi-même si ce script est sur un objet avec un renderer principal.
            if (mainRenderer.gameObject.name.EndsWith("_OutlineVisual")) continue;


            MeshFilter meshFilter = mainRenderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;

            GameObject outlineHolder = new GameObject(mainRenderer.name + "_OutlineVisual");
            outlineHolder.transform.SetParent(mainRenderer.transform, false);
            outlineHolder.transform.localPosition = Vector3.zero;
            outlineHolder.transform.localRotation = Quaternion.identity;
            outlineHolder.transform.localScale = Vector3.one;

            MeshRenderer outlineRendererComponent = outlineHolder.AddComponent<MeshRenderer>();
            MeshFilter outlineMeshFilter = outlineHolder.AddComponent<MeshFilter>();
            outlineMeshFilter.sharedMesh = meshFilter.sharedMesh;

            Material instancedMaterial = new Material(outlineMaterialSource);
            outlineRendererComponent.material = instancedMaterial; // Utilise .material pour l'instance
            instancedOutlineMaterials.Add(instancedMaterial);

            outlineHolder.layer = LayerMask.NameToLayer("Ignore Raycast"); // Ou un layer dédié
            outlineHolderObjects.Add(outlineHolder);

            outlineRendererComponent.enabled = false; // Les RENDERERS commencent désactivés. Seront activés par SetOutlineVisibility.
        }
    }

    private void ClearOutlineData()
    {
        foreach (GameObject outlineObj in outlineHolderObjects)
        {
            if (outlineObj != null)
            {
                DestroyAppropriate(outlineObj);
            }
        }
        outlineHolderObjects.Clear();

        foreach (Material mat in instancedOutlineMaterials)
        {
            if (mat != null)
            {
                DestroyAppropriate(mat);
            }
        }
        instancedOutlineMaterials.Clear();
        // hasBeenInitialized reste tel quel, ou est remis à false si on veut forcer une réinitialisation complète.
    }

    private void DestroyAppropriate(Object objToDestroy)
    {
        if (Application.isPlaying)
        {
            Destroy(objToDestroy);
        }
        else
        {
            DestroyImmediate(objToDestroy);
        }
    }

    private void SetOutlineVisibility(bool visible)
    {
        // Debug.Log($"[{gameObject.name}/SimpleOutlineEffect] SetOutlineVisibility({visible}). Nombre d'objets d'outline: {outlineHolderObjects.Count}");
        foreach (GameObject outlineObj in outlineHolderObjects)
        {
            if (outlineObj != null)
            {
                Renderer r = outlineObj.GetComponent<Renderer>();
                if (r != null)
                {
                    r.enabled = visible;
                }
            }
        }
    }

    private void ApplyMaterialProperties()
    {
        // Debug.Log($"[{gameObject.name}/SimpleOutlineEffect] ApplyMaterialProperties(). Color: {OutlineColor}, Width: {OutlineWidth}. Matériaux instanciés: {instancedOutlineMaterials.Count}");
        foreach (Material mat in instancedOutlineMaterials)
        {
            if (mat != null)
            {
                mat.SetColor("_OutlineColor", OutlineColor);
                mat.SetFloat("_OutlineWidth", OutlineWidth);
            }
        }
    }

    // Méthode appelée par BuildingOutlineFeedback (via réflexion) pour forcer la mise à jour des propriétés
    public void ExternalUpdateProperties()
    {
        // Debug.Log($"[{gameObject.name}/SimpleOutlineEffect] ExternalUpdateProperties() appelée. this.enabled: {this.enabled}");
        if (!hasBeenInitialized) {
            InitializeAndCreateObjects(); // S'assurer que tout est prêt
        }

        // Si le composant est activé (ce qui signifie que l'outline DOIT être visible),
        // alors on applique les propriétés.
        if (this.enabled)
        {
            ApplyMaterialProperties();
            SetOutlineVisibility(true); // S'assurer aussi que les renderers sont visibles
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying || !gameObject.scene.IsValid()) return;

        // Utiliser delayCall pour éviter les problèmes d'exécution directe dans OnValidate
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || gameObject == null) return; // L'objet a peut-être été supprimé

            // Forcer la réinitialisation et la recréation si le matériel source a changé par exemple
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this.gameObject)) return; // Ne pas exécuter sur les assets de prefab eux-mêmes

            InitializeAndCreateObjects(); // Assure que les objets sont créés/mis à jour

            if (this.enabled) // Si le composant est coché "enabled" dans l'inspecteur
            {
                SetOutlineVisibility(true);
                ApplyMaterialProperties();
            }
            else // Si décoché
            {
                SetOutlineVisibility(false);
            }
        };
    }
#endif
}