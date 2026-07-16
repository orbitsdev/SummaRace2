using System.Collections;
using System.Collections.Generic;
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
    /// wrong ones wiggle amber and return to the pool. Unlimited retries;
    /// a hint appears after 3 misses on the same piece.
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

        private static readonly Color SlotEmpty = new Color(0.92f, 0.92f, 0.88f);
        private static readonly Color SlotFilled = new Color(0.96f, 0.87f, 0.70f);
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

        private void Start()
        {
            _story = GameManager.Instance != null ? GameManager.Instance.CurrentStory : null;
            if (_story == null) _story = StoryLoader.Load("s01_easy"); // editor-direct fallback
            if (_story == null) { Debug.LogError("Arrange: no story."); return; }

            // Pieces come from the race result when available (same texts either way).
            var result = GameManager.Instance != null ? GameManager.Instance.LastRaceResult : null;
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

            RefreshUI();
            SetStatus("Tap a story part, then tap its place in the order.");
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
            if (_busy || _slotLocked[slot]) return;
            PlayClick();

            if (_slotContent[slot] >= 0)
            {
                // Tapping a filled slot returns its piece to the pool.
                _slotContent[slot] = -1;
                RefreshUI();
                return;
            }

            if (_selectedPiece < 0) return;

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
        }

        private void OnVerify()
        {
            if (_busy) return;
            for (int i = 0; i < 5; i++)
                if (_slotContent[i] < 0) { SetStatus("Fill every slot first!"); return; }

            PlayClick();
            _attempts++;
            StartCoroutine(VerifyRoutine());
        }

        private IEnumerator VerifyRoutine()
        {
            _busy = true;
            bool allCorrect = true;
            int hintElement = -1;

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
                    if (_missCount[wrongElement] >= 3) hintElement = wrongElement;

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
                SetStatus("Perfect order! Great job!");
                if (GameManager.Instance != null) GameManager.Instance.SetArrangeResult(_attempts);
                EventBus.Raise(new ArrangeVerified { correct = true, attemptCount = _attempts });

                yield return new WaitForSeconds(1f);
                if (SceneLoader.Instance != null) SceneLoader.Instance.Load(SceneNames.Summary);
            }
            else
            {
                EventBus.Raise(new ArrangeVerified { correct = false, attemptCount = _attempts });
                SetStatus(hintElement >= 0
                    ? "Hint: " + GameText.LoadingTips[hintElement]
                    : "Almost! The green ones are locked in — try the others again.");
            }

            _busy = false;
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
                if (slotLabels[i] != null)
                {
                    slotLabels[i].text = _slotContent[i] >= 0
                        ? _pieceTexts[_slotContent[i]]
                        : _story.elements[i].type;
                    slotLabels[i].fontStyle = _slotContent[i] >= 0 ? FontStyles.Normal : FontStyles.Bold;
                }
                if (slotButtons[i] != null && !_slotLocked[i])
                    slotButtons[i].image.color = _slotContent[i] >= 0 ? SlotFilled : SlotEmpty;

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
