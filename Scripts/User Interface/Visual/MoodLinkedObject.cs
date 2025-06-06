using UnityEngine;

[ExecuteAlways]
public class MoodLinkedObject : MonoBehaviour
{
    [SerializeField] private MoodType targetMood;
    
    private void OnEnable()
    {
        // Register with MoodManager if in play mode
        if (Application.isPlaying)
        {
            var moodManager = FindFirstObjectByType<VisualMoodManager>();
            if (moodManager != null)
            {
                UpdateVisibility(moodManager.CurrentMood);
            }
        }
    }
    
    public void UpdateVisibility(MoodType currentMood)
    {
        gameObject.SetActive(targetMood == currentMood);
    }
    
    // Editor-only functionality to update visibility
    #if UNITY_EDITOR
    private void Update()
    {
        if (!Application.isPlaying)
        {
            var moodManager = FindFirstObjectByType<VisualMoodManager>();
            if (moodManager != null)
            {
                UpdateVisibility(moodManager.CurrentMood);
            }
        }
    }
    #endif
}