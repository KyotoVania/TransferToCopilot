using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#region Gameplay Integration

/// <summary>
/// Manages game state transitions and their impact on music and gameplay systems.
/// Handles transitions between Exploration, Combat, and Boss states with appropriate music changes.
/// </summary>
public class GameStateManager : MonoBehaviour
{
    /// <summary>
    /// Enumeration of possible game states during gameplay.
    /// </summary>
    public enum GameState 
    { 
        /// <summary>Exploration phase - players can move freely and plan strategies.</summary>
        Exploration, 
        /// <summary>Combat phase - active combat is occurring.</summary>
        Combat, 
        /// <summary>Boss phase - boss enemy is active and requires special handling.</summary>
        Boss 
    }

    /// <summary>
    /// The current game state.
    /// </summary>
    [SerializeField] private GameState currentState = GameState.Exploration;

    /// <summary>
    /// Gets or sets the current game state.
    /// </summary>
    public GameState CurrentState
    {
        get => currentState;
        set => currentState = value;
    }
    
    /// <summary>
    /// Debug key for manually triggering exploration state.
    /// </summary>
    [SerializeField] private KeyCode explorationKey = KeyCode.Alpha1;
    
    /// <summary>
    /// Debug key for manually triggering combat state.
    /// </summary>
    [SerializeField] private KeyCode combatKey = KeyCode.Alpha2;
    
    /// <summary>
    /// Debug key for manually triggering boss state.
    /// </summary>
    [SerializeField] private KeyCode bossKey = KeyCode.Alpha3;

    /// <summary>
    /// Initializes the game state manager and sets the initial state.
    /// </summary>
    private void Start()
    {
        // Initialize the starting state
        UpdateGameState(currentState);
    }
    /// <summary>
    /// Subscribes to enemy registry events when the component is enabled.
    /// </summary>
    private void OnEnable()
    {
        // Subscribe to the new registry event
        EnemyRegistry.OnBossSpawned += HandleBossSpawn;
    }
    
    /// <summary>
    /// Handles the boss spawn event by transitioning to the Boss game state.
    /// </summary>
    /// <param name="bossUnit">The boss unit that was spawned.</param>
    private void HandleBossSpawn(EnemyUnit bossUnit)
    {
        Debug.Log($"[GameStateManager] Événement OnBossSpawned reçu pour '{bossUnit.name}'. Changement d'état vers Boss.");
        UpdateGameState(GameState.Boss);
    }
    /// <summary>
    /// Handles debug input for manually changing game states during development.
    /// </summary>
    private void Update()
    {
        // Debug controls for manual state changes
        if (Input.GetKeyDown(explorationKey))
            UpdateMusicBasedOnDebugKey("Exploration");
        else if (Input.GetKeyDown(combatKey))
            UpdateMusicBasedOnDebugKey("Combat");
        else if (Input.GetKeyDown(bossKey))
            UpdateMusicBasedOnDebugKey("Boss");
    }

    /// <summary>
    /// Updates the current game state and triggers appropriate music changes.
    /// </summary>
    /// <param name="newState">The new game state to transition to.</param>
    /// <param name="immediateTransition">Whether the music transition should be immediate or gradual.</param>
    public void UpdateGameState(GameState newState, bool immediateTransition = false)
    {
        if (currentState != newState)
        {
            currentState = newState;
            MusicManager.Instance.SetMusicState(newState.ToString(), immediateTransition);
        }
    }

     /// <summary>
     /// Debug method for manually changing music state using keyboard input.
     /// </summary>
     /// <param name="musicState">The music state to transition to.</param>
     /// <param name="immediateTransition">Whether the music transition should be immediate or gradual.</param>
     public void UpdateMusicBasedOnDebugKey(string musicState, bool immediateTransition = false)
    {
        if (MusicManager.Instance != null)
        {
            // The 'immediateTransition' boolean in SetMusicState is now less critical
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
