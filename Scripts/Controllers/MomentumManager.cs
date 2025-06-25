using System;
using Gameplay;
using UnityEngine;

/// <summary>
/// Gère la ressource de Momentum du joueur.
/// Le Momentum est gagné par des actions réussies et se dégrade avec le temps.
/// Il est divisé en charges qui peuvent être dépensées pour des actions puissantes.
/// </summary>
public class MomentumManager : SingletonPersistent<MomentumManager>
{
    // --- CONSTANTES ---
    private const float MAX_MOMENTUM = 3.0f;
    private const int DECAY_THRESHOLD_BEATS = 24; // Nombre de beats d'inactivité avant que la dégradation ne commence.
    private const float DECAY_AMOUNT_PER_BEAT = 0.05f; // Vitesse de la dégradation.

    // --- ÉVÉNEMENTS ---
    public event Action<int, float> OnMomentumChanged; // Notifie l'UI. int: charges, float: valeur brute.

    // --- PROPRIÉTÉS PUBLIQUES ---
    public int CurrentCharges { get; private set; }
    public float CurrentMomentumValue => _currentMomentum;

    // --- ÉTAT INTERNE ---
    private float _currentMomentum;
    private int _lastBeatCountWithoutGain;
    private MusicManager _musicManager;
    private AllyUnitRegistry _allyUnitRegistry;

    private bool _momentumGainFlag = false;

    protected override void Awake()
    {
        base.Awake();
        // Initialisation de l'état
        _currentMomentum = 0f;
        CurrentCharges = 0;
        _lastBeatCountWithoutGain = 0;
        _momentumGainFlag = false;
    }

    private void Start()
    {
        // Récupération de la référence au MusicManager, source de vérité du rythme.
        _musicManager = MusicManager.Instance;
        if (_musicManager == null)
        {
            Debug.LogError("MusicManager not found! MomentumManager requires it to function.");
            enabled = false;
            return;
        }
        
        // Abonnement à l'événement de battement.
        _musicManager.OnBeat += HandleBeat;
        _allyUnitRegistry = FindFirstObjectByType<AllyUnitRegistry>(); // Alternative: passer par un manager central si existant
        if (_allyUnitRegistry != null)
        {
            _allyUnitRegistry.OnDefensiveKillConfirmed += HandleDefensiveKill;
        }
    }

    private void OnDestroy()
    {
        // Se désabonner pour éviter les fuites de mémoire.
        if (_musicManager != null)
        {
            _musicManager.OnBeat -= HandleBeat;
        }
        if (_allyUnitRegistry != null)
        {
            _allyUnitRegistry.OnDefensiveKillConfirmed -= HandleDefensiveKill;
        }
    }
    private void HandleDefensiveKill(AllyUnit defensiveKiller)
    {
        if (defensiveKiller.MomentumGainOnObjectiveComplete > 0)
        {
            AddMomentum(defensiveKiller.MomentumGainOnObjectiveComplete);
        }
    }
    /// <summary>
    /// Ajoute du Momentum à la jauge. Appelé par des actions de jeu réussies.
    /// </summary>
    /// <param name="amount">La quantité de momentum à ajouter (fraction de charge).</param>
    public void AddMomentum(float amount)
    {
        _momentumGainFlag = true;
        
        float previousMomentum = _currentMomentum;
        _currentMomentum = Mathf.Clamp(_currentMomentum + amount, 0f, MAX_MOMENTUM);
        Debug.Log($"[MomentumManager] Ajout de {amount} de momentum. Valeur actuelle: {_currentMomentum}");
        if (_currentMomentum != previousMomentum)
        {
            UpdateChargesAndNotify();
        }
    }

    /// <summary>
    /// Tente de dépenser un certain nombre de charges de Momentum.
    /// </summary>
    /// <param name="chargeCost">Le nombre de charges requises.</param>
    /// <returns>True si le joueur a assez de charges et qu'elles ont été dépensées, False sinon.</returns>
    public bool TrySpendMomentum(int chargeCost)
    {
        if (chargeCost <= 0) return true; // Pas de coût, toujours un succès.
        if (CurrentCharges < chargeCost) return false; // Pas assez de charges.

        _currentMomentum -= chargeCost;
        UpdateChargesAndNotify();
        return true;
    }

    /// <summary>
    /// Gère la logique de décroissance à chaque battement de la musique.
    /// </summary>
    private void HandleBeat(float beatDuration)
    {
        if (_momentumGainFlag)
        {
            _lastBeatCountWithoutGain = 0;
            _momentumGainFlag = false;
            Debug.Log("[MomentumManager] Gain de Momentum détecté. Compteur d'inactivité réinitialisé.");
        }

        _lastBeatCountWithoutGain++;
        Debug.Log($"[MomentumManager] Compteur d'inactivité: {_lastBeatCountWithoutGain} beats.");
        if (_lastBeatCountWithoutGain > DECAY_THRESHOLD_BEATS)
        {
            float momentumAvantCalcul = _currentMomentum;

            if (momentumAvantCalcul <= 0)
            {
                return;
            }
            
            Debug.LogWarning($"[HandleBeat] Début de la DÉCROISSANCE. Compteur: {_lastBeatCountWithoutGain}, Momentum actuel: {momentumAvantCalcul}");

            float palier = Mathf.Floor(momentumAvantCalcul);
            float momentumApresSoustraction = momentumAvantCalcul - DECAY_AMOUNT_PER_BEAT;
            float momentumFinal = Mathf.Max(momentumApresSoustraction, palier);

            _currentMomentum = momentumFinal;
        
            if (_currentMomentum != momentumAvantCalcul)
            {
                Debug.LogWarning($"[HandleBeat] Changement appliqué ! Nouvelle valeur : {_currentMomentum}");
                UpdateChargesAndNotify();
            }
        }
        
    }

    /// <summary>
    /// Met à jour les charges, la valeur brute et notifie les observateurs.
    /// </summary>
    private void UpdateChargesAndNotify()
    {
        CurrentCharges = Mathf.FloorToInt(_currentMomentum);
        OnMomentumChanged?.Invoke(CurrentCharges, _currentMomentum);
    }
}