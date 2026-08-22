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

        // --- "you can tap this" ring (see MarkPlayableStop) ---------------------------------
        /// <summary>
        /// LOCKED AND PLAYABLE DIFFERED BY 1.03:1 IN LUMINANCE. StopPlayable and StopLocked
        /// above were tuned to fix a different complaint (ten black holes) and landed on the
        /// same VALUE with different warmth — which reads on a calibrated desk monitor and does
        /// not read on a classroom tablet at an angle in daylight. That left the padlock
        /// carrying the entire distinction: one small glyph, on the screen whose whole job is
        /// "which of these ten may I tap".
        ///
        /// Colour cannot be the answer on its own anyway (F49): a shape difference is
        /// perceivable to everyone. So a playable stop gains a gold rim, the same device Story
        /// Select already uses for the card it is asking for, which also makes the two screens
        /// agree about what "open" looks like.
        /// </summary>
        private const float StopRingOutset = 16f;
        private static string StopRingName(Transform stop) => "PlayRing_" + stop.name;

        // --- the session-complete cheer (see EnsureCheerLine) --------------------------------
        private const string CheerName = "SessionCheer";
        /// <summary>How far above the hint the cheer pill sits, in reference pixels. Measured
        /// on the 1080x1920 reference: the hint band is 105.6px tall, so a 24px-shorter pill
        /// lifted this far occupies canvas y 152-234 — clear of the hint under it and clear of
        /// the bottom row of stops, whose plates stop at y 243.</summary>
        private const float CheerLiftPixels = 108f;

        private GameObject _cheerPill;
        private TMP_Text _cheerText;

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
            int current = CurrentSession(unlocked);

            for (int i = 0; i < stops.Length; i++)
                SetupStop(stops[i], i + 1, unlocked, current);

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

            // THE CHEER GETS ITS OWN LINE. It used to overwrite the locked-sessions hint, and
            // the arrival that triggers it is the single most likely arrival on this screen —
            // a learner walks in here having just finished a mission, so the sentence
            // explaining why the next nine stops are shut ("Your teacher opens the next
            // mission!") was replaced by a cheer at exactly the moment they turn to the next
            // stop and find it locked. Two different jobs, two labels; PlayLockedNudge no
            // longer has to race the celebration to restore the explanation.
            ShowCheer(GameText.SessionCompleteCheer);

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

        private void SetupStop(SessionStop stop, int session, int unlockedSession, int currentSession)
        {
            if (stop == null) return;

            bool playable = session <= unlockedSession;

            if (stop.numberText != null) stop.numberText.text = session.ToString();
            if (stop.lockIcon != null) stop.lockIcon.gameObject.SetActive(!playable);

            // The glow marks where the learner is now — the mission still being worked on, not
            // the newest one the teacher happened to open (see CurrentSession).
            if (stop.glow != null) stop.glow.gameObject.SetActive(session == currentSession);

            // Colour alone was not telling open from locked apart; a rim is a shape.
            MarkPlayableStop(stop, playable);

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

        /// <summary>
        /// The mission the learner is actually IN: the lowest open one with fewer than three
        /// stories finished.
        ///
        /// The glow used to mark <c>session == unlockedSession</c>, which is a different
        /// question — how far the TEACHER has opened the game. The two diverge as soon as a
        /// learner leaves a story unfinished and the teacher opens the next session anyway
        /// (which is the normal case: sessions open on the classroom schedule, GDD §8.3, not on
        /// the child's progress). The screen then pointed at the newest stop while an earlier,
        /// half-finished mission sat looking done with — the one thing this map exists to show.
        ///
        /// Falls back to the unlocked session when everything open is already complete, so the
        /// glow still marks "you are here" while the learner waits for the teacher.
        /// </summary>
        private static int CurrentSession(int unlockedSession)
        {
            for (int session = 1; session <= unlockedSession; session++)
                if (CompletedInSession(session) < StoryIds.Difficulties.Length) return session;

            return unlockedSession;
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

            // Re-assert the explanation every time. The cheer has its own line now, so this is
            // no longer undoing it — but the hint is hidden by Start when nothing is locked, and
            // a tap on a locked stop is the one moment it must certainly be on screen.
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

        /// <summary>
        /// Puts the cheer on screen. Null-safe and cheap: the line is built the first time a
        /// session is actually finished, so an ordinary visit never creates it.
        /// </summary>
        private void ShowCheer(string text)
        {
            var label = EnsureCheerLine();
            if (label == null) return;

            label.text = text;
            if (_cheerPill != null) _cheerPill.SetActive(true);
        }

        /// <summary>
        /// A second line, above the locked-sessions hint, that the celebration owns.
        ///
        /// Built the way AddHintBacking builds its pill and for the same reason: this screen
        /// draws over painted landscape whose brightest pixels are pure white (the grass has
        /// daisies in it), and an average contrast figure is the wrong statistic — the worst
        /// pixel under a glyph decides legibility. The label is CLONED from the hint so it
        /// carries this screen's own TMP font asset and material; a freshly built TMP_Text
        /// falls back to the project default and reads as a different typeface.
        ///
        /// The clone is taken BEFORE the pill is parented, exactly as SubtitleLine does — with
        /// the order reversed the copy would include the pill about to hold it.
        /// </summary>
        private TMP_Text EnsureCheerLine()
        {
            if (_cheerText != null) return _cheerText;
            if (lockedHintText == null || lockedHintText.transform.parent == null) return null;

            var parent = lockedHintText.transform.parent;

            var existing = parent.Find(CheerName);
            if (existing != null)   // idempotent
            {
                _cheerPill = existing.gameObject;
                _cheerText = existing.GetComponentInChildren<TMP_Text>(true);
                return _cheerText;
            }

            var pillGo = new GameObject(CheerName, typeof(RectTransform));

            var pill = pillGo.AddComponent<Image>();
            pill.sprite = Resources.Load<Sprite>("UI/bar_bg");
            if (pill.sprite != null) pill.type = Image.Type.Sliced;
            pill.color = pill.sprite != null
                ? Theme.Alpha(Color.white, 0.92f)
                : Theme.Alpha(Theme.Ink, 0.80f);
            pill.raycastTarget = false;   // never steal a tap from anything under it

            var src = lockedHintText.rectTransform;
            var rt = pill.rectTransform;
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot = src.pivot;
            rt.sizeDelta = src.sizeDelta + new Vector2(36f, -24f);
            rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, CheerLiftPixels);

            var labelGo = Instantiate(lockedHintText.gameObject, pillGo.transform, false);
            labelGo.name = "Label";
            labelGo.SetActive(true);

            // Safe now: the clone was taken while the pill was still unparented.
            pillGo.transform.SetParent(parent, false);
            pillGo.transform.SetSiblingIndex(lockedHintText.transform.GetSiblingIndex() + 1);

            var label = labelGo.GetComponent<TMP_Text>();
            if (label == null) { Destroy(pillGo); return null; }

            // Strip anything the hint carried that would re-word or animate this line.
            foreach (var extra in labelGo.GetComponents<MonoBehaviour>())
                if (!(extra is TMP_Text)) Destroy(extra);

            label.text = string.Empty;
            label.color = Theme.Gold;              // celebratory, and ~11:1 on the navy pill
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 34f;
            label.raycastTarget = false;

            var lrect = label.rectTransform;
            lrect.localScale = Vector3.one;
            lrect.localRotation = Quaternion.identity;
            lrect.anchorMin = Vector2.zero;
            lrect.anchorMax = Vector2.one;
            lrect.pivot = new Vector2(0.5f, 0.5f);
            lrect.offsetMin = new Vector2(18f, 4f);
            lrect.offsetMax = new Vector2(-18f, -4f);

            pillGo.SetActive(false);   // shown only when there is something to cheer

            _cheerPill = pillGo;
            _cheerText = label;
            return _cheerText;
        }

        /// <summary>
        /// Shows or hides a stop's gold rim. See StopRingOutset for why a rim and not a colour.
        /// Null-safe: a stop with no button, or no parent to put the rim in, is left alone.
        /// </summary>
        private void MarkPlayableStop(SessionStop stop, bool playable)
        {
            if (stop == null || stop.button == null) return;

            var root = stop.button.GetComponent<RectTransform>();
            if (root == null || root.parent == null) return;

            var ring = root.parent.Find(StopRingName(root));

            if (!playable)
            {
                if (ring != null) ring.gameObject.SetActive(false);
                return;
            }

            if (ring == null) ring = BuildStopRing(stop, root);
            if (ring != null) ring.gameObject.SetActive(true);

            // Deliberately NOT animated. Story Select breathes its single ring because there is
            // exactly one; up to ten breathing at once here would be noise, and the ONE stop
            // that deserves motion already has the glow.
        }

        /// <summary>
        /// A gold silhouette of the stop, slightly larger, inserted at the STOP'S OWN sibling
        /// index — so it draws behind the plate while every other element keeps its relative
        /// order (each stop's Glow sits immediately before it and must stay further back).
        /// It cannot be a child: uGUI draws a parent's graphic first and its children after, so
        /// a child would land in FRONT of the number and the padlock.
        ///
        /// It COPIES the stop's own sprite rather than choosing one, so the rim can never be a
        /// different shape from the plate it rings.
        /// </summary>
        private static Transform BuildStopRing(SessionStop stop, RectTransform root)
        {
            var parent = root.parent as RectTransform;
            if (parent == null) return null;

            var go = new GameObject(StopRingName(root), typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(root.GetSiblingIndex());

            var img = go.AddComponent<Image>();
            var plate = stop.button.GetComponent<Image>();
            if (plate != null)
            {
                img.sprite = plate.sprite;
                img.type = plate.type;
            }
            img.color = Theme.GoldDeep;
            img.raycastTarget = false;   // never steal the stop's own tap

            var rt = (RectTransform)go.transform;
            rt.anchorMin = root.anchorMin;
            rt.anchorMax = root.anchorMax;
            rt.pivot = root.pivot;
            rt.anchoredPosition = root.anchoredPosition;
            rt.sizeDelta = root.sizeDelta + new Vector2(StopRingOutset, StopRingOutset);
            return go.transform;
        }
    }
}
