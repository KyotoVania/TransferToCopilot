using UnityEngine;
using Unity.Behavior;
using System;
using Unity.Properties;
using Unity.Behavior.GraphFramework;

/// <summary>
/// A custom Behavior Graph Action node that waits for the next beat signal
/// from the RhythmManager before returning Success.
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
public class WaitForBeatNode : Unity.Behavior.Action // Assurez-vous que la classe de base est correcte
{
    private bool beatOccurredThisFrame = false;
    private bool hasSubscribedThisRun = false;

    /// <summary>
    /// Appelé une fois lorsque le nœud commence à s'exécuter.
    /// S'abonne à l'événement OnBeat du RhythmManager.
    /// </summary>
    protected override Status OnStart()
    {
        // Réinitialiser le flag à chaque fois que le nœud démarre.
        beatOccurredThisFrame = false;
        hasSubscribedThisRun = false; // Réinitialiser l'état d'abonnement pour cette exécution.

        if (RhythmManager.Instance != null)
        {
            // S'abonner à l'événement.
            RhythmManager.OnBeat += HandleBeat;
            hasSubscribedThisRun = true;
            // Optionnel: Log pour débogage
            // Debug.Log($"[{Time.frameCount}] WaitForBeatNode: OnStart - Subscribed to RhythmManager.OnBeat.");
            return Status.Running; // Commencer à attendre le battement.
        }
        else
        {
            Debug.LogError("[WaitForBeatNode] OnStart: RhythmManager.Instance is null. Cannot wait for beat. Returning Failure.");
            return Status.Failure; // Impossible de fonctionner sans RhythmManager.
        }
    }

    /// <summary>
    /// Appelé à chaque frame tant que le statut du nœud est Running.
    /// Vérifie si un battement a été détecté.
    /// </summary>
    protected override Status OnUpdate()
    {
        // Si le flag beatOccurredThisFrame a été mis à true par HandleBeat,
        // cela signifie qu'un battement s'est produit depuis le dernier OnUpdate ou OnStart.
        if (beatOccurredThisFrame)
        {
            // Optionnel: Log pour débogage
            // Debug.Log($"[{Time.frameCount}] WaitForBeatNode: OnUpdate - Beat occurred. Returning Success.");
            // Le flag sera réinitialisé au prochain OnStart.
            // Le désabonnement se fera dans OnEnd.
            return Status.Success; // Le battement a été reçu.
        }

        // Si aucun battement n'a encore eu lieu pendant cette exécution du nœud, continuer à attendre.
        return Status.Running;
    }

    /// <summary>
    /// Appelé lorsque le nœud a terminé son exécution (après avoir retourné Success ou Failure)
    /// ou s'il est interrompu. Crucial pour se désabonner des événements.
    /// </summary>
    protected override void OnEnd()
    {
        // Se désabonner de l'événement pour éviter les fuites de mémoire ou les appels non désirés.
        if (hasSubscribedThisRun && RhythmManager.Instance != null)
        {
            RhythmManager.OnBeat -= HandleBeat;
            // Optionnel: Log pour débogage
            // Debug.Log($"[{Time.frameCount}] WaitForBeatNode: OnEnd - Unsubscribed from RhythmManager.OnBeat.");
        }
        else if (hasSubscribedThisRun && RhythmManager.Instance == null)
        {
            // Si RhythmManager a disparu pendant que nous étions abonnés.
            Debug.LogWarning("[WaitForBeatNode] OnEnd: RhythmManager.Instance became null while subscribed. Could not formally unsubscribe.");
        }

        // Réinitialiser le flag d'abonnement pour la prochaine exécution.
        hasSubscribedThisRun = false;
        beatOccurredThisFrame = false; // Assurer la propreté pour la prochaine fois.

        // Il n'est généralement pas nécessaire d'appeler base.OnEnd() car elle est souvent vide.
    }

    /// <summary>
    /// Méthode de handler pour l'événement OnBeat du RhythmManager.
    /// Met à jour le flag pour indiquer qu'un battement a été reçu.
    /// </summary>
    private void HandleBeat()
    {
        // Ce handler est appelé directement par l'événement OnBeat du RhythmManager.
        // Il met simplement le flag à true. OnUpdate vérifiera ce flag.
        beatOccurredThisFrame = true;
        // Optionnel: Log pour débogage
        // Debug.Log($"[{Time.frameCount}] WaitForBeatNode: HandleBeat - Beat signal received. beatOccurredThisFrame = true.");
    }
}