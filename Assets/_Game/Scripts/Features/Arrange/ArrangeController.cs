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
        private static readonly Color PieceNormal = new Color(0.96f, 0.87f, 0.70f);
        private static readonly Color PieceSelected = new Color(0.5f, 0.75f, 1f);

        private StoryData _story;
        private string[] _pieceTexts = new string[5];   // piece i = element i's correct text
        private int[] _poolOrder = new int[5];          // shuffled display order (element index per pool button)
        private readonly int[] _slotContent = new int[5];   // element index in each slot, -1 = empty
        private readonly bool[] _slotLocked = new bool[5];
        private readonly int[] _missCount = new int[5];     // per element, for hints
        private readonly Stack<(int piece, int slot)> _undoStack = new();
        private int _selectedPiece = -1;                // element index of selected pool piece
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
                int slotIndex = i, poolIndex = i; // capture
                if (slotButtons[i] != null)
                    slotButtons[i].onClick.AddListener(() => OnSlotTapped(slotIndex));
                if (pieceButtons[i] != null)
                    pieceButtons[i].onClick.AddListener(() => OnPieceTapped(poolIndex));
            }
            if (verifyButton != null) verifyButton.onClick.AddListener(OnVerify);
            if (undoButton != null) undoButton.onClick.AddListener(OnUndo);

            if (titleText != null) titleText.text = GameText.ArrangeTitle;
            if (undoLabel != null) undoLabel.text = GameText.UndoLabel;
            if (verifyLabel != null) verifyLabel.text = GameText.VerifyLabel;

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

            _busy = false;
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
                    slotButtons[i].image.color = filled ? SlotFilled : SwbstPalette.PastelForIndex(i);

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
