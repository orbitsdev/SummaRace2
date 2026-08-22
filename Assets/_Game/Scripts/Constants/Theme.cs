using UnityEngine;

namespace SummaRace.Constants
{
    /// <summary>
    /// The UI palette, in one place — the colour counterpart to <see cref="GameText"/> (strings)
    /// and <see cref="GameRules"/> (numbers).
    ///
    /// WHY THIS EXISTS. Measured 2026-08-19 across the live UI scripts: **84 colour literals
    /// holding 58 distinct RGB values**, for what is really about a dozen intended colours. The
    /// brand gold alone appeared as **ten different values, nine of them used exactly once** —
    /// nobody was reusing anything, so every new screen invented its own gold. Several emergent
    /// conventions did exist (navy 1C2B54 five times, brown 593F19 four times); they simply had
    /// no name, so nothing propagated them and nothing stopped the next drift.
    ///
    /// The values below are not new inventions — each is the shade already used most often for
    /// that intent, so naming them changed the look as little as possible.
    ///
    /// RULE: reach for a token here before writing `new Color(...)` in a UI script. A genuinely
    /// one-off colour (a per-world sky, a debug tint) is still fine as a literal — see the
    /// deliberate exclusions at the bottom.
    /// </summary>
    public static class Theme
    {
        // ---- Brand -----------------------------------------------------------------------
        /// <summary>Deep gold. Progress bars, the briefing's title pill, filled meters.</summary>
        public static readonly Color GoldDeep = new Color(1f, 0.78f, 0.18f);

        /// <summary>Bright gold. Headline type, highlights, "this is the answer" emphasis.</summary>
        public static readonly Color Gold = new Color(1f, 0.85f, 0.35f);

        /// <summary>Warm story gold. The race's collectible sparkle and FX tint (was StoryGold).</summary>
        public static readonly Color StoryGold = new Color(1f, 0.85f, 0.45f);

        /// <summary>Amber. A state that needs attention but is NEVER a punishment — a slot in the
        /// wrong place, a chip waiting to be earned. Deliberately not red: D7, never punish.</summary>
        public static readonly Color AmberWarn = new Color(0.96f, 0.69f, 0.26f);

        // ---- Surfaces --------------------------------------------------------------------
        /// <summary>Card interiors and popup bodies.</summary>
        public static readonly Color Cream = new Color(0.976f, 0.929f, 0.800f);

        /// <summary>Off-white for type on dark, and for panels that must read as paper.</summary>
        public static readonly Color Paper = new Color(0.969f, 0.969f, 1f);

        // ---- Type ------------------------------------------------------------------------
        /// <summary>Body copy on cream. Warm brown rather than black — softer for a child.</summary>
        public static readonly Color TextBrown = new Color(0.35f, 0.247f, 0.098f);

        /// <summary>Headings on cream, where TextBrown lacks weight.</summary>
        public static readonly Color TextBrownDeep = new Color(0.24f, 0.157f, 0.086f);

        /// <summary>
        /// PANEL FILL. Warm mid-brown wood — the colour a card, banner or plaque is PAINTED,
        /// as opposed to <see cref="TextBrownDeep"/>, which is a colour text is WRITTEN in.
        ///
        /// WHY THIS EXISTS (owner, 2026-08-22: <i>"the dark colour you use is not good, in the
        /// eye or in the context of friendly theme game — too dark"</i>). The 2026-08-22 pass
        /// took the app's furniture off saturated kit yellow, which was right, and then painted
        /// it all <c>TextBrownDeep</c>, which was wrong: that token was authored as a HEADING
        /// colour for dark text on cream, and at luminance 0.026 it is very nearly black. Used
        /// as a fill, against this game's bright playground art, ten of them read as holes
        /// punched in the screen — the Session Map in particular looked entirely disabled.
        ///
        /// This is <b>3.6x lighter</b> (L 0.026 -> 0.096) and visibly wood rather than
        /// near-black, while still carrying cream type at <b>6.6:1</b> and gold at 5.2:1 — both
        /// past WCAG AA for the sizes used. Warm rather than grey, because every other surface
        /// this game owns is warm and a neutral dark reads as "system UI", not "toy".
        ///
        /// ⚠️ Two different jobs, two different tokens. Do not merge them: TextBrownDeep is
        /// still correct for TEXT on cream (11.9:1) and would fail as a fill; Wood is correct as
        /// a FILL and would be too light to write body text in.
        /// </summary>
        public static readonly Color Wood = new Color(0.46f, 0.31f, 0.17f);

        /// <summary>A step lighter again, for a plaque that has to sit ON Wood and still be
        /// seen as a separate object (the tracker's current slot, a chip on a card).</summary>
        public static readonly Color WoodLight = new Color(0.56f, 0.40f, 0.22f);

        // ---- Depth -----------------------------------------------------------------------
        /// <summary>Navy. Progress pills, HUD backings, the loading bar's trough.</summary>
        public static readonly Color Navy = new Color(0.11f, 0.169f, 0.33f);

        /// <summary>Near-black for panel backings. Not pure black — pure black on an OLED
        /// tablet reads as a hole punched in the screen.</summary>
        public static readonly Color Ink = new Color(0.098f, 0.118f, 0.157f);

        /// <summary>Cool grey-blue for secondary type and locked-card labels.</summary>
        public static readonly Color Slate = new Color(0.20f, 0.278f, 0.318f);

        /// <summary>Deep green. Confirm rings and the "correct" state on light surfaces.</summary>
        public static readonly Color GreenDeep = new Color(0.118f, 0.42f, 0.176f);

        // ---- Contrast, measured 2026-08-19 (WCAG: 4.5 = body text AA, 3.0 = large/bold AA) ----
        // Safe for TEXT — all pass AA comfortably:
        //   TextBrown/Cream 8.4 · TextBrownDeep/Cream 11.9 · TextBrown/Paper 9.2
        //   Paper/Navy 12.9 · White/Navy 13.8 · Gold/Navy 10.0 · GoldDeep/Navy 8.8
        //   Paper/Ink 15.7 · Gold/Ink 12.2 · Slate/Paper 9.1 · Slate/Cream 8.3 · GreenDeep/Cream 5.6
        //
        // NEVER use as text — gold on cream is a fill combination, not a type one:
        //   Gold/Cream 1.2 · GoldDeep/Cream 1.3 · AmberWarn/Cream 1.6  (4.5 is the floor)
        // A gold pill ON a cream card is fine; gold LETTERING on cream is invisible to a child
        // on a classroom tablet at arm's length. Put gold type on Navy or Ink instead.

        /// <summary>Same colour, different alpha — Unity has no Color.WithAlpha.</summary>
        public static Color Alpha(Color c, float a) { c.a = a; return c; }

        // ---- Deliberately NOT here -------------------------------------------------------
        // SwbstPalette owns the five element colours (S/W/B/S/T); it is a teaching device, not
        // decoration, and must not be re-tinted to suit a screen.
        // RaceWorlds owns the ten worlds' sun / fog / ambient / sky. Those are per-world DATA
        // whose whole job is to differ, so a shared token would be exactly wrong.
        // RaceController.cs (legacy Race.unity, zero call sites) was left untouched on purpose —
        // migrating dead code is churn with no upside.
    }
}
