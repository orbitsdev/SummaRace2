using System;

namespace SummaRace.Data
{
    /// <summary>One story parsed from its JSON file (TDD §6.1). Content is data, never code.</summary>
    [Serializable]
    public class StoryData
    {
        public string id;              // "s01_easy"
        public int session;            // 1..10
        public string difficulty;      // "easy" | "average" | "hard"
        public string title;
        public string heroImage;       // Resources path, no extension
        public string mainIdea;
        public PageData[] pages;       // 1..5
        public ElementData[] elements; // exactly 5, S-W-B-S-T order
        public MissionConfig mission;
    }

    [Serializable]
    public class PageData
    {
        public string text;
        public string narration;       // Resources path, optional
        public QuestionData question;
    }

    [Serializable]
    public class QuestionData
    {
        public string text;
        public string[] options;       // 3 options
        public int correctIndex;       // 0..2
    }

    [Serializable]
    public class ElementData
    {
        public string type;            // "SOMEBODY" .. "THEN"
        public string correct;
        public string[] distractors;   // 2
    }

    [Serializable]
    public class MissionConfig
    {
        public float playerSpeed;
        public float checkpointSpacing;
        public float startingDanger;
        public float dangerPerSecond;
    }
}
