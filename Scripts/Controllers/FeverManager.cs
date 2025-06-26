using UnityEngine;
using System;
using Game.Observers;

public class FeverManager : MonoBehaviour
{
    public static FeverManager Instance { get; private set; }

    [Header("Configuration")]
    [Tooltip("Nombre de combos réussis nécessaires pour activer le Mode Fever.")]
    [SerializeField]
    private int feverThreshold = 10;

    [Header("État (lecture seule)")]
    [SerializeField]
    private bool _isFeverActive = false;
    public bool IsFeverActive => _isFeverActive;

    public event Action<bool> OnFeverStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        // On s'abonne aux événements du ComboController
        if (ComboController.Instance != null)
        {
            // Un combo réussi est simplement un appui correct sur une touche
            // On s'abonne à OnComboUpdated qui est appelé à chaque incrémentation
            ComboController.Instance.AddObserver(new FeverComboObserver(this));
        }
        else
        {
            Debug.LogError("[FeverManager] ComboController.Instance est introuvable ! Le Mode Fever ne fonctionnera pas.");
        }
    }

    private void OnDisable()
    {
        // Se désabonner proprement
        // La gestion est faite via l'observer pour la propreté.
    }

    private void HandleComboUpdated(int newComboCount)
    {
        if (!_isFeverActive && newComboCount >= feverThreshold)
        {
            ActivateFeverMode(true);
        }
    }

    private void HandleComboBroken()
    {
        if (_isFeverActive)
        {
            ActivateFeverMode(false);
        }
    }

    private void ActivateFeverMode(bool activate)
    {
        if (_isFeverActive == activate) return; // Pas de changement d'état

        _isFeverActive = activate;
        Debug.Log($"[FeverManager] Mode Fever {(activate ? "ACTIVÉ" : "DÉSACTIVÉ")} !");
        
        // Notifier tous les observateurs (unités, UI, etc.)
        OnFeverStateChanged?.Invoke(_isFeverActive);
    }

    // Classe interne pour implémenter l'interface IComboObserver proprement
    private class FeverComboObserver : IComboObserver
    {
        private readonly FeverManager _manager;

        public FeverComboObserver(FeverManager manager)
        {
            _manager = manager;
        }

        public void OnComboUpdated(int newCombo)
        {
            _manager.HandleComboUpdated(newCombo);
        }

        public void OnComboReset()
        {
            _manager.HandleComboBroken();
        }
    }
}