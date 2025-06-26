using UnityEngine;
using UnityEngine.UI;

public class FeverDisplayUI : MonoBehaviour
{
    [Header("Références UI")]
    [Tooltip("L'élément visuel à activer/désactiver (ex: une image, un panel, un effet de particules).")]
    [SerializeField]
    private GameObject feverVisualEffect;

    private void Start()
    {
        // S'assurer que l'effet est désactivé au démarrage
        if (feverVisualEffect != null)
        {
            feverVisualEffect.SetActive(false);
        }
        else
        {
            Debug.LogError("[FeverDisplayUI] Aucun 'feverVisualEffect' n'est assigné !", this);
            enabled = false;
            return;
        }

        // S'abonner à l'événement du FeverManager
        if (FeverManager.Instance != null)
        {
            FeverManager.Instance.OnFeverStateChanged += HandleFeverStateChanged;
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
        }
    }

    private void HandleFeverStateChanged(bool isFeverActive)
    {
        if (feverVisualEffect != null)
        {
            feverVisualEffect.SetActive(isFeverActive);
        }
    }
}