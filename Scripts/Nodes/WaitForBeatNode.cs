using UnityEngine;
using Unity.Behavior;
using System;
using Unity.Properties;
using Unity.Behavior.GraphFramework;

/// <summary>
/// A custom Behavior Graph Action node that waits for the next beat signal
/// Uses the correct OnStart, OnUpdate, OnEnd lifecycle methods.
/// </summary>
[Serializable]
[GeneratePropertyBag]
[NodeDescription(
    name: "Wait For Beat",
    story: "Wait For Beat",
    category: "My Actions",
    id: "YOUR_UNIQUE_ID_WaitForBeat" // Generate a unique GUID string here if needed, or omit for auto
)]
public class WaitForBeatNode : Unity.Behavior.Action
{
    private bool beatOccurredThisFrame = false;
    private bool hasSubscribedThisRun = false;

    /// <summary>
    /// Appelé une fois lorsque le nœud commence à s'exécuter.
    /// S'abonne à l'événement OnBeat du MusicManager.
    /// </summary>
    protected override Status OnStart()
    {
        // Réinitialiser le flag à chaque fois que le nœud démarre.
        beatOccurredThisFrame = false;
        hasSubscribedThisRun = false;

        if (MusicManager.Instance != null)
        {
            // S'abonner à l'événement.
            MusicManager.Instance.OnBeat += HandleBeat;
            hasSubscribedThisRun = true;
            return Status.Running; // Commencer à attendre le battement.
        }
        else
        {
            Debug.LogError("[WaitForBeatNode] OnStart: MusicManager.Instance is null. Cannot wait for beat. Returning Failure.");
            return Status.Failure; // Impossible de fonctionner sans MusicManager.
        }
    }

    /// <summary>
    /// Appelé à chaque frame tant que le statut du nœud est Running.
    /// Vérifie si un battement a été détecté.
    /// </summary>
    protected override Status OnUpdate()
    {
        if (beatOccurredThisFrame)
        {
            return Status.Success; // Le battement a été reçu.
        }
        return Status.Running;
    }

    /// <summary>
    /// Appelé lorsque le nœud a terminé son exécution (après avoir retourné Success ou Failure)
    /// ou s'il est interrompu. Crucial pour se désabonner des événements.
    /// </summary>
    protected override void OnEnd()
    {
        if (hasSubscribedThisRun && MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleBeat; // Se désabonner de l'événement.
        }
        else if (hasSubscribedThisRun && MusicManager.Instance == null)
        {
            Debug.LogWarning("[WaitForBeatNode] OnEnd: MusicManager.Instance became null while subscribed. Could not formally unsubscribe.");
        }

        hasSubscribedThisRun = false;
        beatOccurredThisFrame = false;
    }

    /// <summary>
    /// Méthode de handler pour l'événement OnBeat du MusicManager.
    /// Met à jour le flag pour indiquer qu'un battement a été reçu.
    /// </summary>
    private void HandleBeat(float beatDuration) // La signature accepte le float, même si on ne l'utilise pas ici.
    {
        beatOccurredThisFrame = true;
    }
}