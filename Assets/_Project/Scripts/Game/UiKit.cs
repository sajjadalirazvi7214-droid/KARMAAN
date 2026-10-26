using System.Collections.Generic;
using UnityEngine;

namespace Suraksha.Game
{
    /// <summary>
    /// Font resolution with a fallback chain.
    ///
    /// Hindi needs a Devanagari face or every word renders as empty boxes. We bundle Noto Sans
    /// Devanagari (OFL) and fall back to an OS font, then to Unity's builtin. If all three fail
    /// the app is still playable but unreadable, which is why this logs loudly instead of
    /// silently drawing tofu.
    /// </summary>
    public static class Fonts
    {
        private static Font _devanagari;
        private static Font _olChiki;
        private static bool _warned;

        public static Font Devanagari
        {
            get
            {
                if (_devanagari == null) _devanagari = Resolve("Fonts/NotoSansDevanagari",
                    new[] { "Nirmala UI", "Mangal", "Noto Sans Devanagari", "Arial Unicode MS" });
                return _devanagari;
            }
        }

        public static Font OlChiki
        {
            get
            {
                if (_olChiki == null) _olChiki = Resolve("Fonts/NotoSansOlChiki",
                    new[] { "Noto Sans Ol Chiki", "Nirmala UI" });
                return _olChiki != null ? _olChiki : Devanagari;
            }
        }

        public static Font For(bool olChikiScript)
        {
            return olChikiScript ? OlChiki : Devanagari;
        }

        private static Font Resolve(string resourcePath, string[] osNames)
        {
            var bundled = Resources.Load<Font>(resourcePath);
            if (bundled != null) return bundled;

            var os = Font.CreateDynamicFontFromOSFont(osNames, 48);
            if (os != null)
            {
                Debug.LogWarning("[Fonts] Bundled font '" + resourcePath + "' missing, using OS font '" + os.name + "'.");
                return os;
            }

            if (!_warned)
            {
                _warned = true;
                Debug.LogError("[Fonts] No Devanagari-capable font found. Hindi text will not render correctly.");
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    /// <summary>
    /// A button that does its own hit-testing.
    ///
    /// Deliberately does not use UnityEngine.UI.Button + EventSystem. AR Foundation drags in the
    /// Input System package, and if the project flips to "new input only" the legacy
    /// StandaloneInputModule throws on every frame and no button works. Rect testing against a
    /// screen point sidesteps that entire class of failure.
    /// </summary>
    public sealed class UiButton : MonoBehaviour
    {
        public System.Action onClick;
        public Color idleColor = new Color(0.16f, 0.19f, 0.24f);
        public Color hoverColor = new Color(0.24f, 0.32f, 0.44f);
        public Color textColor = Color.white;
        public bool interactable = true;

        public UnityEngine.UI.Image background;
        public UnityEngine.UI.Text label;

        private RectTransform _rect;
        private bool _hover;
        private static readonly List<UiButton> Active = new List<UiButton>();

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void Update()
        {
            if (_rect == null || !interactable)
            {
                Apply(false);
                return;
            }

            var over = Contains(InputBridge.PointerPosition);
            Apply(over);

            if (over && InputBridge.TapUp)
            {
                ToneBank.Click();
                if (onClick != null) onClick();
            }
        }

        private bool Contains(Vector3 screenPoint)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(_rect, screenPoint, null);
        }

        private void Apply(bool over)
        {
            if (_hover == over) return;
            _hover = over;

            if (background != null)
            {
                background.color = over ? hoverColor : idleColor;
            }

            if (label != null)
            {
                label.color = over ? Color.white : textColor;
            }
        }

        /// <summary>Pure geometry query — no Update ordering assumptions.</summary>
        public static bool IsPointerOverButton(Vector3 screenPoint)
        {
            for (var i = 0; i < Active.Count; i++)
            {
                var button = Active[i];
                if (button == null || !button.isActiveAndEnabled || !button.interactable) continue;
                if (button.Contains(screenPoint)) return true;
            }
            return false;
        }

        public static void ClearRegistry()
        {
            Active.Clear();
        }
    }

    /// <summary>Small helpers for building uGUI at runtime without prefabs.</summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.94f, 0.95f, 0.96f);
        public static readonly Color Muted = new Color(0.62f, 0.66f, 0.72f);
        public static readonly Color Card = new Color(0.10f, 0.12f, 0.15f, 0.96f);
        public static readonly Color Go = new Color(0.10f, 0.62f, 0.34f);
        public static readonly Color Stop = new Color(0.72f, 0.16f, 0.16f);
        public static readonly Color Warn = new Color(0.85f, 0.60f, 0.10f);

        public static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static UnityEngine.UI.Image Panel(Transform parent, string name, Color color)
        {
            var rect = Node(parent, name);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static UnityEngine.UI.Text Label(Transform parent, string name, string text, int size,
            Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool olChiki = false)
        {
            var rect = Node(parent, name);
            var label = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            label.font = Fonts.For(olChiki);
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = true;
            return label;
        }

        public static UiButton Button(Transform parent, string name, string text, Vector2 anchor,
            Vector2 position, Vector2 size, System.Action onClick, bool olChiki = false)
        {
            var rect = Node(parent, name);
            Place(rect, anchor, new Vector2(0.5f, 0.5f), position, size);

            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.16f, 0.19f, 0.24f);
            image.raycastTarget = false;

            var label = Label(rect, "Label", text, 40, Color.white, TextAnchor.MiddleCenter, olChiki);
            Stretch((RectTransform)label.transform, 12f, 6f, 12f, 6f);

            var button = rect.gameObject.AddComponent<UiButton>();
            button.background = image;
            button.label = label;
            button.onClick = onClick;

            return button;
        }

        /// <summary>Blanks the label and drops the click handler. Used while a screen is loading.</summary>
        public static void Kill(UiButton button)
        {
            if (button == null) return;
            button.interactable = false;
            button.onClick = null;
            UnityEngine.Object.Destroy(button.gameObject);
        }

        public static void Clear(Transform parent)
        {
            if (parent == null) return;

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
            }
        }

        private static Sprite _white;
        private static Sprite _circle;

        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;

                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color32[16];
                for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels);
                texture.Apply();

                _white = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
                return _white;
            }
        }

        /// <summary>Antialiased disc, generated once. The reticle needs a circle and we ship no sprites.</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;

                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                var centre = (size - 1) * 0.5f;

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var distance = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre));
                        var alpha = Mathf.Clamp01((centre - 2f) - distance);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();

                _circle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
                return _circle;
            }
        }

        public static UnityEngine.UI.Image Image(Transform parent, string name, Color color,
            Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = Node(parent, name);
            Place(rect, anchor, new Vector2(0.5f, 0.5f), position, size);

            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;

            return image;
        }
    }
}
