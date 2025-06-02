public interface IMenuStateManager
{
    MenuState CurrentState { get; }
    void SetState(MenuState newState);
    void AddStateChangeListener(System.Action<MenuState> listener);
    void RemoveStateChangeListener(System.Action<MenuState> listener);
} 