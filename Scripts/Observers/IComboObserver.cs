using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Observers
{
    // Interface for combo observers
    public interface IComboObserver
    {
        void OnComboUpdated(int newCombo);
        void OnComboReset();
    }
}
