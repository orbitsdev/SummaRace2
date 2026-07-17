namespace SummaRace.Features.Race.Endless
{
    /// <summary>
    /// Set true by EndlessRaceDirector (MainSummaRace.unity only) while that scene runs.
    /// TrackManager checks it to suppress coins/premium/powerups — the SWBST answer
    /// gates become the only collectibles. Default false: the original Main.unity
    /// and the whole Trash Dash package behave exactly as shipped.
    /// </summary>
    public static class EndlessRaceMode
    {
        public static bool Active;
    }
}
