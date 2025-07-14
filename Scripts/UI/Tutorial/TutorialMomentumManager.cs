using UnityEngine;
using System;
using System.Collections;


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
    // Flag pour savoir si l'abonnement a été fait
    private bool isSubscribed = false;

    #endregion

    #region Cycle de Vie Unity

    private void Start()
    {
        // Utilise une coroutine pour s'assurer que le MomentumManager est complètement initialisé
        StartCoroutine(WaitForMomentumManagerAndSubscribe());
    }

    private IEnumerator WaitForMomentumManagerAndSubscribe()
    {
        // Attend que le MomentumManager soit initialisé (après son Start())
        while (MomentumManager.Instance == null)
        {
            yield return null;
        }
        
        // Attend une frame supplémentaire pour s'assurer que le Start() du MomentumManager s'est exécuté
        yield return null;
        
        SubscribeToMomentumManager();
    }

    private void SubscribeToMomentumManager()
    {
        if (MomentumManager.Instance != null && !isSubscribed)
        {
            MomentumManager.Instance.OnMomentumChanged += HandleMomentumChanged;
            // Initialise la valeur de `previousCharges` avec l'état actuel au moment de l'abonnement.
            previousCharges = MomentumManager.Instance.CurrentCharges;
            isSubscribed = true;
            Debug.Log("[TutorialMomentumManager] Abonnement au MomentumManager réussi.");
        }
        else if (MomentumManager.Instance == null)
        {
            Debug.LogError("[TutorialMomentumManager] MomentumManager.Instance non trouvé ! Ce script ne pourra pas fonctionner.", this);
        }
    }

    // L'abonnement aux événements se fait maintenant dans Start() via la coroutine.
    private void OnEnable()
    {
        // Si on était déjà abonné et qu'on se réactive, on se réabonne
        if (isSubscribed && MomentumManager.Instance != null)
        {
            MomentumManager.Instance.OnMomentumChanged += HandleMomentumChanged;
            previousCharges = MomentumManager.Instance.CurrentCharges;
        }
    }

    // Le désabonnement se fait dans OnDisable pour éviter les fuites de mémoire.
    private void OnDisable()
    {
        // On vérifie à nouveau que l'instance existe avant de se désabonner.
        if (MomentumManager.Instance != null && isSubscribed)
        {
            MomentumManager.Instance.OnMomentumChanged -= HandleMomentumChanged;
        }
    }

    private void OnDestroy()
    {
        // Nettoyage final au cas où
        if (MomentumManager.Instance != null && isSubscribed)
        {
            MomentumManager.Instance.OnMomentumChanged -= HandleMomentumChanged;
        }
        isSubscribed = false;
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
