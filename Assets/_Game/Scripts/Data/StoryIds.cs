namespace SummaRace.Data
{
    /// <summary>
    /// The story id convention: <c>sNN_&lt;difficulty&gt;</c> (TDD §6.1). Kept in one place so
    /// StorySelect and SessionMap cannot disagree about which file a session maps to.
    /// </summary>
    public static class StoryIds
    {
        /// <summary>Difficulty order is also the unlock order within a session (GDD §3.1).</summary>
        public static readonly string[] Difficulties = { "easy", "average", "hard" };

        public static string For(int session, string difficulty) => $"s{session:00}_{difficulty}";

        public static string For(int session, int difficultyIndex) =>
            For(session, Difficulties[difficultyIndex]);
    }
}
