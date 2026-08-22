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
    /// cannot find the fill. So the states are:
    ///
    ///   * fill found (parent, or the named sibling) -> wood fill + cream label (6.6:1, below)
    ///   * no fill found                             -> nothing happens at all
    ///
    /// There is no path that leaves dark text on a dark fill, which is the failure a blind
    /// colour change would normally risk on a device nobody can playtest today.
    ///
    /// ⚠️ BUT "NOTHING HAPPENS" IS NOT AUTOMATICALLY SAFE, and this comment used to claim it was
    /// ("grey fill + the scene's own dark label, still readable"). Results disproved that: its
    /// label is authored GOLD RGB(131,98,24), so when the lookup missed there, the untinted grey
    /// banner RGB(93,96,97) left the screen's largest text at <b>1.12:1</b> — invisible on the
    /// device. Doing nothing is only safe where the scene's own colours were already readable,
    /// which is why the resolution in <see cref="Apply"/> had to be widened rather than left to
    /// fail quietly.
    ///
    /// CONTRAST. Fill <see cref="Theme.Wood"/> (0.46, 0.31, 0.17) against the cream
    /// (1, 0.96, 0.88) this writes: <b>6.6:1</b>, past WCAG AA for large text with margin.
    ///
    /// It was TextBrownDeep (12.4:1) until 2026-08-22 — technically excellent contrast, and far
    /// too dark to look at: the owner reported the whole app reading as heavy and unfriendly,
    /// and the Session Map's ten near-black tiles as "everything is switched off". Contrast is a
    /// floor to clear, not a score to maximise; 6.6:1 clears it and looks like a children's game.
    /// </summary>
    public static class TitleBannerSkin
    {
        /// <summary>The type colour this writes onto the darkened banner.</summary>
        public static readonly Color Label = new Color(1f, 0.96f, 0.88f);

        /// <summary>The GameObject name this trusts when the banner is not the title's parent.</summary>
        public const string BannerName = "TitleBanner";

        /// <summary>
        /// Darkens the banner behind <paramref name="title"/> and turns its type cream.
        ///
        /// ⚠️ THE BANNER IS NOT ALWAYS THE PARENT. This comment previously asserted the banner is
        /// "the title's PARENT Image, which is how all five scenes are built (TitleBanner ->
        /// Title)". That claim is FALSE, and it was false for the one screen a learner ends every
        /// story on. Measured by parsing the scene YAML, 2026-08-22:
        ///
        /// <code>
        ///   Results.unity      Title -> parent 'Canvas'       TitleBanner is a SIBLING
        ///   SessionMap.unity   Title -> parent 'TitleBanner'
        ///   StorySelect.unity  Title -> parent 'TitleBanner'
        ///   NameEntry.unity    Title -> parent 'TitleBanner'
        ///   TeacherMenu.unity  Title -> parent 'TitleBanner'
        /// </code>
        ///
        /// In Results the two sit side by side under Canvas (TitleBanner's only child is
        /// TrophyIcon), so a parent-only lookup returned early and repainted NOTHING — banner
        /// left on the untinted GREY.png RGB(93,96,97), title left on its authored gold
        /// RGB(131,98,24), <b>1.12:1</b>. See the class remarks for why "do nothing" was not the
        /// safe fallback it had been written to be.
        ///
        /// Resolution order: the parent's Image, ELSE a sibling Image named
        /// <see cref="BannerName"/>, ELSE nothing. Results is fixed WITHOUT editing the scene —
        /// its Title is anchored to Canvas, so re-parenting it under TitleBanner would move it.
        ///
        /// The four TitleBanner-parented scenes are untouched by the widening: the parent Image
        /// is found on the first test, so the sibling walk never runs for them.
        ///
        /// Still a lookup rather than a serialized field on five controllers: the Editor cannot
        /// load this project's scripts right now (see the 2026-08-21 playtest doc), so five new
        /// references could not have been wired, and an unwired reference would have silently
        /// done nothing on exactly the screens this is for.
        /// </summary>
        public static void Apply(TMP_Text title)
        {
            if (title == null) return;

            var parent = title.transform.parent;
            if (parent == null) return;

            var fill = parent.GetComponent<Image>();
            if (fill == null) fill = FindSiblingBanner(title.transform, parent);
            if (fill == null) return;   // structure differs — change nothing, including the text

            fill.color = Theme.Wood;
            title.color = Label;
        }

        /// <summary>
        /// The banner Image sitting BESIDE the title, matched by <see cref="BannerName"/>.
        ///
        /// By name, and never "the first sibling that happens to carry an Image": Results' Canvas
        /// holds eleven other Image siblings (Sky, Hill, the three stars, MainIdeaPanel,
        /// ResultsPanel, TreasureChest, NextButton, NextButtonFrame), so a positional guess would
        /// have painted one of those dark and STILL left the title gold on grey — a worse outcome
        /// than the bug. If nothing matches, this returns null and Apply changes nothing.
        /// </summary>
        private static Image FindSiblingBanner(Transform title, Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == title) continue;
                if (child.name != BannerName) continue;

                var image = child.GetComponent<Image>();
                if (image != null) return image;
            }

            return null;
        }
    }
}
