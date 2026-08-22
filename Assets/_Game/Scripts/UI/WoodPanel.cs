using UnityEngine;
using UnityEngine.UI;
using SummaRace.Constants;

namespace SummaRace.UI
{
    /// <summary>
    /// THE GAME'S CARD: a dark wood frame around a cream reading face.
    ///
    /// Owner device playtest 2026-08-21, callout anchored over the race briefing screenshot:
    /// "instead of yellow border you can make it gray or brown like back button gray or some
    /// our brown, more like game feel rather than colour yellow... if you can improve this
    /// scene or panel please improve."
    ///
    /// The race cards (mission briefing, pause, leave confirmation) were all on Resources
    /// "UI/panel_gold" - a gold-bordered card with a yellow title pill over it. Tinting that
    /// sprite brown was not available: one Image cannot darken the border without darkening the
    /// cream interior with it, and the body text on these cards is Theme.TextBrown, which on a
    /// browned interior stops being readable. So the frame and the face become two graphics: the
    /// outer takes the wood the race HUD already speaks (tracker board, feedback pill, pause
    /// chip, gate chip), the inner keeps the cream.
    ///
    /// WHY IT LIVES HERE RATHER THAN IN THE RACE (owner, 2026-08-22: <i>"why you use simple flat
    /// card in confirms when leave? consider as well the consistency of game interface"</i>).
    /// This was a private static inside EndlessRaceDirector, so the race's three cards had it and
    /// the app-wide BACK confirmation - which lives on [Core] and cannot reach into a feature -
    /// did not. That confirmation drew a bare untextured rectangle with flat-colour buttons, and
    /// a learner meets it on TOP of a screen already using this panel, so the two were visibly
    /// different games one over the other. Copying the generator into Core instead would have let
    /// the two drift the first time either was retuned, which is the exact disease
    /// <see cref="Theme"/> exists to cure.
    ///
    /// Deliberately NOT applied to the gold title lockups on Boot, Main Menu, Story Select and
    /// Results: that gold is the app's brand across five screens and the owner's note is about
    /// panels. One place to change it back if that call is ever revisited.
    /// </summary>
    public static class WoodPanel
    {
        /// <summary>
        /// Adds the frame+face pair to <paramref name="card"/> and returns the FRAME image, so
        /// the caller positions one RectTransform and the face follows.
        ///
        /// The inner face is added FIRST so uGUI draws it behind everything the caller adds
        /// afterwards, and it is not a raycast target, so nothing about hit-testing changes.
        /// </summary>
        public static Image Build(GameObject card)
        {
            var frame = card.AddComponent<Image>();
            frame.sprite = Plaque();
            frame.type = Image.Type.Sliced;
            frame.color = Theme.Wood;

            var inner = new GameObject("Inner");
            inner.transform.SetParent(card.transform, false);
            var innerImg = inner.AddComponent<Image>();
            innerImg.sprite = Plaque();
            innerImg.type = Image.Type.Sliced;
            innerImg.color = Theme.Cream;
            innerImg.raycastTarget = false;
            var irt = innerImg.rectTransform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(18f, 18f); irt.offsetMax = new Vector2(-18f, -18f);
            return frame;
        }

        /// <summary>Light beveled wooden plaque (tintable, 9-sliced). Generated once: a warm
        /// cream base with a raised frame, a recessed centre, and subtle grain — so tinting it
        /// with an SWBST colour reads as a coloured wooden block.
        /// <para>
        /// Statically cached and never freed: it backs the tracker plaques, every card built by
        /// <see cref="Build"/> and the confirmation buttons, so it outlives any one scene. Do not
        /// pass it to a "free this generated sprite" helper — see
        /// EndlessRaceDirector.FreeGeneratedSprite, which calls this one out by name.
        /// </para></summary>
        private static Sprite _woodPlaque;
        public static Sprite Plaque()
        {
            if (_woodPlaque != null) return _woodPlaque;
            const int S = 100, R = 22, FRAME = 10;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float half = S * 0.5f;
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float px = x - (S - 1) * 0.5f, py = y - (S - 1) * 0.5f;
                float qx = Mathf.Abs(px) - (half - R);
                float qy = Mathf.Abs(py) - (half - R);
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                float dist = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - R; // <=0 inside
                float depth = -dist;
                if (depth <= 0f) { tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f)); continue; }

                float grain = (Mathf.PerlinNoise(x * 0.12f, y * 0.5f) - 0.5f) * 0.10f;
                float lum;
                if (depth < FRAME) lum = 0.86f + (FRAME - depth) / FRAME * 0.10f; // raised bright frame
                else lum = 0.66f + (y / (float)S) * 0.10f;                        // recessed centre, top a touch darker
                lum = Mathf.Clamp01(lum + grain);
                // warm cream tint so an SWBST colour multiply still shows through
                tex.SetPixel(x, y, new Color(lum, lum * 0.93f, lum * 0.80f, 1f));
            }
            tex.Apply();
            _woodPlaque = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(R, R, R, R));
            return _woodPlaque;
        }
    }
}
