using UnityEngine;
using System.Collections.Generic;

public class MenuStateManager : MonoBehaviour, IMenuStateManager
{
    public MenuState CurrentState { get; private set; }

    private List<System.Action<MenuState>> stateChangeListeners = new List<System.Action<MenuState>>();

    private void Start()
    {
        SetState(MenuState.MainMenu);
    }

    public void SetState(MenuState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        NotifyStateChangeListeners(newState);
    }

    public void AddStateChangeListener(System.Action<MenuState> listener)
    {
        if (!stateChangeListeners.Contains(listener))
        {
            stateChangeListeners.Add(listener);
        }
    }

    public void RemoveStateChangeListener(System.Action<MenuState> listener)
    {
        stateChangeListeners.Remove(listener);
    }

    private void NotifyStateChangeListeners(MenuState newState)
    {
        foreach (var listener in stateChangeListeners)
        {
            listener?.Invoke(newState);
        }
    }
} 