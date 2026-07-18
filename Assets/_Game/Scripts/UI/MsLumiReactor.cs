using System.Collections;
using PrimeTween;
using SummaRace.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.UI
{
    /// <summary>
    /// Makes Ms. Lumi a living reading buddy: idle = friendly wave, and she pops to a
    /// cheer pose (with a happy punch) when the learner answers a Reader question
    /// correctly, then settles back. Pose swaps only — lightweight, offline, on-style.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class MsLumiReactor : MonoBehaviour
    {
        [SerializeField] private Sprite idleSprite;   // waving
        [SerializeField] private Sprite cheerSprite;  // both hands up
        [SerializeField] private float cheerSeconds = 1.6f;

        private Image _image;
        private Coroutine _routine;

        private void Awake()
        {
            _image = GetComponent<Image>();
            if (idleSprite != null) _image.sprite = idleSprite;
        }

        private void OnEnable() => EventBus.Subscribe<PageAnswered>(OnAnswered);
        private void OnDisable() => EventBus.Unsubscribe<PageAnswered>(OnAnswered);

        private void OnAnswered(PageAnswered evt)
        {
            if (!evt.correct) return; // never react to a wrong answer (never-punish)
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Cheer());
        }

        private IEnumerator Cheer()
        {
            if (cheerSprite != null) _image.sprite = cheerSprite;
            Tween.PunchScale(transform, Vector3.one * 0.18f, 0.5f);
            yield return new WaitForSeconds(cheerSeconds);
            if (idleSprite != null) _image.sprite = idleSprite;
            _routine = null;
        }
    }
}
