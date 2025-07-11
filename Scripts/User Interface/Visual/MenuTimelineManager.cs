using UnityEngine;
using System.Collections;

public class MenuTimelineManager : MonoBehaviour
{
    [Header("Skybox Settings")]
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material sunsetSkybox;
    [SerializeField] private float skyboxBlendDuration = 2f;

    [Header("Fog Settings")]
    [SerializeField] private Color dayFogColor = new Color(0.8f, 0.9f, 1f);
    [SerializeField] private Color sunsetFogColor = new Color(1f, 0.6f, 0.2f);
    [SerializeField] private float dayFogDensity = 0.01f;
    [SerializeField] private float sunsetFogDensity = 0.02f;

    [Header("Lighting Settings")]
    [SerializeField] private Light mainLight;
    [SerializeField] private Color dayLightColor = Color.white;
    [SerializeField] private Color sunsetLightColor = new Color(1f, 0.8f, 0.6f);
    [SerializeField] private float dayLightIntensity = 1f;
    [SerializeField] private float sunsetLightIntensity = 0.7f;

    private void Start()
    {
        // Configuration initiale du fog
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        
        // Démarrer la séquence d'introduction
        StartCoroutine(IntroSequence());
    }

    private IEnumerator IntroSequence()
    {
        // Commencer avec les paramètres de coucher de soleil
        SetupSunsetEnvironment();
        yield return new WaitForSeconds(3f); // Attendre que le joueur prenne en compte l'ambiance

        // Transition vers le jour
        yield return StartCoroutine(TransitionToDaylight());
    }

    private void SetupSunsetEnvironment()
    {
        RenderSettings.skybox = sunsetSkybox;
        RenderSettings.fogColor = sunsetFogColor;
        RenderSettings.fogDensity = sunsetFogDensity;
        if (mainLight != null)
        {
            mainLight.color = sunsetLightColor;
            mainLight.intensity = sunsetLightIntensity;
        }
    }

    private IEnumerator TransitionToDaylight()
    {
        float elapsed = 0f;
        Material currentSkybox = RenderSettings.skybox;
        Color startFogColor = RenderSettings.fogColor;
        float startFogDensity = RenderSettings.fogDensity;
        Color startLightColor = mainLight.color;
        float startLightIntensity = mainLight.intensity;

        while (elapsed < skyboxBlendDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / skyboxBlendDuration;
            float smoothT = Mathf.SmoothStep(0, 1, t);

            // Transition du fog
            RenderSettings.fogColor = Color.Lerp(startFogColor, dayFogColor, smoothT);
            RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, dayFogDensity, smoothT);

            // Transition de la lumière
            if (mainLight != null)
            {
                mainLight.color = Color.Lerp(startLightColor, dayLightColor, smoothT);
                mainLight.intensity = Mathf.Lerp(startLightIntensity, dayLightIntensity, smoothT);
            }

            // Transition du skybox
            if (currentSkybox != null && daySkybox != null)
            {
                currentSkybox.Lerp(sunsetSkybox, daySkybox, smoothT);
            }

            yield return null;
        }

        // S'assurer que nous sommes exactement aux valeurs finales
        RenderSettings.skybox = daySkybox;
        RenderSettings.fogColor = dayFogColor;
        RenderSettings.fogDensity = dayFogDensity;
        if (mainLight != null)
        {
            mainLight.color = dayLightColor;
            mainLight.intensity = dayLightIntensity;
        }
    }
} 