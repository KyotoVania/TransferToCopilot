using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // Assurez-vous d'avoir le bon namespace si vous utilisez URP/HDRP
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public interface IMoodManager
{
    void SetMood(MoodType moodType);
    void RegisterMoodElement(IMoodElement element);
    void UnregisterMoodElement(IMoodElement element);
    MoodType CurrentMood { get; }
}

// Ajout de ExecuteAlways pour que Awake/OnDestroy fonctionnent aussi en Editor
[ExecuteAlways]
public class VisualMoodManager : MonoBehaviour, IMoodManager
{
    [Header("Configuration")]
    [SerializeField] private MoodType currentMood = MoodType.Day;
    [SerializeField] private VisualMoodData[] availableMoods;

    [Header("References")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private Transform lightSpawnPoint; // Optionnel, peut être null

    // Renommé pour plus de clarté
    private GameObject _instantiatedLightObject;
    private readonly List<IMoodElement> _moodElements = new List<IMoodElement>();
    private MoodLinkedObject[] _moodLinkedObjects; // Cache pour les objets liés

    public MoodType CurrentMood => currentMood;

    // --- Gestion du Cycle de Vie ---

    private void Awake()
    {
        // Ce Awake s'exécute en Editor ET en Play Mode grâce à [ExecuteAlways]

        // Si on entre en Play Mode, on veut détruire toute lumière potentielle
        // créée par OnValidate pendant l'édition.
        if (Application.isPlaying)
        {
            // Chercher une lumière enfant potentiellement laissée par l'éditeur
            CleanupPotentialEditorLight();
        }
    }

    private void Start()
    {
        // S'exécute seulement en Play Mode après Awake
        if (Application.isPlaying)
        {
            // Cache les objets liés une seule fois au démarrage
            // Correction de l'avertissement CS0618 (première occurrence)
            _moodLinkedObjects = FindObjectsByType<MoodLinkedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Appliquer le mood initial configuré dans l'inspecteur
            SetMood(currentMood);
        }
    }

    private void OnEnable()
    {
        #if UNITY_EDITOR
        // S'abonner aux changements d'état playMode pour nettoyer en sortie de jeu
        EditorApplication.playModeStateChanged += HandlePlayModeStateChange;
        #endif
    }

    private void OnDisable()
    {
        #if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChange;
        #endif

        // Nettoyage final si le composant est désactivé/détruit en Play Mode
        // (OnDestroy gère aussi, mais OnDisable est plus tôt si juste désactivé)
         if (Application.isPlaying)
         {
             CleanupInstantiatedLight(false); // false = utiliser Destroy
         }
    }

     private void OnDestroy()
     {
        // Ce OnDestroy s'exécute en Editor ET en Play Mode
         // Assure le nettoyage dans tous les cas de destruction de l'objet manager
         // Utilise DestroyImmediate SEULEMENT si on est en Editor ET pas en train de jouer
         bool useImmediate = !Application.isPlaying;
         CleanupInstantiatedLight(useImmediate);
     }

    #if UNITY_EDITOR
    // Gère le nettoyage quand on sort du Play Mode vers l'Editor Mode
    private void HandlePlayModeStateChange(PlayModeStateChange state)
    {
        // Si on sort du Play Mode (vers l'Editor)
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            // La lumière créée en jeu sera détruite par Unity.
            // On s'assure juste que notre référence est nulle.
            _instantiatedLightObject = null;
        }
        // Si on entre en mode Éditeur après avoir joué
         else if (state == PlayModeStateChange.EnteredEditMode)
         {
             // Forcer OnValidate à s'exécuter pour recréer la lumière de preview
             // si l'objet est sélectionné ou si la scène est modifiée.
             // Alternativement, on peut juste attendre que l'utilisateur change le mood dans l'inspecteur.
              // EditorApplication.delayCall += OnValidate; // Peut être un peu agressif
              // On s'assure que _instantiatedLightObject est null pour que OnValidate recrée
              _instantiatedLightObject = null;
              // Forcer une mise à jour de la scène pourrait être utile
               // SceneView.RepaintAll();
         }
         // Si on s'apprête à entrer en Play Mode depuis l'Editor
          else if (state == PlayModeStateChange.ExitingEditMode)
          {
              // Nettoyer la lumière de preview AVANT d'entrer en Play Mode
              CleanupInstantiatedLight(true); // true = DestroyImmediate
          }
    }

