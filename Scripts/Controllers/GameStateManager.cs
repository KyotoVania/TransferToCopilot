using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#region Gameplay Integration

/// <summary>
/// Gère les transitions d'état de jeu et leur impact sur la musique
/// </summary>
public class GameStateManager : MonoBehaviour
{
    public enum GameState 
    { 
        Exploration, 
        Combat, 
        Boss 
    }

    [SerializeField] private GameState currentState = GameState.Exploration;
    
    [SerializeField] private KeyCode explorationKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode combatKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode bossKey = KeyCode.Alpha3;

    private void Start()
    {
        // Initialiser l'état de départ
        UpdateGameState(currentState);
    }

    private void Update()
    {
        // Debug controls
        if (Input.GetKeyDown(explorationKey)) //
            UpdateMusicBasedOnDebugKey("Exploration"); //
        else if (Input.GetKeyDown(combatKey)) //
            UpdateMusicBasedOnDebugKey("Combat"); //
        else if (Input.GetKeyDown(bossKey)) //
            UpdateMusicBasedOnDebugKey("Boss"); //
    }

    public void UpdateGameState(GameState newState, bool immediateTransition = false)
    {
        if (currentState != newState)
        {
            currentState = newState;
            MusicManager.Instance.SetMusicState(newState.ToString(), immediateTransition);
        }
    }

     public void UpdateMusicBasedOnDebugKey(string musicState, bool immediateTransition = false)
    {
        if (MusicManager.Instance != null)
        {
            // Le booléen 'immediateTransition' dans SetMusicState est maintenant moins critique
            MusicManager.Instance.SetMusicState(musicState, immediateTransition);
            Debug.Log($"[GameStateManager_Debug] Musique changée vers : {musicState}");
        }
        else
        {
            Debug.LogWarning("[GameStateManager_Debug] MusicManager.Instance est null.");
        }
    }
}

#endregion