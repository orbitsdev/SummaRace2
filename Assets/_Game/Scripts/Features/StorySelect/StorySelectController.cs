using System;
using PrimeTween;
using SummaRace.Constants;
using SummaRace.Core;
using SummaRace.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Trash Dash ships its own GameManager in the global namespace, and a type in an enclosing
// namespace outranks a using-directive — so bare "GameManager" binds to theirs. An alias
// named GameManager is illegal for the same reason (CS0576), hence the namespace alias.
using Core = SummaRace.Core;

namespace SummaRace.Features.StorySelect
{
    /// <summary>
    /// Pick Easy/Average/Hard within the current session (TDD §9.4).
    /// Every card is built from its story JSON, so all 30 stories are reachable through
    /// this one screen. Stories unlock in difficulty order within a session (GDD §3.1).
    /// </summary>
    public class StorySelectController : MonoBehaviour
    {
        /// <summary>
        /// One difficulty card. Every reference is optional and null-checked, so a card that
        /// is only partly dressed still works instead of throwing.
        /// </summary>
        [Serializable]
        private class DifficultyCard
        {
            public Button button;
            public Image heroImage;
            public TMP_Text titleText;
            public TMP_Text chipText;
            /// <summary>Optional. Falls back to the Image on chipText's parent, which is
            /// where all three cards keep it (Text &lt; Chip).</summary>
            public Image chipBackground;
            public Image lockIcon;
            public TMP_Text lockedLabel;
            public TMP_Text lockedHint;
            public Image[] stars;
        }

        [Tooltip("Easy, Average, Hard — in unlock order.")]
        [SerializeField] private DifficultyCard[] cards = new DifficultyCard[3];
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text titleText;

        // Same silhouette trick as ResultsController: the sprite is golden, so "off" is dark.
        private static readonly Color StarOff = Theme.Slate;
        private static readonly Color StarOn = Color.white;
        private static readonly Color CardLocked = new Color(0.62f, 0.66f, 0.70f);

        /// <summary>
        /// Ceiling on how bright a LOCKED card's hero art is allowed to render. An Image's
        /// colour MULTIPLIES its sprite, so tinting by this caps the background luminance
        /// whatever the picture turns out to be — which is the whole point here: 27 of the 30
        /// hero images are still placeholders, so no fixed pair of text/background colours
        /// could be certified against art nobody has drawn yet.
        ///
        /// It replaces work the scene's grad_scrim cannot do. That scrim covers only the
        /// card's bottom 55% and ramps from alpha 0.85 at the card's foot to 0 at its top:
        /// by the hint's upper edge (card y 0.44) it is down to 0.09, and the "Locked" label
        /// (card y 0.45-0.65) sits almost entirely ABOVE it. Measured on the worst case an
        /// unknown picture can produce (an all-white hero): "Locked" goes 1.72:1 -> 6.86:1
        /// and the hint 1.85:1 -> 6.09:1, against WCAG AA's 4.5:1. Any real art is darker
        /// than white and the scrim only subtracts further, so those two numbers are floors.
        /// </summary>
        private static readonly Color HeroLocked = new Color(0.30f, 0.32f, 0.36f);

        /// <summary>
        /// The difficulty chips, brought to the prototype's three colours: EASY green,
        /// AVERAGE tan, HARD red-orange. Before this, AVERAGE wore the kit's orange pill and
        /// HARD its light-red one, so the ladder read green-orange-red with AVERAGE and HARD
        /// too close to tell apart at a glance.
        ///
        /// The AVERAGE change could not be a tint. An Image MULTIPLIES its sprite, so the kit
        /// orange (236, 144, 35) can only be made darker - tan (189, 172, 126) needs more
        /// green and nearly four times the blue. Hence the one new asset in this pass: the
        /// kit's own GOLDEN pill copied into Resources so it can be swapped in at runtime
        /// without a scene edit. It carries the same 9-slice border (24, 20, 24, 20) as the
        /// other two, so the three chips stay the same shape.
        ///
        /// HARD is a tint, because red-orange IS light red with the blue pulled down:
        /// (247, 80, 87) x (1, 1, 0.46) = (247, 80, 40).
        ///
        /// CONTRAST, measured, because this is the part that was actually broken: every chip
        /// label is white, and white on the old orange was 2.45:1 - below WCAG AA even for
        /// large text. White on tan would be worse (2.24:1). So AVERAGE gets dark-brown ink
        /// (6.18:1) while the other two keep white (green 5.26:1, red-orange 3.42:1, which
        /// clears the 3.0 large-text bar these bold chips sit on).
        /// </summary>
        private static readonly Color HardChipTint = new Color(1f, 1f, 0.46f);
        private static readonly Color ChipInkLight = Color.white;
        private static readonly Color ChipInkDark = Theme.TextBrownDeep;

