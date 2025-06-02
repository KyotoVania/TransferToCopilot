using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Observers
{
    // Interface for gold observers
    public interface IGoldObserver
    {
        void OnGoldUpdated(int newAmount);
    }
}
