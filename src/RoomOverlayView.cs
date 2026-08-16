using System.Collections.Generic;
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
        private const float DotSize = 3f;
        private const float DotMargin = 3f;
        private const float DividerWidth = 1f;
        private const float DividerMargin = 5f;
        private const float InsetX = 6f;
        private const float InsetY = 6f;
        private const int SortingOrder = 5;

        private static readonly Color ChipColor = new Color(0f, 0f, 0f, 0.85f);

        private static TMP_FontAsset _sharedFont;

        private GameObject _container;
        private readonly List<IconSlot> _icons = new List<IconSlot>();
        private readonly List<Image> _seps = new List<Image>();
        private int _builtIcons;
        private int _builtSeps;
        private string _cachedSig;
        private RoomReqs _reqs;

        private class IconSlot
        {
            public GameObject Go;
            public RectTransform Rect;
            public Image Image;
            public RectTransform Chip;
            public TextMeshProUGUI Label;
        }

        private void OnDestroy() => RoomOverlay.Unregister(this);

        public void Build(RoomReqs reqs)
        {
            RoomOverlay.Register(this);
            _reqs = reqs;

            if (reqs == null || reqs.IsEmpty)
            {
                _cachedSig = null;
                if (_container != null)
                    _container.SetActive(false);
                return;
            }

            if (reqs.Signature == _cachedSig && _container != null)
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
            RoomReqs fresh = _reqs;
            var terrain = GetComponentInParent<SecretHistories.Tokens.Payloads.TerrainFeature>();
            if (terrain != null)
                fresh = RoomOverlay.GetReqs(terrain);
            Build(fresh);
        }

        public void ApplyVisibility()
        {
            if (_container == null)
                return;

            bool show = RoomOverlay.IsVisible && _reqs != null && !_reqs.IsEmpty;
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
        }

        private void Layout(RoomReqs reqs)
        {
            int iconCount = reqs.Essential.Count + reqs.Required.Count;
            int sepCount = reqs.Required.Count > 0 ? reqs.Required.Count - 1 : 0;
            if (reqs.Essential.Count > 0 && reqs.Required.Count > 0)
                sepCount += 1;

            while (_builtIcons < iconCount)
                AddIconSlot(_builtIcons++);
            while (_builtSeps < sepCount)
                AddSeparator(_builtSeps++);

            float natural = MeasureNatural(reqs);
            float hostWidth = ((RectTransform)transform).sizeDelta.x;
            float scale = hostWidth <= 0f || natural <= hostWidth ? 1f : hostWidth / natural;

            float icon = IconSize * scale;
            float x = 0f;
            int iconIndex = 0;
            int sepIndex = 0;

            for (int i = 0; i < reqs.Essential.Count; i++)
            {
                x = PlaceIcon(iconIndex++, reqs.Essential[i], x, icon, scale, false);
                if (i < reqs.Essential.Count - 1)
                    x += IconGap * scale;
            }

            if (reqs.Essential.Count > 0 && reqs.Required.Count > 0)
            {
                x += DividerMargin * scale;
                PlaceSeparator(sepIndex++, x, DividerWidth * scale, icon * 0.75f, icon, 0.55f);
                x += DividerWidth * scale + DividerMargin * scale;
            }

            for (int i = 0; i < reqs.Required.Count; i++)
            {
                x = PlaceIcon(iconIndex++, reqs.Required[i], x, icon, scale, true);
                if (i < reqs.Required.Count - 1)
                {
                    x += DotMargin * scale;
                    PlaceSeparator(sepIndex++, x, DotSize * scale, DotSize * scale, icon, 0.75f);
                    x += DotSize * scale + DotMargin * scale;
                }
            }

            for (int i = iconIndex; i < _builtIcons; i++)
                _icons[i].Go.SetActive(false);
            for (int i = sepIndex; i < _builtSeps; i++)
                _seps[i].gameObject.SetActive(false);

            ((RectTransform)_container.transform).sizeDelta = new Vector2(x, icon);
        }

        private float MeasureNatural(RoomReqs reqs)
        {
            float w = reqs.Essential.Count * IconSize + reqs.Required.Count * IconSize;
            if (reqs.Essential.Count > 1)
                w += (reqs.Essential.Count - 1) * IconGap;
            if (reqs.Required.Count > 1)
                w += (reqs.Required.Count - 1) * (DotSize + DotMargin * 2f);
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

            if (slot.Chip != null && slot.Label != null)
            {
                bool showLevel = spec.Level > 1;
                slot.Chip.gameObject.SetActive(showLevel);
                if (showLevel)
                {
                    slot.Label.text = spec.Level.ToString();
                    slot.Label.fontSize = Mathf.Max(7f, 11f * scale);
                    slot.Label.ForceMeshUpdate(false, false);
                    ResizeChip(slot.Chip, slot.Label);
                }
            }

            return x + size;
        }

        // The chip hugs the glyphs, so it has to be sized after the text mesh is rebuilt.
        private static void ResizeChip(RectTransform chip, TextMeshProUGUI tmp) =>
            chip.sizeDelta = new Vector2(tmp.preferredWidth + 2f, tmp.preferredHeight);

        private void PlaceSeparator(int index, float x, float width, float height, float rowHeight, float alpha)
        {
            Image sep = _seps[index];
            var rect = (RectTransform)sep.transform;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, (rowHeight - height) * 0.5f);
            sep.color = new Color(1f, 1f, 1f, alpha);
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
            chip.sizeDelta = Vector2.zero;

            Image bg = chipGo.AddComponent<Image>();
            bg.color = ChipColor;
            bg.raycastTarget = false;

            var textGo = new GameObject("Value");
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.SetParent(chip, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(1f, 0f);
            textRect.offsetMax = new Vector2(-1f, 0f);

            var label = textGo.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 11f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;

            slot.Chip = chip;
            slot.Label = label;
        }

        private TMP_FontAsset ResolveFont()
        {
            if (_sharedFont != null)
                return _sharedFont;

            // Borrow whatever font the room art already uses, so the numbers match the game.
            var local = GetComponentInChildren<TextMeshProUGUI>(true);
            if (local != null && local.font != null)
            {
                _sharedFont = local.font;
                return _sharedFont;
            }

            var all = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            foreach (TextMeshProUGUI t in all)
            {
                if (t != null && t.font != null)
                {
                    _sharedFont = t.font;
                    return _sharedFont;
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
            rect.sizeDelta = new Vector2(DotSize, DotSize);

            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            _seps.Add(image);
        }
    }
}
