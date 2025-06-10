using UnityEngine;
using System.Collections;
using ScriptableObjects;

public class BoardButtonInteraction : MonoBehaviour
{
    public enum BoardActionType
    {
        GoToLobby,
        GoToNextLevel
        // Tu pourrais ajouter d'autres actions comme RestartLevel, etc.
    }

    [Tooltip("Définit l'action que ce bouton déclenchera.")]
    public BoardActionType actionType;

    [Header("Configuration pour 'Prochain Niveau'")]
    [Tooltip("Assigner le LevelData_SO pour le prochain niveau (seulement si actionType est GoToNextLevel).")]
    public LevelData_SO nextLevelData; // Référence au SO du niveau suivant

    private void OnMouseDown() // Nécessite un Collider 3D sur ce GameObject
    {
        Debug.Log($"[BoardButtonInteraction] Clic sur le board : {gameObject.name}, Action : {actionType}", this);

        // Optionnel : Animation de clic sur le board lui-même (petit scale down/up)
        StartCoroutine(ClickFeedbackAnimation());

        // Réinitialiser Time.timeScale AVANT de charger une nouvelle scène
        Time.timeScale = 1f;

        if (GameManager.Instance == null)
        {
            Debug.LogError("[BoardButtonInteraction] GameManager.Instance non trouvé ! Impossible de changer de scène.", this);
            return;
        }

        switch (actionType)
        {
            case BoardActionType.GoToLobby:
                GameManager.Instance.LoadHub(); // Ton HubManager gère la scène du Hub
                break;
            case BoardActionType.GoToNextLevel:
                if (nextLevelData != null)
                {
                    GameManager.Instance.LoadLevel(nextLevelData); //
                }
                else
                {
                    Debug.LogWarning($"[BoardButtonInteraction] 'Next Level Data' non assigné pour {gameObject.name}. Retour au Hub par défaut.", this);
                    GameManager.Instance.LoadHub();
                }
                break;
        }
    }

    private IEnumerator ClickFeedbackAnimation()
    {
        Vector3 originalScale = transform.localScale;
        float clickScaleFactor = 0.9f;
        float animSpeed = 0.1f; // Rapide

        // Scale down
        float t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animSpeed;
            transform.localScale = Vector3.Lerp(originalScale, originalScale * clickScaleFactor, t);
            yield return null;
        }

        // Scale up
        t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animSpeed;
            transform.localScale = Vector3.Lerp(originalScale * clickScaleFactor, originalScale, t);
            yield return null;
        }
        transform.localScale = originalScale;
    }

    // Optionnel : Feedback visuel au survol
    // void OnMouseEnter() { /* ... code pour scale up ou changer de couleur ... */ }
    // void OnMouseExit() { /* ... code pour revenir à l'état normal ... */ }
}