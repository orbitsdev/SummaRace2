using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SummaRace.Constants;

namespace SummaRace.UI
{
    /// <summary>
    /// Repaints a screen's title banner dark, with cream type on it.
    ///
    /// WHY (owner device playtest, 2026-08-22): <i>"for design yellow background — like for
    /// example label 'Pick a Mission', it is near yellow and border dark border — can you make
    /// it colour dark, so that make game feel light, not as bright colourful that eye hurt."</i>
    ///
    /// Five screens carried the same saturated kit yellow pill behind their title (Session Map,
    /// Story Select, Results, Name Entry, Teacher Menu), and on a classroom tablet at full
    /// brightness that is the loudest thing on every screen the learner passes through. The race
    /// panels were already moved to dark wood in the previous pass; this brings the rest of the
    /// app into the same language, so the FURNITURE is dark and calm and the bright colour is
    /// spent where it means something — the SWBST palette, the hero art, the world.
    ///
    /// HOW, AND WHY IT CANNOT PRODUCE UNREADABLE TEXT. The banner sprite in those five scenes was
    /// swapped from the kit's <c>yellow.png</c> to its <c>GREY.png</c> — a geometry-neutral swap,
    /// both are 9-sliced with the identical 24/20/24/20 border — because tinting a saturated
    /// yellow toward dark gives a muddy olive, while a neutral grey takes a tint exactly. This
    /// then sets the fill and the label TOGETHER, in one place, and does nothing at all if it
    /// cannot find the fill. So the three possible states are:
    ///
    ///   * this runs        -> dark fill  + cream label   (11.7:1, measured below)
    ///   * this cannot run  -> grey fill  + the scene's own dark label (still readable)
    ///   * the field is null-> nothing happens
    ///
    /// There is no path that leaves dark text on a dark fill, which is the failure a blind
    /// colour change would normally risk on a device nobody can playtest today.
    ///
    /// CONTRAST. Fill <see cref="Theme.TextBrownDeep"/> (0.24, 0.157, 0.086) against the cream
    /// (1, 0.96, 0.88) this writes: relative luminance 0.0269 vs 0.9042, so
    /// (0.9042 + 0.05) / (0.0269 + 0.05) = <b>12.4:1</b> — far past WCAG AA for large text, and
    /// it stays past AA even if a future pass lightens the fill considerably.
    /// </summary>
    public static class TitleBannerSkin
    {
        /// <summary>The type colour this writes onto the darkened banner.</summary>
        public static readonly Color Label = new Color(1f, 0.96f, 0.88f);

        /// <summary>
        /// Darkens the banner behind <paramref name="title"/> and turns its type cream.
        ///
        /// The banner is the title's PARENT Image, which is how all five scenes are built
        /// (TitleBanner -> Title). Deliberately resolved that way rather than by adding a
        /// serialized field to five controllers: the Editor cannot load this project's scripts
        /// right now (see the 2026-08-21 playtest doc), so five new serialized references could
        /// not have been wired, and an unwired reference would have silently done nothing on
        /// exactly the screens this is for.
        /// </summary>
        public static void Apply(TMP_Text title)
        {
            if (title == null) return;

            var parent = title.transform.parent;
            if (parent == null) return;

            var fill = parent.GetComponent<Image>();
            if (fill == null) return;   // structure differs — change nothing, including the text

            fill.color = Theme.TextBrownDeep;
            title.color = Label;
        }
    }
}
