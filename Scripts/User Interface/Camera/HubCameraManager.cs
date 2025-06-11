using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;

public enum HubViewpoint
{
    General,
    LevelSelection,
    TeamManagement
}

public class HubCameraManager : MonoBehaviour
{
    public static HubCameraManager Instance { get; private set; }
    
    [System.Serializable]
    public struct ViewpointCameraMapping
    {
        public HubViewpoint viewpoint;
        public CinemachineCamera virtualCamera;
    }

    [Header("Cinemachine Virtual Cameras")]
    [Tooltip("Liez chaque point de vue à sa caméra virtuelle correspondante.")]
    [SerializeField] private List<ViewpointCameraMapping> viewpointCameras;

    private Dictionary<HubViewpoint, CinemachineCamera> _cameraDictionary = new Dictionary<HubViewpoint, CinemachineCamera>();
    private CinemachineBrain _cinemachineBrain;
    private bool _isTransitioning = false;

    private const int PRIORITY_ACTIVE = 15;
    private const int PRIORITY_INACTIVE = 10;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Remplir le dictionnaire pour un accès rapide
        foreach (var mapping in viewpointCameras)
        {
            if (mapping.virtualCamera != null)
            {
                _cameraDictionary[mapping.viewpoint] = mapping.virtualCamera;
            }
        }
    }

    void Start()
    {
        if (Camera.main != null) _cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
        if (_cinemachineBrain == null) Debug.LogError("[HubCameraManager] CinemachineBrain non trouvé sur la caméra principale !");

        // Assurer que toutes les caméras ont une cible dans l'éditeur
        foreach (var mapping in viewpointCameras)
        {
            if (mapping.virtualCamera.Follow == null || mapping.virtualCamera.LookAt == null)
            {
                Debug.LogWarning($"[HubCameraManager] La VCam pour '{mapping.viewpoint}' n'a pas de cible Follow/LookAt assignée dans l'inspecteur.");
            }
        }
        
        // État initial
        TransitionTo(HubViewpoint.General, true);
    }

    public IEnumerator TransitionTo(HubViewpoint view, bool instant = false)
    {
        if (_isTransitioning && !instant) yield break;
        if (!_cameraDictionary.ContainsKey(view))
        {
            Debug.LogError($"[HubCameraManager] Aucune VCam n'est configurée pour le point de vue '{view}'.");
            yield break;
        }

        _isTransitioning = true;
        
        CinemachineCamera camToActivate = _cameraDictionary[view];

        // Mettre toutes les caméras en priorité basse
        foreach (var cam in _cameraDictionary.Values)
        {
            cam.Priority = PRIORITY_INACTIVE;
        }
        
        // Activer la bonne caméra
        camToActivate.Priority = PRIORITY_ACTIVE;
        
        // Attendre la fin de la transition
        if (!instant && _cinemachineBrain != null)
        {
            // Attendre une frame pour que le brain détecte le changement de priorité
            yield return null; 
            // Attendre que le blending soit terminé
            yield return new WaitUntil(() => !_cinemachineBrain.IsBlending);
        }
        
        _isTransitioning = false;
    }
}