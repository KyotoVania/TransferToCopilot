public interface IMenuObserver
{
    void OnMenuStateChanged(MenuState newState);
    void OnVolumeChanged(AudioType type, float value);
    void OnSceneTransitionStarted(string sceneName);
    void OnSceneTransitionCompleted(string sceneName);
} 