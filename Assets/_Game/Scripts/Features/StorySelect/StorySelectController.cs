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

        private void Start()
        {
            if (titleText != null)
            {
                titleText.text = GameText.StorySelectTitle;
                SummaRace.UI.TitleBannerSkin.Apply(titleText);
                // Names the rule the three cards already follow but never stated. Without it a
                // learner sees two padlocks and no reason for them.
                SummaRace.UI.SubtitleLine.Add(titleText, GameText.StorySelectSubtitle);
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

            int session = Core.GameManager.Instance != null ? Core.GameManager.Instance.SelectedSession : 1;

            // Easy is always open; each later difficulty waits on the one before it.
            bool unlocked = true;
            int count = Mathf.Min(cards.Length, StoryIds.Difficulties.Length);
            for (int i = 0; i < count; i++)
            {
                string difficulty = StoryIds.Difficulties[i];
                string storyId = StoryIds.For(session, difficulty);
                var story = StoryLoader.Load(storyId);
                SetupCard(cards[i], difficulty, storyId, story, unlocked);

                // A story whose JSON is missing can never be cleared, so gating on it would
                // lock the whole session behind content the learner cannot reach. Missing
                // content opens the gate instead of closing it (GDD D7).
                unlocked = unlocked && (story == null || IsCleared(storyId));
            }

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
                               StoryData story, bool unlocked)
        {
            if (card == null) return;

            if (card.chipText != null) card.chipText.text = DifficultyLabel(difficulty);
            StyleChip(card, difficulty);

            // A story that fails to load must never present itself as playable.
            bool playable = unlocked && story != null;

            // Missing content is not the learner's doing, so it says so in its own words
            // instead of borrowing the "finish the story above" hint.
            string hint = story == null ? GameText.StoryUnavailableHint : GameText.LockedHint;

            // A card whose JSON failed to load used to keep whatever title was baked into the
            // scene, so it named a story sitting right beside "this story isn't ready yet" —
            // and a learner who taps it gets nothing but a nudge. Say nothing rather than
            // promise a story that cannot open; the hint below does the explaining.
            if (card.titleText != null) card.titleText.text = story != null ? story.title : string.Empty;

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

            MarkPlayable(card, playable);

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
        private void MarkPlayable(DifficultyCard card, bool playable)
        {
            if (card == null || card.button == null) return;

            var root = card.button.GetComponent<RectTransform>();
            if (root == null) return;

            var ring = root.parent != null ? root.parent.Find(RingName(root)) : null;
            var badge = root.Find(BadgeName);

            if (!playable)
            {
                if (ring != null) ring.gameObject.SetActive(false);
                if (badge != null) badge.gameObject.SetActive(false);
                return;
            }

            if (ring == null) ring = BuildPlayRing(card, root);
            if (badge == null) badge = BuildPlayBadge(card, root);

            if (badge != null) badge.gameObject.SetActive(true);

            // The badge occupies x 0.60–0.94 of the card, and the scene-authored title rect
            // runs underneath it (EasyCard's reaches 0.90), so a long title vanished behind
            // the pill (owner playtest 2026-08-21: "The Crowded House: A Folktale"). Clamp the
            // title's right edge to end before the badge; its autosize + wrap absorb the lost
            // width. Only the playable card is clamped — locked cards carry no badge.
            if (card.titleText != null)
            {
                var trect = card.titleText.rectTransform;
                if (trect.anchorMax.x > TitleClearOfBadgeX)
                    trect.anchorMax = new Vector2(TitleClearOfBadgeX, trect.anchorMax.y);
            }

            if (ring == null) return;

            ring.gameObject.SetActive(true);
            // Breathe on the RING, never on the card root: the root carries ButtonSquash, which
            // drives the same localScale from its own tween, and two tweens on one transform
            // leave it wherever the last one wrote. Same rule as the punches elsewhere here.
            Tween.Scale(ring, Vector3.one * RingBreath, RingBreathSeconds, Ease.InOutSine,
                        cycles: -1, cycleMode: CycleMode.Yoyo);
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
        /// The word PLAY on a gold pill at the card's foot — the readable counterpart to the
        /// padlock on the other two, so the state is carried by a WORD and not only by how
        /// bright the picture is (the same reasoning that removed colour-only feedback from the
        /// Reader and Arrange).
        ///
        /// The label is cloned from the difficulty chip's own text so it carries this screen's
        /// TMP font asset and material; a fresh TMP_Text falls back to the project default and
        /// reads as a different typeface. Dark brown on gold, because gold on gold is invisible
        /// and gold type generally is a fill colour here, never a text one (Theme).
        /// </summary>
        private Transform BuildPlayBadge(DifficultyCard card, RectTransform root)
        {
            if (card.chipText == null) return null;   // nothing to borrow a font from

            var pillGo = new GameObject(BadgeName, typeof(RectTransform));
            pillGo.transform.SetParent(root, false);

            var pill = pillGo.AddComponent<Image>();
            pill.sprite = Resources.Load<Sprite>("UI/bar_bg");
            if (pill.sprite != null) pill.type = Image.Type.Sliced;
            pill.color = Theme.GoldDeep;
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
            label.color = Theme.TextBrownDeep;
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
