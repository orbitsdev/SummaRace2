using System.Collections.Generic;
using SummaRace.Constants;

namespace SummaRace.Core
{
    /// <summary>
    /// Ms. Lumi's voice for every "correct!" moment (GDD §7.4 tone).
    ///
    /// Draws from the pools in <see cref="GameText"/> using a shuffle bag rather
    /// than plain random: a bag hands out every line once before refilling, so the
    /// learner never hears the same praise twice in a row. That matters here —
    /// across 10 sessions x 3 stories a learner sees ~12 praise moments per story,
    /// and a repeated line reads as canned.
    ///
    /// Strings stay in GameText (tone-editable in one place); only the picking
    /// lives here.
    /// </summary>
    public static class Praise
    {
        private static readonly Dictionary<string, Bag> Bags = new Dictionary<string, Bag>();

        /// <summary>Every Nth correct pick in the race names the SWBST part instead
        /// of praising generically — keeps it fresh and reinforces the framework.</summary>
        private const int SpecificEvery = 3;

        private static int _racePickCount;

        /// <summary>Generic process praise — used where the SWBST part isn't known
        /// (Reader questions carry no element type).</summary>
        public static string Generic() => Draw("generic", GameText.PraiseGeneric);

        /// <summary>Praise for a race pickup: mostly generic, every third one names
        /// the element. <paramref name="elementIndex"/> is S=0 W=1 B=2 S=3 T=4.</summary>
        public static string ForRace(int elementIndex)
        {
            _racePickCount++;
            bool nameTheElement = _racePickCount % SpecificEvery == 0
                                  && elementIndex >= 0
                                  && elementIndex < GameText.PraiseByElement.Length;

            return nameTheElement
                ? Draw("elem" + elementIndex, GameText.PraiseByElement[elementIndex])
                : Generic();
        }

        /// <summary>Praise for a completed Arrange grid.</summary>
        public static string ArrangePerfect() => Draw("arrange", GameText.ArrangePerfectPool);

        /// <summary>Results praise for a 1–3 star finish.</summary>
        public static string ForStars(int stars)
        {
            if (stars < 1 || stars >= GameText.PraiseByStars.Length) stars = 1;
            return Draw("stars" + stars, GameText.PraiseByStars[stars]);
        }

        /// <summary>Called when a new story starts so the race cadence restarts.
        /// Starts at a random offset: a race is only 5 gates long, so a fixed start
        /// would name the same SWBST element every single story.</summary>
        public static void ResetRun() => _racePickCount = UnityEngine.Random.Range(0, SpecificEvery);

        // ---------- shuffle bag ----------

        private static string Draw(string key, string[] pool)
        {
            if (pool == null || pool.Length == 0) return "";
            if (pool.Length == 1) return pool[0];

            Bag bag;
            if (!Bags.TryGetValue(key, out bag) || bag.Pool != pool)
            {
                bag = new Bag(pool);
                Bags[key] = bag;
            }
            return bag.Next();
        }

        private sealed class Bag
        {
            public readonly string[] Pool;
            private readonly List<int> _remaining = new List<int>();
            private int _lastDrawn = -1;

            public Bag(string[] pool) { Pool = pool; }

            public string Next()
            {
                if (_remaining.Count == 0) Refill();
                int slot = UnityEngine.Random.Range(0, _remaining.Count);
                int index = _remaining[slot];
                _remaining.RemoveAt(slot);
                _lastDrawn = index;
                return Pool[index];
            }

            private void Refill()
            {
                for (int i = 0; i < Pool.Length; i++) _remaining.Add(i);
                // Don't let the refill immediately repeat the line that just played.
                if (_lastDrawn >= 0 && _remaining.Count > 1) _remaining.Remove(_lastDrawn);
            }
        }
    }
}
