using UnityEngine;
using UnityEngine.UI;

public class LogoAnimator : MonoBehaviour
{
    [SerializeField] private RectTransform logoTransform;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseScale = 0.1f;

    private Vector3 originalScale;

    private void Start()
    {
        originalScale = logoTransform.localScale;
    }

    private void Update()
    {
        AnimateLogo();
    }

    private void AnimateLogo()
    {
        float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseScale;
        logoTransform.localScale = originalScale * (1f + pulse);
    }
} 