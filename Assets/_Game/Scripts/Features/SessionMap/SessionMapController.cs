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
        // Empty stars on a LOCKED (dark) stop. Was a steel blue-grey that rendered olive-on-
        // black in the first real learner-state capture (2026-08-23) — muddy, and easy to
        // misread as "earned but dull". A soft warm grey sits clearly above the dark plate
        // while staying obviously not-gold.
        private static readonly Color StarOff = new Color(0.62f, 0.60f, 0.58f);
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
                // Drawn LAST so the newly framed board (StyleBoard) cannot swallow it — the
                // board's top edge moved up when it gained its rim, and the subtitle sat right
                // under the title, half-behind it in the first capture.
                var subtitle = SummaRace.UI.SubtitleLine.Add(titleText, GameText.SessionMapSubtitle);
                if (subtitle != null) subtitle.transform.parent.SetAsLastSibling();
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

            BuildDriftingClouds();

            // The board the ten tiles sit on, styled BEFORE them (owner references, 2026-08-23:
            // "gold border and wood back, then dark card"). Every level-select he sent is one
            // panel — gold rim, wood body, tiles floating on a darker inner face — and ours was
            // a washed-out pale rectangle, which is why ten wood planks on it looked like too
            // much wood: nothing framed them.
            StyleBoard();

            for (int i = 0; i < stops.Length; i++)
                SetupStop(stops[i], i + 1, unlocked, current);

            // NO PATH DOTS. They were my own addition, not from any reference the owner sent,
            // and on the framed board they read as debris between the tiles ("what is that
            // diamond in centre", 2026-08-23). A level-select board is a GRID of tiles; the
            // journey is already told by the numbers and by which ones are open.
            // BuildPathDots() intentionally not called.

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
        /// The level-select board: gold rim → wood body → dark inner face, the composition every
        /// reference the owner sent is built on. Three layers because that is what makes the
        /// tiles read: a wood tile on a wood board is camouflage, a wood tile on a DARK face is
        /// a button. Built once (found by name on a revisit) and null-safe — no board, no change.
        /// The inner face is inserted as the board's FIRST child so every tile still draws over it.
        /// </summary>
        private void StyleBoard()
        {
            var first = stops != null && stops.Length > 0 && stops[0] != null ? stops[0].button : null;
            var board = first != null ? first.transform.parent as RectTransform : null;
            if (board == null) return;

            // ---- A QUIET WRAPPER, NOT A LOUD ONE ------------------------------------------
            // The ten tiles do need containing (they floated on open grass), but the earlier
            // attempts failed by making the frame the loudest thing on screen: wood body, dark
            // face, gold rim — a picture frame competing with its own picture. This is the
            // opposite: a soft translucent ink panel that darkens the art just enough for the
            // light tiles to lift off it, with a thin gold edge to say "this is a board". The
            // hierarchy stays tiles-first, which is what a level select is for.
            // ---- THE BOARD GETS ITS OWN MATERIAL ------------------------------------------
            // Wood banner + wood board + paper tiles was one material doing two jobs, which is
            // why no amount of re-toning the browns made it sit together (owner, 2026-08-23:
            // "even the colour and design doesn't fit as one"). The kit ships a panel built for
            // exactly this screen — teal with a gold rim, Panel_Sprites/Level screen pannel —
            // and it was what this scene used before tonight. With it back the hierarchy reads
            // in one glance: WOOD SIGN above, TEAL BOARD below, PAPER PAGES on it.
            var boardImg = board.GetComponent<Image>();
            if (boardImg != null)
            {
                boardImg.enabled = true;
                var panel = Resources.Load<Sprite>("UI/board_level");
                if (panel != null)
                {
                    boardImg.sprite = panel;
                    boardImg.type = Image.Type.Sliced;
                    // ---- WARM BROWN, AND TRANSLUCENT SO THE WORLD SHOWS THROUGH -------------
                    // Three attempts failed here and each failed the same way: this sprite is
                    // TEAL, and a uGUI tint MULTIPLIES, so brown over teal lands near-black —
                    // "a hole punched in a sunny playground" (owner device shots, 2026-08-23).
                    // A multiply cannot make a cool sprite warm.
                    //
                    // So the tint carries a strong red/green lift and a low alpha: the red
                    // channel is pushed far past 1 to overpower the sprite's own blue, and the
                    // panel sits at ~72% so the playground reads through it as depth instead of
                    // being covered by a slab. Result: a warm brown board of the same family as
                    // the banner and the tiles, on a screen that still feels outdoors.
                    boardImg.color = new Color(2.2f, 1.35f, 0.75f, 0.72f);
                }
                else boardImg.color = new Color(0.94f, 0.88f, 0.78f, 0.92f);
            }

            // ⚠️ NO DIRT TRAIL. Tying the map to the race with its own trail_dirt texture was a
            // good idea that does not survive contact: a tan texture at 30% alpha over a teal
            // board renders as a pale BLUE-GREY STRIPE (owner device shot, 2026-08-23), which
            // reads as a rendering fault, not as a path. A tint cannot add warmth it does not
            // have. Any trail left by an earlier build is removed here so a rebuilt scene
            // cannot keep one.
            var staleTrail = board.Find("BoardTrail");
            if (staleTrail != null) Destroy(staleTrail.gameObject);

            // Panel parts an earlier build added are removed, so a rebuilt scene cannot keep a
            // stale wood frame around.
            var oldFace = board.Find("BoardFace");
            if (oldFace != null) Destroy(oldFace.gameObject);
            var oldRim = board.Find("BoardRim");
            if (oldRim != null) Destroy(oldRim.gameObject);
        }

        /// <summary>
        /// Clouds drifting across the sky behind the board (owner, 2026-08-23: "can you add
        /// something moving in the choose-mission scene background?"). Built from the game's own
        /// soft cloud sprite, three of them on slow independent loops, inserted as the canvas's
        /// FIRST children so they pass behind the board and every tile. Never raycast targets,
        /// and silently absent if the sprite or canvas is missing.
        /// </summary>
        private void BuildDriftingClouds()
        {
            var first = stops != null && stops.Length > 0 && stops[0] != null ? stops[0].button : null;
            var canvas = first != null ? first.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return;
            // The scene's own cloud art is not in Resources (it is authored into the canvas), so
            // the drifting layer uses the soft gold glow instead, tinted white and stretched
            // wide — at 40% alpha it reads as a light cloud passing, which is all this needs.
            var cloud = Resources.Load<Sprite>("UI/glow_gold");
            if (cloud == null) return;

            var root = new GameObject("DriftClouds", typeof(RectTransform));
            var rrt = (RectTransform)root.transform;
            rrt.SetParent(canvas.transform, false);
            rrt.SetAsFirstSibling();
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Cloud_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(root.transform, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.80f + i * 0.06f);
                rt.sizeDelta = new Vector2(260f + i * 60f, 120f + i * 20f);
                rt.anchoredPosition = new Vector2(-260f - i * 220f, 0f);
                var img = go.GetComponent<Image>();
                img.sprite = cloud;
                img.color = new Color(1f, 1f, 1f, 0.55f - i * 0.08f);
                img.raycastTarget = false;

                // Slow drift right across the screen and around again — different speeds so the
                // three never travel as a block.
                PrimeTween.Tween.UIAnchoredPositionX(rt, 1400f, 26f + i * 9f, PrimeTween.Ease.Linear,
                    cycles: -1, cycleMode: PrimeTween.CycleMode.Restart, startDelay: i * 5f);
            }
        }

        /// <summary>
        /// The "2/3 stories" badge on a tile, built into the old star row's transform so the
        /// scene's own placement is reused. Sits in the tile's lower-right on a small ink pill:
        /// dark enough to read on the page at any brightness, small enough that the mission
        /// number still owns the tile. Built once, then only its text changes.
        /// </summary>
        private static void SetStopCounter(RectTransform row, int done, bool playable)
        {
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(1f, 0f);
            row.anchoredPosition = new Vector2(-16f, 14f);
            row.sizeDelta = new Vector2(84f, 40f);
            row.SetAsLastSibling();

            var pill = row.GetComponent<Image>();
            if (pill == null)
            {
                pill = row.gameObject.AddComponent<Image>();
                pill.sprite = Resources.Load<Sprite>("UI/bar_bg");
                if (pill.sprite != null) pill.type = Image.Type.Sliced;
                pill.raycastTarget = false;
            }
            pill.color = playable ? Color.white : new Color(1f, 1f, 1f, 0.55f);

            var label = row.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                var go = new GameObject("Count", typeof(RectTransform));
                go.transform.SetParent(row, false);
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                tmp.fontSize = 26f;
                tmp.raycastTarget = false;
                var lrt = tmp.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                label = tmp;
            }
            label.text = done + "/" + StoryIds.Difficulties.Length;
            label.color = new Color(1f, 0.96f, 0.88f, playable ? 1f : 0.75f);
            label.gameObject.SetActive(true);
        }

        /// <summary>The thin wood picture-frame behind an OPEN stop — the owner's storybook
        /// rule made literal: wood carries the border, parchment carries the page. Border-only
        /// (fillCenter off), sibling behind the button at its own index, found by name on a
        /// revisit so re-running SetupStop never stacks a second frame.</summary>
        private static void EnsureStopFrame(SessionStop stop)
        {
            if (stop == null || stop.button == null) return;
            var rt0 = stop.button.transform as RectTransform;
            if (rt0 == null || rt0.parent == null) return;

            string frameName = "WoodFrame_" + rt0.name;
            var parent = rt0.parent;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == frameName) return;

            var plaque = Resources.Load<Sprite>("UI/wood_plaque");
            if (plaque == null) return;

            var frame = new GameObject(frameName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)frame.transform;
            rt.SetParent(parent, false);
            rt.SetSiblingIndex(rt0.GetSiblingIndex());
            rt.anchorMin = rt0.anchorMin; rt.anchorMax = rt0.anchorMax; rt.pivot = rt0.pivot;
            rt.anchoredPosition = rt0.anchoredPosition;
            rt.sizeDelta = rt0.sizeDelta + new Vector2(20f, 20f);
            rt.localScale = rt0.localScale;   // stops are scaled 1.22x; the frame must ride along

            var img = frame.GetComponent<Image>();
            img.sprite = plaque;
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            img.raycastTarget = false;
        }

        /// <summary>
        /// A dotted trail from stop 1 to stop 10, three dots per leg, drawn behind the plates
        /// (inserted at the first stop's own sibling index, so every plate still draws over
        /// it). Small wood-toned diamonds rather than a line: they read as a footpath on the
        /// cream board, they need no sprite, and rotation carries the shape so colour is never
        /// the only channel (F49). Entirely code-built and null-safe — a stop without a button
        /// simply breaks the trail rather than throwing.
        /// </summary>
        private void BuildPathDots()
        {
            if (stops == null || stops.Length < 2) return;
            var first = stops[0] != null ? stops[0].button : null;
            if (first == null) return;

            var board = first.transform.parent as RectTransform;
            if (board == null) return;

            var root = new GameObject("PathDots", typeof(RectTransform));
            root.transform.SetParent(board, false);
            root.transform.SetSiblingIndex(first.transform.GetSiblingIndex());
            var rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero; rootRt.offsetMax = Vector2.zero;

            for (int i = 0; i < stops.Length - 1; i++)
            {
                var a = stops[i] != null ? stops[i].button : null;
                var b = stops[i + 1] != null ? stops[i + 1].button : null;
                if (a == null || b == null) continue;

                Vector3 from = a.transform.position;
                Vector3 to = b.transform.position;
                for (int d = 1; d <= 3; d++)
                {
                    var dot = new GameObject("Dot", typeof(RectTransform));
                    dot.transform.SetParent(root.transform, false);
                    var img = dot.AddComponent<Image>();
                    img.color = Theme.Alpha(Theme.Wood, 0.45f);
                    img.raycastTarget = false;
                    var rt = img.rectTransform;
                    rt.sizeDelta = new Vector2(16f, 16f);
                    dot.transform.position = Vector3.Lerp(from, to, d / 4f);
                    dot.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }
            }
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

            if (stop.button == null) return;

            // ---- THE WARM WOOD NEVER RENDERED (owner device report, 2026-08-23) --------------
            // A tint MULTIPLIES the sprite, and these plates sit on the kit's GREY.png (~0.37),
            // so StopPlayable (0.56, 0.40, 0.22) rendered as (0.21, 0.15, 0.08) — the dark
            // chocolate the owner photographed, with every value in this file's history debated
            // against a colour the screen never actually showed. A tint can only darken; no
            // number here could fix it. So an OPEN stop is re-sprited onto UI/panel_gold — the
            // parchment family the title banners and briefing card now share — untinted, with
            // its number flipped to TextBrownDeep (that card's measured pairing) and its stars
            // to gold-on-parchment. A LOCKED stop keeps the grey sprite and dark tint: "not
            // yet" reading as a dimmed object is exactly right, and the value gap between the
            // two states is finally real instead of 1.03:1.
            // ---- THE LEVEL-SELECT TILE (owner references, 2026-08-23) ----------------------
            // Redesigned against the wood level-select boards he sent: every tile is a WOOD
            // PLANK — open or locked — with a big centred number, the three stars sitting
            // UNDER the tile rather than crowded inside it, and a locked tile trading its
            // number for a big padlock. Locked is the same plank drained and darkened, so the
            // board reads as one set of ten and not as two different controls.
            // ---- THE TILE MUST CONTRAST WITH WHAT IT SITS ON ------------------------------
            // Wood tiles on a wood board is camouflage — that is the "weird" the owner kept
            // seeing, and no amount of re-toning the board fixes it. The references work
            // because their tiles are LIGHTER than the panel behind them. So: dark wood board
            // (StyleBoard), LIGHT PARCHMENT tiles with dark ink numbers. Locked tiles are the
            // same page dimmed, so the ten still read as one set.
            var background = stop.button.GetComponent<Image>();
            var page = Resources.Load<Sprite>("UI/panel_gold");
            if (background != null)
            {
                if (page != null)
                {
                    background.sprite = page;
                    background.type = Image.Type.Sliced;
                    // Locked pages are dimmed but still clearly PAGES: at 0.62 grey on the dark
                    // board they lost their gold edge and read as flat slabs (device shot,
                    // 2026-08-23). Warmer and lighter keeps the set coherent — the padlock and
                    // the missing number carry "not yet", not a colour change this heavy.
                    // ⚠️ AND THE GAP HAS TO BE BIG. That 0.78 sat within a whisker of white on
                    // the device: mission 1 was told apart from the nine locked ones only by
                    // its ring, so a screen of padlocks read as the normal state rather than as
                    // "not yet". Locked steps down hard AND cools off; open stays full parchment.
                    background.color = playable ? Color.white : new Color(0.55f, 0.53f, 0.51f);
                }
                else background.color = playable ? StopPlayable : StopLocked;
            }

            // Number: dark ink on the page, hidden when locked (the padlock speaks instead).
            if (stop.numberText != null)
            {
                stop.numberText.color = playable ? Theme.TextBrownDeep
                                                 : Theme.Alpha(Theme.TextBrownDeep, 0.75f);
                stop.numberText.fontStyle = TMPro.FontStyles.Bold;
                stop.numberText.gameObject.SetActive(playable);
            }

            // Padlock: centred and large, the way every level-select in the references does it.
            if (stop.lockIcon != null && !playable)
            {
                // ---- A LOCK IS A WHISPER, NOT A SHOUT ---------------------------------------
                // Nine bright padlocks made a child's first impression "almost everything here
                // is shut" (owner device shot, 2026-08-23). Sessions ARE teacher-gated — that is
                // the study's own exposure control — but the screen does not have to announce it
                // ten times. The padlock is now small and faint: present enough to explain why a
                // tap does nothing, quiet enough that the OPEN mission is what the eye lands on.
                var lrt = stop.lockIcon.rectTransform;
                lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.anchoredPosition = Vector2.zero;
                lrt.sizeDelta = new Vector2(52f, 52f);
                stop.lockIcon.preserveAspect = true;
                stop.lockIcon.color = new Color(1f, 1f, 1f, 0.55f);
            }

            // A locked tile also SHRINKS. Size is the loudest hierarchy cue there is, and it
            // does the job colour alone could not: the open mission is visibly the biggest thing
            // on the board, so the screen reads as "here is your mission" rather than as a wall
            // of locks. The scale is absolute, so re-running this never compounds it.
            stop.button.transform.localScale = Vector3.one * (playable ? 1.22f : 1.02f);

            // ---- A COUNTER, NOT STARS (owner, 2026-08-23: "why not put a number instead?") --
            // Three stars here meant "stories finished", while three stars on the Story Select
            // card one tap away mean "your race score" — the same icon carrying two meanings a
            // single tap apart, which is why this screen needed a subtitle to explain itself.
            // "2/3" cannot be misread, and it frees the tile's corner.
            if (stop.stars != null && stop.stars.Length > 0 && stop.stars[0] != null)
            {
                var row = stop.stars[0].transform.parent as RectTransform;
                foreach (var s in stop.stars) if (s != null) s.gameObject.SetActive(false);
                if (row != null) SetStopCounter(row, done, playable);
            }

            // (Tile scale is set with the lock above — open tiles are deliberately larger than
            // locked ones, which is the screen's main hierarchy cue.)

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
