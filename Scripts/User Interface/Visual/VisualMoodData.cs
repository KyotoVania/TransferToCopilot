using UnityEngine;
using UnityEngine.Rendering;

public interface IMoodElement
{
    void OnMoodActivated();
    void OnMoodDeactivated();
}

[CreateAssetMenu(fileName = "New Visual Mood", menuName = "Menu/Visual Mood Data")]
public class VisualMoodData : ScriptableObject
{
    [Header("Identification")]
    public MoodType moodType;
    public string moodName;

    [Header("Sky Settings")]
    public Material skyboxMaterial;

    [Header("Lighting")]
    public GameObject directionalLightPrefab;
    public Vector3 lightPosition = new Vector3(0, 3, 0);
    public Vector3 lightRotation = new Vector3(50, -30, 0);

    [Header("Fog Settings")]
    public bool useFog = true;
    public Color fogColor = Color.white;
    public float fogDensity = 0.01f;
    public FogMode fogMode = FogMode.ExponentialSquared;

    [Header("Post-Processing")]
    public VolumeProfile volumeProfile;
}