        // Set from here rather than left in the scene so the pairing above stays a measured
        // property of the code: the ceiling is worthless if the label drifts darker later.
        private static readonly Color LockedLabelInk = new Color(0.88f, 0.95f, 0.97f);
        private static readonly Color LockedHintInk = new Color(0.82f, 0.90f, 0.93f);

        // --- "tap this one" marker (see MarkPlayable) ---------------------------------
        /// <summary>How far the gold ring stands proud of the card, in reference pixels.</summary>
        private const float RingOutset = 18f;
        /// <summary>Peak of the ring's idle breath. Deliberately tiny: the ring sits directly
        /// behind a card the learner is asked to read, and anything larger reads as a wobble.</summary>
        private const float RingBreath = 1.012f;
        private const float RingBreathSeconds = 1.1f;
        private const string BadgeName = "PlayBadge";

        /// <summary>Where a playable card's title must stop so it never runs under the PLAY
        /// badge (which starts at x 0.60 — see BuildPlayBadge).</summary>
        private const float TitleClearOfBadgeX = 0.58f;

        /// <summary>
        /// What state a card is being dressed in. The screen used to know only two
        /// (locked / playable), and that is exactly why the "tap this one" marking broke down:
        /// a finished story stays replayable, so it stayed "playable" and kept its gold ring
        /// and PLAY badge forever. After Easy was done two cards shouted; after all three,
        /// three did — the signal died precisely when the learner most needs pointing.
        /// </summary>
        private enum CardState
        {
            /// <summary>Not open yet, or its story JSON is missing. Never the learner's fault.</summary>
            Locked,
            /// <summary>The one card being asked for. Gold ring + breath + glow + PLAY.</summary>
            Next,
            /// <summary>Already finished. Still tappable, but quietly: REPLAY, no ring.</summary>
            Cleared
        }

        // --- the scene's floating glow (see PlaceGlow) ---------------------------------
        /// <summary>Scene name of the soft gold halo under the cards. Historical — it was
        /// authored behind the EASY card and hard-anchored there, which is the bug PlaceGlow
        /// exists to undo. Renaming it would need a scene edit, so the name stays.</summary>
        private const string GlowName = "EasyGlow";
        /// <summary>How far the halo spills past the card it marks, as a fraction of the
        /// board. X keeps the authored 0.145; Y is smaller because the cards are stacked and
        /// a taller halo would bleed onto the neighbour it is trying NOT to point at.</summary>
        private const float GlowPadX = 0.145f;
        private const float GlowPadY = 0.12f;

        // --- card title (see SetupCard) ------------------------------------------------
        /// <summary>Autosize floor for a story title. The scene authored 8pt, which on the
        /// longest title in the corpus ("Baba Yaga, the Girl, and the Hedgehog", 37 chars, on
        /// the narrowest of the three boxes) is smaller than the difficulty chip beside it and
        /// well under the study's 20-arcmin legibility floor. Measured against the clamped box
        /// (0.04–0.58 of a 943px card = 509px wide, 93px tall): 37 chars wrap to two lines at
        /// ~28pt and still fit, so raising the floor costs nothing and no title needs 8pt.</summary>
        private const float TitleFontMin = 20f;

        // --- star-row caption (see BuildStarCaption) ------------------------------------
        private const string StarCaptionName = "StarCaption";
        /// <summary>TODO GameText: move to GameText.StorySelectStarCaption. Kept local because
        /// this pass may not edit Constants/. Three gold stars mean FIRST-PICK RACE ACCURACY
        /// here and STORIES FINISHED on the Session Map one tap earlier — same sprite, same
        /// row, opposite meanings, and nothing on either screen said which.</summary>

        /// <summary>TODO GameText: move to GameText.ReplayBadge. The badge on a card whose
        /// story is already finished. It said PLAY, which is what the ONE unfinished card
        /// says — so three identical PLAY badges meant "tap any of these".</summary>

        /// <summary>TODO GameText: move to GameText.StorySelectMissionLine(int). Nothing on
        /// this screen named the mission the learner is inside, even though SelectedSession is
        /// already read here — so a teacher checking a tablet over a shoulder could not tell
        /// session 3 from session 7, and neither could the child.</summary>


