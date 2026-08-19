using UnityEngine;
using SummaRace.Constants;

namespace SummaRace.Features.Race
{
    /// <summary>
    /// One visual recipe per session day, so the ten sessions read as a journey rather than the
    /// same corridor ten times (see Documentation/AssetGeneration/README.md §1–2).
    ///
    /// A world is data, not a scene, and that is the property that let ten of them exist: adding
    /// a world costs a table row.
    ///
    /// UNTIL F48 A ROW ONLY CARRIED LIGHT — sun colour/angle, ambient, linear fog, sky tint and
    /// the colour grade that reaches the track's unlit art. The geometry was whatever the track
    /// happened to spawn, which was always the same thing: their <c>Begin()</c> starts at zone 0
    /// and rotates every 500m, and the run is ~840m, so all thirty races opened on the same
    /// Industrial warehouses and, near the end, drifted into the same Suburbs. One street, thirty
    /// times, ten sessions apart — which is exactly what the owner reported ("one track is
    /// boring"). A second complete theme (NightTime: its own models, its own sky dome, its own
    /// clouds) was sitting unused on disk the whole time.
    ///
    /// A row now also names the PLACE: which theme, which zone family, which sky dome, and how
    /// much roadside greenery. All four are selected through Trash Dash's own public API by
    /// <see cref="Endless.EndlessWorldDressing"/> — no change to their scripts, no new art.
    /// </summary>
    public static class RaceWorlds
    {
        /// <summary>ThemeDatabase keys — the two complete themes shipped in Bundles/Themes.</summary>
        public const string ThemeDay = "Day";
        public const string ThemeNight = "NightTime";

        // Indices into ThemeData.zones. Both themes carry the same three families in the same
        // order (verified against both themeData.assets, guid by guid):
        //   0 = 2x IndustrialWarehouse + a road T-section  — big blank sheds, widest sky
        //   1 = SuburbsGarage + 3x SuburbsHouse            — houses, fences, a tree, bin bags
        //   2 = 14x Urban                                  — brick/plaster walls, blocks, dumpsters
        public const int ZoneIndustrial = 0;
        public const int ZoneSuburbs = 1;
        public const int ZoneUrban = 2;

        public struct World
        {
            public Color sun;
            public float sunIntensity;
            public Vector3 sunAngles;
            public Color ambient;
            public Color fog;
            public float fogStart;
            public float fogEnd;
            public Color sky;

            // ---- Place (F48). Everything above is light; everything below is where you are.
            public string theme;    // ThemeDay / ThemeNight
            public int zone;        // Zone* above — the family this world is MOSTLY built from
            public bool nightSky;   // sky dome, chosen independently of the theme (see below)
            public int trees;       // roadside Tree01 per track segment, both sides (0 = none)
            public int grass;       // roadside GrassClump01 per track segment

            // ---- Mix (F49). A world is mostly `zone`, with blocks of one or two other
            // families cut into it. weight <= 0 means "no accent", which reproduces F48's
            // single-family behaviour exactly, so an un-mixed row is unchanged.
            public int accentA;
            public int accentAWeight;
            public int accentB;
            public int accentBWeight;
            public float primaryRunMetres;  // roughly how far the primary family runs
            public float accentRunMetres;   // roughly how far an accent block runs

            /// <summary>True when this world has anything to mix in at all.</summary>
            public bool HasAccents { get { return accentAWeight > 0 || accentBWeight > 0; } }
        }

        // Ordered deliberately: day -> sunset -> night -> mist -> finale, so session 10 feels
        // like arriving somewhere.
        // ---- Weather -------------------------------------------------------------------
        // A world's weather, keyed by id rather than added to the World struct: the struct is
        // built by three chained builders (Make -> Place -> Mix) and threading one more value
        // through all of them touches ten recipes for a purely additive layer.
        //
        // Deliberately DETERMINISTIC per world. Every learner running s07 gets the same night
        // rain — weather that varied per run would be an uncontrolled variable in an instrument
        // whose whole job is to compare raceFirstPickCorrect across children.
        public const int WeatherNone = 0;
        public const int WeatherSnow = 1;   // slow, drifting, settles nothing
        public const int WeatherRain = 2;   // fast vertical streaks
        public const int WeatherMotes = 3;  // dust / pollen / fireflies hanging in the light

