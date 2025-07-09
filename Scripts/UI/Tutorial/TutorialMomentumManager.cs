using UnityEngine;
using System;


/// <summary>
/// Gère la détection de la dépense de momentum pour le système de tutoriel.
/// Il s'abonne au MomentumManager et déclenche un événement lorsque des charges sont dépensées.
/// </summary>
public class TutorialMomentumManager : MonoBehaviour
{
    #region Singleton

    // Implémentation du pattern Singleton pour un accès facile et unique dans toute la scène.
    public static TutorialMomentumManager Instance { get; private set; }

    private void Awake()
    {
        // Assure qu'il n'y a qu'une seule instance de ce manager.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    #endregion

    #region Événements Publics

    /// <summary>
    /// Événement déclenché chaque fois qu'au moins une charge de momentum est dépensée.
    /// Le système de tutoriel peut s'abonner à cet événement.
    /// </summary>
    public event Action OnMomentumSpent;

    #endregion

    #region Variables Privées

    // Stocke la valeur précédente des charges pour comparaison.
    private int previousCharges;

    #endregion

    #region Cycle de Vie Unity

    // L'abonnement aux événements se fait dans OnEnable pour plus de robustesse.
    private void OnEnable()
    {
        // On vérifie que le MomentumManager existe avant de s'abonner.
        if (MomentumManager.Instance != null)
        {
            MomentumManager.Instance.OnMomentumChanged += HandleMomentumChanged;
            // Initialise la valeur de `previousCharges` avec l'état actuel au moment de l'activation.
            previousCharges = MomentumManager.Instance.CurrentCharges;
        }
        else
        {
            Debug.LogError("[TutorialMomentumManager] MomentumManager.Instance non trouvé ! Ce script ne pourra pas fonctionner.", this);
        }
    }

    // Le désabonnement se fait dans OnDisable pour éviter les fuites de mémoire.
    private void OnDisable()
    {
        // On vérifie à nouveau que l'instance existe avant de se désabonner.
        if (MomentumManager.Instance != null)
        {
            MomentumManager.Instance.OnMomentumChanged -= HandleMomentumChanged;
        }
    }

    #endregion

    #region Logique de Détection

    /// <summary>
    /// Méthode appelée à chaque fois que l'événement OnMomentumChanged du MomentumManager est déclenché.
    /// </summary>
    /// <param name="newCharges">Le nombre actuel de charges de momentum.</param>
    /// <param name="newMomentumValue">La valeur brute actuelle du momentum (non utilisée ici, mais requise par la signature de l'événement).</param>
    private void HandleMomentumChanged(int newCharges, float newMomentumValue)
    {
        // Condition principale : on détecte si le nombre de charges a diminué.
        if (newCharges < previousCharges)
        {
            Debug.Log("Le joueur a dépensé du momentum ! Déclenchement de l'événement OnMomentumSpent.");
            // On déclenche notre propre événement pour que les autres systèmes (comme le TutorialManager) soient notifiés.
            OnMomentumSpent?.Invoke();
        }
        
        // Mise à jour de la valeur pour la prochaine comparaison, que le momentum ait été dépensé ou non.
        previousCharges = newCharges;
    }

    #endregion
}
