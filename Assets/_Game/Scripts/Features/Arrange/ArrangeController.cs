using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using SummaRace.Constants;
using SummaRace.Core;
using SummaRace.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.Features.Arrange
{
    /// <summary>
    /// Order the 5 collected pieces into S-W-B-S-T slots (TDD §10.2).
    /// Tap a piece, then tap a slot. VERIFY locks correct slots green;
    /// wrong ones wiggle amber and return to the pool. Retries never run out and
    /// never block: a hint appears after GameRules.ArrangeHintAfterMisses misses on
    /// the same piece (and unconditionally on the attempt before the assist, so the
    /// ladder can never be skipped), and after GameRules.ArrangeMaxAttempts failed
    /// verifies the screen finishes the order with the learner (see AssistRoutine).
    /// </summary>
    public class ArrangeController : MonoBehaviour
    {
        [Header("Slots (S-W-B-S-T order)")]
        [SerializeField] private Button[] slotButtons = new Button[5];
        [SerializeField] private TMP_Text[] slotLabels = new TMP_Text[5];

        [Header("Piece pool")]
        [SerializeField] private Button[] pieceButtons = new Button[5];
        [SerializeField] private TMP_Text[] pieceLabels = new TMP_Text[5];

        [Header("Actions")]
        [SerializeField] private Button verifyButton;
        [SerializeField] private Button undoButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Labels (set from GameText)")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text undoLabel;
        [SerializeField] private TMP_Text verifyLabel;

        private static readonly Color SlotFilled = new Color(0.96f, 0.87f, 0.70f);
        private static readonly Color LabelFilled = new Color(0.25f, 0.20f, 0.12f);
        private static readonly Color SlotLocked = new Color(0.55f, 0.85f, 0.45f);
        private static readonly Color SlotWrong = new Color(0.95f, 0.65f, 0.3f);
        /// <summary>The pool pills. Warm YELLOW, and deliberately not SlotFilled's cream:
        /// the two were byte-identical (0.96, 0.87, 0.70), so a part waiting in the pool and a
        /// part already placed in a slot were the same colour, and the board never showed at a
        /// glance how much was left to do. Now the screen reads in three states - pastel
        /// element colour = empty slot, yellow = still to place, cream = placed, green =
        /// checked and locked.
        ///
        /// Measured against the pill's own ink (0.2, 0.2, 0.25, set in the scene): 8.6:1, well
        /// clear of WCAG AA, and these labels carry the whole story text so they are the most
        /// read type on the screen.</summary>
        private static readonly Color PieceNormal = new Color(0.99f, 0.82f, 0.36f);
        private static readonly Color PieceSelected = new Color(0.5f, 0.75f, 1f);

        /// <summary>Built once per scene load; the two dash sprites are shared and cached
        /// across loads so replaying a story does not allocate a new texture each time.</summary>
        private bool _slotBoardBuilt;
        private static Sprite _dashH;
        private static Sprite _dashV;

        private StoryData _story;
        private string[] _pieceTexts = new string[5];   // piece i = element i's correct text
        private int[] _poolOrder = new int[5];          // shuffled display order (element index per pool button)
        private readonly int[] _slotContent = new int[5];   // element index in each slot, -1 = empty
        private readonly bool[] _slotLocked = new bool[5];
        private readonly int[] _missCount = new int[5];     // per element, for hints
        private readonly Stack<(int piece, int slot)> _undoStack = new();
        private int _selectedPiece = -1;                // element index of selected pool piece

        /// <summary>An empty slot while a piece is held: brighter than its resting pastel so it
        /// reads as "drop it here", and applied to ALL empty slots so it never hints which one
        /// is correct. Warm cream-white rather than a colour, so it does not collide with the
        /// SWBST palette those slots are already labelled in.</summary>
        private static readonly Color SlotArmed = new Color(1f, 0.97f, 0.86f);
        private int _attempts;
        private bool _busy;

        /// <summary>The board exactly as the learner submitted it on the current attempt —
        /// element index per slot. Taken in <see cref="OnVerify"/> because verification empties
        /// every wrong slot back into the pool, so the coroutine that raises
        /// <see cref="ArrangeVerified"/> can no longer see what was submitted. A fresh array per
        /// attempt: the event carries the reference, and reusing one would let a later attempt
        /// rewrite an order a listener has already been handed.</summary>
        private int[] _submittedOrder;

        private void Start()
        {
            _story = SummaRace.Core.GameManager.Instance != null ? SummaRace.Core.GameManager.Instance.CurrentStory : null;
                        if (_story == null) _story = StoryLoader.Load("s01_easy"); // editor-direct fallback
            if (_story == null)
            {
                // Nothing below this line has run yet, so returning would leave the learner
                // on a screen whose every button is unwired — dead, with no way out. Route
                // back to Story Select instead, the same friendly fail GameManager uses when
                // a story won't load (GDD §11.6 / TDD §13).
                Debug.LogError("Arrange: no story; returning to Story Select.");
                SetStatus(GameText.ArrangeNoStory);
                SceneLoader.Go(SceneNames.StorySelect);
                return;
            }

            // The back half of the loop had NO music at all. The Reader stops the track on
            // purpose (narration is the accessibility support the study depends on) and the race
            // stops its own at FINISH — but nothing ever started one again, so Arrange, Summary
            // and Results ran in silence, three screens in a row. The menus were the liveliest
            // part of the game and the actual learning was dead air.
            //
            // PlayMusic no-ops when the same clip is already running, so this costs nothing on
            // the way through and cannot restart the loop between these three screens. The voice
            // channel is a separate AudioSource on its own level, so the instruction still reads
            // over the top. Below the no-story guard on purpose: that path leaves for Story
            // Select, which starts this same loop itself.
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(AudioKeys.MusicMenu);

            // Ms. Lumi reacts here now (see MsLumiReactor.AttachBadge). Null-safe and pool-safe:
            // absent object or absent badge art simply leaves the screen as it was.
            SummaRace.UI.MsLumiReactor.AttachBadge();

            EnsureSlotBoard();

            // Pieces come from the race result when available (same texts either way).
            var result = SummaRace.Core.GameManager.Instance != null ? SummaRace.Core.GameManager.Instance.LastRaceResult : null;
            for (int i = 0; i < 5; i++)
                _pieceTexts[i] = result != null && !string.IsNullOrEmpty(result.collectedPieces[i])
                    ? result.collectedPieces[i]
                    : _story.elements[i].correct;

            // Shuffle the pool so the order is never given away.
            for (int i = 0; i < 5; i++) _poolOrder[i] = i;
            for (int i = 4; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_poolOrder[i], _poolOrder[j]) = (_poolOrder[j], _poolOrder[i]);
            }

            for (int i = 0; i < 5; i++)
            {
                _slotContent[i] = -1;
                SummaRace.UI.LabelFit.Harden(pieceLabels != null && i < pieceLabels.Length ? pieceLabels[i] : null);
                SummaRace.UI.LabelFit.Harden(slotLabels != null && i < slotLabels.Length ? slotLabels[i] : null);

                int slotIndex = i, poolIndex = i; // capture
                if (slotButtons[i] != null)
                    slotButtons[i].onClick.AddListener(() => OnSlotTapped(slotIndex));
                if (pieceButtons[i] != null)
                    pieceButtons[i].onClick.AddListener(() => OnPieceTapped(poolIndex));
            }
            if (verifyButton != null) verifyButton.onClick.AddListener(OnVerify);
            if (undoButton != null) undoButton.onClick.AddListener(OnUndo);

            if (titleText != null)
            {
                // The bubble now acknowledges the run before it instructs - the learner arrives
                // here straight off the race, and the screen used to open with a bare command.
                titleText.text = GameText.ArrangeLumiIntro;

                // Autosizing was OFF at a pinned 34pt. Measured, the bubble's title band is
                // ~739 x 83 px, and this longer line needs two wrapped lines at 34pt (~82 px)
                // - i.e. it fit only by luck and any font-metric difference would have clipped
                // it on the device. The floor is the readability audit's 24pt acuity minimum.
                titleText.enableAutoSizing = true;
                titleText.fontSizeMax = titleText.fontSize;
                titleText.fontSizeMin = 24f;
                titleText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            }
            if (undoLabel != null) undoLabel.text = GameText.UndoLabel;
            if (verifyLabel != null) verifyLabel.text = GameText.VerifyLabel;

            // Mid-run, so there is no legal exit: the race is already logged and the
            // ladder is not finished. Android BACK says so warmly instead of doing
            // nothing (owner, 2026-08-22) - see Core/BackButtonGuard.
            Core.BackButtonGuard.RegisterBlocked(GameText.BackBlockedArrange);

            RefreshUI();
            SetStatus(GameText.ArrangeIntroStatus);

            // Read the screen's title and its instruction aloud. Both are pure interface text:
            // a learner who cannot read "Tap a story part, then tap where it goes" is stuck on
            // HOW to answer rather than on the story, which is the one thing this instrument
            // must not measure. The five collected parts stay unspoken — those are the answer.
            // Queued, so the loading overlay's SWBST tip finishes its sentence first.
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayVoice(AudioKeys.VoArrangeTitle, true);
                AudioManager.Instance.PlayVoice(AudioKeys.VoArrangeHow, true);
            }
        }


        // ---------- the slot board ----------

        /// <summary>
        /// A dashed gold rectangle drawn AROUND the five slots, so the board the learner is
        /// filling reads as one object instead of five loose bars floating on the sky. The
        /// pool below it is deliberately left unframed - the frame is what says "these five
        /// places are the answer".
        ///
        /// Two things here are not arbitrary:
        ///
        /// It is a SIBLING inserted at Slot_0's own index, never a child of anything. uGUI
        /// draws in hierarchy order, so a child drawn "behind" its parent is impossible and a
        /// frame parented to the slots would paint over them - that is exactly the bug F58(d)
        /// found in the race's arrival cue, where a rim meant to sit behind a board covered
        /// its interior. Taking Slot_0's index puts it behind all five and leaves every other
        /// element's relative order untouched.
        ///
        /// The dashes are a TILED one-dash sprite on four thin strips, not N dot objects: one
        /// texture, four Images, no per-dot GameObjects to place or to keep in step when the
        /// board's size changes.
        ///
        /// Bounds are measured from the scene, not guessed: Slot_0 spans y 0.820-0.885 and
        /// Slot_4 spans 0.520-0.585, so 0.505-0.900 clears both with a little air, and
        /// x 0.03-0.97 sits just outside the slots' own 0.06-0.94.
        /// </summary>
        private void EnsureSlotBoard()
        {
            if (_slotBoardBuilt) return;
            var anchorSlot = slotButtons != null && slotButtons.Length > 0 ? slotButtons[0] : null;
            if (anchorSlot == null) return;

            var parent = anchorSlot.transform.parent;
            if (parent == null) return;
            _slotBoardBuilt = true;

            var boardGo = new GameObject("SlotBoard", typeof(RectTransform));
            boardGo.transform.SetParent(parent, false);
            var rect = (RectTransform)boardGo.transform;
            rect.anchorMin = new Vector2(0.030f, 0.505f);
            rect.anchorMax = new Vector2(0.970f, 0.900f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            // Behind the slots, ahead of the sky.
            boardGo.transform.SetSiblingIndex(anchorSlot.transform.GetSiblingIndex());

            var dashH = DashSprite(horizontal: true);
            var dashV = DashSprite(horizontal: false);
            const float T = 6f;   // strip thickness in reference px

            BuildEdge(rect, "Top",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -T), new Vector2(0f, 0f),  dashH);
            BuildEdge(rect, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),  new Vector2(0f, T),  dashH);
            BuildEdge(rect, "Left",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f),  new Vector2(T, 0f),  dashV);
            BuildEdge(rect, "Right",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-T, 0f),  new Vector2(0f, 0f), dashV);
        }

        /// <summary>One dashed edge of the board.</summary>
        private static void BuildEdge(RectTransform board, string name, Vector2 aMin, Vector2 aMax,
                                      Vector2 offMin, Vector2 offMax, Sprite dash)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(board, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = aMin;
            r.anchorMax = aMax;
            r.offsetMin = offMin;
            r.offsetMax = offMax;

            var img = go.AddComponent<Image>();
            img.sprite = dash;
            img.type = Image.Type.Tiled;
            img.color = Theme.GoldDeep;
            img.raycastTarget = false;   // the slots underneath must keep every tap
        }

        /// <summary>
        /// One dash plus its gap, as a 1-bit sprite the Image tiles along an edge. Built in
        /// code rather than imported for the same reason the loading bar's pills are: it is
        /// two colours and a rectangle, and an imported PNG would be one more asset to keep
        /// in step with the palette.
        /// </summary>
        private static Sprite DashSprite(bool horizontal)
        {
            if (horizontal && _dashH != null) return _dashH;
            if (!horizontal && _dashV != null) return _dashV;

            const int Dash = 14, Gap = 10, Thick = 6;
            int w = horizontal ? Dash + Gap : Thick;
            int h = horizontal ? Thick : Dash + Gap;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = horizontal ? "dash_h" : "dash_v",
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int along = horizontal ? x : y;
                    bool ink = along < Dash;
                    px[y * w + x] = ink ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);   // no mips, then make it non-readable to free the CPU copy

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = tex.name;
            if (horizontal) _dashH = sprite; else _dashV = sprite;
            return sprite;
        }

        // ---------- interactions ----------

        private void OnPieceTapped(int poolIndex)
        {
            if (_busy) return;
            int element = _poolOrder[poolIndex];
            if (IsPlaced(element)) return;

            PlayClick();
            _selectedPiece = _selectedPiece == element ? -1 : element;
            RefreshUI();
        }

        private void OnSlotTapped(int slot)
        {
            if (_busy) return;
            if (_slotLocked[slot])
            {
                // A locked slot used to swallow the tap with no sound, no motion and no word.
                // The learner is usually still holding a selected piece at that moment, so the
                // screen read as broken rather than as "that one is already done" — and this is
                // the one screen the story cannot leave until the order is right, so a tap that
                // seems to do nothing is exactly where a learner gives up (GDD D7).
                NudgeLockedSlot(slot);
                return;
            }
            PlayClick();

            if (_slotContent[slot] >= 0)
            {
                // Tapping a filled slot returns its piece to the pool.
                _slotContent[slot] = -1;
                RefreshUI();
                return;
            }

            if (_selectedPiece < 0)
            {
                // An empty slot tapped with nothing in hand played a click and then did nothing
                // at all — the same "is this broken?" silence the locked slot used to give. Say
                // what the screen is waiting for instead; it is the instruction line the learner
                // arrived on, so it is never new information to decode.
                SetStatus(GameText.ArrangeIntroStatus);
                if (slotLabels[slot] != null)
                    Tween.PunchScale(slotLabels[slot].transform, Vector3.one * 0.15f, 0.3f);
                return;
            }

            _slotContent[slot] = _selectedPiece;
            _undoStack.Push((_selectedPiece, slot));
            _selectedPiece = -1;
            RefreshUI();
        }

        private void OnUndo()
        {
            if (_busy) return;
            PlayClick();
            while (_undoStack.Count > 0)
            {
                var (piece, slot) = _undoStack.Pop();
                // Only undo if that piece is still sitting in that slot, unlocked.
                if (_slotContent[slot] == piece && !_slotLocked[slot])
                {
                    _slotContent[slot] = -1;
                    RefreshUI();
                    return;
                }
            }

            // Nothing left to take back (an empty board, or everything still on it is locked
            // green). The tap used to end here with a click and no change anywhere on screen.
            SetStatus(GameText.ArrangeIntroStatus);
        }

        private void OnVerify()
        {
            if (_busy) return;

            // Click before the fill check, not after it: a half-filled board used to answer
            // VERIFY with a status line and no sound at all, and a learner looking at the
            // pieces rather than at the text above them saw a button that does nothing.
            PlayClick();
            for (int i = 0; i < 5; i++)
                if (_slotContent[i] < 0) { SetStatus(GameText.ArrangeFillFirst); return; }

            _attempts++;
            // Capture WHICH order was produced, not merely that it was wrong. Must happen here:
            // VerifyRoutine returns every misplaced piece to the pool as it goes, so the board is
            // already gone by the time it raises. Slots locked green on an earlier attempt carry
            // their (correct) element, which is why only the FIRST entry is an unconstrained
            // production — the log's arrangeOrders documents that for the researcher.
            _submittedOrder = new int[5];
            for (int i = 0; i < 5; i++) _submittedOrder[i] = _slotContent[i];

            StartCoroutine(VerifyRoutine());
        }

        private IEnumerator VerifyRoutine()
        {
            _busy = true;
            // The board is unusable while _busy is set, and Arrange is the ONE screen a story
            // cannot get past until the order is right — so a throw anywhere below would leave
            // VERIFY, UNDO, every slot and every piece dead with no exit. That class of bug has
            // already stranded a learner twice in this project (Results, F46g). EventBus.Raise
            // now guards its own subscribers, which was the one plausible thrower here; this is
            // the belt to that pair of braces.
            //
            // finally, not catch: an iterator cannot yield inside a try that has a catch
            // (CS1626), and wrapping the body in a child coroutine does not work either --
            // Unity logs an inner coroutine's exception and simply never resumes the outer one,
            // so the finally would not run at all.
            bool handedOff = false;
            try
            {
                bool allCorrect = true;
                int worstElement = -1;   // the piece this learner has misplaced most often
                int worstMisses = 0;

                for (int i = 0; i < 5; i++)
                {
                    if (_slotLocked[i]) continue;

                    if (_slotContent[i] == i)
                    {
                        _slotLocked[i] = true;
                        if (slotButtons[i] != null) slotButtons[i].image.color = SlotLocked;
                        if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxSlotLock);
                        yield return new WaitForSeconds(0.15f);
                    }
                    else
                    {
                        allCorrect = false;
                        int wrongElement = _slotContent[i];
                        _missCount[wrongElement]++;
                        // Hint about the part the learner is struggling with MOST, not simply the
                        // last one in slot order to qualify — otherwise a piece missed eight times
                        // is shadowed by one that has only just crossed the threshold. The
                        // threshold is applied below, not here, so the last attempt can still
                        // reach for this piece when nothing has crossed it.
                        if (_missCount[wrongElement] > worstMisses)
                        {
                            worstMisses = _missCount[wrongElement];
                            worstElement = wrongElement;
                        }

                        if (slotButtons[i] != null) slotButtons[i].image.color = SlotWrong;
                        if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxSlotWiggle);
                        yield return new WaitForSeconds(0.35f);
                        _slotContent[i] = -1; // wrong piece returns to the pool
                    }
                }

                RefreshUI();

                if (allCorrect)
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxCorrect);
                    SetStatus(SummaRace.Core.Praise.ArrangePerfect());
                    if (SummaRace.Core.GameManager.Instance != null) SummaRace.Core.GameManager.Instance.SetArrangeResult(_attempts);
                    EventBus.Raise(new ArrangeVerified
                    {
                        correct = true,
                        attemptCount = _attempts,
                        placement = _submittedOrder
                    });

                    yield return new WaitForSeconds(1f);
                    SceneLoader.Go(SceneNames.Summary);

                    // Stays busy on purpose, exactly as the assist below does. Falling through to
                    // _busy = false re-armed a board whose slots are all filled and locked while
                    // the load was still in flight, and a second VERIFY there re-raises
                    // ArrangeVerified with a higher attemptCount — which SessionLogService takes
                    // as the study's arrangeAttempts, because the last raise wins. The loading
                    // fade blocks taps today, so this is the guard rather than the cure; the
                    // honesty of a study variable should not rest on a fade.
                    handedOff = true;
                    yield break;
                }

                EventBus.Raise(new ArrangeVerified
                {
                    correct = false,
                    attemptCount = _attempts,
                    placement = _submittedOrder
                });

                if (_attempts >= GameRules.ArrangeMaxAttempts)
                {
                    // Stays busy on purpose: the assist ends by leaving the scene, so the
                    // board must not accept taps while it plays out.
                    yield return StartCoroutine(AssistRoutine());
                    handedOff = true;
                    yield break;
                }

                // The hint is meant to arrive BEFORE the assist does: GameRules sets
                // ArrangeMaxAttempts to 4 precisely so the last attempt is the first one the
                // learner makes with a hint in front of them. Counting misses per piece did not
                // guarantee that — a learner who reshuffles everything each verify spreads the
                // misses so no single piece ever reaches ArrangeHintAfterMisses, and the screen
                // went straight from "Almost!" to finishing the order for them without once
                // saying what any part means. On the last attempt the most-missed piece gets the
                // hint whatever its count.
                bool lastAttempt = _attempts >= GameRules.ArrangeMaxAttempts - 1;
                bool hintEarned = worstElement >= 0
                    && (worstMisses >= GameRules.ArrangeHintAfterMisses || lastAttempt);

                // LoadingTips is the S-W-B-S-T definition list in element order — the array
                // is named for the loading overlay that also shows it, but index i really is
                // element i. See the note on GameText.LoadingTips before touching either.
                SetStatus(hintEarned
                    ? GameText.ArrangeHintPrefix + GameText.LoadingTips[worstElement]
                    : GameText.ArrangeAlmost);
            }
            finally
            {
                // Two paths above stay busy ON PURPOSE -- the solve and the assist both end by
                // leaving the scene, and re-arming a finished board lets a second VERIFY raise
                // ArrangeVerified again with a higher attemptCount, which is a study variable.
                if (!handedOff) _busy = false;
            }
        }

        /// <summary>
        /// Anti-frustration path (GDD "never punish the learner" / TDD §13 "never a dead end").
        /// Arrange is the only screen the story cannot pass until the answer is right, so after
        /// <see cref="GameRules.ArrangeMaxAttempts"/> failed verifies the remaining parts are
        /// placed for the learner and the story continues to Summary. In a 55-minute classroom
        /// session the alternative is the supervising researcher force-quitting the app — which
        /// files the run as abandoned and loses its data.
        ///
        /// The measure stays honest: the last <see cref="ArrangeVerified"/> raised carries
        /// correct = false and the REAL attempt count, so an assisted finish is visible in the
        /// log (attempts >= ArrangeMaxAttempts with correct = false) rather than dressed up as
        /// a solve. Nothing further is raised here — helping must not rewrite the record.
        /// </summary>
        private IEnumerator AssistRoutine()
        {
            _selectedPiece = -1;
            SetStatus(GameText.ArrangeAssistIntro);
            yield return new WaitForSeconds(1.2f);

            for (int i = 0; i < 5; i++)
            {
                if (_slotLocked[i]) continue;

                _slotContent[i] = i;    // element i is, by definition, slot i's part
                _slotLocked[i] = true;
                RefreshUI();            // skips locked slots, so paint the lock colour after it
                if (slotButtons[i] != null) slotButtons[i].image.color = SlotLocked;
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxSlotLock);
                yield return new WaitForSeconds(0.35f);
            }

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxCorrect);
            SetStatus(GameText.ArrangeAssistDone);
            if (SummaRace.Core.GameManager.Instance != null)
                SummaRace.Core.GameManager.Instance.SetArrangeResult(_attempts);

            // Record the assist explicitly. The true attempt count is already logged, but the
            // researcher must be able to separate "solved it" from "was helped to the end"
            // without inferring it from a threshold that may later be retuned. correct stays
            // false: the learner did not order these themselves, and the log should not say so.
            //
            // placement stays NULL on purpose, for the same reason. The board is now the correct
            // order, but the app put it there — appending it to arrangeOrders would file the
            // app's own answer as a sixth thing the learner produced, and a per-slot confusion
            // table built from that would count assisted runs as five slots correct. The
            // learner's real last attempt is already the final entry.
            EventBus.Raise(new ArrangeVerified
            {
                correct = false,
                attemptCount = _attempts,
                assisted = true
            });

            yield return new WaitForSeconds(1.6f); // time to read the completed order
            SceneLoader.Go(SceneNames.Summary);
        }

        // ---------- helpers ----------

        private bool IsPlaced(int element)
        {
            foreach (int content in _slotContent)
                if (content == element) return true;
            return false;
        }

        private void RefreshUI()
        {
            for (int i = 0; i < 5; i++)
            {
                bool filled = _slotContent[i] >= 0;
                if (slotLabels[i] != null)
                {
                    slotLabels[i].text = filled ? _pieceTexts[_slotContent[i]] : _story.elements[i].type;
                    slotLabels[i].fontStyle = filled ? FontStyles.Normal : FontStyles.Bold;
                    // Empty slot wears its SWBST colour; a placed piece reads as normal text.
                    // Ink, not Deep: these five labels sit on their own pastel slot, and a
                    // contrast pass measured Deep-on-pastel at 3.30-4.44:1 — under WCAG AA's
                    // 4.5:1, on the words that NAME the framework this game exists to teach,
                    // read by 9-year-olds on a classroom tablet at low brightness. Ink is the
                    // same hue darkened further (0.45 vs 0.30), so the SWBST colour language
                    // is unchanged and only the legibility moves.
                    slotLabels[i].color = filled ? LabelFilled : SwbstPalette.InkForIndex(i);
                }
                if (slotButtons[i] != null && !_slotLocked[i])
                {
                    // ---- "WHERE CAN THIS GO?" (owner, 2026-08-22) ----------------------------
                    //
                    // "when you select an answer, at least highlight the box where the answer
                    // can be put, so there is an indicator that it can be selected or moved,
                    // because the player doesn't know how to do it."
                    //
                    // Correct, and it was the screen's biggest gap: tap-a-piece-then-tap-a-slot
                    // is only discoverable if the second tap TARGET announces itself. Nothing
                    // changed at all between "nothing selected" and "a piece is in my hand", so
                    // a child who tapped a piece saw one pill tint and no hint of what to do
                    // next.
                    //
                    // Every empty slot now brightens and breathes while a piece is held. Every
                    // empty slot, not the correct one — this screen is a test of sequence, and
                    // lighting only the right box would answer the question for them.
                    bool armed = _selectedPiece >= 0 && !filled && !_slotLocked[i];
                    slotButtons[i].image.color =
                        filled ? SlotFilled
                               : armed ? SlotArmed
                                       : SwbstPalette.PastelForIndex(i);
                    var srt = slotButtons[i].transform as RectTransform;
                    if (srt != null) srt.localScale = Vector3.one * (armed ? 1.04f : 1f);
                }

                int element = _poolOrder[i];
                bool placed = IsPlaced(element);
                if (pieceButtons[i] != null)
                {
                    pieceButtons[i].gameObject.SetActive(!placed);
                    pieceButtons[i].image.color = element == _selectedPiece ? PieceSelected : PieceNormal;
                }
                if (pieceLabels[i] != null) pieceLabels[i].text = _pieceTexts[element];
            }
        }

        // The pool pills are a 2-column grid matching the web prototype exactly (x 0.06-0.485
        // and 0.515-0.94, three rows), so a pill is ~459 units wide on a 1080 reference - and
        // its label was authored with autosize OFF and overflow mode Overflow. The content
        // pipeline allows 53-character parts, which is three lines in that pill, and the third
        // line would have been DRAWN OUTSIDE it, over the neighbouring piece. Hardened above
        // through the shared helper; see SummaRace.UI.LabelFit for the count of how many other
        // labels in this app are one content change away from the same bug.

        /// <summary>
        /// Answers a tap on an already-solved slot with "that one is done" rather than silence.
        /// Reuses the lock sound the slot earned, so nothing here reads as a mistake.
        /// </summary>
        private void NudgeLockedSlot(int slot)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxSlotLock);

            // Punch the label, never the slot root: the root carries ButtonSquash, which drives
            // the same localScale from its own tween, and two tweens on one transform leave it
            // wherever the last one wrote.
            if (slotLabels[slot] != null)
                Tween.PunchScale(slotLabels[slot].transform, Vector3.one * 0.2f, 0.3f);
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private static void PlayClick()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
        }
    }
}
