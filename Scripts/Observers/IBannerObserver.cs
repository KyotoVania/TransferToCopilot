using UnityEngine;

namespace Game.Observers
{
    public interface IBannerObserver
    {
        void OnBannerPlaced(int column, int row);
    }
}