using System.Collections.Generic;
using System.Text;
using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Services;
using SecretHistories.Tokens.Payloads;
using SecretHistories.UI;
using UnityEngine;
using UnityEngine.Events;

namespace RoomIconsMod
{
    public struct IconSpec
    {
        public string AspectId;
        public int Level;
        public Sprite Sprite;
    }

    public sealed class RoomReqs
    {
        public readonly List<IconSpec> Essential = new List<IconSpec>();
        public readonly List<IconSpec> Required = new List<IconSpec>();
        public string Signature = string.Empty;

        public bool IsEmpty => Essential.Count == 0 && Required.Count == 0;
    }

    public static class RoomOverlay
    {
        private static readonly HashSet<RoomOverlayView> Live = new HashSet<RoomOverlayView>();
        private static readonly Dictionary<string, RoomReqs> ReqsByTerrainId =
            new Dictionary<string, RoomReqs>();

        private static bool _userVisible = true;
        private static bool _contentEventsWired;
        private static float _appliedScale = -1f;
        private static float _lastPendingTick = -999f;
        private static float _lastVisibilityTick = -999f;
        private const float PendingRetryInterval = 0.5f;
        private const float VisibilityRetryInterval = 0.25f;

        /// Safety rail only. The camera clamps its own z, so the reference below already caps the
        /// natural scale at 2400 / 500 = 4.8, and per-room clamping usually binds before either.
        private const float MaxIconScale = 12f;

        public static bool IsVisible => _userVisible;

        public static void ToggleVisible()
        {
            _userVisible = !_userVisible;
            ApplyVisibilityToAll();
        }

        public static void Register(RoomOverlayView v)
        {
            if (v != null)
                Live.Add(v);
        }

        public static void Unregister(RoomOverlayView v) => Live.Remove(v);

        public static void ApplyVisibilityToAll()
        {
            foreach (RoomOverlayView v in Live)
            {
                if (v != null)
                    v.ApplyVisibility();
            }
        }

        public static void RefreshAll()
        {
            foreach (RoomOverlayView v in Live)
            {
                if (v != null)
                    v.Rebuild();
            }
        }

        public static void ClearCache() => ReqsByTerrainId.Clear();

        /// A room's lock state changes without `UpdateVisuals` necessarily firing where this mod
        /// sees it, so visibility is re-asserted on a slow tick.
        public static void TickVisibility()
        {
            if (Live.Count == 0)
                return;
            if (Time.unscaledTime - _lastVisibilityTick < VisibilityRetryInterval)
                return;
            _lastVisibilityTick = Time.unscaledTime;
            ApplyVisibilityToAll();
        }

        /// The font is borrowed from a card, which need not exist when rooms are first seeded, and
        /// `UpdateVisuals` does not fire again for a room sitting shrouded.
        public static void TickPendingAssets()
        {
            if (Live.Count == 0)
                return;
            if (Time.unscaledTime - _lastPendingTick < PendingRetryInterval)
                return;
            _lastPendingTick = Time.unscaledTime;

            foreach (RoomOverlayView v in Live)
            {
                if (v != null && v.PendingAssets)
                {
                    RefreshAll();
                    return;
                }
            }
        }

        /// Read from the live Compendium on every cache miss, so a content mod that alters a room's
        /// unlock cost is picked up with no change here.
        public static RoomReqs GetReqs(TerrainFeature terrain)
        {
            if (terrain == null)
                return null;

            string id = terrain.Id;
            if (string.IsNullOrEmpty(id))
                return null;

            RoomReqs cached;
            if (ReqsByTerrainId.TryGetValue(id, out cached))
                return cached;

            RoomReqs reqs = BuildReqs(terrain);
            ReqsByTerrainId[id] = reqs;
            return reqs;
        }

        private static RoomReqs BuildReqs(TerrainFeature terrain)
        {
            var reqs = new RoomReqs();

            Recipe recipe = terrain.GetInfoRecipe();
            if (recipe == null || !recipe.IsValid() || recipe.PreSlots == null)
                return reqs;

            // Every preslot rather than one: base content has a single "infoRecipeInput", a content
            // mod may add more.
            foreach (SphereSpec spec in recipe.PreSlots)
            {
                if (spec == null)
                    continue;
                Collect(spec.Essential, reqs.Essential);
                Collect(spec.Required, reqs.Required);
            }

            var sb = new StringBuilder();
            AppendSig(sb, reqs.Essential);
            sb.Append('|');
            AppendSig(sb, reqs.Required);
            reqs.Signature = sb.ToString();
            return reqs;
        }

