using PrimeTween;
using TMPro;
using UnityEngine;

namespace SummaRace.UI
{
    /// <summary>
    /// Types the story text out each time the Reader page changes (Phase F juice).
    /// Watches its own TMP_Text for a content change and replays, so the Reader
    /// controller needs no wiring. Speed is capped so long pages never make a
    /// Grade-4 reader wait (GDD north star: reading is never a chore).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class StoryTextReveal : MonoBehaviour
    {
        [SerializeField] private float secondsPerChar = 0.016f;
        [SerializeField] private float minDuration = 0.25f;
        [SerializeField] private float maxDuration = 0.9f;

        private const int ShowAll = 99999;

        private TMP_Text _text;
        private string _last;

        private void Awake() => _text = GetComponent<TMP_Text>();

        private void LateUpdate()
        {
            if (_text.text == _last) return;
            _last = _text.text;
            if (string.IsNullOrEmpty(_text.text)) { _text.maxVisibleCharacters = ShowAll; return; }
            Play();
        }

        private void Play()
        {
            _text.ForceMeshUpdate();
            int total = _text.textInfo.characterCount;
            if (total <= 0) { _text.maxVisibleCharacters = ShowAll; return; }

            Tween.StopAll(onTarget: _text);
            _text.maxVisibleCharacters = 0;
            float dur = Mathf.Clamp(total * secondsPerChar, minDuration, maxDuration);
            Tween.Custom(_text, 0f, total, dur, (t, v) => t.maxVisibleCharacters = Mathf.RoundToInt(v), Ease.Linear)
                 .OnComplete(_text, t => t.maxVisibleCharacters = ShowAll);
        }
    }
}
