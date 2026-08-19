using System.Collections.Generic;
using UnityEngine;

namespace SummaRace.UI
{
    /// <summary>
    /// Lets Ms. Lumi have as many expressions as there are files, instead of the two sprite
    /// slots MsLumiReactor was built with.
    ///
    /// Drop PNGs into Assets/_Game/Resources/UI/Lumi/ and name them by pool:
    ///
    ///     idle_wave.png     idle_smile.png     idle_point.png   ...
    ///     cheer_hooray.png  cheer_clap.png     cheer_thumbsup.png ...
    ///
    /// Anything starting idle_ joins the idle pool; cheer_ joins the cheer pool. Count does not
    /// matter and no code or scene change is needed to add one — the same "swap a file" rule the
    /// hero art and the audio already follow.
    ///
    /// Selection is a shuffle bag, not a plain random: every pose in a pool is shown once before
    /// any repeats, and a refilled bag never starts with the pose that just played. That is the
    /// same picker Core/Praise.cs uses for praise lines, and it exists because true random on a
    /// small pool visibly repeats, which is exactly what makes a character feel canned.
    ///
    /// If the folder is missing or empty this returns null and MsLumiReactor falls back to its
    /// serialized sprites, so the game is never worse off than before.
    /// </summary>
    public static class LumiExpressions
    {
        public const string FolderPath = "UI/Lumi";
        public const string IdlePrefix = "idle_";
        public const string CheerPrefix = "cheer_";

        private static Sprite[] _idle;
        private static Sprite[] _cheer;
        private static bool _loaded;

        private static readonly List<Sprite> IdleBag = new List<Sprite>();
        private static readonly List<Sprite> CheerBag = new List<Sprite>();
        private static Sprite _lastIdle;
        private static Sprite _lastCheer;

        /// <summary>A pose from the idle pool, or null when no idle_* files exist.</summary>
        public static Sprite NextIdle()
        {
            Load();
            return Draw(_idle, IdleBag, ref _lastIdle);
        }

        /// <summary>A pose from the cheer pool, or null when no cheer_* files exist.</summary>
        public static Sprite NextCheer()
        {
            Load();
            return Draw(_cheer, CheerBag, ref _lastCheer);
        }

        /// <summary>How many poses each pool found. Handy in a preflight or a log line.</summary>
        public static void Count(out int idle, out int cheer)
        {
            Load();
            idle = _idle != null ? _idle.Length : 0;
            cheer = _cheer != null ? _cheer.Length : 0;
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            var all = Resources.LoadAll<Sprite>(FolderPath);
            if (all == null || all.Length == 0)
            {
                _idle = new Sprite[0];
                _cheer = new Sprite[0];
                return;
            }

            var idle = new List<Sprite>();
            var cheer = new List<Sprite>();

            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null || string.IsNullOrEmpty(s.name)) continue;
                var name = s.name.ToLowerInvariant();
                if (name.StartsWith(IdlePrefix)) idle.Add(s);
                else if (name.StartsWith(CheerPrefix)) cheer.Add(s);
                // Anything else is ignored rather than guessed at: a mis-named file should do
                // nothing visible, not land in the wrong pool where it reads as a bug.
            }

            _idle = idle.ToArray();
            _cheer = cheer.ToArray();
        }

        private static Sprite Draw(Sprite[] pool, List<Sprite> bag, ref Sprite last)
        {
            if (pool == null || pool.Length == 0) return null;
            if (pool.Length == 1) return pool[0];

            if (bag.Count == 0)
            {
                bag.AddRange(pool);
                // Refill must not hand back the pose already on screen.
                if (bag.Count > 1 && bag[bag.Count - 1] == last)
                {
                    var swap = bag[bag.Count - 1];
                    bag[bag.Count - 1] = bag[0];
                    bag[0] = swap;
                }
            }

            int index = Random.Range(0, bag.Count);
            var pick = bag[index];
            bag.RemoveAt(index);
            last = pick;
            return pick;
        }

        /// <summary>Editor-only: forget the cached pools so a newly added file is picked up.</summary>
        public static void Reset()
        {
            _loaded = false;
            _idle = null;
            _cheer = null;
            IdleBag.Clear();
            CheerBag.Clear();
            _lastIdle = null;
            _lastCheer = null;
        }
    }
}
