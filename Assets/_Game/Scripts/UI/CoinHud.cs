using PrimeTween;
using SummaRace.Constants;
using SummaRace.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.UI
{
    /// <summary>
    /// The coin counter and the "you earned it" payout — the reward loop the learning screens
    /// were missing (client feedback 2026-09-14: "feels like a survey, not a game").
    ///
    /// A correct answer used to change one colour and one line of text. Every game a Grade-4
    /// learner knows answers success with something EARNED that flies somewhere and ADDS UP:
    /// here, coins burst out of the thing they got right, fly into a counter that stays on
    /// screen, and the counter ticks and bounces with a chime.
    ///
    /// Coins are pure motivation (GameManager.RunCoins): not stars, not unlocks, not study data.
    /// Built in code like the rest of the runtime HUD, so no scene needs editing and a scene
    /// without it simply has no counter.
    /// </summary>
    public class CoinHud : MonoBehaviour
    {
        public static CoinHud Current { get; private set; }

        private TMP_Text _count;
        private RectTransform _icon;
        private int _shown;

        private static Sprite _coinSprite;
        private static Sprite _starSprite;

        private static Sprite CoinSprite => _coinSprite != null ? _coinSprite : (_coinSprite = Resources.Load<Sprite>("UI/icon_coin"));
        private static Sprite StarSprite => _starSprite != null ? _starSprite : (_starSprite = Resources.Load<Sprite>("UI/icon_star"));

        private void OnDestroy() { if (Current == this) Current = null; }

        /// <summary>Builds (once per scene) the counter chip inside <paramref name="canvasRoot"/>.</summary>
        public static CoinHud Ensure(Transform canvasRoot, Vector2 anchorMin, Vector2 anchorMax)
            => Ensure(canvasRoot, anchorMin, anchorMax, wallet: false);

        /// <summary>The same chip showing the learner's SAVED wallet (Session Map, Story Select,
        /// Main Menu, Results): the running total that gives a learner a reason to come back.</summary>
        public static CoinHud EnsureWallet(Transform canvasRoot, Vector2 anchorMin, Vector2 anchorMax)
            => Ensure(canvasRoot, anchorMin, anchorMax, wallet: true);

        private static CoinHud Ensure(Transform canvasRoot, Vector2 anchorMin, Vector2 anchorMax, bool wallet)
        {
            if (canvasRoot == null) return null;
            if (Current != null && Current.transform.parent == canvasRoot) return Current;

            var go = new GameObject("CoinHud", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();

            var bg = go.AddComponent<Image>();
            bg.raycastTarget = false;
            GameSkin.Card(bg, Theme.Navy, 3f, 5f);

            var iconGo = new GameObject("Coin", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var icon = iconGo.AddComponent<Image>();
            icon.sprite = CoinSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var irt = icon.rectTransform;
            irt.anchorMin = new Vector2(0f, 0.5f); irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(78f, 78f);
            irt.anchoredPosition = new Vector2(30f, 0f);

            var textGo = new GameObject("Count", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            GameSkin.Heading(text, Theme.Gold);
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true; text.fontSizeMin = 20f; text.fontSizeMax = 40f;
            text.raycastTarget = false;
            var trt = text.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(62f, 4f); trt.offsetMax = new Vector2(-10f, -4f);

            var hud = go.AddComponent<CoinHud>();
            hud._count = text;
            hud._icon = irt;
            var gm = SummaRace.Core.GameManager.Instance;
            hud._shown = gm == null ? 0 : wallet ? gm.WalletCoins : gm.RunCoins;
            text.text = hud._shown.ToString();
            Current = hud;
            return hud;
        }

        /// <summary>
        /// Pays <paramref name="amount"/> coins for something the learner just did at
        /// <paramref name="source"/>: sparkles there, coins fly to the counter, the counter ticks.
        /// Safe with no counter on screen (coins are still banked) and with no GameManager.
        /// </summary>
        public static void Reward(RectTransform source, int amount)
        {
            if (amount <= 0) return;
            if (SummaRace.Core.GameManager.Instance != null) SummaRace.Core.GameManager.Instance.AddCoins(amount);

            var hud = Current;
            if (source != null) Sparkle(source);
            if (hud == null || source == null)
            {
                if (hud != null) hud.SetCount(SummaRace.Core.GameManager.Instance != null ? SummaRace.Core.GameManager.Instance.RunCoins : hud._shown + amount);
                return;
            }
            hud.FlyCoins(source, amount);
        }

        /// <summary>
        /// Wallet chip in the top-right corner of the active scene's own canvas — the one call
        /// the menu screens (Main Menu, Session Map, Story Select) make. Skips SceneLoader's
        /// persistent overlay canvas, which lives in DontDestroyOnLoad.
        /// </summary>
        public static CoinHud EnsureWalletTopRight()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (c.gameObject.scene != scene || !c.isRootCanvas) continue;
                if (c.renderMode == RenderMode.WorldSpace) continue;
                return EnsureWallet(c.transform, new Vector2(0.70f, 0.952f), new Vector2(0.975f, 0.992f));
            }
            return null;
        }

        /// <summary>Counts the number up from <paramref name="from"/> to <paramref name="to"/>
        /// with coins raining into the icon — Results uses it to bank a story into the wallet.</summary>
        public void CountUp(int from, int to, float seconds = 1.2f, float delay = 0f)
        {
            if (to <= from) { SetCount(to); return; }
            SetCount(from);
            int last = from;
            Tween.Custom(this, (float)from, (float)to, seconds, (hud, v) =>
            {
                int n = Mathf.RoundToInt(v);
                if (n == last) return;
                last = n;
                hud.SetCount(n);
                if (hud._icon != null) { Tween.StopAll(onTarget: hud._icon); hud._icon.localScale = Vector3.one; Tween.PunchScale(hud._icon, Vector3.one * 0.2f, 0.12f); }
                if (AudioManager.Instance != null && n % 2 == 0) AudioManager.Instance.PlaySfx(AudioKeys.SfxCoin, 1f + 0.03f * (n % 8));
            }, Ease.OutQuad, startDelay: delay);
        }

        private void SetCount(int value)
        {
            _shown = value;
            if (_count != null) _count.text = value.ToString();
        }

        private void FlyCoins(RectTransform source, int amount)
        {
            var canvasRoot = transform.parent;
            int sprites = Mathf.Clamp(amount, 1, GameRules.CoinBurstMaxSprites);
            int target = SummaRace.Core.GameManager.Instance != null ? SummaRace.Core.GameManager.Instance.RunCoins : _shown + amount;
            int perCoin = Mathf.Max(1, amount / sprites);

            for (int i = 0; i < sprites; i++)
            {
                var go = new GameObject("FlyingCoin", typeof(RectTransform));
                go.transform.SetParent(canvasRoot, false);
                go.transform.SetAsLastSibling();
                var img = go.AddComponent<Image>();
                img.sprite = CoinSprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.sizeDelta = new Vector2(70f, 70f);
                rt.position = source.position;

                // Burst outward first, then home in — the arc is what makes it read as a payout.
                Vector3 burst = source.position + (Vector3)(Random.insideUnitCircle * 120f * source.lossyScale.x);
                float delay = i * 0.07f;
                bool last = i == sprites - 1;
                int stepValue = last ? target : Mathf.Min(target, _shown + perCoin * (i + 1));
                rt.localScale = Vector3.zero;
                Tween.Scale(rt, Vector3.one, 0.18f, Ease.OutBack, startDelay: delay);
                Sequence.Create()
                    .ChainDelay(delay)
                    .Chain(Tween.Position(rt, burst, 0.22f, Ease.OutQuad))
                    .Chain(Tween.Position(rt, _icon != null ? _icon.position : transform.position, 0.45f, Ease.InQuad))
                    .ChainCallback(() =>
                    {
                        if (go != null) Destroy(go);
                        if (this == null) return;
                        SetCount(stepValue);
                        if (_icon != null) { Tween.StopAll(onTarget: _icon); _icon.localScale = Vector3.one; Tween.PunchScale(_icon, Vector3.one * 0.35f, 0.25f); }
                        if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxCoin, 1f + 0.06f * (stepValue % 6));
                    });
            }
        }

        /// <summary>A quick ring of gold stars around a success. No asset needed beyond the star icon.</summary>
        public static void Sparkle(RectTransform source)
        {
            if (source == null) return;
            var canvas = source.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var root = canvas.rootCanvas.transform;
            const int count = 7;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Sparkle", typeof(RectTransform));
                go.transform.SetParent(root, false);
                go.transform.SetAsLastSibling();
                var img = go.AddComponent<Image>();
                img.sprite = StarSprite != null ? StarSprite : GameSkin.RoundSprite;
                img.color = Theme.Gold;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.sizeDelta = new Vector2(44f, 44f);
                rt.position = source.position;
                float ang = (i / (float)count) * Mathf.PI * 2f + Random.value * 0.4f;
                float dist = Mathf.Max(source.rect.width, source.rect.height) * 0.5f * source.lossyScale.x + 40f * source.lossyScale.x;
                Vector3 to = source.position + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * dist;
                rt.localScale = Vector3.one * 0.3f;
                Tween.Position(rt, to, 0.45f, Ease.OutCubic);
                Tween.Rotation(rt, Quaternion.Euler(0, 0, Random.Range(-180f, 180f)), 0.45f);
                Sequence.Create()
                    .Chain(Tween.Scale(rt, Vector3.one, 0.2f, Ease.OutBack))
                    .Chain(Tween.Scale(rt, Vector3.zero, 0.25f, Ease.InQuad))
                    .ChainCallback(() => { if (go != null) Destroy(go); });
            }
        }
    }
}
