using UnityEngine;
using System.Collections.Generic;
using System;
using Game.Observers;

[DefaultExecutionOrder(-100)]
public class GoldController : MonoBehaviour
{
    public static GoldController Instance { get; private set; }

    [SerializeField] private int initialGold = 100; 

    private int _currentGold;
    private readonly List<IGoldObserver> _observers = new List<IGoldObserver>();

    public event Action<int> OnGoldAdded;
    public event Action<int> OnGoldRemoved;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        InitializeGold();
    }


    private void OnEnable()
    {
        //SequenceController.OnSequenceExecuted += OnSequenceExecuted;
    }

    private void OnDisable()
    {
      //  SequenceController.OnSequenceExecuted -= OnSequenceExecuted;
    }

    #region Observer Pattern Methods
    public void AddObserver(IGoldObserver observer)
    {
        if (observer == null)
        {
            Debug.LogError($"[{nameof(GoldController)}] Attempt to add a null observer.");
            return;
        }
        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
            observer.OnGoldUpdated(_currentGold);
        }
    }

    public void RemoveObserver(IGoldObserver observer)
    {
        if (observer == null) return;
        _observers.Remove(observer);
    }

    private void NotifyObservers()
    {
        for (int i = _observers.Count - 1; i >= 0; i--)
        {
            if (_observers[i] != null)
            {
                _observers[i].OnGoldUpdated(_currentGold);
            }
        }
    }
    #endregion

    #region Gold Management Methods
    private void InitializeGold()
    {
        _currentGold = Mathf.Max(0, initialGold);
        NotifyObservers();
    }

    public void AddGold(int amount)
    {
        if (amount < 0)
        {
            Debug.LogError($"[{nameof(GoldController)}] Attempt to add negative gold ({amount}).");
            return;
        }
        if (amount == 0) return;
        _currentGold += amount;
        NotifyObservers();
        OnGoldAdded?.Invoke(amount);
    }

    public void RemoveGold(int amount)
    {
        if (amount < 0)
        {
            Debug.LogError($"[{nameof(GoldController)}] Attempt to remove negative gold ({amount}).");
            return;
        }
        if (amount == 0) return;
        int clampedAmount = Mathf.Min(amount, _currentGold);
        _currentGold -= clampedAmount;
        NotifyObservers();
        OnGoldRemoved?.Invoke(clampedAmount);
        if (clampedAmount != amount)
        {
            Debug.LogWarning($"[{nameof(GoldController)}] Attempted to remove {amount} gold but only {clampedAmount} was available.");
        }
    }

    

    // Helper method to compare two sequences
    private bool AreSequencesEqual(List<KeyCode> seq1, List<KeyCode> seq2)
    {
        if (seq1.Count != seq2.Count) return false;
        for (int i = 0; i < seq1.Count; i++)
        {
            if (seq1[i] != seq2[i])
                return false;
        }
        return true;
    }

    public int GetCurrentGold() => _currentGold;
    #endregion
}