        /// <summary>Weather kind and how heavy it is (particles/sec) for a world id.</summary>
        public static void WeatherFor(string worldId, out int kind, out float rate)
        {
            switch (worldId)
            {
                // Only the worlds whose mood the weather SERVES. A clear bright morning with
                // snow in it reads as a bug, not as variety.
                case "overcast_industrial": kind = WeatherRain;  rate = 320f; return;
                case "misty_morning":       kind = WeatherMotes; rate =  70f; return;
                case "golden_fields":       kind = WeatherMotes; rate =  55f; return;
                case "autumn_lane":         kind = WeatherMotes; rate =  90f; return;
                case "starlit_finale":      kind = WeatherSnow;  rate = 110f; return;
                case "night_city":          kind = WeatherRain;  rate = 240f; return;
                default:                    kind = WeatherNone;  rate =   0f; return;
            }
        }

        private static World Make(float sr, float sg, float sb, float intensity,
                                  float pitch, float yaw,
                                  float ar, float ag, float ab,
                                  float fr, float fg, float fb, float fogStart, float fogEnd,
                                  float kr, float kg, float kb)
        {
            return new World
            {
                sun = new Color(sr, sg, sb),
                sunIntensity = intensity,
                sunAngles = new Vector3(pitch, yaw, 0f),
                ambient = new Color(ar, ag, ab),
                fog = new Color(fr, fg, fb),
                fogStart = fogStart,
                fogEnd = fogEnd,
                sky = new Color(kr, kg, kb),
            };
        }

        /// <summary>
        /// The second half of a row: which theme, which zone family, which sky dome, how green.
        /// Split from <see cref="Make"/> rather than added to it because Make already takes 17
        /// unlabelled floats — five more would make every row unreadable, and these five are the
        /// ones a reader actually needs to see to know where a session happens.
        ///
        /// WHY THE SKY DOME IS CHOSEN SEPARATELY FROM THE THEME. Their <c>Begin()</c> takes the
        /// dome from the theme, which binds a dark sky to dark buildings. Splitting them buys a
        /// combination neither theme ships: <c>blue_hour_suburbs</c> is daytime houses under the
        /// night dome, which is what blue hour actually looks like — the buildings have not lit
        /// up yet, the sky already has.
        /// </summary>
        private static World Place(World w, string theme, int zone, bool nightSky, int trees, int grass)
        {
            w.theme = theme;
            w.zone = zone;
            w.nightSky = nightSky;
            w.trees = trees;
            w.grass = grass;
            return w;
        }

        /// <summary>
        /// The third part of a row: WHAT ELSE this place is made of (F49).
        ///
        /// THE BUG THIS EXISTS TO FIX. F48 gave each world one zone family and then held it for
        /// the whole run, which was right about place and wrong about variety, because the three
        /// families are wildly uneven: Industrial has 3 segment prefabs, Suburbs 4, Urban 14. A
        /// five-gate race is ~840m and the pieces are 9–27m long, so a run lays roughly 55
        /// segments — and five of the ten worlds (morning_suburbs, bright_park,
        /// blue_hour_suburbs, autumn_lane, starlit_finale) were drawing all 55 of them from the
        /// same four Suburbs prefabs. One garage and three houses, each seen a dozen times per
        /// race, for half the study's sessions. Urban's fourteen pieces were reachable by only
        /// three worlds. The owner reported the race as repetitive twice, and was right.
        ///
        /// THE FIX IS A POOL, NOT A PREFAB. Nothing new is imported and the live segment count is
        /// untouched (their k_DesiredSegmentCount stays 10): a world now lays a BLOCK of its
        /// primary family, then a shorter block of an accent family, and alternates. Blocks are
        /// measured in metres of laid track, so they read the same whether the pieces are 9m
        /// walls or 27m warehouses. Suburbs worlds can now show up to 18 distinct segment
        /// prefabs in one race instead of 4.
        ///
        /// WHY BLOCKS AND NOT A PER-SEGMENT DRAW. A weighted coin flip per segment would put a
        /// warehouse between two houses every few seconds and the world would stop reading as
        /// one place — the thing F48 was built to fix. A block is long enough to be a stretch of
        /// street you travel down (roughly 3–8 seconds at race speed) and the accent is always
        /// the minority, so the primary family still owns the run.
        ///
        /// Every world gets a DIFFERENT mix and different block lengths, which is what keeps the
        /// five Suburbs worlds from becoming the same world with different lighting.
        /// </summary>
        private static World Mix(World w, int accentA, int weightA, int accentB, int weightB,
                                 float primaryRunMetres, float accentRunMetres)
        {
            w.accentA = accentA;
            w.accentAWeight = weightA;
            w.accentB = accentB;
            w.accentBWeight = weightB;
            w.primaryRunMetres = primaryRunMetres;
            w.accentRunMetres = accentRunMetres;
            return w;
        }

