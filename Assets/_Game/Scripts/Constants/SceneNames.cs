namespace SummaRace.Constants
{
    /// <summary>Scene name constants — the only place scene names are typed.</summary>
    public static class SceneNames
    {
        public const string Boot = "Boot";
        public const string MainMenu = "MainMenu";
        public const string NameEntry = "NameEntry";
        public const string SessionMap = "SessionMap";
        public const string StorySelect = "StorySelect";
        public const string Reader = "Reader";
        public const string Race = "Race";           // legacy park/trail race; not on the shipping path
        public const string RaceEndless = "MainSummaRace"; // experiment: Trash Dash base + SWBST gates
        public const string Arrange = "Arrange";
        public const string Summary = "Summary";
        public const string Results = "Results";
        public const string TeacherMenu = "TeacherMenu";
        // No Settings: the scene was never built and nothing routed to it. The narration
        // toggle learners actually need lives in the Reader, and AppSettings still carries
        // volumes/haptics in the model.
    }
}
