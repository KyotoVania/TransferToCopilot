using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class TimelineManager : MonoBehaviour
{
    public static TimelineManager Instance { get; private set; }

    [Header("Timeline Management")]
    [SerializeField] private PlayableDirector[] playableDirectors;
    [SerializeField] private TimelineAsset[] initialTimelines;
    [SerializeField] private TimelineAsset[] playTimelines;
    [SerializeField] private bool autoFindDirectors = false;
    [SerializeField] private GameObject[] targetObjects;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (autoFindDirectors)
        {
            FindAllPlayableDirectors();
        }
    }

    private void FindAllPlayableDirectors()
    {
        PlayableDirector[] foundDirectors = FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None);
        playableDirectors = foundDirectors;
        
        Debug.Log($"Trouvé {foundDirectors.Length} PlayableDirectors dans la scène");
        
        // Sauvegarder les timelines actuelles comme timelines initiales
        initialTimelines = new TimelineAsset[foundDirectors.Length];
        for (int i = 0; i < foundDirectors.Length; i++)
        {
            if (foundDirectors[i].playableAsset is TimelineAsset timeline)
            {
                initialTimelines[i] = timeline;
            }
        }
    }

    public void SwitchToPlayTimelines()
    {
        for (int i = 0; i < playableDirectors.Length; i++)
        {
            if (playableDirectors[i] != null && playTimelines[i] != null && targetObjects[i] != null)
            {
                // Arrêter la timeline actuelle
                playableDirectors[i].Stop();
            
                // Changer la timeline
                playableDirectors[i].playableAsset = playTimelines[i];
            
                // Réassigner les bindings
                AssignBindingsToTimeline(playableDirectors[i], targetObjects[i]);
                
                // Redémarrer la timeline
                playableDirectors[i].Play();
            }
        }
    }

    public void SwitchToInitialTimelines()
    {
        for (int i = 0; i < playableDirectors.Length; i++)
        {
            if (playableDirectors[i] != null && initialTimelines[i] != null && targetObjects[i] != null)
            {
                // Arrêter la timeline actuelle
                playableDirectors[i].Stop();
            
                // Changer la timeline
                playableDirectors[i].playableAsset = initialTimelines[i];
            
                // Réassigner les bindings
                AssignBindingsToTimeline(playableDirectors[i], targetObjects[i]);
                
                // Redémarrer la timeline
                playableDirectors[i].Play();
            }
        }
    }

    public void SwitchTimeline(int directorIndex, TimelineAsset newTimeline, GameObject targetObject = null)
    {
        if (directorIndex >= 0 && directorIndex < playableDirectors.Length && playableDirectors[directorIndex] != null)
        {
            PlayableDirector director = playableDirectors[directorIndex];
            
            // Arrêter la timeline actuelle
            director.Stop();
            
            // Changer la timeline
            director.playableAsset = newTimeline;
            
            // Réassigner les bindings si un objet cible est fourni
            if (targetObject != null)
            {
                AssignBindingsToTimeline(director, targetObject);
            }
            else if (directorIndex < targetObjects.Length && targetObjects[directorIndex] != null)
            {
                AssignBindingsToTimeline(director, targetObjects[directorIndex]);
            }
            
            // Redémarrer la timeline
            director.Play();
        }
    }

    private void AssignBindingsToTimeline(PlayableDirector director, GameObject targetObject)
    {
        if (director.playableAsset is TimelineAsset timeline)
        {
            foreach (var track in timeline.GetOutputTracks())
            {
                // Pour les Animation Tracks
                if (track is AnimationTrack)
                {
                    var animator = targetObject.GetComponent<Animator>();
                    if (animator != null)
                    {
                        director.SetGenericBinding(track, animator);
                    }
                }
                // Pour les Activation Tracks
                else if (track.GetType().Name == "ActivationTrack")
                {
                    director.SetGenericBinding(track, targetObject);
                }
                // Pour d'autres types de tracks, assignez le Transform par défaut
                else
                {
                    director.SetGenericBinding(track, targetObject.transform);
                }
            }
        
            Debug.Log($"Bindings assignés pour {targetObject.name}");
        }
    }

    public void PlayTimeline(int directorIndex)
    {
        if (directorIndex >= 0 && directorIndex < playableDirectors.Length && playableDirectors[directorIndex] != null)
        {
            playableDirectors[directorIndex].Play();
        }
    }

    public void StopTimeline(int directorIndex)
    {
        if (directorIndex >= 0 && directorIndex < playableDirectors.Length && playableDirectors[directorIndex] != null)
        {
            playableDirectors[directorIndex].Stop();
        }
    }

    public void StopAllTimelines()
    {
        foreach (var director in playableDirectors)
        {
            if (director != null)
            {
                director.Stop();
            }
        }
    }
    
    public void PlayAllTimelines()
    {
        foreach (var director in playableDirectors)
        {
            if (director != null)
            {
                director.Play();
            }
        }
    }
}