        private void Start()
        {
            // Read before the title, which now names it.
            int session = Core.GameManager.Instance != null ? Core.GameManager.Instance.SelectedSession : 1;

            if (titleText != null)
            {
                titleText.text = GameText.StorySelectTitle;
                SummaRace.UI.TitleBannerSkin.Apply(titleText);
                // Names the rule the three cards already follow but never stated, AND which
                // mission these three stories belong to. Without the first a learner sees two
                // padlocks and no reason for them; without the second the screen never says
                // where in the ten-session ladder they are.
                SummaRace.UI.SubtitleLine.Add(titleText, GameText.StorySelectMissionLine(session));
            }

            // Android BACK now answers instead of being swallowed (owner, 2026-08-22).
            // Registered rather than handled here, so one overlay serves every scene and
            // each screen only supplies its own rule - see Core/BackButtonGuard.
            Core.BackButtonGuard.RegisterExit(GameText.BackLeaveToMap,
                () => SceneLoader.Go(SceneNames.SessionMap));

            // The Reader stops the music so nothing sits under the narration; pick the loop
            // back up here, or the second and third story of a session are chosen in silence.
            // PlayMusic no-ops when the same clip is already playing, so arriving from the
            // menu costs nothing.
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(AudioKeys.MusicMenu);

            // TWO PASSES, deliberately. Which card is the "tap this one" card can only be known
            // after every card's story and progress is resolved, so nothing may be dressed
            // during the walk that resolves them — that single-pass shape is what forced the
            // old code to mark every playable card instead of the one that matters.
            int count = Mathf.Min(cards.Length, StoryIds.Difficulties.Length);
            var ids = new string[count];
            var stories = new StoryData[count];
            var open = new bool[count];     // unlocked by the difficulty ladder
            var finished = new bool[count]; // actually completed by THIS learner

            // Easy is always open; each later difficulty waits on the one before it.
            bool unlocked = true;
            for (int i = 0; i < count; i++)
            {
                ids[i] = StoryIds.For(session, StoryIds.Difficulties[i]);
                stories[i] = StoryLoader.Load(ids[i]);
                open[i] = unlocked;
                finished[i] = stories[i] != null && IsCompleted(ids[i]);

                // A story whose JSON is missing can never be cleared, so gating on it would
                // lock the whole session behind content the learner cannot reach. Missing
                // content opens the gate instead of closing it (GDD D7).
                unlocked = unlocked && (stories[i] == null || IsCleared(ids[i]));
            }

            // The one card being asked for: the first open, loadable story not yet finished.
            // -1 when the whole mission is done — then nothing is singled out, which is the
            // truth (the next thing to tap is BACK, to the map).
            int next = -1;
            for (int i = 0; i < count; i++)
                if (open[i] && stories[i] != null && !finished[i]) { next = i; break; }

            for (int i = 0; i < count; i++)
            {
                // Keyed on the index rather than on finished[i] so exactly ONE card can ever
                // carry the ring. Cards that reach Cleared here are always earlier than `next`,
                // and open[] is built from the same completion test, so the label cannot lie.
                CardState state = !(open[i] && stories[i] != null) ? CardState.Locked
                                : i == next ? CardState.Next
                                : CardState.Cleared;
                SetupCard(cards[i], StoryIds.Difficulties[i], ids[i], stories[i], state);
            }

            // The scene's halo is one object, always on, hard-anchored behind EASY and never
            // referenced by this controller — so the screen's largest animated attention magnet
            // pointed at the wrong card the moment Easy was finished. Drive it, or hide it.
            PlaceGlow(next >= 0 ? cards[next] : null);

            // The learner arrived MainMenu → SessionMap → here, so back is the map: picking
            // another story in the same session stays one tap.
            if (backButton != null)
                backButton.onClick.AddListener(() =>
                {
                    PlayClick();
                    SceneLoader.Go(SceneNames.SessionMap);
                });
        }

