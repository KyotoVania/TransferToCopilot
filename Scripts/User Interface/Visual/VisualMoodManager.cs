using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using ScriptableObjects;

#if UNITY_EDITOR
using UnityEditor;
#endif

// L'interface IMoodManager reste la même, pas besoin de la modifier.
public interface IMoodManager
{
    void SetMood(MoodType moodType);
    void RegisterMoodElement(IMoodElement element);
    void UnregisterMoodElement(IMoodElement element);
    MoodType CurrentMood { get; }
}

// Le script hérite maintenant de MonoBehaviour et utilise [ExecuteAlways]
// pour fonctionner en éditeur et en mode jeu.
[ExecuteAlways]
public class VisualMoodManager : MonoBehaviour, IMoodManager
{
    // --- Champs de configuration de l'inspecteur ---
    [Header("Configuration")]
    [SerializeField] private MoodType currentMood = MoodType.Day;
    [SerializeField] private VisualMoodData[] availableMoods;

    [Header("Références de scène")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private Transform lightSpawnPoint;

    // --- Variables privées ---
    // Référence à l'objet lumière géré. Il est un enfant de ce manager.
    private GameObject _managedLightObject;
    private readonly List<IMoodElement> _moodElements = new List<IMoodElement>();
    private MoodLinkedObject[] _moodLinkedObjectsCache;

    // --- Propriété publique de l'interface ---
    public MoodType CurrentMood => currentMood;

    // =======================================================================
    // CYCLE DE VIE UNITY (SIMPLIFIÉ)
    // =======================================================================

    private void Awake()
    {
        // En mode jeu, cette méthode est simple.
        // Son rôle principal est de mettre en cache les références nécessaires.
        if (Application.isPlaying)
        {
            // Met en cache tous les objets qui doivent changer avec le mood.
            _moodLinkedObjectsCache = FindObjectsByType<MoodLinkedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Applique le "mood" initial qui a été configuré dans l'éditeur.
            // Note: La lumière existe DEJA car elle a été sauvegardée avec la scène.
            SetMood(currentMood);
        }
    }

    private void OnDestroy()
    {
        // Nettoyage en cas de destruction de cet objet (ex: changement de scène)
        // Ceci est surtout pour le nettoyage en éditeur.
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            CleanupManagedLight(true); // Utilise DestroyImmediate en éditeur
        }
        #endif
    }

    // =======================================================================
    // LOGIQUE SPÉCIFIQUE À L'ÉDITEUR
    // =======================================================================

    #if UNITY_EDITOR
    private void OnValidate()
    {
        // OnValidate est appelé lorsque qu'une valeur est changée dans l'inspecteur.
        // C'est notre point d'entrée unique pour modifier la scène en mode édition.
        // On vérifie qu'on n'est pas en train de jouer pour éviter les effets de bord.
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        // On utilise delayCall pour éviter des erreurs Unity courantes avec OnValidate.
        EditorApplication.delayCall += () =>
        {
            // Vérifier si l'objet n'a pas été détruit entre-temps
            if (this == null || this.gameObject == null) return;

            // Appliquer le mood sélectionné dans l'inspecteur
            ApplyMoodSettingsForEditor();
        };
    }

    // Le menu contextuel peut être utile pour forcer une mise à jour manuelle.
    [MenuItem("CONTEXT/VisualMoodManager/Refresh Mood in Editor")]
    private static void RefreshMoodFromContextMenu(MenuCommand command)
    {
        VisualMoodManager manager = (VisualMoodManager)command.context;
        if (manager != null)
        {
            Debug.Log($"[VisualMoodManager] Refreshing mood for '{manager.name}' via context menu.");
            manager.ApplyMoodSettingsForEditor();
        }
    }
    #endif

    // =======================================================================
    // GESTION DES MOODS (Logique principale)
    // =======================================================================

    /// <summary>
    /// La méthode publique principale pour changer le mood (appelable en runtime).
    /// </summary>
    public void SetMood(MoodType moodType)
    {
        VisualMoodData moodData = FindMoodData(moodType);
        if (moodData == null)
        {
            Debug.LogWarning($"[VisualMoodManager] Mood data for {moodType} not found.");
            return;
        }

        // --- Application des propriétés ---
        // Skybox
        if (moodData.skyboxMaterial != null) RenderSettings.skybox = moodData.skyboxMaterial;

        // Fog
        RenderSettings.fog = moodData.useFog;
        if (moodData.useFog)
        {
            RenderSettings.fogMode = moodData.fogMode;
            RenderSettings.fogColor = moodData.fogColor;
            RenderSettings.fogDensity = moodData.fogDensity;
        }

        // Post-Processing
        if (postProcessVolume != null && moodData.volumeProfile != null)
        {
            postProcessVolume.profile = moodData.volumeProfile;
        }

        // --- Mise à jour de la lumière et des objets liés ---
        // En mode jeu, on ne crée/détruit plus la lumière. On met juste à jour ses propriétés.
        if (Application.isPlaying)
        {
            UpdateManagedLightProperties(moodData);
        }

        // Mettre à jour les objets de la scène qui dépendent du mood
        UpdateMoodLinkedObjects(moodType);

        // Mettre à jour les éléments d'UI spécifiques
        NotifyMoodElementsActivation();

        // Mettre à jour l'état interne
        currentMood = moodType;
        Debug.Log($"[VisualMoodManager] Mood set to {moodType}");
    }