        private static void AppendSig(StringBuilder sb, List<IconSpec> icons)
        {
            for (int i = 0; i < icons.Count; i++)
                sb.Append(icons[i].AspectId).Append(':').Append(icons[i].Level).Append(',');
        }

        private static void Collect(AspectsDictionary source, List<IconSpec> into)
        {
            if (source == null)
                return;

            Compendium compendium = Watchman.Get<Compendium>();
            foreach (KeyValuePair<string, int> pair in source)
            {
                if (string.IsNullOrEmpty(pair.Key))
                    continue;

                Sprite sprite = null;
                Element element = compendium != null
                    ? compendium.GetEntityById<Element>(pair.Key)
                    : null;

                if (element != null)
                {
                    // Element.Icon falls back to the element id when unset, and mod sprites win
                    // inside ResourcesManager.
                    sprite = element.IsAspect
                        ? ResourcesManager.GetSpriteForAspect(element.Icon)
                        : ResourcesManager.GetSpriteForElement(element.Icon);
                }

                if (sprite == null)
                    sprite = ResourcesManager.GetSpriteForAspect(pair.Key);

                into.Add(new IconSpec { AspectId = pair.Key, Level = pair.Value, Sprite = sprite });
            }
        }

        /// Holds the row at a fixed size on screen as the camera pulls back, clamped at 1 below the
        /// reference so zooming in lets the icons grow with the room. The reference is the one knob
        /// for both when growth starts and how large it settles; see room-icons.md, "Zoom".
        public static float CurrentIconScale()
        {
            CamOperator cam = Watchman.Get<CamOperator>();
            if (cam == null)
                return 1f;

            float reference = Mathf.Abs((cam.ZOOM_Z_QUITE_CLOSE + cam.ZOOM_Z_MID) / 2);
            if (reference <= 0f)
                return 1f;

            return Mathf.Clamp(Mathf.Abs(cam.GetCurrentZoomHeight()) / reference, 1f, MaxIconScale);
        }

        /// The camera glides continuously rather than snapping between levels, so this compares
        /// against the scale actually applied and skips the fan-out until it drifts far enough to
        /// see.
        public static void TickZoomScale()
        {
            if (Live.Count == 0)
                return;

            float scale = CurrentIconScale();
            if (Mathf.Abs(scale - _appliedScale) < 0.01f)
                return;

            _appliedScale = scale;
            foreach (RoomOverlayView v in Live)
            {
                if (v != null)
                    v.SetIconScale(scale);
            }
        }

        /// `UpdateVisuals` fires only on change, so rooms present at world load never get an
        /// overlay without this sweep. Returns false until the world exists.
        public static bool TrySeedOnce()
        {
            var terrains = Resources.FindObjectsOfTypeAll<ConnectedTerrain>();
            if (terrains == null || terrains.Length == 0)
                return false;

            bool sawAny = false;
            foreach (ConnectedTerrain terrain in terrains)
            {
                // FindObjectsOfTypeAll also returns prefab assets; only scene objects matter.
                if (terrain == null || !terrain.gameObject.scene.IsValid())
                    continue;

                sawAny = true;
                Token token = terrain.GetToken();
                if (token == null)
                    continue;

                var host = token.GetManifestation() as MonoBehaviour;
                if (host != null)
                    RoomOverlayBuilder.BuildOrRefresh(host, terrain);
            }

            return sawAny;
        }

        /// `Compendium.Reload()` runs whenever the player closes the mod panel after a change, so
        /// cached requirements are dropped when content is republished.
        public static bool TryWireContentEventsOnce()
        {
            if (_contentEventsWired)
                return true;

            Concursum concursum = Watchman.Get<Concursum>();
            if (concursum == null || concursum.ContentUpdatedEvent == null)
                return false;

            concursum.ContentUpdatedEvent.AddListener(
                new UnityAction<ContentUpdatedArgs>(OnContentUpdated));
            _contentEventsWired = true;
            return true;
        }

        private static void OnContentUpdated(ContentUpdatedArgs args)
        {
            ClearCache();
            RefreshAll();
        }
    }
}
