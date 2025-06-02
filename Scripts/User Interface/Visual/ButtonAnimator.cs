using UnityEngine;
using UnityEngine.UI;

public class ButtonAnimator : MonoBehaviour
{
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float animationSpeed = 0.2f;

    public void OnHoverEnter(Button button)
    {
        LeanTween.scale(button.gameObject, Vector3.one * hoverScale, animationSpeed)
            .setEaseOutBack();
    }

    public void OnHoverExit(Button button)
    {
        LeanTween.scale(button.gameObject, Vector3.one, animationSpeed)
            .setEaseOutBack();
    }

    public void OnClick(Button button)
    {
        LeanTween.scale(button.gameObject, Vector3.one * 0.9f, animationSpeed * 0.5f)
            .setEaseInBack()
            .setOnComplete(() => {
                LeanTween.scale(button.gameObject, Vector3.one, animationSpeed)
                    .setEaseOutBack();
            });
    }
} 