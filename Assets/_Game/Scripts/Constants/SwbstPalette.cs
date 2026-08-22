using UnityEngine;

namespace SummaRace.Constants
{
    /// <summary>
    /// The SWBST framework's five signature colors — one visual language across
    /// the briefing chips, race gate pills, Arrange slots and Summary reference.
    /// </summary>
    public static class SwbstPalette
    {
        public static readonly Color Somebody = new Color(0.30f, 0.55f, 0.95f); // blue
        public static readonly Color Wanted   = new Color(0.30f, 0.75f, 0.40f); // green
        public static readonly Color But      = new Color(0.93f, 0.35f, 0.35f); // red
        public static readonly Color So       = new Color(0.98f, 0.62f, 0.15f); // orange
        public static readonly Color Then     = new Color(0.65f, 0.45f, 0.90f); // purple

        private static readonly Color[] ByIndex = { Somebody, Wanted, But, So, Then };

        /// <summary>Element index 0-4 in S-W-B-S-T order.</summary>
        public static Color ForIndex(int index) =>
            index >= 0 && index < ByIndex.Length ? ByIndex[index] : Color.white;

        /// <summary>Soft background tint of the element color (readable behind dark text).</summary>
        public static Color PastelForIndex(int index) =>
            Color.Lerp(Color.white, ForIndex(index), 0.30f);

        /// <summary>Darkened variant for text on light backgrounds.</summary>
        public static Color DeepForIndex(int index) =>
            Color.Lerp(ForIndex(index), Color.black, 0.30f);

        /// <summary>
        /// How far the ink variant is pulled toward black. 0.45 rather than Deep's 0.30
        /// because Deep is not dark enough to READ at body size: measured against the cream
        /// interior of the kit's "Daily Reward pannel" (0.971, 0.923, 0.829 — the Summary
        /// reference card and the Reader's question card are both on it), Deep leaves
        /// WANTED at 3.84:1 and SO at 3.50:1, under the 4.5:1 WCAG AA needs for normal text.
        /// At 0.45 the five land at 7.13 / 5.61 / 7.31 / 5.18 / 7.25:1 and every hue is
        /// still plainly itself — the palette has to keep teaching the framework, so this
        /// darkens toward the element's own colour rather than replacing it.
        /// </summary>
        private const float InkDarken = 0.45f;

        /// <summary>
        /// Ink variant — the element colour used as BODY TEXT on a light card, where Deep
        /// is a background/large-label colour. Kept separate rather than deepening
        /// <see cref="DeepForIndex"/> in place: Deep is what Arrange paints its empty-slot
        /// labels with, over the matching pastel fill, and that pairing was measured and
        /// signed off at its current values. One helper per background, not one helper
        /// stretched over two.
        /// </summary>
        public static Color InkForIndex(int index) =>
            Color.Lerp(ForIndex(index), Color.black, InkDarken);

        /// <summary>Hex string (no #) for TMP rich text tags.</summary>
        public static string HexForIndex(int index) =>
            ColorUtility.ToHtmlStringRGB(ForIndex(index));

        /// <summary>Hex string (no #) of <see cref="InkForIndex"/>, for TMP rich text tags
        /// that colour a word inside a longer line of body text.</summary>
        public static string InkHexForIndex(int index) =>
            ColorUtility.ToHtmlStringRGB(InkForIndex(index));

        /// <summary>
        /// Hex string (no #) of <see cref="PastelForIndex"/> - the light-on-DARK counterpart of
        /// <see cref="InkHexForIndex"/>, for a coloured word inside a line of body text that
        /// sits on one of this game's dark plaques rather than on cream.
        ///
        /// Measured against the race's question plaque (Theme.Ink at 0.86 alpha) over the three
        /// backgrounds the road can actually supply - pure ink, a mid world, a bright sky - the
        /// five pastels land at 8.4:1 to 13.4:1. The FULL palette colours over that same plaque
        /// fall to 3.55-3.65:1 in the bright-sky case, which clears AA only for large text and
        /// only just. Pastel on dark, Ink on cream; measure before inventing a third.
        /// </summary>
        public static string PastelHexForIndex(int index) =>
            ColorUtility.ToHtmlStringRGB(PastelForIndex(index));
    }
}
