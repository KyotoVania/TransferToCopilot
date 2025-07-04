using UnityEngine;

public class WwiseMotionInitializer : MonoBehaviour
{
    void Awake()
    {
        // Attendre que Wwise soit initialisé
        AkSoundEngine.RegisterGameObj(gameObject, "MotionInit");
        
        // Ajouter le Motion output
        // Updated constructor syntax for 2024.1.5
        AkOutputSettings motionSettings = new AkOutputSettings(
            "Motion",  // ShareSet name for Motion device
            0,         // Device ID (0 for default)
            new AkChannelConfig(),  // Channel configuration
            AkPanningRule.AkPanningRule_Speakers  // Panning rule
        );
        
        AkSoundEngine.AddOutput(motionSettings);
        Debug.Log("[WwiseMotionInitializer] Motion output initialized with settings: " + motionSettings);
        
    }
}