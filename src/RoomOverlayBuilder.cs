using SecretHistories.Abstract;
using SecretHistories.Tokens.Payloads;
using UnityEngine;

namespace RoomIconsMod
{
    public static class RoomOverlayBuilder
    {
        public static void BuildOrRefresh(MonoBehaviour host, IManifestable manifestable)
        {
            if (host == null)
                return;

            // ConnectedTerrain covers Hush House rooms. WisdomNodeTerrain subclasses it but
            // renders through WisdomNodeManifestation, so it never reaches this patch.
            var terrain = manifestable as ConnectedTerrain;
            if (terrain == null)
                return;

            var view = host.GetComponent<RoomOverlayView>();

            // Shrouded means unopened; sealed means no adjacent open room yet. The game
            // itself refuses to show unlock details unless shrouded and unsealed.
            if (!terrain.IsShrouded || terrain.IsSealed)
            {
                if (view != null)
                    view.Build(terrain, null);
                return;
            }

            RoomReqs reqs = RoomOverlay.GetReqs(terrain);
            if (reqs == null || reqs.IsEmpty)
            {
                if (view != null)
                    view.Build(terrain, null);
                return;
            }

            if (view == null)
                view = host.gameObject.AddComponent<RoomOverlayView>();

            view.Build(terrain, reqs);
        }
    }
}
