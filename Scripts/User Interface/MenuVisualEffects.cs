using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MenuVisualEffects : MonoBehaviour
{
    [Header("Logo Animation")]
    [SerializeField] private RectTransform logoTransform;
    [SerializeField] private float logoPulseSpeed = 2f;
    [SerializeField] private float logoPulseScale = 0.1f;

    [Header("Button Effects")]
    [SerializeField] private float buttonHoverScale = 1.1f;
    [SerializeField] private float buttonAnimationSpeed = 0.2f;

    [Header("Background Effects")]
    [SerializeField] private float backgroundPulseSpeed = 1f;
    [SerializeField] private float backgroundPulseIntensity = 0.05f;

    private Vector3 originalLogoScale;
    private Vector3 originalButtonScale;

    private void Start()
    {
        originalLogoScale = logoTransform.localScale;
        originalButtonScale = transform.localScale;
    }

    private void Update()
    {
        AnimateLogo();
        AnimateBackground();
    }

    private void AnimateLogo()
    {
        float pulse = Mathf.Sin(Time.time * logoPulseSpeed) * logoPulseScale;
        logoTransform.localScale = originalLogoScale * (1f + pulse);
    }

    private void AnimateBackground()
    {
        float pulse = Mathf.Sin(Time.time * backgroundPulseSpeed) * backgroundPulseIntensity;
    }

    public void OnButtonHoverEnter(Button button)
    {
        LeanTween.scale(button.gameObject, Vector3.one * buttonHoverScale, buttonAnimationSpeed)
            .setEaseOutBack();
    }

    public void OnButtonHoverExit(Button button)
    {
        LeanTween.scale(button.gameObject, Vector3.one, buttonAnimationSpeed)
            .setEaseOutBack();
    }

    public void OnButtonClick(Button button)
    {
        LeanTween.scale(button.gameObject, Vector3.one * 0.9f, buttonAnimationSpeed * 0.5f)
            .setEaseInBack()
            .setOnComplete(() => {
                LeanTween.scale(button.gameObject, Vector3.one, buttonAnimationSpeed)
                    .setEaseOutBack();
            });
    }
} 