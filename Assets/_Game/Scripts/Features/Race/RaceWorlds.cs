using UnityEngine;

namespace SummaRace.Features.Race
{
    /// <summary>
    /// One visual recipe per session day, so the ten sessions read as a journey rather than the
    /// same corridor ten times (see Documentation/AssetGeneration/README.md §1–2).
    ///
    /// A world is data, not a scene: sun colour/angle, ambient, linear fog and sky colour. The
    /// props come from the theme the track already spawns, so adding a world costs a table row.
    /// </summary>
    public static class RaceWorlds
    {
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

        public static World For(string worldId)
        {
            switch (worldId)
            {
                case "morning_suburbs":     // fresh, gentle, low sun
                    return Make(1.00f, 0.94f, 0.82f, 1.05f, 22f, 340f,
                                0.52f, 0.55f, 0.62f, 0.72f, 0.80f, 0.88f, 60f, 160f,
                                0.56f, 0.74f, 0.92f);
                case "bright_park":         // open and sunny, air is clear
                    return Make(1.00f, 0.98f, 0.90f, 1.25f, 55f, 330f,
                                0.58f, 0.62f, 0.66f, 0.70f, 0.85f, 0.95f, 90f, 260f,
                                0.35f, 0.65f, 0.95f);
                case "sunset_town":         // warm gold, long light
                    return Make(1.00f, 0.72f, 0.42f, 1.10f, 14f, 300f,
                                0.55f, 0.44f, 0.42f, 0.95f, 0.62f, 0.42f, 45f, 150f,
                                0.98f, 0.60f, 0.42f);
                case "blue_hour_suburbs":   // cool dusk, lights coming on
                    return Make(0.62f, 0.68f, 0.92f, 0.75f, 10f, 290f,
                                0.34f, 0.38f, 0.52f, 0.30f, 0.36f, 0.56f, 40f, 130f,
                                0.24f, 0.30f, 0.52f);
                case "overcast_industrial": // flat, heavy, close
                    return Make(0.82f, 0.84f, 0.86f, 0.70f, 60f, 20f,
                                0.48f, 0.50f, 0.53f, 0.62f, 0.64f, 0.67f, 30f, 95f,
                                0.60f, 0.63f, 0.66f);
                case "golden_fields":       // wide and hopeful
                    return Make(1.00f, 0.86f, 0.58f, 1.15f, 28f, 315f,
                                0.58f, 0.52f, 0.42f, 0.92f, 0.80f, 0.55f, 70f, 200f,
                                0.86f, 0.78f, 0.52f);
                case "night_city":          // electric, dark, glowing
                    return Make(0.48f, 0.56f, 0.85f, 0.45f, 35f, 200f,
                                0.20f, 0.22f, 0.32f, 0.10f, 0.12f, 0.20f, 35f, 110f,
                                0.05f, 0.06f, 0.12f);
                case "misty_morning":       // quiet, the fog IS the world
                    return Make(0.94f, 0.94f, 0.90f, 0.80f, 18f, 350f,
                                0.62f, 0.64f, 0.64f, 0.86f, 0.88f, 0.86f, 18f, 70f,
                                0.82f, 0.85f, 0.86f);
                case "autumn_lane":         // crisp russet
                    return Make(1.00f, 0.82f, 0.60f, 0.95f, 30f, 310f,
                                0.52f, 0.46f, 0.40f, 0.80f, 0.66f, 0.48f, 45f, 140f,
                                0.74f, 0.66f, 0.52f);
                case "starlit_finale":      // deep navy, celebratory
                    return Make(0.60f, 0.66f, 0.95f, 0.55f, 40f, 220f,
                                0.24f, 0.26f, 0.40f, 0.12f, 0.14f, 0.28f, 45f, 150f,
                                0.06f, 0.08f, 0.20f);
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
                // No skybox material in this scene, so the camera's clear colour IS the sky.
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = w.sky;
            }
        }

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
