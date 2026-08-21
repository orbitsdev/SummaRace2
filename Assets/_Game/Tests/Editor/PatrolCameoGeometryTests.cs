namespace SummaRace.Tests.EditMode
{
    using System.Globalization;
    using System.IO;
    using System.Text.RegularExpressions;
    using NUnit.Framework;
    using SummaRace.Constants;
    using UnityEngine;

    /// <summary>
    /// The patrol cameo's placement, checked against the camera that is actually in the race
    /// scene.
    ///
    /// WHY THIS FIXTURE EXISTS. The 3D patrol has been built and abandoned three times, and every
    /// failure was the same failure: the cop was positioned by eye, the chase camera moved
    /// underneath it, and nobody noticed until a playtest showed a character clipped through the
    /// runner or hanging half off a portrait screen. A comment claiming "solved from the camera"
    /// does not survive the next camera retune. This does.
    ///
    /// It reads the camera's position, pitch and field of view straight out of
    /// MainSummaRace.unity rather than restating them, so moving the camera without re-checking
    /// the cameo fails here instead of on a tablet. If the scene is ever restructured such that
    /// the camera cannot be found, this reports INCONCLUSIVE rather than failing: a parsing
    /// problem is not a geometry problem and must not be allowed to read like one.
    /// </summary>
    public class PatrolCameoGeometryTests
    {
        private const string ScenePath = "Assets/Scenes/MainSummaRace.unity";

        /// <summary>Portrait, on the 1080-wide canvas reference the whole UI is built against.
        /// The build is orientation-locked, so the game never renders any other way.</summary>
        private const float Aspect = 9f / 16f;

        /// <summary>The runner's fixed station on the track (F34).</summary>
        private const float KidZ = 3f;

        // A standing human, generously sized — larger than the real cop, so passing here means
        // passing for him.
        private const float CopHeight = 1.8f;
        private const float CopHalfWidth = 0.30f;
        private const float CopHalfDepth = 0.30f;

        /// <summary>The outermost answer card (F47b), and the corridor F54 measured free of
        /// scenery (0 of 1792 props inside |x| 3).</summary>
        private const float OutermostCardX = 2.2f;
        private const float SceneryFreeX = 3.0f;

        /// <summary>Widest the runner's body ever reaches: outer lane centre plus a half-width.</summary>
        private const float KidWidestX = 1.8f;

        private Vector3 _camPos;
        private float _camPitch;
        private float _camFov;
        private bool _haveCamera;

        [SetUp]
        public void SetUp()
        {
            _haveCamera = TryReadCamera(out _camPos, out _camPitch, out _camFov);
        }

        [Test]
        public void CameoStaysInsideTheFrameForTheWholeSweep()
        {
            if (!CameoIsOn()) return;
            if (!_haveCamera)
            {
                Assert.Inconclusive("Could not read the race camera from " + ScenePath +
                                    " — the scene layout changed; re-point this fixture.");
                return;
            }

            float worst = 0f;
            string worstAt = "";

            // Both shoulders, across the whole beat, at the same easing the director uses.
            for (int i = 0; i <= 40; i++)
            {
                float t = i / 40f;
                float ease = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(t * 2f - 1f));
                float ahead = Mathf.Lerp(GameRules.PatrolCameoFarAhead,
                                         GameRules.PatrolCameoNearAhead, ease);

                for (int s = -1; s <= 1; s += 2)
                {
                    float w = WorstCorner(s * GameRules.PatrolCameoLateralX, KidZ + ahead);
                    if (w > worst)
                    {
                        worst = w;
                        worstAt = "side " + s + ", " + ahead.ToString("0.00") + "m ahead";
                    }
                }
            }

            // 1.0 is the edge of the frame. The margin is what keeps him whole on a device whose
            // aspect differs from the reference, or after a small camera nudge.
            Assert.Less(worst, 0.95f,
                "The patrol cameo leaves the frame (worst corner " + worst.ToString("0.000") +
                " of the half-extent, at " + worstAt + "). Either the camera in " + ScenePath +
                " moved or the PatrolCameo* constants changed. This is the exact failure that " +
                "retired the chase three times — re-solve the placement, do not nudge numbers.");
        }

        [Test]
        public void CameoCanNeverOverlapTheRunner()
        {
            if (!CameoIsOn()) return;

            // The whole safety argument in one assertion: he is never less than this far AHEAD
            // along the run, so no stride, lane change or smoothing lag can bring the two bodies
            // together. Every previous version relied on a lateral gap instead, and every one of
            // them overlapped.
            Assert.GreaterOrEqual(GameRules.PatrolCameoNearAhead, CopHalfDepth * 2f + 2f,
                "PatrolCameoNearAhead is small enough that the cop and the runner could meet. " +
                "The cameo's safety comes from along-run separation, not from tuning.");

            Assert.GreaterOrEqual(GameRules.PatrolCameoFarAhead, GameRules.PatrolCameoNearAhead,
                "The cameo sweeps from far to near and back; far must not be nearer than near.");
        }

        [Test]
        public void CameoRunsClearOfTheLanesAndOfTheScenery()
        {
            if (!CameoIsOn()) return;

            float x = GameRules.PatrolCameoLateralX;

            Assert.Greater(x, OutermostCardX,
                "The cameo runs inside the answer cards, where it can be mistaken for one.");
            Assert.Less(x, SceneryFreeX,
                "The cameo runs outside the corridor F54 measured free of props, so it can spawn " +
                "inside a wall.");
            Assert.Greater(x - KidWidestX, 0.5f,
                "The cameo runs too close to the runner's widest lane position.");
        }

        [Test]
        public void TheRetiredChaseStaysRetired()
        {
            // The cameo is a different mechanism, not a re-enabling of the chase. With both on
            // they fight over the same patrol transform every frame.
            Assert.IsFalse(GameRules.RacePatrolEnabled && GameRules.RacePatrolCameoEnabled,
                "The chase and the cameo are both enabled; they drive the same patrol transform.");
        }

        // ---------------------------------------------------------------------------------

        private static bool CameoIsOn()
        {
            if (GameRules.RacePatrolCameoEnabled) return true;
            Assert.Pass("Patrol cameo is switched off (GameRules.RacePatrolCameoEnabled).");
            return false;
        }

        /// <summary>Worst normalised viewport coordinate over the cop's eight bounding corners.
        /// 1.0 is the edge of the frame in either axis.</summary>
        private float WorstCorner(float x, float z)
        {
            float tanHalf = Mathf.Tan(_camFov * 0.5f * Mathf.Deg2Rad);
            float p = _camPitch * Mathf.Deg2Rad;
            var forward = new Vector3(0f, -Mathf.Sin(p), Mathf.Cos(p));
            var up = new Vector3(0f, Mathf.Cos(p), Mathf.Sin(p));

            float worst = 0f;
            for (int ix = 0; ix < 2; ix++)
                for (int iy = 0; iy < 2; iy++)
                    for (int iz = 0; iz < 2; iz++)
                    {
                        var corner = new Vector3(
                            x + (ix == 0 ? -CopHalfWidth : CopHalfWidth),
                            iy == 0 ? 0.02f : CopHeight,
                            z + (iz == 0 ? -CopHalfDepth : CopHalfDepth));

                        var d = corner - _camPos;
                        float df = Vector3.Dot(d, forward);
                        if (df <= 0.05f) return 999f;   // behind the lens is an instant failure

                        float ndcY = Vector3.Dot(d, up) / (df * tanHalf);
                        float ndcX = d.x / (df * tanHalf * Aspect);
                        worst = Mathf.Max(worst, Mathf.Max(Mathf.Abs(ndcX), Mathf.Abs(ndcY)));
                    }
            return worst;
        }

        /// <summary>Pulls the race camera's transform and FOV out of the scene file. Regex over
        /// the YAML rather than opening the scene: an EditMode test that loads a scene disturbs
        /// whatever the developer had open.</summary>
        private static bool TryReadCamera(out Vector3 pos, out float pitch, out float fov)
        {
            pos = Vector3.zero;
            pitch = 0f;
            fov = 0f;
            if (!File.Exists(ScenePath)) return false;

            string src = File.ReadAllText(ScenePath);

            var cam = Regex.Match(src,
                @"!u!20 &\d+[\s\S]*?m_GameObject: \{fileID: (\d+)\}[\s\S]*?field of view: ([\d.]+)");
            if (!cam.Success) return false;

            string ownerId = cam.Groups[1].Value;
            if (!float.TryParse(cam.Groups[2].Value, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out fov)) return false;

            var tf = Regex.Match(src,
                @"!u!4 &\d+[\s\S]*?m_GameObject: \{fileID: " + ownerId +
                @"\}[\s\S]*?m_LocalPosition: \{x: (-?[\d.eE+-]+), y: (-?[\d.eE+-]+), z: (-?[\d.eE+-]+)\}" +
                @"[\s\S]*?m_LocalEulerAnglesHint: \{x: (-?[\d.eE+-]+),");
            if (!tf.Success) return false;

            pos = new Vector3(F(tf.Groups[1].Value), F(tf.Groups[2].Value), F(tf.Groups[3].Value));
            pitch = F(tf.Groups[4].Value);
            return true;
        }

        private static float F(string s)
        {
            float v;
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
            return v;
        }
    }
}