    // Fonction appelée quand les valeurs sont modifiées dans l'Inspecteur (Editor Mode UNIQUEMENT)
    private void OnValidate()
    {
        // IMPORTANT: Vérifier qu'on n'est pas en train de jouer ou de changer de mode
        // et que les références nécessaires sont présentes.
        if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode && availableMoods != null)
        {
            // Utiliser EditorApplication.delayCall pour éviter les erreurs pendant OnValidate
            // qui peuvent survenir lors de la modification de prefabs ou de l'instanciation/destruction.
            EditorApplication.delayCall += () =>
            {
                // Vérifier si l'objet existe toujours après le délai
                if (this == null || this.gameObject == null) return;

                // Cache les objets liés (pour le preview)
                // Correction de l'avertissement CS0618 (deuxième occurrence)
                _moodLinkedObjects = FindObjectsByType<MoodLinkedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);


                // Appliquer le mood sélectionné pour la preview
                var moodData = FindMoodData(currentMood);
                if (moodData != null)
                {
                    // Nettoyer l'ancienne lumière (Immediate pour l'éditeur)
                    CleanupInstantiatedLight(true);
                    // Appliquer les nouveaux paramètres (ce qui créera la nouvelle lumière)
                    ApplyMoodSettings(moodData);
                    // Mettre à jour les objets liés
                    UpdateMoodLinkedObjects(currentMood);
                    // Forcer la mise à jour de la vue Scène
                    SceneView.RepaintAll();
                }
                 else
                 {
                      // Si aucune data trouvée pour le mood, nettoyer la lumière existante
                      CleanupInstantiatedLight(true);
                 }
            };
        }
    }

    [MenuItem("GameObject/Refresh Mood Preview", priority = 20)]
    private static void RefreshMoodPreview()
    {
        // Utiliser FindFirstObjectByType qui est plus récent que FindObjectOfType
        var moodManager = FindFirstObjectByType<VisualMoodManager>();
        if (moodManager != null)
        {
            // Déclencher manuellement OnValidate pour rafraîchir
            moodManager.OnValidate();
        }
    }
    #endif

    // --- Gestion des Moods ---

    public void SetMood(MoodType moodType)
    {
        var moodData = FindMoodData(moodType);
        if (moodData == null)
        {
            Debug.LogWarning($"[VisualMoodManager] Mood data for {moodType} not found.");
            return;
        }

        // Déterminer si on doit utiliser DestroyImmediate (seulement en Editor hors PlayMode)
        bool useImmediate = !Application.isPlaying;

        // 1. Nettoyer l'ancienne lumière
        CleanupInstantiatedLight(useImmediate);

        // 2. Appliquer les nouveaux paramètres (crée la nouvelle lumière)
        ApplyMoodSettings(moodData);

        // 3. Mettre à jour les objets liés au mood
        UpdateMoodLinkedObjects(moodType);

        // 4. Notifier les éléments d'UI/VFX spécifiques au mood
        NotifyMoodElementsActivation(); // Renommé pour clarté

        // 5. Mettre à jour l'état interne
        currentMood = moodType;

        if (Debug.isDebugBuild) // Log seulement en build de dev ou éditeur
            Debug.Log($"[VisualMoodManager] Set mood to {moodType}");
    }

    private void ApplyMoodSettings(VisualMoodData moodData)
    {
        // Appliquer Skybox
        if (moodData.skyboxMaterial != null) RenderSettings.skybox = moodData.skyboxMaterial;
        // Appliquer Fog
        RenderSettings.fog = moodData.useFog;
        if (moodData.useFog)
        {
            RenderSettings.fogMode = moodData.fogMode;
            RenderSettings.fogColor = moodData.fogColor;
            RenderSettings.fogDensity = moodData.fogDensity;
        }
        // Appliquer Post-Processing
        ApplyPostProcessProfile(moodData);
        // Gérer la lumière (Création)
        HandleDirectionalLight(moodData);
    }

    // Fonction pour nettoyer la référence et détruire l'objet lumière
    private void CleanupInstantiatedLight(bool useImmediate)
    {
        if (_instantiatedLightObject != null)
        {
             // Notifier les éléments avant de détruire (si nécessaire, dépend de la logique de IMoodElement)
             // NotifyMoodElementsDeactivation();

            if (useImmediate)
            {
                 #if UNITY_EDITOR
                 // Vérification supplémentaire pour éviter de l'appeler en Play Mode par erreur
                 if (!Application.isPlaying)
                 {
                     DestroyImmediate(_instantiatedLightObject);
                 }
                 else
                 {
                      Destroy(_instantiatedLightObject); // Fallback sécurité
                 }
                 #else
                 Destroy(_instantiatedLightObject); // Hors éditeur, toujours Destroy
                 #endif
            }
            else
            {
                Destroy(_instantiatedLightObject);
            }
            _instantiatedLightObject = null;
        }
    }

     // Nettoie spécifiquement une lumière potentiellement orpheline de l'éditeur
     private void CleanupPotentialEditorLight()
     {
         // Cherche un enfant lumière qui aurait pu être laissé par OnValidate
         foreach (Transform child in transform)
         {
             // Vérifier par nom ou par composant Light
             if (child.name.StartsWith("DirectionalLight_") && child.GetComponent<Light>() != null)
             {
                 Debug.LogWarning("[VisualMoodManager] Found and destroying potential orphaned Editor light.");
                 Destroy(child.gameObject); // Utiliser Destroy normal au début du Play Mode
                  // Si on avait une référence (peu probable ici), la nullifier :
                  // if (child.gameObject == _instantiatedLightObject) _instantiatedLightObject = null;
             }
         }
         // S'assurer que la référence interne est nulle aussi au cas où
          _instantiatedLightObject = null;
     }

    private void HandleDirectionalLight(VisualMoodData moodData)
    {
        // Le nettoyage est fait AVANT dans SetMood

        if (moodData.directionalLightPrefab != null)
        {
            Vector3 spawnPosition = lightSpawnPoint != null
                ? lightSpawnPoint.position + moodData.lightPosition
                : moodData.lightPosition;

            _instantiatedLightObject = Instantiate(
                moodData.directionalLightPrefab,
                spawnPosition,
                Quaternion.Euler(moodData.lightRotation)
            );
            _instantiatedLightObject.name = $"DirectionalLight_{moodData.moodName}";

            // **Toujours parenter la lumière au manager** pour une meilleure gestion.
            // Si VisualMoodManager est DontDestroyOnLoad, la lumière le sera aussi.
            if (Application.isPlaying)
            {
                // true = worldPositionStays, garde la position/rotation absolue après le parentage.
                _instantiatedLightObject.transform.SetParent(transform, true);
            }
        }
        else
        {
            Debug.LogWarning($"[VisualMoodManager] No directional light prefab assigned for mood: {moodData.moodName}");
        }
    }

    private void ApplyPostProcessProfile(VisualMoodData moodData)
    {
        if (postProcessVolume == null) return; // Optionnel: chercher si null? Volume ppVolume = FindFirstObjectByType<Volume>();

        if (moodData.volumeProfile != null)
        {
            postProcessVolume.profile = moodData.volumeProfile;
        }
        else
        {
            Debug.LogWarning($"[VisualMoodManager] No volume profile assigned for mood: {moodData.moodName}");
            // Optionnel: Mettre un profil par défaut ou désactiver ?
            // postProcessVolume.profile = null;
        }
    }

    private VisualMoodData FindMoodData(MoodType type)
    {
        if (availableMoods == null) return null;
        return System.Array.Find(availableMoods, mood => mood.moodType == type);
    }

    // --- Gestion des Mood Elements & Linked Objects ---

    public void RegisterMoodElement(IMoodElement element)
    {
        if (!_moodElements.Contains(element)) _moodElements.Add(element);
    }

    public void UnregisterMoodElement(IMoodElement element)
    {
        _moodElements.Remove(element);
    }

    // Notifie les éléments *après* l'activation du nouveau mood
    private void NotifyMoodElementsActivation()
    {
        foreach (var element in _moodElements) element?.OnMoodActivated();
    }

    // Notifie les éléments *avant* la désactivation de l'ancien mood (si nécessaire)
    // Note: Actuellement non utilisé directement, SetMood gère la notification après activation.
    private void NotifyMoodElementsDeactivation()
    {
        foreach (var element in _moodElements) element?.OnMoodDeactivated();
    }

    private void UpdateMoodLinkedObjects(MoodType moodType)
    {
        // Le cache _moodLinkedObjects est rempli dans Start()
        if (_moodLinkedObjects == null) return;

        foreach (var linkedObject in _moodLinkedObjects)
        {
            if (linkedObject != null) // Vérifier si l'objet n'a pas été détruit entre temps
            {
                linkedObject.UpdateVisibility(moodType);
            }
        }
    }

    // Méthode publique pour rafraîchir la liste si des objets sont ajoutés/supprimés en jeu
    public void RefreshMoodLinkedObjects()
    {
        // Correction de l'avertissement CS0618 (troisième occurrence)
         _moodLinkedObjects = FindObjectsByType<MoodLinkedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // Mettre à jour immédiatement leur visibilité
        if (_moodLinkedObjects != null)
        {
             UpdateMoodLinkedObjects(currentMood);
        }
    }
}