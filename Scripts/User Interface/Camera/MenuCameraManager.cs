using UnityEngine;
using System.Collections;
using Unity.Cinemachine; // Ou Cinemachine s'il n'est pas dans le namespace Unity.


public class MenuCameraManager : MonoBehaviour, IMenuCameraManager
{
    [Header("Cinemachine Virtual Cameras")]
    [SerializeField] private CinemachineCamera vcamBannerView;
    [SerializeField] private CinemachineCamera vcamOptionsTableView;

    private bool isTransitioning = false;

    private const int PRIORITY_ACTIVE = 15;
    private const int PRIORITY_INACTIVE = 10;

    private CinemachineBrain _cinemachineBrain;

    private void Start()
    {
        // Assurer que les VCams sont assignées
        if (vcamBannerView == null || vcamOptionsTableView == null)
        {
            Debug.LogError("[MenuCameraManager] Une ou plusieurs caméras virtuelles ne sont pas assignées !");
            enabled = false; 
            return;
        }

        if (_cinemachineBrain == null)
        {
            if (Camera.main != null) _cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
            if (_cinemachineBrain == null) Debug.LogWarning("[MenuCameraManager] CinemachineBrain non trouvé. Les blends pourraient ne pas fonctionner comme prévu.");
        }


        // État initial : la vue bannière est active
        vcamBannerView.Priority = PRIORITY_ACTIVE;
        vcamOptionsTableView.Priority = PRIORITY_INACTIVE;
    }

    public void TransitionToOptions()
    {
        if (isTransitioning || vcamBannerView == null || vcamOptionsTableView == null) return;
        Debug.Log("[MenuCameraManager] Transitioning to Options View...");
        StartCoroutine(SwitchCameraCoroutine(vcamOptionsTableView, vcamBannerView));
    }

    public void TransitionToMainMenu()
    {
        if (isTransitioning || vcamBannerView == null || vcamOptionsTableView == null) return;
        Debug.Log("[MenuCameraManager] Transitioning to MainMenu View (Banner)...");
        StartCoroutine(SwitchCameraCoroutine(vcamBannerView, vcamOptionsTableView));
    }

    private IEnumerator SwitchCameraCoroutine(CinemachineCamera camToActivate, CinemachineCamera camToDeactivate)
    {
        isTransitioning = true;

        camToActivate.Priority = PRIORITY_ACTIVE;
        camToDeactivate.Priority = PRIORITY_INACTIVE;

        // Attendre que le blend de Cinemachine se termine, si un brain est disponible
        if (_cinemachineBrain != null && _cinemachineBrain.IsBlending)
        {
            // La durée d'attente ici dépend du temps de blend configuré sur le CinemachineBrain
            // ou sur une blend list spécifique.
            // On peut attendre la durée du blend + une petite marge.
            // Si customBlend est défini et utilisé par le brain pour cette transition:
            // float blendDuration = (_cinemachineBrain.DefaultBlend.BlendTime > 0) ? _cinemachineBrain.DefaultBlend.BlendTime : 1f; // Fallback
            // Pour une attente plus fiable, on attend que IsBlending soit faux.
            yield return new WaitUntil(() => !_cinemachineBrain.IsBlending);
            Debug.Log($"[MenuCameraManager] Cinemachine blend completed. Active VCam: {_cinemachineBrain.ActiveVirtualCamera?.Name}");
        }
        else if (_cinemachineBrain == null)
        {
            // Si pas de brain, la transition de priorité est instantanée, mais on peut ajouter un délai manuel pour l'effet
            yield return new WaitForSeconds(1f); // Durée arbitraire si pas de blend Brain
        }


        isTransitioning = false;
        Debug.Log($"[MenuCameraManager] Transition to {camToActivate.Name} complete.");
    }
}