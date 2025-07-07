using UnityEngine;
using System;
using Game.Observers;

public class FeverManager : MonoBehaviour
{
    public static FeverManager Instance { get; private set; }

    [Header("Configuration")]
    [Tooltip("Nombre de combos par palier de Fever (ex: 10 = paliers à 10, 20, 30, etc.)")]
    [SerializeField]
    private int combosPerFeverLevel = 10;
    
    [Tooltip("Nombre maximum de paliers de Fever (0-based, donc 4 = 5 niveaux: 0,1,2,3,4)")]
    [SerializeField]
    private int maxFeverLevel = 4;
    public int MaxFeverLevel => maxFeverLevel; // Rendre le niveau max accessible publiquement

    [Header("État (lecture seule)")]
    [SerializeField]
    private int _currentFeverLevel = 0;
    public int CurrentFeverLevel => _currentFeverLevel;
    
    [SerializeField]
    private bool _isFeverActive = false;
    public bool IsFeverActive => _isFeverActive;

    [Header("Audio")]
    [Tooltip("Son à jouer quand le combo est brisé (optionnel)")]
    [SerializeField]
    private AK.Wwise.Event comboBrokenSound;

    // Événements
    public event Action<bool> OnFeverStateChanged;
    public event Action<int> OnFeverLevelChanged; // Nouveau : notifie le changement de niveau

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
        // S'abonner aux événements du ComboController
        if (ComboController.Instance != null)
        {
            ComboController.Instance.AddObserver(new FeverComboObserver(this));
        }
        else
        {
            Debug.LogError("[FeverManager] ComboController.Instance est introuvable ! Le Mode Fever ne fonctionnera pas.");
        }

        // S'abonner aux changements d'état musical pour réappliquer le Fever
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnMusicStateChanged += OnMusicStateChanged;
        }
    }

    private void OnDisable()
    {
        // Se désabonner des événements pour éviter les fuites mémoire
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnMusicStateChanged -= OnMusicStateChanged;
        }
    }

    private void OnMusicStateChanged(string newMusicState)
    {
        Debug.Log($"[FeverManager] Transition musicale détectée : {newMusicState}. Réapplication de l'intensité Fever.");
        
        // Réappliquer l'intensité Fever actuelle après la transition
        float rtpcValue = CalculateRTPCValue(_currentFeverLevel);
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SetFeverIntensity(rtpcValue);
        }
    }

    private void HandleComboUpdated(int newComboCount)
    {
        // Calculer le niveau de Fever basé sur le combo actuel
        int newFeverLevel = CalculateFeverLevel(newComboCount);
        
        // Si le niveau a changé, mettre à jour
        if (newFeverLevel != _currentFeverLevel)
        {
            UpdateFeverLevel(newFeverLevel);
        }
        
        // Gérer l'activation/désactivation du mode Fever
        bool shouldBeActive = newFeverLevel > 0;
        if (shouldBeActive != _isFeverActive)
        {
            ActivateFeverMode(shouldBeActive);
        }
        
        // on veut que tout les observateurs soient notifiés du changement de niveau
        
    }

    private void HandleComboBroken()
    {
        Debug.Log("[FeverManager] Combo brisé ! Réinitialisation du Mode Fever.");
        
        // Jouer le son de combo brisé si configuré
        if (comboBrokenSound != null && comboBrokenSound.IsValid())
        {
            comboBrokenSound.Post(gameObject);
        }
        
        // Réinitialiser le niveau et désactiver le mode Fever
        if (_currentFeverLevel > 0)
        {
            UpdateFeverLevel(0);
        }
        
        if (_isFeverActive)
        {
            ActivateFeverMode(false);
        }
    }

    private int CalculateFeverLevel(int comboCount)
    {
        if (combosPerFeverLevel <= 0) return 0;
        
        // Calculer le niveau basé sur le combo
        int level = comboCount / combosPerFeverLevel;
        
        // Limiter au niveau maximum
        return Mathf.Clamp(level, 0, maxFeverLevel);
    }

    private void UpdateFeverLevel(int newLevel)
    {
        if (_currentFeverLevel == newLevel) return;
        
        int previousLevel = _currentFeverLevel;
        _currentFeverLevel = newLevel;
        
        Debug.Log($"[FeverManager] Niveau de Fever : {previousLevel} → {_currentFeverLevel}");
        
        // Calculer l'intensité RTPC (0-100)
        float rtpcValue = CalculateRTPCValue(_currentFeverLevel);
        
        // Mettre à jour le RTPC dans Wwise
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SetFeverIntensity(rtpcValue);
        }
        
        // Notifier les observateurs du changement de niveau
        OnFeverLevelChanged?.Invoke(_currentFeverLevel);
        // Notifier l'état du mode Fever
        
    }

    private float CalculateRTPCValue(int feverLevel)
    {
        // Convertir le niveau (0-maxLevel) en valeur RTPC (0-100)
        if (maxFeverLevel <= 0) return 0f;
        
        float normalizedLevel = (float)feverLevel / maxFeverLevel;
        return normalizedLevel * 100f;
    }

    private void ActivateFeverMode(bool activate)
    {
        if (_isFeverActive == activate) return;

        _isFeverActive = activate;
        Debug.Log($"[FeverManager] Mode Fever {(activate ? "ACTIVÉ" : "DÉSACTIVÉ")} !");

        // Notifier tous les observateurs (unités, UI, etc.)
        OnFeverStateChanged?.Invoke(_isFeverActive);
    }

    // Méthode publique pour obtenir l'intensité actuelle (0-1)
    public float GetCurrentIntensity()
    {
        if (maxFeverLevel <= 0) return 0f;
        return (float)_currentFeverLevel / maxFeverLevel;
    }

    // Classe interne pour implémenter l'interface IComboObserver
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