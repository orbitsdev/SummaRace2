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

        // Set from here rather than left in the scene so the pairing above stays a measured
        // property of the code: the ceiling is worthless if the label drifts darker later.
        private static readonly Color LockedLabelInk = new Color(0.88f, 0.95f, 0.97f);
        private static readonly Color LockedHintInk = new Color(0.82f, 0.90f, 0.93f);

        private void Start()
        {
            if (titleText != null) titleText.text = GameText.StorySelectTitle;

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

        private static void PlayClick()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
        }
    }
}