        private void SetupCard(DifficultyCard card, string difficulty, string storyId,
                               StoryData story, CardState state)
        {
            if (card == null) return;

            if (card.chipText != null) card.chipText.text = DifficultyLabel(difficulty);
            StyleChip(card, difficulty);

            // A story that fails to load never reaches anything but Locked (see Start).
            bool playable = state != CardState.Locked;

            // Missing content is not the learner's doing, so it says so in its own words
            // instead of borrowing the "finish the story above" hint.
            string hint = story == null ? GameText.StoryUnavailableHint : GameText.LockedHint;

            // A card whose JSON failed to load used to keep whatever title was baked into the
            // scene, so it named a story sitting right beside "this story isn't ready yet" —
            // and a learner who taps it gets nothing but a nudge. Say nothing rather than
            // promise a story that cannot open; the hint below does the explaining.
            if (card.titleText != null)
            {
                card.titleText.text = story != null ? story.title : string.Empty;

                // THE SCENE AUTHORED AN 8pt FLOOR on the two CardTitle boxes, so a long title
                // simply shrank until it fitted on one line — smaller than the EASY/MEDIUM/HARD
                // chip beside it, on the field that says what the story IS. Raise the floor and
                // let it wrap instead. Ellipsis rather than Overflow so a freak title clips
                // inside its own box instead of spilling across the hero art (the failure F46
                // had to fix on the race tracker); measured, no title in the corpus reaches it.
                card.titleText.enableAutoSizing = true;
                card.titleText.fontSizeMax = Mathf.Max(card.titleText.fontSizeMax, TitleFontMin + 8f);
                card.titleText.fontSizeMin = TitleFontMin;
                card.titleText.enableWordWrapping = true;
                card.titleText.overflowMode = TextOverflowModes.Ellipsis;
            }

            // Hero art is optional: a story with no illustration yet falls back to the
            // title-only card rather than showing a broken image (TDD §9.4).
            if (card.heroImage != null)
            {
                var sprite = story == null || string.IsNullOrEmpty(story.heroImage)
                    ? null
                    : Resources.Load<Sprite>(story.heroImage);
                if (sprite != null) card.heroImage.sprite = sprite;

                // Full colour once the story is open; capped while it is locked, so the two
                // locked labels always have a background dark enough to read against (see
                // HeroLocked). This is also just what "not yet" should look like.
                card.heroImage.color = playable ? Color.white : HeroLocked;

                // A locked card keeps the panel even with no picture to put in it: with the
                // sprite cleared the Image draws a flat slate rect, which is exactly the
                // background the contrast above was measured against. A PLAYABLE card with no
                // art still falls back to the title-only card (TDD §9.4).
                if (!playable && sprite == null) card.heroImage.sprite = null;
                card.heroImage.gameObject.SetActive(sprite != null || !playable);
            }

            if (card.lockIcon != null) card.lockIcon.gameObject.SetActive(!playable);
            if (card.lockedLabel != null)
            {
                card.lockedLabel.text = GameText.LockedLabel;
                card.lockedLabel.color = LockedLabelInk;
                card.lockedLabel.gameObject.SetActive(!playable);
            }
            if (card.lockedHint != null)
            {
                card.lockedHint.text = hint;
                card.lockedHint.color = LockedHintInk;
                card.lockedHint.gameObject.SetActive(!playable);
            }
            else if (!playable && card.titleText != null)
            {
                // The EASY card was built (F22) as the always-open one, so it carries no lock
                // icon, label or hint. When its JSON is missing it would otherwise go grey and
                // untappable in silence — its title is then the only field that can explain it.
                card.titleText.text = GameText.LockedCardLine(hint);
            }

            int best = playable && Core.GameManager.Instance != null
                ? Core.GameManager.Instance.GetBestStars(storyId)
                : 0;
            if (card.stars != null)
                for (int i = 0; i < card.stars.Length; i++)
                    if (card.stars[i] != null) card.stars[i].color = i < best ? StarOn : StarOff;

            if (card.button == null) return;

                        var background = card.button.GetComponent<Image>();
            if (background != null) background.color = playable ? Color.white : CardLocked;

            // Says what THESE three stars count, on the screen they are on. Built after the
            // stars so a card with no star row simply never gets one.
            BuildStarCaption(card);

            MarkPlayable(card, state);

            card.button.onClick.RemoveAllListeners();
            if (playable)
            {
                string id = storyId;   // capture per card, not per loop
                card.button.onClick.AddListener(() => SelectStory(id));
            }
            else
            {
                var locked = card;   // capture per card, not per loop
                card.button.onClick.AddListener(() => PlayLockedNudge(locked));
            }
        }

        /// <summary>
        /// A story counts as cleared once it has been completed. With no learner profile —
        /// playing this scene directly, or before Phase I creates profiles — every story
        /// stays reachable so all 30 remain testable.
        /// </summary>
        private static bool IsCleared(string storyId)
        {
            var learner = Core.GameManager.Instance != null ? Core.GameManager.Instance.CurrentLearner : null;
            if (learner == null) return true;

            return IsCompleted(storyId);
        }

