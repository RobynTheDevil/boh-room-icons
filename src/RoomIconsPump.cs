using UnityEngine;

namespace RoomIconsMod
{
    /// Single per-frame driver. The views themselves are passive.
    public class RoomIconsPump : MonoBehaviour
    {
        private bool _seeded;
        private bool _contentWired;

        private void Update()
        {
            if (!_contentWired && RoomOverlay.TryWireContentEventsOnce())
                _contentWired = true;

            if (!_seeded && RoomOverlay.TrySeedOnce())
                _seeded = true;

            RoomOverlay.TickZoomScale();
            RoomOverlay.TickVisibility();
            RoomOverlay.TickPendingAssets();
        }
    }
}
