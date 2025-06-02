using UnityEngine;

// Add this component to particle effects to ensure they maintain their correct local position
// This helps prevent position drift in team-based particle effects on NeutralBuildings
public class ParticlePositionStabilizer : MonoBehaviour
{
    private Vector3 initialLocalPosition;
    private Vector3 initialLocalScale;

    private void Start()
    {
        // Store the initial local position and scale
        initialLocalPosition = transform.localPosition;
        initialLocalScale = transform.localScale;
    }

    private void LateUpdate()
    {
        // Check if the position or scale has changed, and reset if needed
        if (transform.localPosition != initialLocalPosition)
        {
            transform.localPosition = initialLocalPosition;
        }

        if (transform.localScale != initialLocalScale)
        {
            transform.localScale = initialLocalScale;
        }
    }
}