    /// <summary>
    /// Trouve le ScriptableObject correspondant à un MoodType.
    /// </summary>
    private VisualMoodData FindMoodData(MoodType type)
    {
        if (availableMoods == null) return null;
        return System.Array.Find(availableMoods, mood => mood.moodType == type);
    }

    // =======================================================================
    // GESTION DE LA LUMIÈRE (Séparée pour l'éditeur et le runtime)
    // =======================================================================

    #if UNITY_EDITOR
    /// <summary>
    /// Logique d'application complète pour l'éditeur, incluant la gestion de la lumière.
    /// </summary>
    private void ApplyMoodSettingsForEditor()
    {
        // On applique d'abord tous les settings non-lumière
        SetMood(currentMood);

        // Ensuite, on gère spécifiquement la lumière (création/destruction/màj)
        VisualMoodData moodData = FindMoodData(currentMood);
        RefreshManagedLight(moodData);

        // Forcer la vue scène à se redessiner pour voir les changements
        SceneView.RepaintAll();
    }
    #endif

    /// <summary>
    /// La méthode centrale qui gère la création/destruction/mise à jour de la lumière EN ÉDITEUR.
    /// </summary>
    private void RefreshManagedLight(VisualMoodData moodData)
    {
        // Trouver la lumière enfant gérée actuelle
        _managedLightObject = null; // Réinitialiser la référence
        foreach (Transform child in transform)
        {
            // On identifie la lumière par un nom spécifique pour être sûr
            if (child.name == "_ManagedDirectionalLight")
            {
                _managedLightObject = child.gameObject;
                break;
            }
        }

        // CAS 1: Le mood ne requiert pas de lumière, mais une existe -> on la supprime.
        if (moodData == null || moodData.directionalLightPrefab == null)
        {
            if (_managedLightObject != null)
            {
                CleanupManagedLight(true);
            }
            return;
        }

        // CAS 2: Une lumière existe, mais ce n'est pas la bonne (le prefab a changé) -> on la remplace.
        if (_managedLightObject != null && _managedLightObject.name != moodData.directionalLightPrefab.name + "_Managed")
        {
            CleanupManagedLight(true);
            // _managedLightObject est maintenant null, le code ci-dessous en créera une nouvelle.
        }

        // CAS 3: Aucune lumière n'existe, on la crée.
        if (_managedLightObject == null)
        {
            GameObject prefab = moodData.directionalLightPrefab;
            _managedLightObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
            _managedLightObject.name = "_ManagedDirectionalLight"; // Nom fixe et clair
            
            // On stocke le nom du prefab original sur l'instance pour pouvoir le vérifier plus tard
            var utility = _managedLightObject.AddComponent<EditorOnlyObjectIdentifier>();
            utility.originalPrefabName = prefab.name;

            Debug.Log($"[VisualMoodManager] Created new light for mood '{moodData.moodName}'.", this);
        }

        // CAS 4: La bonne lumière existe (ou vient d'être créée), on met à jour ses propriétés.
        UpdateManagedLightProperties(moodData);
    }

    /// <summary>
    /// Met simplement à jour la position et la rotation de la lumière gérée (runtime et éditeur).
    /// </summary>
    private void UpdateManagedLightProperties(VisualMoodData moodData)
    {
        // En runtime, on doit d'abord s'assurer d'avoir la référence
        if (Application.isPlaying && _managedLightObject == null)
        {
            _managedLightObject = null; // Réinitialiser la référence
            foreach (Transform child in transform)
            {
                if (child.name == "_ManagedDirectionalLight")
                {
                    _managedLightObject = child.gameObject;
                    break;
                }
            }
        }
        
        if (_managedLightObject != null && moodData != null)
        {
            Vector3 spawnPosition = lightSpawnPoint != null ? lightSpawnPoint.position : Vector3.zero;
            _managedLightObject.transform.position = spawnPosition + moodData.lightPosition;
            _managedLightObject.transform.rotation = Quaternion.Euler(moodData.lightRotation);
        }
    }

    /// <summary>
    /// Détruit proprement la lumière enfant gérée.
    /// </summary>
    private void CleanupManagedLight(bool immediate)
    {
        if (_managedLightObject == null) return;

        if (immediate)
        {
            DestroyImmediate(_managedLightObject);
        }
        else
        {
            Destroy(_managedLightObject);
        }
        _managedLightObject = null;
    }

    // =======================================================================
    // GESTION DES OBSERVEURS ET OBJETS LIÉS (Logique inchangée)
    // =======================================================================

    public void RegisterMoodElement(IMoodElement element)
    {
        if (!_moodElements.Contains(element)) _moodElements.Add(element);
    }

    public void UnregisterMoodElement(IMoodElement element)
    {
        _moodElements.Remove(element);
    }

    private void NotifyMoodElementsActivation()
    {
        foreach (var element in _moodElements) element?.OnMoodActivated();
    }

    private void UpdateMoodLinkedObjects(MoodType moodType)
    {
        // En éditeur, on doit rafraîchir le cache à chaque fois.
        if (!Application.isPlaying)
        {
             _moodLinkedObjectsCache = FindObjectsByType<MoodLinkedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        if (_moodLinkedObjectsCache == null) return;

        foreach (var linkedObject in _moodLinkedObjectsCache)
        {
            if (linkedObject != null)
            {
                linkedObject.UpdateVisibility(moodType);
            }
        }
    }
    
    #if UNITY_EDITOR
    // Composant utilitaire pour identifier le prefab d'origine d'un objet en éditeur
    private class EditorOnlyObjectIdentifier : MonoBehaviour
    {
        [HideInInspector]
        public string originalPrefabName;
    }
    #endif
}