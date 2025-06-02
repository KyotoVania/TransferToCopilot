using UnityEngine;
using System;

public class SequenceObserver : MonoBehaviour
{
    private void OnEnable()
    {
       // SequenceController.OnSequenceExecuted += HandleSequenceExecuted;
    }

    private void OnDisable()
    {
        //SequenceController.OnSequenceExecuted -= HandleSequenceExecuted;
    }

    /*
    private void HandleSequenceExecuted(Sequence executedSequence, int perfectCount)
    {
        Debug.Log($"Sequence executed: {executedSequence.responseMessage} with {perfectCount} perfect inputs.");
        // Add custom reactions here (e.g., trigger unit production, play effects, etc.)
    } */
}