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
        // ---- THE TEN STOPS ARE DARK, NOT GREEN --------------------------------------------
        //
        // Owner device playtest 2026-08-21: "I don't like the design colour green square with
        // number, please choose black or dark colours instead?"
        //
        // The green was not a tint we chose - it is the kit sprite's own paint
        // (Hyper_Casual_UI/Sprites/Buttons/empty_buttons/green.png), shown untinted (white) when
        // a session is playable. So darkening it in code was not available: a multiply toward
        // black over saturated green gives dark GREEN, which is the same complaint one shade
        // down. The ten stops now use the kit's neutral GREY.png instead - same 9-slice border
        // (24/20/24/20), so nothing about the layout moves - and these two tints do the work.
        //
        // Contrast measured against the white number each carries: 7.3:1 playable, 12.7:1
        // locked, both clear of WCAG AA for large text with room. Locked is darker rather than
        // greyer so "not yet" still reads as the same object dimmed, not as a different control.
        // 2026-08-22, SECOND PASS. These were (0.26,0.33,0.38) and (0.16,0.20,0.23) — dark
        // slate — and on the device they read as ten black holes punched through a cream board
        // on a sunny playground: the one PLAYABLE mission was indistinguishable from the nine
        // locked ones except for a padlock, so the whole screen looked switched off.
        //
        // The distinction now carries on WARMTH AND SATURATION rather than on brightness, which
        // is what actually says "open" versus "not yet" to a child:
        //   playable — warm wood, the same family as every other surface this game owns
        //   locked   — the same value, drained to neutral grey (plus its padlock and dim stars)
        // Both hold a white number at ~5:1, past AA, and neither is a hole.
        private static readonly Color StopPlayable = new Color(0.56f, 0.40f, 0.22f);
        private static readonly Color StopLocked = new Color(0.44f, 0.42f, 0.40f);

        // Theme.Slate (0.20, 0.278, 0.318) was an unearned star on a bright green plate. On the
        // dark plate above it is very nearly the plate itself, so an unearned star would vanish
        // rather than read as empty - and "how many of the three you finished" is the only
        // progress this screen shows. Lifted to a muted steel that is visibly a star and
        // visibly not a gold one.
        private static readonly Color StarOff = new Color(0.45f, 0.50f, 0.55f);
        private static readonly Color StarOn = Color.white;

        private void Start()
        {
            if (titleText != null)
            {
                titleText.text = GameText.SessionMapTitle;
                SummaRace.UI.TitleBannerSkin.Apply(titleText);
                // Says what a "mission" IS and what these three stars count. The stars matter
                // most: the same sprite in the same row one tap away (Story Select) means "how
                // well you did", while here it means "stories finished" — so an unlabelled two
                // stars is read wrongly by a teacher, not just by a child.
                SummaRace.UI.SubtitleLine.Add(titleText, GameText.SessionMapSubtitle);
            }

            // Android BACK now answers instead of being swallowed (owner, 2026-08-22).
            // Registered rather than handled here, so one overlay serves every scene and
            // each screen only supplies its own rule - see Core/BackButtonGuard.
            Core.BackButtonGuard.RegisterExit(GameText.BackLeaveToMenu,
                () => SceneLoader.Go(SceneNames.MainMenu));

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
                bool anyLocked = unlocked < GameRules.SessionCount;
                lockedHintText.text = GameText.SessionLockedHint;
                lockedHintText.gameObject.SetActive(anyLocked);

                // AND HIDE ITS BACKING WITH IT. The pill is a SIBLING of the hint (it has to be
                // - a child would draw in front of the words), and its active state was never
                // linked to the hint's. So once a teacher had unlocked all ten sessions, the text
                // went away and a near-black bar stayed on screen with nothing in it, for the
                // last week of the study, on every tablet.
                var backing = AddHintBacking(lockedHintText);
                if (backing != null) backing.SetActive(anyLocked);
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

            if (lockedHintText != null)
            {
                // Start hides this label when nothing is locked, and the cheer borrows it — so
                // on a tablet with all ten sessions open the celebration wrote into a disabled
                // object and the learner was told nothing at all.
                lockedHintText.text = GameText.SessionCompleteCheer;
                lockedHintText.gameObject.SetActive(true);
            }

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
            if (background != null) background.color = playable ? StopPlayable : StopLocked;

            stop.button.onClick.RemoveAllListeners();
            if (playable)
            {
                int number = session;   // capture per stop, not per loop
                stop.button.onClick.AddListener(() => SelectSession(number));
            }
            else
            {
                var locked = stop;   // capture per stop, not per loop
                stop.button.onClick.AddListener(() => PlayLockedNudge(locked));
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

        /// <summary>
        /// Locked is a friendly "not yet", never a scold and never a dead end (GDD D7). It is
        /// also never the learner's doing — sessions open on the teacher's PIN (GDD §8.3) — so
        /// the tap has to leave that sentence on screen, not just play a sound a muted
        /// classroom tablet will never make.
        /// </summary>
        private void PlayLockedNudge(SessionStop stop)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxSlotWiggle);

            // Punch the lock, not the stop root: the root carries ButtonSquash, which drives
            // the same localScale from its own tween, and two tweens on one transform leave it
            // wherever the last one wrote.
            if (stop != null && stop.lockIcon != null)
                Tween.PunchScale(stop.lockIcon.transform, Vector3.one * 0.3f, 0.35f);

            // Re-assert the explanation every time. The session-complete cheer borrows this
            // same label, so a learner who finished a session and then reached for the next
            // stop was answered by "Mission complete!" — which says nothing about the lock in
            // front of them.
            if (lockedHintText != null)
            {
                lockedHintText.text = GameText.SessionLockedHint;
                lockedHintText.gameObject.SetActive(true);
            }
        }

        private static void PlayClick()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
        }

        /// <summary>
        /// Put a soft dark pill behind the hint line.
        ///
        /// The line sits at the bottom of the screen straight over the painted landscape, and
        /// that art has WHITE DAISIES scattered through the grass. Measured off a portrait
        /// render: the background beside the glyphs averages a respectable 3.7-5.4:1 against
        /// white text, but its brightest pixels are pure white — so wherever a flower lands
        /// behind a letter that letter is invisible (1.00:1). An average is the wrong statistic
        /// for legibility; the worst pixel under a glyph is the one that decides it.
        ///
        /// Built here rather than in the scene so it cannot be lost if the screen is rebuilt,
        /// and inserted at the hint's own sibling index so it draws BEHIND the text while every
        /// other element keeps its relative order. Null-safe: no hint, no backing, no harm.
        /// </summary>
        private static GameObject AddHintBacking(TMP_Text hint)
        {
            if (hint == null || hint.transform.parent == null) return null;
            var existing = hint.transform.parent.Find("HintBacking");
            if (existing != null) return existing.gameObject;   // idempotent

            var go = new GameObject("HintBacking", typeof(RectTransform));
            go.transform.SetParent(hint.transform.parent, false);
            go.transform.SetSiblingIndex(hint.transform.GetSiblingIndex());

            var img = go.AddComponent<Image>();
            // 0.55 alpha left white text at 3.79:1 against the map's brightest pixels (the grass
            // has white daisies in it), and this label autosizes down to 22pt where the large-text
            // exemption stops applying. 0.75 puts the worst case near 6.4:1 and costs nothing.
            img.color = Theme.Alpha(Theme.Ink, 0.75f);
            img.raycastTarget = false;

            var src = hint.rectTransform;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot = src.pivot;
            rt.anchoredPosition = src.anchoredPosition;
            // a little wider than the words so the pill reads as deliberate, not as a clipped box
            rt.sizeDelta = src.sizeDelta + new Vector2(36f, 14f);
            return go;
        }

    }
}
