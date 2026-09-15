using PrimeTween;
using SummaRace.Constants;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.UI
{
    /// <summary>
    /// The story mission as a level path: READ → RACE → ORDER → WRITE.
    ///
    /// Client feedback 2026-09-14, the goal in their own words: the learner must understand and
    /// complete each part before moving on to the next, and it must feel like a game, not a
    /// questionnaire. The parts of a story are now separate mastery gates, but nothing ever
    /// SHOWED the learner that they are on a journey of four parts, which part they just
    /// finished, or what is next — each screen simply replaced the last, like pages of a form.
    ///
    /// Shown on the loading card between parts (SceneLoader): the part just finished pops to a
    /// green check-coin, the path fills to the next part, and the next part pulses gold. On
    /// Results all four are complete. Built from generated shapes and Fredoka, no new art.
    /// </summary>
    public class MissionPath : MonoBehaviour
    {
        private readonly Image[] _nodes = new Image[4];
        private readonly TMP_Text[] _nodeText = new TMP_Text[4];
        private readonly TMP_Text[] _labels = new TMP_Text[4];
        private readonly Image[] _links = new Image[3];
        private TMP_Text _title;

        private static readonly Color Done = Theme.Grass;
        private static readonly Color Next = Theme.Sunny;
        private static readonly Color Later = new Color(0.80f, 0.80f, 0.82f);

        /// <summary>Builds the path inside <paramref name="parent"/> (stretched to fill it).</summary>
        public static MissionPath Build(Transform parent)
        {
            var root = new GameObject("MissionPath", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var path = root.AddComponent<MissionPath>();

            // No title row: the loader's own "Loading..." plaque already sits across the card's
            // top edge, and a title under it was hidden (seen in a capture).
            path._title = null;

            for (int i = 0; i < 3; i++)
            {
                var link = new GameObject("Link" + i, typeof(RectTransform));
                link.transform.SetParent(root.transform, false);
                var img = link.AddComponent<Image>();
                img.raycastTarget = false;
                img.color = Later;
                var lrt = img.rectTransform;
                float x0 = NodeX(i), x1 = NodeX(i + 1);
                lrt.anchorMin = new Vector2(x0, 0.50f); lrt.anchorMax = new Vector2(x1, 0.54f);
                lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                path._links[i] = img;
            }

            for (int i = 0; i < 4; i++)
            {
                var label = GameSkin.Badge(root.transform, "Node" + i, (i + 1).ToString(), Later, Color.white,
                    new Vector2(NodeX(i), 0.52f), new Vector2(0.5f, 0.5f), Vector2.zero, 112f);
                path._nodeText[i] = label;
                path._nodes[i] = label.transform.parent.GetComponent<Image>();

                var name = MakeText(root.transform, "Label" + i,
                    new Vector2(NodeX(i) - 0.12f, 0.04f), new Vector2(NodeX(i) + 0.12f, 0.24f), 34f);
                GameSkin.Heading(name, Theme.TextBrownDeep, outlined: false);
                name.text = i < GameText.MissionSteps.Length ? GameText.MissionSteps[i] : "";
                path._labels[i] = name;
            }
            return path;
        }

        private readonly Image[] _stars = new Image[4];
        private static Sprite _starSprite;

        private void SetStar(int i, bool on)
        {
            if (_stars[i] == null)
            {
                if (!on) return;
                if (_starSprite == null) _starSprite = Resources.Load<Sprite>("UI/icon_star");
                var go = new GameObject("Star", typeof(RectTransform));
                go.transform.SetParent(_nodes[i].transform, false);
                var img = go.AddComponent<Image>();
                img.sprite = _starSprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0.18f, 0.18f); rt.anchorMax = new Vector2(0.82f, 0.82f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                _stars[i] = img;
            }
            _stars[i].gameObject.SetActive(on);
        }

        private static float NodeX(int i) => 0.14f + i * (0.72f / 3f);

        private static TMP_Text MakeText(Transform parent, string name, Vector2 min, Vector2 max, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = true; t.fontSizeMin = 16f; t.fontSizeMax = size;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return t;
        }

        /// <summary>
        /// <paramref name="current"/> = the part the learner is about to do (0..3), or 4 when
        /// all four are done. The part just finished (current-1) is animated in.
        /// </summary>
        public void Show(int current)
        {
            current = Mathf.Clamp(current, 0, 4);
            if (_title != null) _title.text = current >= 4 ? GameText.MissionPathAllDone : GameText.MissionPathTitle;

            for (int i = 0; i < 4; i++)
            {
                if (_nodes[i] == null) continue;
                bool done = i < current;
                bool next = i == current;
                _nodes[i].color = done ? Done : next ? Next : Later;
                // A finished part shows a gold star (the fonts carry no check-mark glyph).
                _nodeText[i].text = done ? "" : (i + 1).ToString();
                _nodeText[i].color = next ? Theme.TextBrownDeep : Color.white;
                SetStar(i, done);

                var t = _nodes[i].transform;
                Tween.StopAll(onTarget: t);
                t.localScale = Vector3.one;
                if (i == current - 1)
                {
                    t.localScale = Vector3.one * 0.6f;
                    Tween.Scale(t, Vector3.one, 0.45f, Ease.OutBack, startDelay: 0.15f);
                }
                else if (next)
                {
                    Tween.Scale(t, Vector3.one * 1.12f, 0.55f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, startDelay: 0.5f);
                }
            }
            for (int i = 0; i < 3; i++)
                if (_links[i] != null) _links[i].color = i < current ? Done : Later;
        }
    }
}
