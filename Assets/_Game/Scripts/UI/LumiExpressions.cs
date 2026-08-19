using System.Collections.Generic;
using UnityEngine;

namespace SummaRace.UI
{
    /// <summary>
    /// Lets Ms. Lumi have as many expressions as there are files, instead of the two sprite
    /// slots MsLumiReactor was built with.
    ///
    /// Drop PNGs into Assets/_Game/Resources/UI/Lumi/ and name them <c>pool_pose.png</c>:
    ///
    ///     idle_wave.png     idle_smile.png     idle_point_up.png  ...
    ///     cheer_hooray.png  cheer_proud.png    cheer_laugh.png    ...
    ///     lumi_thinking.png lumi_surprised.png                    ...
    ///
    /// The text before the first underscore IS the pool name, so a new prefix creates a new
    /// pool with no code change and — unlike the original two-prefix version — no file is
    /// ever silently ignored for being named something else. Count does not matter. This is
    /// the same "swap a file" rule the hero art and the narration already follow.
    ///
    /// Two ways to ask for a pose:
    ///   * <see cref="Next"/> / <see cref="NextIdle"/> / <see cref="NextCheer"/> — draw from a
    ///     pool when any pose in it will do (her resting fidget, her celebration).
    ///   * <see cref="Get"/> — name one exactly, for a beat that means something specific:
    ///     she points at the mission, she thinks about the order. Those must not be random.
    ///
    /// Pool draws are a shuffle bag, not plain random: every pose is shown once before any
    /// repeats, and a refilled bag never starts with the pose that just played. That is the
    /// same picker Core/Praise.cs uses for praise lines, and it exists because true random on
    /// a small pool visibly repeats, which is exactly what makes a character feel canned.
    ///
    /// Every accessor returns null when the folder or the name is missing, and every caller
    /// falls back to what it drew before, so the game is never worse off than without it.
    /// </summary>
    public static class LumiExpressions
    {
        public const string FolderPath = "UI/Lumi";

        /// <summary>Her resting poses — waving, smiling, listening.</summary>
        public const string PoolIdle = "idle";

        /// <summary>Her celebration poses. Only ever shown for something the learner got right.</summary>
        public const string PoolCheer = "cheer";

        /// <summary>
        /// Circular badge portraits, for the screens whose Lumi is a fixed round slot rather
        /// than a free-standing figure (Arrange, Summary). Same poses, cropped to a disc, so a
        /// badge screen can shuffle without a half-body suddenly appearing inside a circle.
        /// </summary>
        public const string PoolBadgeIdle = "badge";

        /// <summary>Badge-cropped celebration poses. <see cref="PoolBadgeIdle"/>'s counterpart.</summary>
        public const string PoolBadgeCheer = "badgecheer";

        private static Dictionary<string, Sprite[]> _pools;
        private static Dictionary<string, Sprite> _byName;
        private static readonly Dictionary<string, List<Sprite>> Bags = new Dictionary<string, List<Sprite>>();
        private static readonly Dictionary<string, Sprite> LastDrawn = new Dictionary<string, Sprite>();
        private static bool _loaded;

        /// <summary>A pose from the idle pool, or null when no idle_* files exist.</summary>
        public static Sprite NextIdle()
        {
            return Next(PoolIdle);
        }

        /// <summary>A pose from the cheer pool, or null when no cheer_* files exist.</summary>
        public static Sprite NextCheer()
        {
            return Next(PoolCheer);
        }

        /// <summary>
        /// A pose from any pool, named by the part of the filename before the first
        /// underscore. Null when that pool is empty.
        /// </summary>
        public static Sprite Next(string pool)
        {
            Load();
            if (string.IsNullOrEmpty(pool)) return null;
            pool = pool.ToLowerInvariant();

            Sprite[] all;
            if (!_pools.TryGetValue(pool, out all) || all.Length == 0) return null;
            if (all.Length == 1) return all[0];

            List<Sprite> bag;
            if (!Bags.TryGetValue(pool, out bag)) { bag = new List<Sprite>(); Bags[pool] = bag; }

            Sprite last;
            LastDrawn.TryGetValue(pool, out last);

            if (bag.Count == 0)
            {
                bag.AddRange(all);
                // A refill must not hand back the pose already on screen.
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
            LastDrawn[pool] = pick;
            return pick;
        }

        /// <summary>
        /// One named pose, e.g. <c>Get("idle_present")</c>. Null when there is no such file,
        /// which is the case every caller already handles by falling back to a pool draw —
        /// so renaming or removing a pose degrades the moment rather than breaking it.
        /// </summary>
        public static Sprite Get(string poseName)
        {
            Load();
            if (string.IsNullOrEmpty(poseName)) return null;
            Sprite s;
            return _byName.TryGetValue(poseName.ToLowerInvariant(), out s) ? s : null;
        }

        /// <summary>The first of these names that exists, or null. For "prefer A, else B".</summary>
        public static Sprite GetAny(params string[] poseNames)
        {
            if (poseNames == null) return null;
            for (int i = 0; i < poseNames.Length; i++)
            {
                var s = Get(poseNames[i]);
                if (s != null) return s;
            }
            return null;
        }

        /// <summary>How many poses the idle and cheer pools found. Handy in a preflight or a log line.</summary>
        public static void Count(out int idle, out int cheer)
        {
            idle = CountIn(PoolIdle);
            cheer = CountIn(PoolCheer);
        }

        /// <summary>How many poses one pool found.</summary>
        public static int CountIn(string pool)
        {
            Load();
            if (string.IsNullOrEmpty(pool)) return 0;
            Sprite[] all;
            return _pools.TryGetValue(pool.ToLowerInvariant(), out all) ? all.Length : 0;
        }

        /// <summary>Every pool name found, for a preflight report.</summary>
        public static ICollection<string> PoolNames()
        {
            Load();
            return _pools.Keys;
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            _pools = new Dictionary<string, Sprite[]>();
            _byName = new Dictionary<string, Sprite>();

            var all = Resources.LoadAll<Sprite>(FolderPath);
            if (all == null || all.Length == 0) return;

            var grouped = new Dictionary<string, List<Sprite>>();
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null || string.IsNullOrEmpty(s.name)) continue;

                var name = s.name.ToLowerInvariant();
                _byName[name] = s;

                int underscore = name.IndexOf('_');
                // No underscore means no pool to belong to. It stays reachable by name, which
                // is better than guessing a pool for it and having it turn up in a beat it was
                // never drawn for.
                if (underscore <= 0) continue;

                var pool = name.Substring(0, underscore);
                List<Sprite> list;
                if (!grouped.TryGetValue(pool, out list)) { list = new List<Sprite>(); grouped[pool] = list; }
                list.Add(s);
            }

            foreach (var kv in grouped) _pools[kv.Key] = kv.Value.ToArray();
        }

        /// <summary>Forget the cached pools so a newly added file is picked up.</summary>
        public static void Reset()
        {
            _loaded = false;
            _pools = null;
            _byName = null;
            Bags.Clear();
            LastDrawn.Clear();
        }
    }
}
