using UnityEngine;

public class WaitForBeat : CustomYieldInstruction
{
    public override bool keepWaiting => !RhythmManager.LastBeatWasProcessed;
}