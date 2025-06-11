// Fichier: Scripts2/Controllers/ComboController.cs (Version mise à jour)
using UnityEngine;
using System.Collections.Generic;
using System;
using Game.Observers;

public class ComboController : MonoBehaviour
{
    // La définition du Singleton et les autres variables restent les mêmes
    private static ComboController instance;
    public static ComboController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<ComboController>();
            }
            return instance;
        }
    }

    public int comboCount { get; private set; } = 0;
    public int maxCombo { get; private set; } = 0;

    private readonly List<IComboObserver> observers = new List<IComboObserver>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Multiple ComboController instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnEnable()
    {
        // --- MODIFIÉ ICI ---
        // On s'abonne à chaque touche correcte, pas seulement à la séquence réussie.
        SequenceController.OnSequenceKeyPressed += HandleCorrectInput;
        
        // On garde la réinitialisation du combo en cas d'échec
        SequenceController.OnSequenceFail += ResetCombo;
    }

    private void OnDisable()
    {
        // --- MODIFIÉ ICI ---
        // On se désabonne du même événement.
        SequenceController.OnSequenceKeyPressed -= HandleCorrectInput;
        SequenceController.OnSequenceFail -= ResetCombo;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    // --- NOUVELLE MÉTHODE ---
    /// <summary>
    /// Gère un input correct et incrémente le combo.
    /// La signature correspond à l'événement OnSequenceKeyPressed.
    /// </summary>
    private void HandleCorrectInput(string key, Color timingColor)
    {
        IncrementCombo();
    }
    
    // La logique de ces méthodes reste la même
    private void IncrementCombo()
    {
        comboCount++;
        maxCombo = Mathf.Max(maxCombo, comboCount);
        NotifyObservers();
    }

    private void ResetCombo()
    {
        // On ne réinitialise que si le combo était supérieur à 0, pour ne pas notifier inutilement.
        if (comboCount > 0)
        {
            comboCount = 0;
            NotifyComboReset();
        }
    }
    
    // Les méthodes d'observateur restent les mêmes
    public void AddObserver(IComboObserver observer)
    {
        if (!observers.Contains(observer))
        {
            observers.Add(observer);
            observer.OnComboUpdated(comboCount);
        }
    }

    public void RemoveObserver(IComboObserver observer)
    {
        observers.Remove(observer);
    }

    private void NotifyObservers()
    {
        foreach (var observer in observers.ToArray()) // Utiliser ToArray pour éviter les problèmes si un observer se retire pendant la notification
        {
            if (observer != null)
            {
                observer.OnComboUpdated(comboCount);
            }
        }
        observers.RemoveAll(o => o == null);
    }

    private void NotifyComboReset()
    {
        foreach (var observer in observers.ToArray())
        {
            if (observer != null)
            {
                observer.OnComboReset();
            }
        }
        observers.RemoveAll(o => o == null);
    }

    private void OnApplicationQuit()
    {
        instance = null;
    }
}