using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Reef
{
    /// <summary>
    /// Small helpers for building world-space UI in code (the recipe game's screens).
    /// Everything is built at runtime, so there are no prefabs to maintain.
    /// </summary>
    public static class ReefUI
    {
        static Sprite s_Rounded, s_Box, s_Check;
        static Material s_OverlayUI;

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 0;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Fixed-size rect anchored at (ax, ay) of the parent, positioned by its pivot.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        /// <summary>A world-space canvas root, drawn on top of the reef (so rocks and fish can't cut through it).</summary>
        public static Canvas MakeCanvas(string name, Transform parent, Vector2 sizeUnits, int sortingOrder)
        {
            var rt = NewRect(name, parent);
            var canvas = rt.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            rt.sizeDelta = sizeUnits;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return canvas;
        }

        public static Image MakeImage(string name, Transform parent, Color color, Sprite sprite = null, bool sliced = false)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            img.material = OverlayUIMaterial();
            if (sprite != null)
            {
                img.sprite = sprite;
                if (sliced) img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static Image MakePanel(string name, Transform parent, Color color, float cornerScale = 0.6f)
        {
            var img = MakeImage(name, parent, color, RoundedSprite(), true);
            img.pixelsPerUnitMultiplier = cornerScale;
            return img;
        }

        public static RawImage MakeRawImage(string name, Transform parent)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            img.material = OverlayUIMaterial();
            return img;
        }

        public static TextMeshProUGUI MakeText(string name, Transform parent, TMP_FontAsset font, float size, FontStyles style, Color color,
                                               TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            MakeTextOverlay(t);
            return t;
        }

        /// <summary>Show a texture filling a square frame (crops the long side so nothing is stretched).</summary>
        public static void SetCover(RawImage img, Texture tex)
        {
            img.texture = tex;
            img.enabled = tex != null;
            if (tex == null) return;
            var r = img.rectTransform.rect;
            float frameAspect = r.height > 1f ? r.width / r.height : 1f;
            float texAspect = tex.height > 0 ? tex.width / (float)tex.height : 1f;
            if (texAspect > frameAspect)
            {
                float w = frameAspect / texAspect;
                img.uvRect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            else
            {
                float h = texAspect / frameAspect;
                img.uvRect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
            }
        }

        // ------------------------------------------------------------------ draw-on-top materials

        /// <summary>UI/Default with depth testing off, so the panel is never hidden by scenery.</summary>
        public static Material OverlayUIMaterial()
        {
            if (s_OverlayUI != null) return s_OverlayUI;
            var sh = Shader.Find("UI/Default");
            if (sh == null) return null;
            s_OverlayUI = new Material(sh) { name = "ReefOverlayUI" };
            s_OverlayUI.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            return s_OverlayUI;
        }

        static readonly System.Collections.Generic.Dictionary<Material, Material> s_TextOverlay =
            new System.Collections.Generic.Dictionary<Material, Material>();

        public static void MakeTextOverlay(TMP_Text t)
        {
            var src = t.fontSharedMaterial;
            if (src == null) return;
            if (!s_TextOverlay.TryGetValue(src, out var m) || m == null)
            {
                m = new Material(src) { name = src.name + " (Reef overlay)" };
                m.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                s_TextOverlay[src] = m;
            }
            t.fontSharedMaterial = m;
        }

        // ------------------------------------------------------------------ generated sprites

        /// <summary>A rounded-rectangle sprite, used 9-sliced for cards and buttons.</summary>
        public static Sprite RoundedSprite()
        {
            if (s_Rounded != null) return s_Rounded;
            const int size = 64;
            const float radius = 24f;
            s_Rounded = Generate("ReefRounded", size, (x, y) =>
            {
                float cx = Mathf.Clamp(x, radius, size - radius);
                float cy = Mathf.Clamp(y, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                return Mathf.Clamp01(radius - d + 0.5f);
            }, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
            return s_Rounded;
        }

        /// <summary>An empty rounded square outline (unchecked box).</summary>
        public static Sprite BoxSprite()
        {
            if (s_Box != null) return s_Box;
            const int size = 64;
            const float radius = 14f, inset = 4f, thickness = 5f;
            s_Box = Generate("ReefBox", size, (x, y) =>
            {
                float lo = inset + radius, hi = size - inset - radius;
                float cx = Mathf.Clamp(x, lo, hi), cy = Mathf.Clamp(y, lo, hi);
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - radius; // signed distance to the rounded square
                return Mathf.Clamp01(thickness * 0.5f - Mathf.Abs(d + thickness * 0.5f) + 0.5f);
            }, Vector4.zero);
            return s_Box;
        }

        /// <summary>A bold check mark.</summary>
        public static Sprite CheckSprite()
        {
            if (s_Check != null) return s_Check;
            const int size = 64;
            Vector2 a = new Vector2(14, 33), b = new Vector2(27, 19), c = new Vector2(52, 48);
            const float half = 5.5f;
            s_Check = Generate("ReefCheck", size, (x, y) =>
            {
                var p = new Vector2(x, y);
                float d = Mathf.Min(SegmentDistance(p, a, b), SegmentDistance(p, b, c));
                return Mathf.Clamp01(half - d + 0.5f);
            }, Vector4.zero);
            return s_Check;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static Sprite Generate(string name, int size, System.Func<float, float, float> alphaAt, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(alphaAt(x + 0.5f, y + 0.5f) * 255f));
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.name = name;
            return s;
        }
    }
}