        /// <summary>
        /// Has this learner actually FINISHED this story? Deliberately not IsCleared: that one
        /// answers "may the next difficulty open" and says yes with no profile at all, so every
        /// one of the 30 stories stays reachable when the scene is played directly (TDD §13).
        /// That fallback is right for unlocking and wrong for display — it would paint all three
        /// cards "done" in the editor and leave the screen with no card to point at.
        /// </summary>
        private static bool IsCompleted(string storyId)
        {
            var learner = Core.GameManager.Instance != null ? Core.GameManager.Instance.CurrentLearner : null;
            if (learner == null) return false;

            var progress = learner.progress.Find(p => p.storyId == storyId);
            return progress != null && progress.completed;
        }

        /// <summary>Paints one difficulty chip. Leaves EASY exactly as the scene has it -
        /// green was already right, and the fewer chips this touches the fewer can regress.</summary>
        private static void StyleChip(DifficultyCard card, string difficulty)
        {
            if (card == null || card.chipText == null) return;

            var background = card.chipBackground;
            if (background == null && card.chipText.transform.parent != null)
                background = card.chipText.transform.parent.GetComponent<Image>();

            switch (difficulty)
            {
                case "average":
                    if (background != null)
                    {
                        var tan = Resources.Load<Sprite>("UI/chip_tan");
                        // A missing sprite must not leave the chip tinted for art it never got.
                        if (tan != null)
                        {
                            background.sprite = tan;
                            // Simple, not Sliced: the other two chips are Simple, and the
                            // three kit pills already have different native widths (164 /
                            // 130 / 198) stretched into the same rect. Matching them keeps
                            // the corner radius reading the same across the row.
                            background.type = Image.Type.Simple;
                            background.color = Color.white;
                            card.chipText.color = ChipInkDark;
                        }
                    }
                    break;

                case "hard":
                    if (background != null) background.color = HardChipTint;
                    card.chipText.color = ChipInkLight;
                    break;
            }
        }

        private static string DifficultyLabel(string difficulty)
        {
            switch (difficulty)
            {
                case "easy": return GameText.DifficultyEasy;
                case "average": return GameText.DifficultyAverage;
                default: return GameText.DifficultyHard;
            }
        }

        private static void SelectStory(string storyId)
        {
            PlayClick();
            if (Core.GameManager.Instance != null)
            {
                Core.GameManager.Instance.StartStory(storyId);
            }
            else // no GameManager: scene played directly in-editor (TDD §13)
            {
                SceneLoader.Go(SceneNames.Reader);
            }
        }

        /// <summary>
        /// Locked is a friendly nudge, never a scold and never a dead end (GDD D7). The nudge
        /// has to be seen as well as heard: classroom tablets get muted, and a sound-only
        /// answer is indistinguishable from a button that does not work.
        /// </summary>
        private static void PlayLockedNudge(DifficultyCard card)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxSlotWiggle);

            if (card == null) return;

