using SummaRace.Constants;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.UI
{
    /// <summary>
    /// A short explanatory line hung under a screen's title.
    ///
    /// WHY IN CODE. The two screens that need one — Story Select and Session Map — were both
    /// authored before the line existed, so neither has a field to put it in. Cloning the
    /// title carries that screen's own TMP font asset and material; a freshly built TMP_Text
    /// falls back to the project default and reads as a different typeface directly under the
    /// heading it belongs to.
    ///
    /// WHY IT BRINGS ITS OWN BACKING. Both screens draw over painted art whose brightest
    /// pixels are pure white (the Session Map's grass has white daisies in it). An average
    /// contrast figure is the wrong statistic for legibility — the worst pixel under a glyph
    /// is the one that decides it — so the line owns its background instead of depending on
    /// what happens to be behind it.
    /// </summary>
    public static class SubtitleLine
    {
        private const string ObjectName = "Subtitle";

        /// <summary>
        /// Adds a subtitle beneath <paramref name="title"/>, once. Null-safe and idempotent:
        /// a second call returns the line already there rather than stacking another on it.
        /// </summary>
        /// <param name="heightPixels">Line height on the canvas's own reference scale.</param>
        /// <param name="gapPixels">Space between the title's bottom edge and the line.</param>
        public static TMP_Text Add(TMP_Text title, string text,
                                   float heightPixels = 54f, float gapPixels = 8f)
        {
            if (title == null || string.IsNullOrEmpty(text)) return null;

            var host = title.rectTransform;
            var existing = host.Find(ObjectName);
            if (existing != null) return existing.GetComponentInChildren<TMP_Text>();

            // Hung off the TITLE'S OWN bottom edge rather than positioned in the parent, so it
            // lands correctly whatever anchoring that screen's title happens to use — the two
            // callers do not agree, and a fractional guess would put it off-screen on one.
            var pillGo = new GameObject(ObjectName, typeof(RectTransform));
            pillGo.transform.SetParent(host, false);

            var pill = pillGo.AddComponent<Image>();
            pill.sprite = Resources.Load<Sprite>("UI/bar_bg");
            if (pill.sprite != null) pill.type = Image.Type.Sliced;
            pill.color = pill.sprite != null
                ? Theme.Alpha(Color.white, 0.92f)
                : Theme.Alpha(Theme.Ink, 0.55f);
            pill.raycastTarget = false;   // never steal a tap from anything under it

            var prect = pill.rectTransform;
            prect.anchorMin = new Vector2(0.5f, 0f);
            prect.anchorMax = new Vector2(0.5f, 0f);
            prect.pivot = new Vector2(0.5f, 1f);
            prect.anchoredPosition = new Vector2(0f, -gapPixels);
            prect.sizeDelta = new Vector2(Mathf.Max(host.rect.width, 520f), heightPixels);

            var labelGo = Object.Instantiate(title.gameObject, pillGo.transform, false);
            labelGo.name = "Label";
            labelGo.SetActive(true);

            var label = labelGo.GetComponent<TMP_Text>();
            if (label == null) { Object.Destroy(labelGo); return null; }

            // Strip anything the title carried that would animate or re-title this line.
            foreach (var extra in labelGo.GetComponents<MonoBehaviour>())
                if (!(extra is TMP_Text)) Object.Destroy(extra);

            label.text = text;
            label.color = Theme.Paper;            // light on the navy pill (12.9:1, AA)
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 30f;
            label.fontStyle = FontStyles.Normal;  // the title may be bold; this is not a heading
            label.raycastTarget = false;

            var lrect = label.rectTransform;
            lrect.localScale = Vector3.one;
            lrect.localRotation = Quaternion.identity;
            lrect.anchorMin = Vector2.zero;
            lrect.anchorMax = Vector2.one;
            lrect.pivot = new Vector2(0.5f, 0.5f);
            lrect.offsetMin = new Vector2(18f, 4f);
            lrect.offsetMax = new Vector2(-18f, -4f);

            return label;
        }
    }
}
