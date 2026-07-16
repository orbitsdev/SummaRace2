using UnityEngine;

namespace SummaRace.Data
{
    /// <summary>
    /// Loads and validates a story JSON from Resources/Stories by id.
    /// The single pipe that turns content into gameplay (TDD §7.6).
    /// </summary>
    public static class StoryLoader
    {
        public static StoryData Load(string storyId)
        {
            var asset = Resources.Load<TextAsset>("Stories/" + storyId);
            if (asset == null)
            {
                Debug.LogError($"StoryLoader: story '{storyId}' not found in Resources/Stories.");
                return null;
            }

            StoryData story;
            try
            {
                story = JsonUtility.FromJson<StoryData>(asset.text);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"StoryLoader: failed to parse '{storyId}': {e.Message}");
                return null;
            }

            return Validate(story) ? story : null;
        }

        /// <summary>Fail gracefully on bad content — never crash (GDD §11.6).</summary>
        public static bool Validate(StoryData s)
        {
            if (s == null) { Debug.LogError("StoryLoader: null story."); return false; }
            if (string.IsNullOrEmpty(s.id)) { Debug.LogError("StoryLoader: missing id."); return false; }
            if (s.pages == null || s.pages.Length < 1 || s.pages.Length > 5)
            { Debug.LogError($"StoryLoader [{s.id}]: pages must be 1..5."); return false; }
            if (s.elements == null || s.elements.Length != 5)
            { Debug.LogError($"StoryLoader [{s.id}]: exactly 5 SWBST elements required."); return false; }

            foreach (var page in s.pages)
            {
                if (page.question == null) continue;
                if (page.question.options == null || page.question.options.Length != 3)
                { Debug.LogError($"StoryLoader [{s.id}]: each question needs 3 options."); return false; }
                if (page.question.correctIndex < 0 || page.question.correctIndex > 2)
                { Debug.LogError($"StoryLoader [{s.id}]: correctIndex out of range."); return false; }
            }

            foreach (var el in s.elements)
            {
                if (el.distractors == null || el.distractors.Length != 2)
                { Debug.LogError($"StoryLoader [{s.id}]: each element needs 2 distractors."); return false; }
            }

            return true;
        }
    }
}
