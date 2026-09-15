using SummaRace.Constants;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.UI
{
    /// <summary>
    /// The shared "toy" look for the learning screens (client feedback 2026-09-14: the Reader,
    /// Arrange and Summary read as a survey — pastel fills, hairline borders, small regular
    /// type — while the title screens and the race look like a game).
    ///
    /// Three ingredients do most of the work in every casual mobile game, and each is one call
    /// here so the screens cannot drift apart again:
    /// <list type="bullet">
    /// <item><b>Chunky</b>: a saturated fill, a thick dark outline and a drop shadow, so a card
    /// reads as a physical object you can press rather than a form field.</item>
    /// <item><b>Heading</b>: Fredoka with its outlined Button material, so interface words look
    /// like game type, not document type.</item>
    /// <item><b>Badge</b>: a round coloured token (letters, counts) instead of plain text.</item>
    /// </list>
    /// Everything is additive and null-safe: a missing sprite or font falls back to what the
    /// scene already has, never to an error.
    /// </summary>
    public static class GameSkin
    {
        private static TMP_FontAsset _heading;
        private static Material _headingOutlined;
        private static TMP_FontAsset _bodyBold;
        private static Sprite _roundSprite;

        /// <summary>Fredoka SemiBold, found among loaded assets (every scene's buttons use it).</summary>
        public static TMP_FontAsset HeadingFont
        {
            get
            {
                if (_heading == null) _heading = FindFont("Fredoka-SemiBold SDF");
                return _heading;
            }
        }

        public static TMP_FontAsset BodyBoldFont
        {
            get
            {
                if (_bodyBold == null) _bodyBold = FindFont("Nunito-Bold SDF");
                return _bodyBold;
            }
        }

        private static Material HeadingOutlined
        {
            get
            {
                if (_headingOutlined != null) return _headingOutlined;
                foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
                    if (m != null && m.name == "Fredoka-SemiBold SDF Button") { _headingOutlined = m; break; }
                return _headingOutlined;
            }
        }

        private static TMP_FontAsset FindFont(string name)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (f != null && f.name == name) return f;
            return null;
        }

        /// <summary>A soft circle generated once (no asset), for badges and sparkles.</summary>
        public static Sprite RoundSprite
        {
            get
            {
                if (_roundSprite != null) return _roundSprite;
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = Mathf.Clamp01(r - d);       // 1px anti-aliased edge
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
                tex.Apply();
                _roundSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _roundSprite;
            }
        }

        private static Sprite _roundedRect;

        /// <summary>
        /// A pure-white rounded rectangle, 9-sliced, generated once. The kit sprites carry their
        /// own tint (Rectangle 356 is lilac, chip_tan is tan), and Image.color MULTIPLIES the
        /// sprite — so "white" answers came out lilac and "sunny" banners came out mustard. A
        /// neutral sprite makes every fill exactly the colour asked for.
        /// </summary>
        public static Sprite RoundedRect
        {
            get
            {
                if (_roundedRect != null) return _roundedRect;
                const int size = 96; const float radius = 30f;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float cx = Mathf.Clamp(px, radius, size - radius);
                    float cy = Mathf.Clamp(py, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    // A faint top highlight gives the "glossy toy" read without an art asset.
                    float shade = py > size * 0.72f ? 1f : 0.94f + 0.06f * (py / (size * 0.72f));
                    tex.SetPixel(x, y, new Color(shade, shade, shade, a));
                }
                tex.Apply();
                _roundedRect = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                    0, SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
                return _roundedRect;
            }
        }

        /// <summary>A chunky card on the neutral rounded sprite: exact fill colour + outline + shadow.</summary>
        public static void Card(Image img, Color fill, float outline = 5f, float shadow = 7f)
        {
            if (img == null) return;
            img.sprite = RoundedRect;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            Chunky(img, fill, outline, shadow);
        }

        /// <summary>
        /// Buttons use ColorTint, whose disabled colour (0.78 grey, half alpha) MULTIPLIES the
        /// fill — every "not this one" card went dark and muddy. Keep disabled cards light.
        /// </summary>
        public static void FriendlyDisabledTint(Button b)
        {
            if (b == null) return;
            var c = b.colors;
            c.disabledColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            b.colors = c;
        }

        /// <summary>
        /// Saturated fill + thick dark outline + drop shadow. Idempotent: calling it again only
        /// updates the colours, so a state change (normal → correct) is one call.
        /// </summary>
        public static void Chunky(Graphic g, Color fill, float outline = 5f, float shadow = 7f, Color? outlineColor = null)
        {
            if (g == null) return;
            g.color = fill;

            var o = g.GetComponent<Outline>();
            if (outline > 0f)
            {
                if (o == null) o = g.gameObject.AddComponent<Outline>();
                o.effectColor = outlineColor ?? Theme.Alpha(Theme.Outline, 0.9f);
                o.effectDistance = new Vector2(outline, -outline);
                o.useGraphicAlpha = true;
            }
            else if (o != null) o.enabled = false;

            // Shadow is a base class of Outline, so look for an exact Shadow component.
            Shadow s = null;
            foreach (var c in g.GetComponents<Shadow>())
                if (c.GetType() == typeof(Shadow)) { s = c; break; }
            if (shadow > 0f)
            {
                if (s == null) s = g.gameObject.AddComponent<Shadow>();
                s.effectColor = Theme.Alpha(Color.black, 0.35f);
                s.effectDistance = new Vector2(0f, -shadow);
                s.useGraphicAlpha = true;
            }
            else if (s != null) s.enabled = false;
        }

        /// <summary>Game heading type: Fredoka, outlined, in the given colour.</summary>
        public static void Heading(TMP_Text t, Color face, bool outlined = true)
        {
            if (t == null) return;
            if (HeadingFont != null) t.font = HeadingFont;
            if (outlined && HeadingOutlined != null) t.fontSharedMaterial = HeadingOutlined;
            else if (HeadingFont != null) t.fontSharedMaterial = HeadingFont.material;
            t.color = face;
        }

        /// <summary>Readable bold body type (answers, story parts) — Nunito Bold, no outline.</summary>
        public static void BodyBold(TMP_Text t, Color face)
        {
            if (t == null) return;
            if (BodyBoldFont != null) { t.font = BodyBoldFont; t.fontSharedMaterial = BodyBoldFont.material; }
            t.color = face;
        }

        /// <summary>
        /// A round coloured token with centred text, parented under <paramref name="parent"/>.
        /// Returns the label so callers can change the text later.
        /// </summary>
        public static TMP_Text Badge(Transform parent, string name, string text, Color fill, Color ink,
                                     Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, float diameter)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
            }

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(diameter, diameter);

            var img = go.GetComponent<Image>(); if (img == null) img = go.AddComponent<Image>();
            img.sprite = RoundSprite;
            img.raycastTarget = false;
            Chunky(img, fill, 3f, 3f);

            var labelT = go.transform.Find("Label");
            TMP_Text label;
            if (labelT != null) label = labelT.GetComponent<TMP_Text>();
            else
            {
                var lgo = new GameObject("Label", typeof(RectTransform));
                lgo.transform.SetParent(go.transform, false);
                label = lgo.AddComponent<TextMeshProUGUI>();
                var lrt = label.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            }
            Heading(label, ink, outlined: false);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = diameter * 0.6f;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }
    }
}
