using UnityEngine;
using Unity.Cinemachine; // Ou Cinemachine
using System.Collections;

public class HubCameraManager : MonoBehaviour // Potentiellement un Singleton LOCAL à la scène Hub
{
    // --- Singleton Local (Optionnel mais pratique pour accès facile depuis HubManager) ---
    public static HubCameraManager Instance { get; private set; }
    // ------------------------------------------------------------------------------------

    [Header("Cinemachine Virtual Cameras")]
    [SerializeField] private CinemachineCamera vcamGeneralHubView;    // Vue d'ensemble de la clairière
    [SerializeField] private CinemachineCamera vcamLevelSelectionView; // Vue zoomée sur le "camp" de sélection de niveaux
    [SerializeField] private CinemachineCamera vcamTeamManagementView; // Vue zoomée sur le "camp" de gestion d'équipe
    // Ajoutez d'autres VCams pour d'autres camps/points d'intérêt

    [Header("Camera Targets (Transforms)")]
    [Tooltip("Cible pour la vue générale du Hub")]
    [SerializeField] private Transform targetGeneralHub;
    [Tooltip("Cible pour la vue de la sélection de niveaux")]
    [SerializeField] private Transform targetLevelSelection;
    [Tooltip("Cible pour la vue de la gestion d'équipe")]
    [SerializeField] private Transform targetTeamManagement;

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
            Destroy(gameObject); // Empêche les doublons si la scène est rechargée sans DontDestroyOnLoad
            return;
        }
    }

    void Start()
    {
        if (Camera.main != null) _cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
        if (_cinemachineBrain == null) Debug.LogError("[HubCameraManager] CinemachineBrain non trouvé sur la caméra principale !");

        if (vcamGeneralHubView == null || vcamLevelSelectionView == null || vcamTeamManagementView == null ||
            targetGeneralHub == null || targetLevelSelection == null || targetTeamManagement == null)
        {
            Debug.LogError("[HubCameraManager] Une ou plusieurs VCams ou Cibles ne sont pas assignées !");
            enabled = false;
            return;
        }

        // Configurer les cibles des VCams
        vcamGeneralHubView.Follow = targetGeneralHub;
        vcamGeneralHubView.LookAt = targetGeneralHub; // Ou une cible LookAt spécifique

        vcamLevelSelectionView.Follow = targetLevelSelection;
        vcamLevelSelectionView.LookAt = targetLevelSelection;

        vcamTeamManagementView.Follow = targetTeamManagement;
        vcamTeamManagementView.LookAt = targetTeamManagement;


        TransitionToGeneralHubView(true); // true pour une transition instantanée au démarrage
    }

    private IEnumerator SwitchCameraCoroutine(CinemachineCamera camToActivate, CinemachineCamera[] otherCamsToDeactivate, bool instant = false)
    {
        if (_isTransitioning && !instant) yield break; // Empêche les transitions multiples
        _isTransitioning = true;

        camToActivate.Priority = PRIORITY_ACTIVE;
        foreach (var cam in otherCamsToDeactivate)
        {
            if (cam != camToActivate) cam.Priority = PRIORITY_INACTIVE;
        }

        if (instant || _cinemachineBrain == null)
        {
            // Pour une transition instantanée ou si pas de brain, l'effet est immédiat
            if (_cinemachineBrain != null) _cinemachineBrain.ManualUpdate(); // Forcer la mise à jour si possible
        }
        else if (_cinemachineBrain.IsBlending)
        {
            yield return new WaitUntil(() => !_cinemachineBrain.IsBlending);
        } else {
            // Si pas de blend en cours mais un brain existe, une petite attente peut aider
            // si le blend par défaut est très court ou pour des raisons de timing.
            // Alternativement, la durée du blend par défaut du Brain devrait suffire.
            float blendTime = _cinemachineBrain.DefaultBlend.BlendTime;
            if (blendTime > 0) yield return new WaitForSeconds(blendTime);
            else yield return null; // Juste une frame si pas de blend time
        }

        Debug.Log($"[HubCameraManager] Transition vers {camToActivate.Name} complétée.");
        _isTransitioning = false;
    }

    // Méthodes publiques appelées par HubManager
    public void TransitionToGeneralHubView(bool instant = false)
    {
        StartCoroutine(SwitchCameraCoroutine(vcamGeneralHubView, new[] { vcamLevelSelectionView, vcamTeamManagementView }, instant));
    }
    public void TransitionToLevelSelectionView(bool instant = false)
    {
        StartCoroutine(SwitchCameraCoroutine(vcamLevelSelectionView, new[] { vcamGeneralHubView, vcamTeamManagementView }, instant));
    }
    public void TransitionToTeamManagementView(bool instant = false)
    {
        StartCoroutine(SwitchCameraCoroutine(vcamTeamManagementView, new[] { vcamGeneralHubView, vcamLevelSelectionView }, instant));
    }
}