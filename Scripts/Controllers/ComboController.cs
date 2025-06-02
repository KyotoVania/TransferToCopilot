using UnityEngine;
using System.Collections.Generic;
using System;
using Game.Observers;

public class ComboController : MonoBehaviour
{
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
        // Subscribe to the new SequenceController events.
        // These events should be fired from your reworked SequenceController.
        SequenceController.OnSequenceSuccess += IncrementCombo;
        SequenceController.OnSequenceFail += ResetCombo;
    }

    private void OnDisable()
    {
        SequenceController.OnSequenceSuccess -= IncrementCombo;
        SequenceController.OnSequenceFail -= ResetCombo;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

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
        foreach (var observer in observers)
        {
            if (observer != null)
            {
                observer.OnComboUpdated(comboCount);
            }
        }
        // Clean up any null observers.
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
        // Clean up any null observers.
        observers.RemoveAll(o => o == null);
    }

    private void IncrementCombo()
    {
        comboCount++;
        maxCombo = Mathf.Max(maxCombo, comboCount);
        NotifyObservers();
    }

    private void ResetCombo()
    {
        comboCount = 0;
        NotifyComboReset();
    }

    private void OnApplicationQuit()
    {
        instance = null;
    }
}
