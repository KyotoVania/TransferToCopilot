using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Playables;
using UnityEngine.Timeline;


public class MenuIntroCinematic : MonoBehaviour, IMenuObserver
{
    [Header("Cinematic Settings")]
    [SerializeField] private bool playOnStart = false;
    [SerializeField] private float cinematicDuration = 5f;

    [Header("Meteor Effect")]
    [SerializeField] private GameObject meteorPrefab;
    [SerializeField] private Transform meteorSpawnPoint;
    [SerializeField] private float meteorDelay = 1f;
    [SerializeField] private AudioClip meteorSoundEffect;

    [Header("Fire Effects")]
    [SerializeField] private GameObject[] fireFXs;
    [SerializeField] private float fireDelay = 2.5f;
    [SerializeField] private AudioClip fireStartSound;

    [Header("Camera Control")]
    [SerializeField] private CinemachineCamera startCamera;
    [SerializeField] private CinemachineCamera impactCamera;
    [SerializeField] private CinemachineCamera endCamera;
    [SerializeField] private float transitionDelay = 1f;

    // Internal state
    private bool cinematicActive = false;
    private List<GameObject> spawnedObjects = new List<GameObject>();

    private void Start()
    {
        if (fireFXs != null)
        {
            foreach (var fire in fireFXs)
            {
                if (fire != null)
                {
                    fire.SetActive(false);
                }
            }
        }
        RegisterAsObserver();
        if (playOnStart)
        {
            PlayCinematic();
        }
    }

    private void OnDestroy()
    {
        UnregisterAsObserver();
    }

    private void RegisterAsObserver()
    {
        if (MenuSceneTransitionManager.Instance != null)
        {
            // MenuSceneTransitionManager.Instance.AddObserver(this); // Optionnel si MenuSceneTransitionManager n'est plus utilisé pour CETTE transition
        }
    }

    private void UnregisterAsObserver()
    {
        if (MenuSceneTransitionManager.Instance != null)
        {
            // MenuSceneTransitionManager.Instance.RemoveObserver(this);
        }
    }

    public void PlayCinematic()
    {
        if (!cinematicActive)
        {
            cinematicActive = true;
            StartCoroutine(CinematicSequence());
        }
    }

    private IEnumerator CinematicSequence()
    {
        if (startCamera != null)
        {
            startCamera.Priority = 20;
            if (impactCamera != null) impactCamera.Priority = 10;
            if (endCamera != null) endCamera.Priority = 10;
        }

        yield return new WaitForSeconds(0.5f);

        if (meteorPrefab != null && meteorSpawnPoint != null)
        {
            yield return new WaitForSeconds(meteorDelay);
            if (impactCamera != null)
            {
                impactCamera.Priority = 30;
            }
        
            PlaySoundEffect(meteorSoundEffect);
            GameObject meteor = Instantiate(meteorPrefab, meteorSpawnPoint.position, meteorSpawnPoint.rotation);
            spawnedObjects.Add(meteor);
            yield return new WaitForSeconds(fireDelay - meteorDelay);
        }
        else
        {
            yield return new WaitForSeconds(fireDelay);
        }

        if (fireFXs != null)
        {
            PlaySoundEffect(fireStartSound);
            foreach (var fire in fireFXs)
            {
                if (fire != null)
                {
                    fire.SetActive(true);
                }
            }
        }

        if (endCamera != null)
        {
            endCamera.Priority = 40;
       
        }

        // La durée restante est gérée par cinematicDuration. S'assurer que les actions ci-dessus ne la dépassent pas.
        // On peut calculer le temps déjà écoulé pour ajuster l'attente.
        float timeElapsedSoFar = 0.5f + (meteorPrefab != null ? fireDelay : fireDelay) + (endCamera != null ? 0f : 0f) ; // Simplification, ajuster si blendTimes sont réintroduits

        float remainingWait = cinematicDuration - timeElapsedSoFar;
        if (remainingWait > 0)
        {
            yield return new WaitForSeconds(remainingWait);
        }

        yield return new WaitForSeconds(transitionDelay);

        // ----- MODIFICATION PRINCIPALE -----
        if (GameManager.Instance != null)
        {
            Debug.Log("[MenuIntroCinematic] Fin de la cinématique, demande de chargement du Hub via GameManager.");
            GameManager.Instance.LoadHub(); // Appel direct à LoadHub
        }
        else
        {
            Debug.LogError("[MenuIntroCinematic] GameManager.Instance est null! Impossible de charger le Hub.");
            // Fallback (moins idéal car ne met pas à jour GameManager._currentActiveSceneName)
            // UnityEngine.SceneManagement.SceneManager.LoadScene("Hub"); // Mettre le nom exact de votre scène Hub
        }
        // -----------------------------------
        
        cinematicActive = false;
    }

    private void PlaySoundEffect(AudioClip clip)
    {
        if (clip != null && AudioManager.Instance != null)
        {
            AudioSource tempAudio = gameObject.AddComponent<AudioSource>();
            // Configurer la sortie audio vers le bus SFX de Wwise si vous le gérez comme ça,
            // ou utiliser les méthodes de l'AudioManager pour jouer des sons.
            // Pour un simple AudioClip, la méthode actuelle est ok, mais pour Wwise,
            // vous utiliseriez plutôt un AK.Wwise.Event.Post(gameObject);
            tempAudio.clip = clip;
            tempAudio.volume = AudioManager.Instance.SfxVolume;
            tempAudio.Play();
            StartCoroutine(DestroySoundComponentAfterPlay(tempAudio, clip.length));
        }
    }

    private System.Collections.IEnumerator DestroySoundComponentAfterPlay(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(source);
    }

    private void CleanupCinematic()
    {
        foreach (var obj in spawnedObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        spawnedObjects.Clear();
        
        if (fireFXs != null)
        {
            foreach (var fire in fireFXs)
            {
                if (fire != null)
                {
                    fire.SetActive(false);
                }
            }
        }
        cinematicActive = false;
    }

    // IMenuObserver implementation (si MenuSceneTransitionManager est toujours utilisé pour d'autres choses)
    public void OnMenuStateChanged(MenuState newState) { }
    public void OnVolumeChanged(AudioType type, float value) { }
    public void OnSceneTransitionStarted(string sceneName)
    {
        // Si la cinématique est en cours et qu'une autre transition démarre, nettoyer.
        if (cinematicActive)
        {
            CleanupCinematic();
        }
    }
    public void OnSceneTransitionCompleted(string sceneName) { }
}