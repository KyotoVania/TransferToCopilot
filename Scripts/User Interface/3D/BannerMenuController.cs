using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BannerMenuController : MonoBehaviour, IBannerInteractable
{
    [Header("Banner Settings")]
    [SerializeField] private Transform bannerTransform;
    [SerializeField] private float bannerWaveSpeed = 1f;
    [SerializeField] private float bannerWaveAmplitude = 0.1f;
    [SerializeField] private float bannerWaveFrequency = 2f;

    [Header("Chain Settings")]
    [SerializeField] private Transform[] chainPoints;
    [SerializeField] private float chainSwingSpeed = 0.5f;
    [SerializeField] private float chainSwingAmplitude = 0.05f;

    private Vector3 initialBannerPosition;
    private Vector3[] initialChainPositions;
    private float timeOffset;

    private void Start()
    {
        InitializeBanner();
    }

    private void InitializeBanner()
    {
        initialBannerPosition = bannerTransform.position;
        initialChainPositions = new Vector3[chainPoints.Length];
        for (int i = 0; i < chainPoints.Length; i++)
        {
            initialChainPositions[i] = chainPoints[i].position;
        }

        timeOffset = Random.Range(0f, 2f * Mathf.PI);
    }

    private void Update()
    {
        AnimateBanner();
        AnimateChains();
    }

    private void AnimateBanner()
    {
        float time = Time.time + timeOffset;
        
        Vector3 waveOffset = new Vector3(
            Mathf.Sin(time * bannerWaveFrequency) * bannerWaveAmplitude,
            Mathf.Cos(time * bannerWaveSpeed) * bannerWaveAmplitude,
            0
        );

        bannerTransform.position = initialBannerPosition + waveOffset;
    }

    private void AnimateChains()
    {
        float time = Time.time + timeOffset;

        for (int i = 0; i < chainPoints.Length; i++)
        {
            Vector3 swingOffset = new Vector3(
                Mathf.Sin(time * chainSwingSpeed + i) * chainSwingAmplitude,
                Mathf.Cos(time * chainSwingSpeed + i) * chainSwingAmplitude,
                0
            );

            chainPoints[i].position = initialChainPositions[i] + swingOffset;
        }
    }

    public void OnBannerClicked()
    {
        StartCoroutine(BannerClickAnimation());
    }

    public void OnBannerHoverEnter()
    {
        // À implémenter si besoin
    }

    public void OnBannerHoverExit()
    {
        // À implémenter si besoin
    }

    private System.Collections.IEnumerator BannerClickAnimation()
    {
        float duration = 0.2f;
        float elapsed = 0f;
        Vector3 originalScale = bannerTransform.localScale;
        Vector3 targetScale = originalScale * 0.95f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            bannerTransform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            bannerTransform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }

        bannerTransform.localScale = originalScale;
    }
} 