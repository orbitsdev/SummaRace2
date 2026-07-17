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

        /// <summary>Hex string (no #) for TMP rich text tags.</summary>
        public static string HexForIndex(int index) =>
            ColorUtility.ToHtmlStringRGB(ForIndex(index));
    }
}
