using UnityEngine;

public class MusicReactiveVisuals : MonoBehaviour
{
    [Header("Visual Effects")]
    [SerializeField] protected Color beatColor = Color.cyan;
    [SerializeField] protected float glowIntensity = 1.5f;
    [SerializeField] protected bool useEmission = true;

    protected Material material;
    protected Color originalColor;

    protected virtual void Awake()
    {
        material = GetComponent<Renderer>().material;
        originalColor = material.color;
    }

    /// <summary>
    /// Call this during an animation to update color/emission based on progress (t from 0 to 1).
    /// </summary>
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