        /// <summary>
        /// A hash of the story id that is the SAME on every device and every run.
        ///
        /// <c>string.GetHashCode</c> is explicitly documented as not stable across processes or
        /// runtimes, and the director was seeding the world RNG with it. On Mono it happens to be
        /// deterministic today, so nothing was visibly wrong — but a study instrument that
        /// silently lays a different track for the same story on a different tablet, or after a
        /// runtime upgrade, is not reproducible, and reproducibility is the whole reason the
        /// track is seeded at all: every learner running s04_hard should run the SAME s04_hard.
        /// FNV-1a, unchecked so the overflow is the algorithm and not an exception.
        /// </summary>
        public static int StableSeed(string id)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (!string.IsNullOrEmpty(id))
                    for (int i = 0; i < id.Length; i++) { h ^= id[i]; h *= 16777619u; }
                return (int)(h & 0x7fffffff);
            }
        }

        /// <summary>
        /// Makes the THREE STORIES OF A SESSION lay three different streets (F57).
        ///
        /// THE BUG THIS EXISTS TO FIX. F48 gave each world a place and F49 gave it a family mix,
        /// but both are properties of the WORLD, and a world is per session — so easy, average
        /// and hard shared every one of them: same theme, same primary family, same accent
        /// weights, same block lengths, same greenery counts. The only thing that differed
        /// between the three races a learner runs in one session was the time of day and the gate
        /// spacing. Thirty races, ten distinct layouts. The owner reported the race as repetitive
        /// three times across F48/F49/F57 and was right every time, because each fix addressed a
        /// different axis and none of them addressed this one.
        ///
        /// WHAT IS DELIBERATELY *NOT* VARIED: theme, sky dome and the primary family. Those three
        /// are what make a session read as one place, which is the property F48 exists to create
        /// and the reason the ten worlds are ordered as a journey. A learner should recognise
        /// session 4 as the same town on all three runs — walking a different street of it, not
        /// waking up in a different city. <see cref="Endless.EndlessWorldDressing.Configure"/>
        /// also always OPENS the run on the world's own family, so the establishing shot is
        /// identical by construction whatever this does.
        ///
        /// WHAT IS VARIED is how much of the run belongs to the primary family and which accent
        /// cuts into it. Easy stays close to the authored recipe and is the greenest; hard gives
        /// the most road to the accents and strips the verge back. Combined with the story-seeded
        /// segment order (see EndlessWorldDressing.Configure), the three races of a session lay
        /// visibly different geometry from the same pool of prefabs — no new art, no extra
        /// memory, and the live segment count is untouched.
        /// </summary>
        public static World Vary(World w, string difficulty)
        {
            switch (difficulty)
            {
                case "easy":
                    // Closest to the authored recipe: longest primary blocks, shortest accents,
                    // greenest verge. The first race of a session is the one that has to
                    // establish the place, so it is the one that stays most faithful to it.
                    w.primaryRunMetres *= 1.12f;
                    w.accentRunMetres *= 0.85f;
                    w.trees = Mathf.RoundToInt(w.trees * 1.35f);
                    w.grass = Mathf.RoundToInt(w.grass * 1.30f);
                    break;

                case "hard":
                    // The accents take the most road here, and the RARER accent is promoted to
                    // the common one — so the family a learner saw least in the session's first
                    // two races is the one that carries its third. Verge stripped back: a later,
                    // barer version of the same street, which also agrees with the cooler, dimmer
                    // light ApplyTimeOfDay gives this difficulty.
                    w.primaryRunMetres *= 0.70f;
                    w.accentRunMetres *= 1.40f;
                    int swapA = w.accentA, swapAW = w.accentAWeight;
                    w.accentA = w.accentB; w.accentAWeight = w.accentBWeight;
                    w.accentB = swapA; w.accentBWeight = swapAW;
                    w.trees = Mathf.RoundToInt(w.trees * 0.55f);
                    w.grass = Mathf.RoundToInt(w.grass * 0.60f);
                    break;

                default:
                    // Average sits between the two on run length, and flattens the accent
                    // weighting so both accent families get comparable road. That is the single
                    // most varied of the three, which suits the middle race of a session.
                    w.primaryRunMetres *= 0.86f;
                    w.accentRunMetres *= 1.18f;
                    if (w.accentAWeight > 0 && w.accentBWeight > 0)
                    {
                        int flat = Mathf.Max(1, (w.accentAWeight + w.accentBWeight) / 2);
                        w.accentAWeight = flat;
                        w.accentBWeight = flat;
                    }
                    break;
            }

            // A recipe may not scale itself out of usability. The mix sequencer treats a block
            // shorter than the minimum as a reason to restart the block immediately, which at
            // zero length would spin it once per segment and shred the world into single pieces.
            w.primaryRunMetres = Mathf.Max(GameRules.RaceZoneMixMinBlockMetres, w.primaryRunMetres);
            w.accentRunMetres = Mathf.Max(GameRules.RaceZoneMixMinBlockMetres, w.accentRunMetres);
            w.trees = Mathf.Max(0, w.trees);
            w.grass = Mathf.Max(0, w.grass);
            return w;
        }

        /// <summary>
        /// The recipe an actual race should use: the session's world, varied for this story's
        /// difficulty. Call this rather than <see cref="For"/> anywhere a real run is being set
        /// up — For() is the authored row, this is the row as played.
        /// </summary>
        public static World ForRace(string worldId, string difficulty)
        {
            return Vary(For(worldId), difficulty);
        }

        public static World For(string worldId)
        {
            switch (worldId)
            {
                case "morning_suburbs":     // fresh, gentle, low sun
                    // Session 1: houses, with the walls and corner shops of the next street over.
                    return Mix(Place(Make(1.00f, 0.94f, 0.82f, 1.05f, 22f, 340f,
                                0.52f, 0.55f, 0.62f, 0.72f, 0.80f, 0.88f, 60f, 160f,
                                0.56f, 0.74f, 0.92f),
                                ThemeDay, ZoneSuburbs, false, 2, 6),
                                ZoneUrban, 3, ZoneIndustrial, 1, 95f, 40f);
                case "bright_park":         // open and sunny, air is clear
                    // The Industrial pieces are the widest-sky ones in the set, which is the
                    // closest thing the art has to open ground — so they carry "park" here.
                    return Mix(Place(Make(1.00f, 0.98f, 0.90f, 1.25f, 55f, 330f,
                                0.58f, 0.62f, 0.66f, 0.70f, 0.85f, 0.95f, 90f, 260f,
                                0.35f, 0.65f, 0.95f),
                                ThemeDay, ZoneSuburbs, false, 5, 12),
                                ZoneIndustrial, 2, ZoneUrban, 1, 105f, 45f);
                case "sunset_town":         // warm gold, long light
                    return Mix(Place(Make(1.00f, 0.72f, 0.42f, 1.10f, 14f, 300f,
                                0.55f, 0.44f, 0.42f, 0.95f, 0.62f, 0.42f, 45f, 150f,
                                0.98f, 0.60f, 0.42f),
                                ThemeDay, ZoneUrban, false, 1, 2),
                                ZoneSuburbs, 3, ZoneIndustrial, 1, 90f, 45f);
                case "blue_hour_suburbs":   // cool dusk, lights coming on
                    // The most urban of the suburbs worlds: dusk is when the street lights and
                    // lit windows of the Urban pieces do the most work.
                    return Mix(Place(Make(0.62f, 0.68f, 0.92f, 0.75f, 10f, 290f,
                                0.34f, 0.38f, 0.52f, 0.30f, 0.36f, 0.56f, 40f, 130f,
                                0.24f, 0.30f, 0.52f),
                                ThemeDay, ZoneSuburbs, true, 3, 4),
                                ZoneUrban, 4, ZoneIndustrial, 1, 80f, 55f);
                case "overcast_industrial": // flat, heavy, close
                    // Its own family has only THREE prefabs, so this is the world that needs the
                    // mix most: shortest primary block, longest accent block.
                    return Mix(Place(Make(0.82f, 0.84f, 0.86f, 0.70f, 60f, 20f,
                                0.48f, 0.50f, 0.53f, 0.62f, 0.64f, 0.67f, 30f, 95f,
                                0.60f, 0.63f, 0.66f),
                                ThemeDay, ZoneIndustrial, false, 0, 1),
                                ZoneUrban, 3, ZoneSuburbs, 1, 70f, 60f);
                case "golden_fields":       // wide and hopeful
                    return Mix(Place(Make(1.00f, 0.86f, 0.58f, 1.15f, 28f, 315f,
                                0.58f, 0.52f, 0.42f, 0.92f, 0.80f, 0.55f, 70f, 200f,
                                0.86f, 0.78f, 0.52f),
                                ThemeDay, ZoneIndustrial, false, 2, 14),
                                ZoneSuburbs, 2, ZoneUrban, 2, 75f, 55f);
                case "night_city":          // electric, dark, glowing
                    // Already the richest family (14 pieces), so it needs the least help — the
                    // longest primary block of the ten, with warehouse districts cut through it.
                    return Mix(Place(Make(0.48f, 0.56f, 0.85f, 0.45f, 35f, 200f,
                                0.20f, 0.22f, 0.32f, 0.10f, 0.12f, 0.20f, 35f, 110f,
                                0.05f, 0.06f, 0.12f),
                                ThemeNight, ZoneUrban, true, 0, 0),
                                ZoneIndustrial, 2, ZoneSuburbs, 1, 110f, 40f);
                case "misty_morning":       // quiet, the fog IS the world
                    return Mix(Place(Make(0.94f, 0.94f, 0.90f, 0.80f, 18f, 350f,
                                0.62f, 0.64f, 0.64f, 0.86f, 0.88f, 0.86f, 18f, 70f,
                                0.82f, 0.85f, 0.86f),
                                ThemeDay, ZoneUrban, false, 3, 5),
                                ZoneSuburbs, 2, ZoneIndustrial, 1, 95f, 45f);
                case "autumn_lane":         // crisp russet
                    return Mix(Place(Make(1.00f, 0.82f, 0.60f, 0.95f, 30f, 310f,
                                0.52f, 0.46f, 0.40f, 0.80f, 0.66f, 0.48f, 45f, 140f,
                                0.74f, 0.66f, 0.52f),
                                ThemeDay, ZoneSuburbs, false, 6, 8),
                                ZoneUrban, 2, ZoneIndustrial, 1, 100f, 40f);
                case "starlit_finale":      // deep navy, celebratory
                    return Mix(Place(Make(0.60f, 0.66f, 0.95f, 0.55f, 40f, 220f,
                                0.24f, 0.26f, 0.40f, 0.12f, 0.14f, 0.28f, 45f, 150f,
                                0.06f, 0.08f, 0.20f),
                                ThemeNight, ZoneSuburbs, true, 2, 3),
                                ZoneUrban, 3, ZoneIndustrial, 2, 85f, 50f);
                default:
                    return For("bright_park");
            }
        }

        /// <summary>
        /// Within a session the three stories share a world but shift time of day, so all 30
        /// races differ while a day still feels like one place. Light and fog only.
        /// </summary>
        private static void ApplyTimeOfDay(ref World w, string difficulty)
        {
            switch (difficulty)
            {
                case "easy":                       // earlier: warmer, brighter, lower sun
                    w.sunIntensity *= 1.08f;
                    w.sun = Color.Lerp(w.sun, new Color(1f, 0.92f, 0.78f), 0.25f);
                    w.sunAngles.x = Mathf.Max(8f, w.sunAngles.x - 8f);
                    w.fogEnd *= 1.10f;
                    break;
                case "hard":                       // later: cooler, dimmer, closer horizon
                    w.sunIntensity *= 0.86f;
                    w.sun = Color.Lerp(w.sun, new Color(0.72f, 0.78f, 0.98f), 0.25f);
                    w.ambient = Color.Lerp(w.ambient, new Color(0.22f, 0.25f, 0.36f), 0.30f);
                    w.fogEnd *= 0.85f;
                    break;
            }
        }

        /// <summary>
        /// Applies a world to the live scene. Everything is a global render setting or the one
        /// directional light, so nothing in the track's own systems has to change.
        /// </summary>
        public static void Apply(string worldId, string difficulty)
        {
            var w = For(worldId);
            ApplyTimeOfDay(ref w, difficulty);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = w.fog;
            RenderSettings.fogStartDistance = w.fogStart;
            RenderSettings.fogEndDistance = w.fogEnd;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = w.ambient;

            var sun = FindSun();
            if (sun != null)
            {
                sun.color = w.sun;
                sun.intensity = w.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(w.sunAngles);
            }

            var cam = Camera.main;
            if (cam != null && RenderSettings.skybox == null)
            {
                // No skybox material, but the track spawns its own Sky MESH which draws over
                // this, so the clear colour only shows where that mesh does not reach.
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = w.sky;
            }

            ApplyGrade(w);
        }

        /// <summary>
        /// The track's art is <b>unlit</b> — 158 renderers on Unlit/CurvedUnlit, plus an unlit
        /// sky mesh — so sun colour, intensity and ambient change nothing on screen. Without
        /// this, every world rendered as the same bright afternoon and only the numbers
        /// differed. A global colour grade is the one lever that reaches unlit geometry.
        /// </summary>
        private static void ApplyGrade(World w)
        {
            var host = GameObject.Find(GradeObjectName);
            if (host == null)
            {
                host = new GameObject(GradeObjectName);
                var v = host.AddComponent<UnityEngine.Rendering.Volume>();
                v.isGlobal = true;
                v.priority = 100f;                 // above anything the track sets up
                v.profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            }

            var volume = host.GetComponent<UnityEngine.Rendering.Volume>();
            UnityEngine.Rendering.Universal.ColorAdjustments grade;
            if (!volume.profile.TryGet(out grade))
                grade = volume.profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);

            // Brightness follows the world's sun: 1.0 is neutral, so a 0.45-sun night sits
            // about an stop and a bit down. Clamped so no world goes black or blows out.
            //
            // FLOOR RAISED FOR THE NIGHT THEME (F48). These numbers were tuned when every world
            // rendered the same daylit street, so "night" had to be manufactured entirely by
            // pulling the exposure down. A NightTime world now ships its own dark art — dark
            // brick, unlit windows, a night sky dome — and applying the old -1.15 stop on top of
            // that stacked two nights: verified on screen, the runner went to a black silhouette
            // and the road read as a single flat tone. The art carries the night; the grade only
            // has to agree with it.
            float floor = w.theme == ThemeNight ? -0.35f : -1.6f;
            grade.postExposure.overrideState = true;
            grade.postExposure.value = Mathf.Clamp(Mathf.Log(Mathf.Max(w.sunIntensity, 0.05f), 2f),
                                                   floor, 0.4f);

            // Hue comes from the sky, kept well short of full strength so the art stays readable.
            // Same reasoning as the exposure floor above: a night sky is a very dark colour, and
            // multiplying the already-dark night art by it is the second half of the double
            // night. The filter is there to say WHICH night, not how dark.
            grade.colorFilter.overrideState = true;
            grade.colorFilter.value = Color.Lerp(Color.white, w.sky, w.theme == ThemeNight ? 0.22f : 0.5f);

            grade.saturation.overrideState = true;
            grade.saturation.value = w.sunIntensity < 0.7f ? -12f : 0f;   // night reads calmer

            var cam = Camera.main;
            if (cam != null)
            {
                // GetComponent returns null here — their camera has no URP data component, and
                // without one URP renders no post-processing at all, so the grade above would
                // silently do nothing. The extension creates it if missing.
                var data = UnityEngine.Rendering.Universal.CameraExtensions
                    .GetUniversalAdditionalCameraData(cam);
                if (data != null) data.renderPostProcessing = true;
            }
        }

        private const string GradeObjectName = "[RaceWorldGrade]";

        private static Light _sun;

        /// <summary>Cached so the re-apply below costs nothing per frame.</summary>
        private static Light FindSun()
        {
            if (_sun != null) return _sun;

            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional) { _sun = lights[i]; break; }
            return _sun;
        }

        /// <summary>Drops the cached sun when leaving a race, so the next one re-finds its own.</summary>
        public static void Forget() => _sun = null;
    }
}
