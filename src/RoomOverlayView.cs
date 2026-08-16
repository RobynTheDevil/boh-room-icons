using System.Collections.Generic;
using SecretHistories.Manifestations;
using SecretHistories.Tokens.Payloads;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoomIconsMod
{
    /// Draws a room's unlock requirements as a row of aspect icons, bottom-left of the room.
    /// Lives as a component on the RoomManifestation itself, so it survives repeat
    /// UpdateVisuals calls and dies with the room token.
    public class RoomOverlayView : MonoBehaviour
    {
        private const float IconSize = 24f;
        private const float IconGap = 2f;
        private const float DividerWidth = 1f;
        private const float DividerMargin = 2f;
        private const float InsetX = 6f;
        private const float InsetY = 6f;
        private const int SortingOrder = 5;

        private const float BaseFontSize = 11f;
        private const float MinFontSize = 7f;

        // Padding added around the measured glyphs. Didumos uses preferredWidth + 2 with no
        // vertical padding; these are the knobs for tightening or loosening that fit.
        private const float ChipPadX = 2f;
        private const float ChipPadY = 0f;

        // Fallback box, used only if the font exposes no metrics for these characters.
        private const float ChipHeight = 13f;
        private const float ChipDigitWidth = 7f;

        /// Cap the camera counter-scale per room so the row never grows wider than the
        /// room it labels. Set false to let icons keep their on-screen size regardless.
        private const bool ClampScaleToRoomWidth = true;

        private const float FontScanInterval = 1f;
        private const int FontFallbackAfterScans = 8;

        private static readonly Color ChipColor = new Color(0f, 0f, 0f, 0.85f);

        private static TMP_FontAsset _sharedFont;
        private static float _lastFontScan = -999f;
        private static int _fontScanAttempts;

        private GameObject _container;
        private readonly List<IconSlot> _icons = new List<IconSlot>();
        private readonly List<Image> _seps = new List<Image>();
        private int _builtIcons;
        private int _builtSeps;
        private string _cachedSig;
        private RoomReqs _reqs;
        private ConnectedTerrain _terrain;
        private bool _pendingAssets;
        private float _maxScale = float.MaxValue;

        private class IconSlot
        {
            public GameObject Go;
            public RectTransform Rect;
            public Image Image;
            public RectTransform Chip;
            public TextMeshProUGUI Label;
        }

        /// True while the font has not resolved yet, so the pump knows to keep retrying.
        public bool PendingAssets => _pendingAssets;

        private void OnDestroy() => RoomOverlay.Unregister(this);

        public void Build(ConnectedTerrain terrain, RoomReqs reqs)
        {
            RoomOverlay.Register(this);
            _terrain = terrain;
            _reqs = reqs;

            if (reqs == null || reqs.IsEmpty)
            {
                _cachedSig = null;
                if (_container != null)
                    _container.SetActive(false);
                return;
            }

            if (reqs.Signature == _cachedSig && _container != null && !_pendingAssets)
            {
                ApplyVisibility();
                return;
            }

            _cachedSig = reqs.Signature;
            if (_container == null)
                BuildContainer();

            Layout(reqs);
            ApplyVisibility();
        }

        public void Rebuild()
        {
            _cachedSig = null;
            Build(_terrain, _terrain != null ? RoomOverlay.GetReqs(_terrain) : _reqs);
        }

        /// Counter-scales the whole row against camera distance so it stays legible as the
        /// camera pulls back. Applied to the container, so icons, chips and the divider all
        /// scale together and the row stays pinned to the room's bottom-left corner.
        public void SetIconScale(float scale)
        {
            if (_container == null)
                return;
            float s = Mathf.Min(scale, _maxScale);
            _container.transform.localScale = new Vector3(s, s, 1f);
        }

        /// Lock state is re-read here rather than trusted from build time. Seeding runs as
        /// soon as the terrain objects exist, which can be before the save has been applied
        /// to them, and every room defaults to shrouded until then: without this re-check,
        /// rooms that are already open keep an overlay until something happens to fire
        /// UpdateVisuals on them again.
        public void ApplyVisibility()
        {
            if (_container == null)
                return;

            bool locked = _terrain != null && _terrain.IsShrouded && !_terrain.IsSealed;
            bool show = RoomOverlay.IsVisible && locked && _reqs != null && !_reqs.IsEmpty;
            // Never Image.enabled: UICullRoom owns that flag and will stomp it.
            _container.SetActive(show);
        }

        private void BuildContainer()
        {
            _container = new GameObject("RoomIcons_Overlay");
            RectTransform rect = _container.AddComponent<RectTransform>();
            rect.SetParent(transform, false);

            Vector2 bottomLeft = new Vector2(0f, 0f);
            rect.anchorMin = bottomLeft;
            rect.anchorMax = bottomLeft;
            rect.pivot = bottomLeft;
            rect.anchoredPosition = new Vector2(InsetX, InsetY);
            rect.sizeDelta = new Vector2(IconSize, IconSize);

            Canvas canvas = _container.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            // A room built while the camera is already pulled back must not pop in at 1x.
            SetIconScale(RoomOverlay.CurrentIconScale());
        }

        private void Layout(RoomReqs reqs)
        {
            int iconCount = reqs.Essential.Count + reqs.Required.Count;
            // One divider at most: the bar between the essential group and the required one.
            int sepCount = reqs.Essential.Count > 0 && reqs.Required.Count > 0 ? 1 : 0;

            _pendingAssets = false;

            while (_builtIcons < iconCount)
                AddIconSlot(_builtIcons++);
            while (_builtSeps < sepCount)
                AddSeparator(_builtSeps++);

            float natural = MeasureNatural(reqs);
            float hostWidth = HostWidth();
            float fit = hostWidth <= 0f || natural <= hostWidth ? 1f : hostWidth / natural;

            float icon = IconSize * fit;
            float x = 0f;
            int iconIndex = 0;
            int sepIndex = 0;

            for (int i = 0; i < reqs.Essential.Count; i++)
            {
                x = PlaceIcon(iconIndex++, reqs.Essential[i], x, icon, fit, false);
                if (i < reqs.Essential.Count - 1)
                    x += IconGap * fit;
            }

            if (reqs.Essential.Count > 0 && reqs.Required.Count > 0)
            {
                x += DividerMargin * fit;
                PlaceSeparator(sepIndex++, x, DividerWidth * fit, icon * 0.75f, icon);
                x += DividerWidth * fit + DividerMargin * fit;
            }

            for (int i = 0; i < reqs.Required.Count; i++)
            {
                x = PlaceIcon(iconIndex++, reqs.Required[i], x, icon, fit, true);
                if (i < reqs.Required.Count - 1)
                    x += IconGap * fit;
            }

            for (int i = iconIndex; i < _builtIcons; i++)
                _icons[i].Go.SetActive(false);
            for (int i = sepIndex; i < _builtSeps; i++)
                _seps[i].gameObject.SetActive(false);

            ((RectTransform)_container.transform).sizeDelta = new Vector2(x, icon);

            // The row is laid out at 1x and then multiplied by the camera counter-scale, so
            // the ceiling that keeps it inside the room is simply how many times its own
            // width fits across the room. Recomputed here because it moves with both the
            // requirement count and the room.
            _maxScale = ClampScaleToRoomWidth && hostWidth > 0f && x > 0f
                ? Mathf.Max(1f, hostWidth / x)
                : float.MaxValue;

            SetIconScale(RoomOverlay.CurrentIconScale());
        }

        private float HostWidth()
        {
            var rect = (RectTransform)transform;
            float w = rect.rect.width;
            return w > 0f ? w : rect.sizeDelta.x;
        }

        private float MeasureNatural(RoomReqs reqs)
        {
            float w = reqs.Essential.Count * IconSize + reqs.Required.Count * IconSize;
            if (reqs.Essential.Count > 1)
                w += (reqs.Essential.Count - 1) * IconGap;
            if (reqs.Required.Count > 1)
                w += (reqs.Required.Count - 1) * IconGap;
            if (reqs.Essential.Count > 0 && reqs.Required.Count > 0)
                w += DividerWidth + DividerMargin * 2f;
            return w;
        }

        private float PlaceIcon(int index, IconSpec spec, float x, float size, float scale, bool alternative)
        {
            IconSlot slot = _icons[index];
            slot.Rect.sizeDelta = new Vector2(size, size);
            slot.Rect.anchoredPosition = new Vector2(x, 0f);
            slot.Image.sprite = spec.Sprite;
            slot.Image.color = alternative ? new Color(1f, 1f, 1f, 0.92f) : Color.white;
            slot.Go.SetActive(true);

            if (slot.Chip == null)
                CreateValueChip(slot);

            if (slot.Chip == null)
                _pendingAssets = true;

            if (slot.Chip != null && slot.Label != null)
            {
                bool showLevel = spec.Level > 1;
                slot.Chip.gameObject.SetActive(showLevel);
                if (showLevel)
                {
                    string text = spec.Level.ToString();
                    float fontSize = Mathf.Max(MinFontSize, BaseFontSize * scale);
                    slot.Label.text = text;
                    slot.Label.fontSize = fontSize;
                    slot.Chip.sizeDelta = ChipSize(text, fontSize, scale);
                }
            }

            return x + size;
        }

        /// Measured from the font's own glyph metrics, so the chip hugs the number the way
        /// Didumos's does, but without TMP's preferredWidth. preferredWidth needs a rebuilt
        /// mesh and ForceMeshUpdate is a no-op while the object is inactive, which is how
        /// the chip previously ended up 0x0. Glyph metrics are static data on the font
        /// asset and need neither.
        private static Vector2 ChipSize(string text, float fontSize, float scale)
        {
            float w, h;
            if (!TryMeasure(text, fontSize, out w, out h))
            {
                w = ChipDigitWidth * text.Length * scale;
                h = ChipHeight * scale;
            }
            return new Vector2(w + ChipPadX * scale, h + ChipPadY * scale);
        }

        private static bool TryMeasure(string text, float fontSize, out float width, out float height)
        {
            width = 0f;
            height = 0f;

            TMP_FontAsset font = _sharedFont;
            if (font == null || font.faceInfo.pointSize <= 0)
                return false;

            float unit = fontSize / font.faceInfo.pointSize * font.faceInfo.scale;
            float bold = font.boldSpacing / 100f * fontSize;

            foreach (char c in text)
            {
                TMP_Character ch;
                if (!font.characterLookupTable.TryGetValue(c, out ch) || ch.glyph == null)
                    return false;
                width += ch.glyph.metrics.horizontalAdvance * unit + bold;
            }

            height = (font.faceInfo.ascentLine - font.faceInfo.descentLine) * unit;
            return width > 0f && height > 0f;
        }

        private void PlaceSeparator(int index, float x, float width, float height, float rowHeight)
        {
            Image sep = _seps[index];
            var rect = (RectTransform)sep.transform;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, (rowHeight - height) * 0.5f);
            sep.color = new Color(1f, 1f, 1f, 0.55f);
            sep.gameObject.SetActive(true);
        }


        private void AddIconSlot(int index)
        {
            var go = new GameObject("Icon" + index);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.SetParent(_container.transform, false);
            Vector2 bottomLeft = new Vector2(0f, 0f);
            rect.anchorMin = bottomLeft;
            rect.anchorMax = bottomLeft;
            rect.pivot = bottomLeft;
            rect.sizeDelta = new Vector2(IconSize, IconSize);

            Image image = go.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            var slot = new IconSlot { Go = go, Rect = rect, Image = image };
            CreateValueChip(slot);
            _icons.Add(slot);
            if (slot.Chip == null)
                _pendingAssets = true;
        }

        /// Dark chip in the icon's bottom-right corner with the level on top, matching how
        /// Didumos labels its aspect icons. A TMP outline is not an option here: the font
        /// asset is shared with the game's own text, and outlineWidth writes through to the
        /// shared material.
        private void CreateValueChip(IconSlot slot)
        {
            TMP_FontAsset font = ResolveFont();
            if (font == null)
                return;

            var chipGo = new GameObject("ValueChip");
            RectTransform chip = chipGo.AddComponent<RectTransform>();
            chip.SetParent(slot.Rect, false);
            Vector2 bottomRight = new Vector2(1f, 0f);
            chip.anchorMin = bottomRight;
            chip.anchorMax = bottomRight;
            chip.pivot = bottomRight;
            chip.anchoredPosition = Vector2.zero;
            chip.sizeDelta = ChipSize("0", BaseFontSize, 1f);

            Image bg = chipGo.AddComponent<Image>();
            bg.color = ChipColor;
            bg.raycastTarget = false;

            var textGo = new GameObject("Value");
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.SetParent(chip, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var label = textGo.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = BaseFontSize;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;

            slot.Chip = chip;
            slot.Label = label;
        }

        /// Same font Didumos labels its aspect icons with. Didumos takes the first
        /// TextMeshProUGUI under a CardManifestation (AspectOverlayView.BuildOverlay);
        /// rooms carry no text of their own, so borrow from a card the same way rather
        /// than grabbing whatever TMP happens to be first in the scene.
        private static TMP_FontAsset ResolveFont()
        {
            if (_sharedFont != null)
                return _sharedFont;

            // Cards may not exist yet when rooms are first seeded, so this has to be
            // retryable. Throttle the scan for the case where it never resolves.
            if (Time.unscaledTime - _lastFontScan < FontScanInterval)
                return null;
            _lastFontScan = Time.unscaledTime;

            _fontScanAttempts++;

            foreach (CardManifestation card in Resources.FindObjectsOfTypeAll<CardManifestation>())
            {
                if (card == null || !card.gameObject.scene.IsValid())
                    continue;

                var tmp = card.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null && tmp.font != null)
                {
                    _sharedFont = tmp.font;
                    return _sharedFont;
                }
            }

            // No card ever turned up: the hand can be empty, or the view can be somewhere
            // cards do not exist. Numbers in a near-enough font beat no numbers at all.
            if (_fontScanAttempts >= FontFallbackAfterScans)
            {
                foreach (TextMeshProUGUI tmp in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
                {
                    if (tmp != null && tmp.font != null && tmp.gameObject.scene.IsValid())
                    {
                        _sharedFont = tmp.font;
                        return _sharedFont;
                    }
                }
            }

            return null;
        }

        private void AddSeparator(int index)
        {
            var go = new GameObject("Sep" + index);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.SetParent(_container.transform, false);
            Vector2 bottomLeft = new Vector2(0f, 0f);
            rect.anchorMin = bottomLeft;
            rect.anchorMax = bottomLeft;
            rect.pivot = bottomLeft;
            rect.sizeDelta = new Vector2(DividerWidth, DividerWidth);

            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            _seps.Add(image);
        }
    }
}
