using System;
using System.Collections;
using PrimeTween;
using SummaRace.Constants;
using SummaRace.Core;
using SummaRace.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Trash Dash ships its own GameManager in the global namespace, which outranks a
// using-directive, and an alias named GameManager is illegal for the same reason (CS0576).
using Core = SummaRace.Core;

namespace SummaRace.Features.SessionMap
{
    /// <summary>
    /// The ten sessions as stops along a path (TDD §9.3, GDD §3.1). Only sessions up to the
    /// learner's unlocked one are playable; the rest wait on the teacher's PIN (GDD §8.3), so
    /// app exposure stays aligned with the ten scheduled classroom sessions.
    /// </summary>
    public class SessionMapController : MonoBehaviour
    {
        /// <summary>One stop. Every reference is optional and null-checked.</summary>
        [Serializable]
        private class SessionStop
        {
            public Button button;
            public TMP_Text numberText;
            public Image lockIcon;
            public Image glow;
            /// <summary>Filled stars = how many of this session's three stories are done.</summary>
            public Image[] stars;
        }

        [SerializeField] private SessionStop[] stops = new SessionStop[GameRules.SessionCount];
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text lockedHintText;

        // Same silhouette trick as ResultsController: the sprite is golden, so "off" is dark.
        private static readonly Color StarOff = new Color(0.20f, 0.28f, 0.32f);
        private static readonly Color StarOn = Color.white;
        private static readonly Color StopLocked = new Color(0.62f, 0.66f, 0.70f);

        private void Start()
        {
            if (titleText != null) titleText.text = GameText.SessionMapTitle;

            // The Reader stops the music so nothing sits under the narration, and Results ends
            // on the victory sting — so the map is where the loop comes back. PlayMusic no-ops
            // when the same clip is already playing, so arriving from the menu costs nothing.
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(AudioKeys.MusicMenu);

            int unlocked = UnlockedSession();

            for (int i = 0; i < stops.Length; i++)
                SetupStop(stops[i], i + 1, unlocked);

            // Only explain the locks when some are actually locked.
            if (lockedHintText != null)
            {
                lockedHintText.text = GameText.SessionLockedHint;
                lockedHintText.gameObject.SetActive(unlocked < GameRules.SessionCount);
            }

            if (backButton != null)
                backButton.onClick.AddListener(() =>
                {
                    PlayClick();
                    SceneLoader.Go(SceneNames.MainMenu);
                });

            // Consumed, so finishing a session celebrates once and a later visit stays quiet.
            int justFinished = Core.GameManager.Instance != null
                ? Core.GameManager.Instance.ConsumeJustCompletedSession()
                : 0;
            if (justFinished > 0) StartCoroutine(CelebrateSession(justFinished));
        }

        /// <summary>
        /// A short "that day is done" beat on the stop just finished. Deliberately under three
        /// seconds — a 55-minute classroom session must never wait on a celebration (GDD §6).
        /// </summary>
        private IEnumerator CelebrateSession(int session)
        {
            int index = session - 1;
            if (index < 0 || index >= stops.Length || stops[index] == null) yield break;

            var stop = stops[index].button != null ? stops[index].button.transform : null;
            if (stop == null) yield break;

            if (lockedHintText != null) lockedHintText.text = GameText.SessionCompleteCheer;

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxStar);

            // Punch the stop, then light its three stars in turn.
            Tween.PunchScale(stop, Vector3.one * 0.35f, 0.5f);
            yield return new WaitForSeconds(0.35f);

            var stars = stops[index].stars;
            if (stars != null)
                for (int i = 0; i < stars.Length; i++)
                {
                    if (stars[i] == null) continue;
                    stars[i].color = StarOn;
                    Tween.PunchScale(stars[i].transform, Vector3.one * 0.5f, 0.35f);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxCoin);
                    yield return new WaitForSeconds(0.22f);
                }
        }

        private void SetupStop(SessionStop stop, int session, int unlockedSession)
        {
            if (stop == null) return;

            bool playable = session <= unlockedSession;

            if (stop.numberText != null) stop.numberText.text = session.ToString();
            if (stop.lockIcon != null) stop.lockIcon.gameObject.SetActive(!playable);

            // The glow marks where the learner is now, so the current mission is obvious.
            if (stop.glow != null) stop.glow.gameObject.SetActive(session == unlockedSession);

            int done = CompletedInSession(session);
            if (stop.stars != null)
                for (int i = 0; i < stop.stars.Length; i++)
                    if (stop.stars[i] != null) stop.stars[i].color = i < done ? StarOn : StarOff;

            if (stop.button == null) return;

            var background = stop.button.GetComponent<Image>();
            if (background != null) background.color = playable ? Color.white : StopLocked;

            stop.button.onClick.RemoveAllListeners();
            if (playable)
            {
                int number = session;   // capture per stop, not per loop
                stop.button.onClick.AddListener(() => SelectSession(number));
            }
            else
            {
                // Locked is a friendly "not yet", never a scold and never a dead end (GDD D7).
                stop.button.onClick.AddListener(PlayLockedNudge);
            }
        }

        /// <summary>
        /// How far the teacher has opened the game. With no learner profile — playing this
        /// scene directly, or before Phase I creates profiles — every session shows, so all
        /// 30 stories stay reachable for testing.
        /// </summary>
        private static int UnlockedSession()
        {
            var learner = Core.GameManager.Instance != null
                ? Core.GameManager.Instance.CurrentLearner
                : null;
            if (learner == null) return GameRules.SessionCount;

            return Mathf.Clamp(learner.unlockedSession, 1, GameRules.SessionCount);
        }

        private static int CompletedInSession(int session)
        {
            var learner = Core.GameManager.Instance != null
                ? Core.GameManager.Instance.CurrentLearner
                : null;
            if (learner == null) return 0;

            int done = 0;
            for (int d = 0; d < StoryIds.Difficulties.Length; d++)
            {
                string id = StoryIds.For(session, d);
                var progress = learner.progress.Find(p => p.storyId == id);
                if (progress != null && progress.completed) done++;
            }
            return done;
        }

        private static void SelectSession(int session)
        {
            PlayClick();

            // Story Select reads this. Played directly in-editor there is no GameManager to
            // carry it, so Story Select falls back to session 1 (TDD §13).
            if (Core.GameManager.Instance != null)
                Core.GameManager.Instance.SelectedSession = session;

            SceneLoader.Go(SceneNames.StorySelect);
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
