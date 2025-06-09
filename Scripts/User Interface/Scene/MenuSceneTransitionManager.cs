using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class MenuSceneTransitionManager : MonoBehaviour
{
    private static MenuSceneTransitionManager instance;
    public static MenuSceneTransitionManager Instance => instance;

    [SerializeField] private Animator transitionAnimator;
    [SerializeField] private float transitionTime = 1f;

    private List<IMenuObserver> observers = new List<IMenuObserver>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    public void LoadScene(int sceneIndex)
    {
        StartCoroutine(LoadSceneCoroutine(sceneIndex));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        NotifySceneTransitionStarted(sceneName);
        transitionAnimator.SetTrigger("Start");
        yield return new WaitForSeconds(transitionTime);
        
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        transitionAnimator.SetTrigger("End");
        NotifySceneTransitionCompleted(sceneName);
    }

    private IEnumerator LoadSceneCoroutine(int sceneIndex)
    {
        NotifySceneTransitionStarted(SceneManager.GetSceneByBuildIndex(sceneIndex).name);
        transitionAnimator.SetTrigger("Start");
        yield return new WaitForSeconds(transitionTime);
        
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        transitionAnimator.SetTrigger("End");
        NotifySceneTransitionCompleted(SceneManager.GetSceneByBuildIndex(sceneIndex).name);
    }

    public void AddObserver(IMenuObserver observer)
    {
        if (!observers.Contains(observer))
        {
            observers.Add(observer);
        }
    }

    public void RemoveObserver(IMenuObserver observer)
    {
        observers.Remove(observer);
    }

    private void NotifySceneTransitionStarted(string sceneName)
    {
        foreach (var observer in observers)
        {
            observer.OnSceneTransitionStarted(sceneName);
        }
    }

    private void NotifySceneTransitionCompleted(string sceneName)
    {
        foreach (var observer in observers)
        {
            observer.OnSceneTransitionCompleted(sceneName);
        }
    }
} 