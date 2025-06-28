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

[ExecuteAlways]
public class VisualMoodManager : MonoBehaviour, IMoodManager
{
    [Header("Configuration")]
    [SerializeField] private MoodType currentMood = MoodType.Day;
    [SerializeField] private VisualMoodData[] availableMoods;

    [Header("Références de scène")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private Transform lightSpawnPoint;

    private GameObject _managedLightObject;
    private readonly List<IMoodElement> _moodElements = new List<IMoodElement>();
    private MoodLinkedObject[] _moodLinkedObjectsCache;

    public MoodType CurrentMood => currentMood;

    private void Awake()
    {
    }

    private void OnDestroy()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            CleanupManagedLight(true);
        }
        #endif
    }

    #if UNITY_EDITOR
    private void OnValidate()
    {
        // --- CORRECTION 2 : Empêcher l'exécution sur les prefabs dans le projet ---
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
        {
            return;
        }

        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (this == null || this.gameObject == null) return;
            ApplyMoodSettingsForEditor();
        };
    }

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

    public void SetMood(MoodType moodType)
    {
        VisualMoodData moodData = FindMoodData(moodType);
        if (moodData == null)
        {
            Debug.LogWarning($"[VisualMoodManager] Mood data for {moodType} not found.");
            return;
        }

        if (moodData.skyboxMaterial != null) RenderSettings.skybox = moodData.skyboxMaterial;

        RenderSettings.fog = moodData.useFog;
        if (moodData.useFog)
        {
            RenderSettings.fogMode = moodData.fogMode;
            RenderSettings.fogColor = moodData.fogColor;
            RenderSettings.fogDensity = moodData.fogDensity;
        }

        if (postProcessVolume != null && moodData.volumeProfile != null)
        {
            postProcessVolume.profile = moodData.volumeProfile;
        }

        if (Application.isPlaying)
        {
            UpdateManagedLightProperties(moodData);
        }

        UpdateMoodLinkedObjects(moodType);
        NotifyMoodElementsActivation();
        currentMood = moodType;
        Debug.Log($"[VisualMoodManager] Mood set to {moodType}");
    }

    private VisualMoodData FindMoodData(MoodType type)
    {
        if (availableMoods == null) return null;
        return System.Array.Find(availableMoods, mood => mood.moodType == type);
    }


    #if UNITY_EDITOR
    // --- CORRECTION 1 : Toute la logique de manipulation de prefabs est maintenant dans un bloc UNITY_EDITOR ---
    
    /// <summary>
    /// Logique d'application complète pour l'éditeur, incluant la gestion de la lumière.
    /// </summary>
    private void ApplyMoodSettingsForEditor()
    {
        SetMood(currentMood);
        VisualMoodData moodData = FindMoodData(currentMood);
        RefreshManagedLight(moodData);
        SceneView.RepaintAll();
    }

    /// <summary>
    /// La méthode centrale qui gère la création/destruction/mise à jour de la lumière EN ÉDITEUR.
    /// </summary>
    private void RefreshManagedLight(VisualMoodData moodData)
    {
        _managedLightObject = null;
        foreach (Transform child in transform)
        {
            if (child.name == "_ManagedDirectionalLight")
            {
                _managedLightObject = child.gameObject;
                break;
            }
        }

        if (moodData == null || moodData.directionalLightPrefab == null)
        {
            if (_managedLightObject != null)
            {
                CleanupManagedLight(true);
            }
            return;
        }
        
        // On vérifie le nom du prefab original stocké sur le composant pour voir si on doit changer.
        EditorOnlyObjectIdentifier id = _managedLightObject?.GetComponent<EditorOnlyObjectIdentifier>();
        if (_managedLightObject != null && (id == null || id.originalPrefabName != moodData.directionalLightPrefab.name))
        {
            CleanupManagedLight(true);
        }

        if (_managedLightObject == null)
        {
            GameObject prefab = moodData.directionalLightPrefab;
            _managedLightObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
            _managedLightObject.name = "_ManagedDirectionalLight";
            
            var utility = _managedLightObject.AddComponent<EditorOnlyObjectIdentifier>();
            utility.originalPrefabName = prefab.name;

            Debug.Log($"[VisualMoodManager] Created new light for mood '{moodData.moodName}'.", this);
        }

        UpdateManagedLightProperties(moodData);
    }
    #endif

    private void UpdateManagedLightProperties(VisualMoodData moodData)
    {
        if (Application.isPlaying && _managedLightObject == null)
        {
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

    private void CleanupManagedLight(bool immediate)
    {
        if (_managedLightObject == null) return;

        if (immediate)
        {
            #if UNITY_EDITOR
            // N'utiliser DestroyImmediate que dans l'éditeur
            if(!Application.isPlaying) DestroyImmediate(_managedLightObject);
            else Destroy(_managedLightObject);
            #else
            Destroy(_managedLightObject);
            #endif
        }
        else
        {
            Destroy(_managedLightObject);
        }
        _managedLightObject = null;
    }

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
    private class EditorOnlyObjectIdentifier : MonoBehaviour
    {
        [HideInInspector]
        public string originalPrefabName;
    }
    #endif
}