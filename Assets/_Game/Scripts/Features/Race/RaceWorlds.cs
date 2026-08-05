using UnityEngine;

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
            public int zone;        // Zone* above — held for the whole run, never rotated
            public bool nightSky;   // sky dome, chosen independently of the theme (see below)
            public int trees;       // roadside Tree01 per track segment, both sides (0 = none)
            public int grass;       // roadside GrassClump01 per track segment
        }

        // Ordered deliberately: day -> sunset -> night -> mist -> finale, so session 10 feels
        // like arriving somewhere.
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

        public static World For(string worldId)
        {
            switch (worldId)
            {
                case "morning_suburbs":     // fresh, gentle, low sun
                    return Place(Make(1.00f, 0.94f, 0.82f, 1.05f, 22f, 340f,
                                0.52f, 0.55f, 0.62f, 0.72f, 0.80f, 0.88f, 60f, 160f,
                                0.56f, 0.74f, 0.92f),
                                ThemeDay, ZoneSuburbs, false, 2, 6);
                case "bright_park":         // open and sunny, air is clear
                    return Place(Make(1.00f, 0.98f, 0.90f, 1.25f, 55f, 330f,
                                0.58f, 0.62f, 0.66f, 0.70f, 0.85f, 0.95f, 90f, 260f,
                                0.35f, 0.65f, 0.95f),
                                ThemeDay, ZoneSuburbs, false, 5, 12);
                case "sunset_town":         // warm gold, long light
                    return Place(Make(1.00f, 0.72f, 0.42f, 1.10f, 14f, 300f,
                                0.55f, 0.44f, 0.42f, 0.95f, 0.62f, 0.42f, 45f, 150f,
                                0.98f, 0.60f, 0.42f),
                                ThemeDay, ZoneUrban, false, 1, 2);
                case "blue_hour_suburbs":   // cool dusk, lights coming on
                    return Place(Make(0.62f, 0.68f, 0.92f, 0.75f, 10f, 290f,
                                0.34f, 0.38f, 0.52f, 0.30f, 0.36f, 0.56f, 40f, 130f,
                                0.24f, 0.30f, 0.52f),
                                ThemeDay, ZoneSuburbs, true, 3, 4);
                case "overcast_industrial": // flat, heavy, close
                    return Place(Make(0.82f, 0.84f, 0.86f, 0.70f, 60f, 20f,
                                0.48f, 0.50f, 0.53f, 0.62f, 0.64f, 0.67f, 30f, 95f,
                                0.60f, 0.63f, 0.66f),
                                ThemeDay, ZoneIndustrial, false, 0, 1);
                case "golden_fields":       // wide and hopeful
                    return Place(Make(1.00f, 0.86f, 0.58f, 1.15f, 28f, 315f,
                                0.58f, 0.52f, 0.42f, 0.92f, 0.80f, 0.55f, 70f, 200f,
                                0.86f, 0.78f, 0.52f),
                                ThemeDay, ZoneIndustrial, false, 2, 14);
                case "night_city":          // electric, dark, glowing
                    return Place(Make(0.48f, 0.56f, 0.85f, 0.45f, 35f, 200f,
                                0.20f, 0.22f, 0.32f, 0.10f, 0.12f, 0.20f, 35f, 110f,
                                0.05f, 0.06f, 0.12f),
                                ThemeNight, ZoneUrban, true, 0, 0);
                case "misty_morning":       // quiet, the fog IS the world
                    return Place(Make(0.94f, 0.94f, 0.90f, 0.80f, 18f, 350f,
                                0.62f, 0.64f, 0.64f, 0.86f, 0.88f, 0.86f, 18f, 70f,
                                0.82f, 0.85f, 0.86f),
                                ThemeDay, ZoneUrban, false, 3, 5);
                case "autumn_lane":         // crisp russet
                    return Place(Make(1.00f, 0.82f, 0.60f, 0.95f, 30f, 310f,
                                0.52f, 0.46f, 0.40f, 0.80f, 0.66f, 0.48f, 45f, 140f,
                                0.74f, 0.66f, 0.52f),
                                ThemeDay, ZoneSuburbs, false, 6, 8);
                case "starlit_finale":      // deep navy, celebratory
                    return Place(Make(0.60f, 0.66f, 0.95f, 0.55f, 40f, 220f,
                                0.24f, 0.26f, 0.40f, 0.12f, 0.14f, 0.28f, 45f, 150f,
                                0.06f, 0.08f, 0.20f),
                                ThemeNight, ZoneSuburbs, true, 2, 3);
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
