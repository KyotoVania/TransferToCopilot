using UnityEngine;

/// <summary>
/// Manages the visual effects of a music-reactive object, such as color and emission changes.
/// </summary>
public class MusicReactiveVisuals : MonoBehaviour
{
    [Header("Visual Effects")]
    /// <summary>
    /// The color the material will change to on a beat.
    /// </summary>
    [SerializeField] protected Color beatColor = Color.cyan;
    /// <summary>
    /// The intensity of the emission glow.
    /// </summary>
    [SerializeField] protected float glowIntensity = 1.5f;
    /// <summary>
    /// Whether to use emission for the visual effect.
    /// </summary>
    [SerializeField] protected bool useEmission = true;

    /// <summary>
    /// The material of the object.
    /// </summary>
    protected Material material;
    /// <summary>
    /// The original color of the material.
    /// </summary>
    protected Color originalColor;

    /// <summary>
    /// Unity's Awake method. Initializes the material and original color.
    /// </summary>
    protected virtual void Awake()
    {
        material = GetComponent<Renderer>().material;
        originalColor = material.color;
    }

    /// <summary>
    /// Call this during an animation to update color/emission based on progress (t from 0 to 1).
    /// </summary>
    /// <param name="t">The progress of the animation, from 0 to 1.</param>
    public virtual void ApplyVisualEffects(float t)
    {
        if (useEmission)
        {
            float colorT = Mathf.Clamp01(t * 2f);
            material.SetColor("_EmissionColor", Color.Lerp(Color.black, beatColor * glowIntensity, colorT));
            material.color = Color.Lerp(originalColor, beatColor, colorT);
        }
    }

    /// <summary>
    /// Resets the material colors to their original state.
    /// </summary>
    public virtual void ResetVisuals()
    {
        if (useEmission)
        {
            material.SetColor("_EmissionColor", Color.black);
            material.color = originalColor;
        }
    }
}