            // Punch the lock — or the title on the EASY card, which has no lock of its own and
            // is locked only when its JSON is missing. Never the card root: it carries
            // ButtonSquash, which drives the same localScale from its own tween, and two tweens
            // on one transform leave it wherever the last one wrote.
            Transform target = card.lockIcon != null ? card.lockIcon.transform
                             : card.titleText != null ? card.titleText.transform
                             : null;
            if (target != null) Tween.PunchScale(target, Vector3.one * 0.25f, 0.35f);
        }

                /// <summary>
        /// The POSITIVE half of the locked/open pair, which this screen never had. A locked
        /// card says "Locked", carries a padlock and is dimmed to HeroLocked; the open card was
        /// marked only by the ABSENCE of all three. "Which one do I tap" was therefore a
        /// difference a nine-year-old had to NOTICE rather than READ — and the three cards are
        /// otherwise identical in size, shape and position (each 32% of the board, full width).
        ///
        /// Built in code, not in the scene, because WHICH card is playable is decided at
        /// runtime from the learner's progress: a scene-authored marker would sit on the wrong
        /// card for two visits out of every three.
        /// </summary>
        private void MarkPlayable(DifficultyCard card, CardState state)
        {
            if (card == null || card.button == null) return;

            var root = card.button.GetComponent<RectTransform>();
            if (root == null) return;

            var ring = root.parent != null ? root.parent.Find(RingName(root)) : null;
            var badge = root.Find(BadgeName);

            if (state == CardState.Locked)
            {
                if (ring != null) ring.gameObject.SetActive(false);
                if (badge != null) badge.gameObject.SetActive(false);
                return;
            }

            // A CLEARED story stays open — replaying is allowed and encouraged — but it must not
            // compete with the card being asked for. So it keeps a badge (the state still has to
            // be READ, not just noticed) and loses the ring, the breath and the gold.
            bool cleared = state == CardState.Cleared;

            if (!cleared && ring == null) ring = BuildPlayRing(card, root);
            if (badge == null) badge = BuildPlayBadge(card, root);

            if (badge != null)
            {
                badge.gameObject.SetActive(true);
                StyleBadge(badge, cleared);
            }

            // The badge occupies x 0.60–0.94 of the card, and the scene-authored title rect
            // runs underneath it (EasyCard's reaches 0.90), so a long title vanished behind
            // the pill (owner playtest 2026-08-21: "The Crowded House: A Folktale"). Clamp the
            // title's right edge to end before the badge; its autosize + wrap absorb the lost
            // width. Clamped for BOTH badge states (PLAY and REPLAY) — only a locked card,
            // which carries no badge at all, keeps the authored full-width title.
            if (card.titleText != null)
            {
                var trect = card.titleText.rectTransform;
                if (trect.anchorMax.x > TitleClearOfBadgeX)
                    trect.anchorMax = new Vector2(TitleClearOfBadgeX, trect.anchorMax.y);
            }

            if (ring == null) return;

            if (cleared)
            {
                // Only reachable if a previous dressing left one behind; a cleared card is
                // never given a ring in the first place.
                ring.gameObject.SetActive(false);
                return;
            }

            ring.gameObject.SetActive(true);
            // Breathe on the RING, never on the card root: the root carries ButtonSquash, which
            // drives the same localScale from its own tween, and two tweens on one transform
            // leave it wherever the last one wrote. Same rule as the punches elsewhere here.
            Tween.Scale(ring, Vector3.one * RingBreath, RingBreathSeconds, Ease.InOutSine,
                        cycles: -1, cycleMode: CycleMode.Yoyo);
        }

        /// <summary>
        /// GOLD PLAY on the navy pill for the card being asked for; quiet cream REPLAY on a
        /// near-ink pill for one already finished. Both are WORDS — the difference between
        /// "do this next" and "you can do this again" must survive a muted tablet, a
        /// colour-blind reader and a child who is not looking for it.
        ///
        /// WHY NOT A GOLD PILL: the badge sprite is UI/bar_bg, the NAVY loading-bar trough,
        /// and an Image's colour MULTIPLIES its sprite — so tinting it GoldDeep rendered
        /// navy x gold = dark olive-green, and the old TextBrownDeep label on that murk was
        /// ~1.1:1, unreadable over the card art (owner screenshot 2026-08-22). A multiply can
        /// only darken; navy cannot be turned gold, only the AVERAGE-chip trick (a different
        /// sprite) could — and that is a layout change this pass does not make. So the pill
        /// keeps the sprite's own navy (white = neutral tint, same as BuildStarCaption) and
        /// the WORD carries the gold: Gold on Navy is Theme's documented 10.0:1, and Theme's
        /// own rule is "put gold type on Navy or Ink instead". Hierarchy survives — bright
        /// gold type for PLAY, dimmer cream on a darker pill for REPLAY.
        /// </summary>
        private static void StyleBadge(Transform badge, bool cleared)
        {
            if (badge == null) return;

            var pill = badge.GetComponent<Image>();
            if (pill != null) pill.color = cleared ? Theme.Alpha(Theme.Ink, 0.72f) : Color.white;

            var label = badge.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;

            label.text = cleared ? GameText.ReplayBadge : GameText.PlayBadge;
            label.color = cleared ? Theme.Cream : Theme.Gold;
            // REPLAY is 6 chars against PLAY's 4 in the same pill, so give autosize room to
            // find a smaller size rather than letting it run over the pill's rounded ends.
            label.fontSizeMin = cleared ? 14f : 18f;
        }

        /// <summary>
        /// Puts the scene's floating halo behind ONE card — the card the learner is being asked
        /// to tap — instead of leaving it where it was authored.
        ///
        /// It cannot be a scene decision: which card is next is decided at runtime from the
        /// learner's progress, so a fixed position is wrong on two visits out of three. It also
        /// was not even centred on the card it was drawn for (authored y 0.34–1.125 against
        /// EASY's 0.68–1.0, so half of it hung over AVERAGE).
        ///
        /// Null-safe in both directions: no halo in the scene, nothing happens; no next card,
        /// the halo is switched off rather than left pointing at a finished story.
        /// </summary>
        private void PlaceGlow(DifficultyCard card)
        {
            var glow = FindGlow();
            if (glow == null) return;

            var target = card != null && card.button != null
                ? card.button.GetComponent<RectTransform>()
                : null;

            // Every mission story finished: nothing here is "next", so the screen says nothing
            // rather than something false.
            if (target == null || target.parent != glow.parent)
            {
                glow.gameObject.SetActive(false);
                return;
            }

            glow.anchorMin = new Vector2(target.anchorMin.x - GlowPadX, target.anchorMin.y - GlowPadY);
            glow.anchorMax = new Vector2(target.anchorMax.x + GlowPadX, target.anchorMax.y + GlowPadY);
            glow.pivot = target.pivot;
            glow.anchoredPosition = target.anchoredPosition;
            glow.sizeDelta = Vector2.zero;

            // Behind all three cards. uGUI draws by sibling order, and BuildPlayRing inserts the
            // ring at its card's own index — so without this the halo could end up drawn over
            // the very card it is pointing at.
            glow.SetAsFirstSibling();
            glow.gameObject.SetActive(true);
        }

        /// <summary>The halo lives beside the cards, not under one, so it is found from
        /// whichever card is actually wired up rather than from a fixed path.</summary>
        private RectTransform FindGlow()
        {
            if (cards == null) return null;

            for (int i = 0; i < cards.Length; i++)
            {
                var button = cards[i] != null ? cards[i].button : null;
                var parent = button != null ? button.transform.parent : null;
                var found = parent != null ? parent.Find(GlowName) : null;
                if (found != null) return found as RectTransform;
            }
            return null;
        }

        /// <summary>
        /// One line under the star row saying what these stars mean.
        ///
        /// The same three-star sprite in the same corner one tap earlier (Session Map) means
        /// "stories finished"; here it means how many race answers were right first time. A
        /// teacher reading two stars here as "two of three stories done" is wrong, and nothing
        /// on either screen distinguished them. Built in code because neither card has a field
        /// for it, and it brings its own dark pill because it sits over hero art whose brightest
        /// pixels are unknown — the worst pixel under a glyph decides legibility, not the mean.
        ///
        /// Idempotent and null-safe: no star row or no chip to borrow a font from, no caption.
        /// </summary>
        private void BuildStarCaption(DifficultyCard card)
        {
            if (card == null || card.chipText == null) return;
            if (card.stars == null || card.stars.Length == 0 || card.stars[0] == null) return;

            var root = card.button != null ? card.button.GetComponent<RectTransform>() : null;
            if (root == null) return;
            if (root.Find(StarCaptionName) != null) return;   // already built

            var pillGo = new GameObject(StarCaptionName, typeof(RectTransform));
            pillGo.transform.SetParent(root, false);

            var pill = pillGo.AddComponent<Image>();
            pill.sprite = Resources.Load<Sprite>("UI/bar_bg");
            if (pill.sprite != null) pill.type = Image.Type.Sliced;
            pill.color = pill.sprite != null
                ? Theme.Alpha(Color.white, 0.88f)
                : Theme.Alpha(Theme.Ink, 0.75f);
            pill.raycastTarget = false;   // never steal the card's own tap

            // Directly under the star row (card y 0.84–0.96, x 0.62–0.97), widened left so the
            // words have somewhere to go; clear of the locked labels below (top edge 0.65) and
            // of the PLAY badge at the card's foot.
            var prect = pill.rectTransform;
            prect.anchorMin = new Vector2(0.44f, 0.735f);
            prect.anchorMax = new Vector2(0.98f, 0.825f);
            prect.offsetMin = Vector2.zero;
            prect.offsetMax = Vector2.zero;

            var labelGo = Instantiate(card.chipText.gameObject, pillGo.transform, false);
            labelGo.name = "Label";
            labelGo.SetActive(true);

            var label = labelGo.GetComponent<TMP_Text>();
            if (label == null) { Destroy(labelGo); return; }

            // Strip anything the chip carried that would re-style or animate this line.
            foreach (var extra in labelGo.GetComponents<MonoBehaviour>())
                if (!(extra is TMP_Text)) Destroy(extra);

            label.text = GameText.StorySelectStarCaption;
            label.color = Theme.Paper;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Normal;   // the chip is bold; this is a footnote
            label.enableWordWrapping = false;      // one line or nothing — it is a caption
            label.enableAutoSizing = true;
            label.fontSizeMin = 13f;
            label.fontSizeMax = 22f;
            label.raycastTarget = false;

            var lrect = label.rectTransform;
            lrect.localScale = Vector3.one;
            lrect.localRotation = Quaternion.identity;
            lrect.anchorMin = Vector2.zero;
            lrect.anchorMax = Vector2.one;
            lrect.pivot = new Vector2(0.5f, 0.5f);
            lrect.offsetMin = new Vector2(10f, 2f);
            lrect.offsetMax = new Vector2(-10f, -2f);
        }

        private static string RingName(Transform card) => "PlayRing_" + card.name;

        /// <summary>
        /// A gold silhouette of the card, slightly larger, inserted at the CARD'S OWN sibling
        /// index — so it draws behind the card while every other element keeps its relative
        /// order. It cannot be a child: uGUI draws a parent's graphic first and its children
        /// after, so a child would land in FRONT of the hero art and wash it gold (exactly the
        /// bug that had to be fixed on the race's option board).
        ///
        /// It COPIES the card's own sprite rather than choosing one, so the ring can never be
        /// a different shape from the card it rings — the three cards do not share a sprite.
        /// </summary>
        private Transform BuildPlayRing(DifficultyCard card, RectTransform root)
        {
            var parent = root.parent as RectTransform;
            if (parent == null) return null;

            var go = new GameObject(RingName(root), typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(root.GetSiblingIndex());

            var img = go.AddComponent<Image>();
            var cardImage = card.button.GetComponent<Image>();
            if (cardImage != null)
            {
                img.sprite = cardImage.sprite;
                img.type = cardImage.type;
            }
            img.color = Theme.GoldDeep;
            img.raycastTarget = false;   // never steal the card's own tap

            var rt = (RectTransform)go.transform;
            rt.anchorMin = root.anchorMin;
            rt.anchorMax = root.anchorMax;
            rt.pivot = root.pivot;
            rt.anchoredPosition = root.anchoredPosition;
            rt.sizeDelta = root.sizeDelta + new Vector2(RingOutset, RingOutset);
            return go.transform;
        }

        /// <summary>
        /// The word PLAY, in gold, on the navy pill at the card's foot — the readable
        /// counterpart to the padlock on the other two, so the state is carried by a WORD and
        /// not only by how bright the picture is (the same reasoning that removed colour-only
        /// feedback from the Reader and Arrange).
        ///
        /// The label is cloned from the difficulty chip's own text so it carries this screen's
        /// TMP font asset and material; a fresh TMP_Text falls back to the project default and
        /// reads as a different typeface. Colours here are only DEFAULTS — StyleBadge runs
        /// right after and owns the pairing (see its comment for why the pill is navy, not
        /// gold: bar_bg is the navy trough sprite and an Image tint can only darken it).
        /// </summary>
        private Transform BuildPlayBadge(DifficultyCard card, RectTransform root)
        {
            if (card.chipText == null) return null;   // nothing to borrow a font from

            var pillGo = new GameObject(BadgeName, typeof(RectTransform));
            pillGo.transform.SetParent(root, false);

            var pill = pillGo.AddComponent<Image>();
            pill.sprite = Resources.Load<Sprite>("UI/bar_bg");
            if (pill.sprite != null) pill.type = Image.Type.Sliced;
            pill.color = Color.white;   // neutral: the sprite's own navy (see StyleBadge)
            pill.raycastTarget = false;

            var prect = pill.rectTransform;
            prect.anchorMin = new Vector2(0.60f, 0.06f);
            prect.anchorMax = new Vector2(0.94f, 0.26f);
            prect.offsetMin = Vector2.zero;
            prect.offsetMax = Vector2.zero;

            var labelGo = Instantiate(card.chipText.gameObject, pillGo.transform, false);
            labelGo.name = "Label";
            labelGo.SetActive(true);

            var label = labelGo.GetComponent<TMP_Text>();
            if (label == null) { Destroy(labelGo); return pillGo.transform; }

            label.text = GameText.PlayBadge;
            label.color = Theme.Gold;   // Gold on the navy pill: Theme's measured 10.0:1
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 38f;
            label.raycastTarget = false;

            var lrect = label.rectTransform;
            lrect.localScale = Vector3.one;
            lrect.localRotation = Quaternion.identity;
            lrect.anchorMin = Vector2.zero;
            lrect.anchorMax = Vector2.one;
            lrect.pivot = new Vector2(0.5f, 0.5f);
            lrect.offsetMin = new Vector2(8f, 4f);
            lrect.offsetMax = new Vector2(-8f, -4f);

            return pillGo.transform;
        }

private static void PlayClick()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
        }
    }
}
