using System;
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
        private static readonly Color StarOff = new Color(0.20f, 0.28f, 0.32f);
        private static readonly Color StarOn = Color.white;
        private static readonly Color CardLocked = new Color(0.62f, 0.66f, 0.70f);

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

            if (card.titleText != null && story != null) card.titleText.text = story.title;

            // Hero art is optional: a story with no illustration yet falls back to the
            // title-only card rather than showing a broken image (TDD §9.4).
            if (card.heroImage != null)
            {
                var sprite = story == null || string.IsNullOrEmpty(story.heroImage)
                    ? null
                    : Resources.Load<Sprite>(story.heroImage);
                if (sprite != null) card.heroImage.sprite = sprite;
                card.heroImage.gameObject.SetActive(sprite != null);
            }

            if (card.lockIcon != null) card.lockIcon.gameObject.SetActive(!playable);
            if (card.lockedLabel != null)
            {
                card.lockedLabel.text = GameText.LockedLabel;
                card.lockedLabel.gameObject.SetActive(!playable);
            }
            if (card.lockedHint != null)
            {
                card.lockedHint.text = hint;
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
                // Locked is a friendly nudge, never a scold and never a dead end (GDD D7).
                card.button.onClick.AddListener(PlayLockedNudge);
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

        private static void PlayLockedNudge()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxSlotWiggle);
        }

        private static void PlayClick()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
        }
    }
}
