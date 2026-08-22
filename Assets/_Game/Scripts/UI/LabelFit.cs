using TMPro;

namespace SummaRace.UI
{
    /// <summary>
    /// Makes a label survive content longer than it was laid out for.
    ///
    /// THE SYSTEMIC DEFECT THIS EXISTS FOR. Counted across the ten authored scenes on
    /// 2026-08-22, <b>33 of 89 TMP labels have autosize OFF, and all 89 use overflow mode
    /// Overflow</b> — which means text that does not fit is DRAWN OUTSIDE ITS BOX, over whatever
    /// is next to it, rather than shrinking or clipping. The concentration is the tell:
    ///
    /// <code>
    ///   Summary      7 of 7 off      Results     4 of 5 off
    ///   Arrange      8 of 19 off     NameEntry   4 of 5 off
    ///   TeacherMenu  3 of 8 off
    /// </code>
    ///
    /// Those are exactly the screens that display <b>runtime-variable content</b>: a name a child
    /// typed, a story title, the child's own sentence, an export file path. A label showing a
    /// fixed UI string can be laid out once and trusted; a label showing content cannot, and the
    /// project has already shipped this bug three times — the race tracker printing a whole
    /// sentence into a 118px plaque (F46a), "SOMEBODY" ellipsised in every race (F47f), and the
    /// Arrange pool pills, found and fixed this pass.
    ///
    /// The fix is always the same three properties, so it lives in one place instead of being
    /// re-typed per site: let it shrink, clip rather than spill if it still cannot fit, and wrap
    /// normally. The floor is what keeps "it fits" from becoming "it is unreadable" — this game's
    /// binding case is a 7-inch classroom tablet, so a label may get smaller but never tiny.
    /// </summary>
    public static class LabelFit
    {
        /// <summary>Default smallest point size. Above the readability audit's acuity floor for
        /// a 7-inch tablet at arm's length, and well below every authored size in the app, so
        /// applying this changes nothing for content that already fits.</summary>
        public const float DefaultFloor = 22f;

        /// <summary>
        /// Applies the three properties. Null-safe, and idempotent — calling it twice is the
        /// same as calling it once, which matters because scene controllers re-run their setup
        /// when a learner returns to a screen.
        /// </summary>
        /// <param name="label">The label to harden; null is ignored.</param>
        /// <param name="floor">Smallest point size to shrink to.</param>
        public static void Harden(TMP_Text label, float floor = DefaultFloor)
        {
            if (label == null) return;

            // Capture the authored size as the CEILING before enabling autosize: TMP leaves
            // fontSizeMax at 0 on labels that never used autosizing, and a max of 0 makes the
            // label vanish rather than fit.
            if (label.fontSizeMax <= 0f) label.fontSizeMax = label.fontSize;

            label.enableAutoSizing = true;
            label.fontSizeMin = floor;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.textWrappingMode = TextWrappingModes.Normal;
        }
    }
}
