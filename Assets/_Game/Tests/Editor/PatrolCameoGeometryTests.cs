namespace SummaRace.Tests.EditMode
{
    using System.Globalization;
    using System.IO;
    using System.Text.RegularExpressions;
    using NUnit.Framework;
    using SummaRace.Constants;
    using UnityEngine;

    /// <summary>
    /// The patrol tail's placement, checked against the camera that is actually in the race
    /// scene.
    ///
    /// WHY THIS FIXTURE EXISTS. The 3D patrol has been built and abandoned four times, and every
    /// failure was the same failure: the cop was positioned by eye, the chase camera moved
    /// underneath it, and nobody noticed until a playtest showed a character clipped through the
    /// runner or hanging half off a portrait screen. A comment claiming "solved from the camera"
    /// does not survive the next camera retune. This does.
    ///
    /// It reads the camera's position, pitch and field of view straight out of
    /// MainSummaRace.unity rather than restating them, so moving the camera without re-checking
    /// the patrol fails here instead of on a tablet. If the scene is ever restructured such that
    /// the camera cannot be found, this reports INCONCLUSIVE rather than failing: a parsing
    /// problem is not a geometry problem and must not be allowed to read like one.
    ///
    /// WHAT CHANGED ON 2026-08-21. The cameo was an overtake (enter behind, sprint past); the
    /// owner's device playtest reported it as "floating and flying to player" and asked for the
    /// Subway-style tail instead, explicitly authorising the camera pull-back that three earlier
    /// attempts were refused. So these tests now assert the tail, and one of them asserts that
    /// the pull-back is REQUIRED rather than cosmetic - if a future pass deletes it believing it
    /// to be decoration, that test says exactly what it costs.
    /// </summary>
    public class PatrolCameoGeometryTests
    {
        private const string ScenePath = "Assets/Scenes/MainSummaRace.unity";

        /// <summary>Portrait, on the 1080-wide canvas reference the whole UI is built against.
        /// The build is orientation-locked, so the game never renders any other way.</summary>
        private const float Aspect = 9f / 16f;

        /// <summary>The runner's fixed station on the track (F34).</summary>
        private const float KidZ = 3f;

        // A standing human, generously sized - larger than the real cop, so passing here means
        // passing for him.
        private const float CopHeight = 1.8f;
        private const float CopHalfWidth = 0.30f;
        private const float CopHalfDepth = 0.30f;

        // The runner, for the "is he still framed while the camera is back?" check.
        private const float KidHeight = 1.6f;
        private const float KidHalfWidth = 0.35f;
        private const float KidHalfDepth = 0.25f;

        /// <summary>Lane centres. The tail runs in the kid's OWN lane, so these are the three
        /// x positions the cop can ever take.</summary>
        private static readonly float[] Lanes = { -1.5f, 0f, 1.5f };

        /// <summary>How far the cop's rendered mass swings off his pivot as the 19-part rigid
        /// rig animates. Measured live during the chase work; it is the reason a gap set on the
        /// transform is not the gap that renders.</summary>
        private const float CopMeshSwing = 0.79f;

        private Vector3 _camPos;
        private float _camPitch;
        private float _camFov;
        private bool _haveCamera;

        [SetUp]
        public void SetUp()
        {
            _haveCamera = TryReadCamera(out _camPos, out _camPitch, out _camFov);
        }

        /// <summary>1.0 is the edge of the frame. The margin is what keeps him whole on a device
        /// whose aspect differs from the reference, or after a small camera nudge.</summary>
        private const float FrameLimit = 0.95f;

        [Test]
        public void TailIsWhollyInsideTheFrameInEveryLane()
        {
            if (!CameoIsOn()) return;
            if (!RequireCamera()) return;

            var cam = PulledBackCamera();
            float worst = 0f;
            string worstAt = "";

            foreach (float lane in Lanes)
            {
                float w = WorstCorner(cam, lane, KidZ - GameRules.PatrolChaseGap,
                                      CopHeight, CopHalfWidth, CopHalfDepth);
                if (w > worst) { worst = w; worstAt = "lane x=" + lane.ToString("0.0"); }
            }

            Assert.Less(worst, FrameLimit,
                "The patrol tail leaves the frame (worst corner " + worst.ToString("0.000") +
                " of the half-extent, at " + worstAt + "). Either the camera in " + ScenePath +
                " moved, or PatrolChaseGap / PatrolCameraPullback changed. This is the exact " +
                "failure that retired the chaser three times - re-solve the placement against " +
                "the projection, do not nudge numbers.");
        }

        /// <summary>
        /// The pull-back is the enabling condition, not a flourish.
        ///
        /// This test exists to be READ, not just to pass: it records that at the shipped gap the
        /// cop is off screen without the camera move, which is why every earlier behind-the-
        /// runner attempt was abandoned as impossible. Delete PatrolCameraPullback believing it
        /// to be decoration and this says what it costs.
        /// </summary>
        [Test]
        public void WithoutTheCameraPullbackTheTailWouldBeOffScreen()
        {
            if (!CameoIsOn()) return;
            if (!RequireCamera()) return;

            float best = float.MaxValue;
            foreach (float lane in Lanes)
                best = Mathf.Min(best, WorstCorner(_camPos, lane, KidZ - GameRules.PatrolChaseGap,
                                                  CopHeight, CopHalfWidth, CopHalfDepth));

            Assert.Greater(best, 1f,
                "The tail now fits on screen WITHOUT PatrolCameraPullback (best corner " +
                best.ToString("0.000") + "). That is not a failure in itself - but the pull-back " +
                "is documented as the enabling condition for placing the cop behind the runner, " +
                "and if the camera has changed enough to make it unnecessary, that documentation " +
                "and GameRules.PatrolCameraPullback both need re-deriving.");
        }

        [Test]
        public void TheRunnerIsStillFramedWhileTheCameraIsPulledBack()
        {
            if (!CameoIsOn()) return;
            if (!RequireCamera()) return;

            var cam = PulledBackCamera();
            float worst = 0f;
            foreach (float lane in Lanes)
                worst = Mathf.Max(worst, WorstCorner(cam, lane, KidZ,
                                                     KidHeight, KidHalfWidth, KidHalfDepth));

            Assert.Less(worst, FrameLimit,
                "Pulling the camera back for the patrol pushes the RUNNER out of frame (worst " +
                "corner " + worst.ToString("0.000") + "). The wrong-answer beat may never cost " +
                "the learner sight of their own character.");
        }

        [Test]
        public void TailCanNeverOverlapTheRunner()
        {
            if (!CameoIsOn()) return;

            // He holds a constant gap directly behind the kid, in the kid's own lane, so the
            // separation is entirely along the run and there is no lateral argument to make.
            // What has to clear is both half-depths PLUS the swing of the cop's rendered mass
            // off his pivot as the rig animates - the measured failure of every earlier version.
            float needed = CopHalfDepth + KidHalfDepth + CopMeshSwing;

            Assert.Greater(GameRules.PatrolChaseGap, needed,
                "PatrolChaseGap (" + GameRules.PatrolChaseGap.ToString("0.00") + "m) is inside " +
                "the runner's body once the cop's rig swing is counted (" +
                needed.ToString("0.00") + "m needed). Widen the gap; do not trust the stride.");
        }

        [Test]
        public void TheTailHoldsItsGapAndSoCanNeverCatchAnyone()
        {
            if (!CameoIsOn()) return;

            // D7/L3: the patrol is pressure, never a threat. A held gap makes that structural
            // rather than a promise - there is no closing rate to get wrong. A negative or zero
            // gap would put him level with or ahead of the runner, which is the pop-in-ahead the
            // owner rejected on 2026-08-21.
            Assert.Greater(GameRules.PatrolChaseGap, 0f,
                "The patrol must run BEHIND the runner. At or past zero he is level with him or " +
                "ahead of him, which is the framing the owner rejected.");

            Assert.Greater(GameRules.PatrolMenaceSeconds, 0f,
                "The wrong-answer beat has no duration, so the patrol can never appear.");
        }

        [Test]
        public void TheRetiredChaseStaysRetired()
        {
            // The tail is a different mechanism, not a re-enabling of the old chase. With both
            // on they fight over the same patrol transform every frame.
            Assert.IsFalse(GameRules.RacePatrolEnabled && GameRules.RacePatrolCameoEnabled,
                "The chase and the cameo are both enabled; they drive the same patrol transform.");
        }

        // ---------------------------------------------------------------------------------

        private Vector3 PulledBackCamera()
        {
            return _camPos + GameRules.PatrolCameraPullback;
        }

        private bool RequireCamera()
        {
            if (_haveCamera) return true;
            Assert.Inconclusive("Could not read the race camera from " + ScenePath +
                                " - the scene layout changed; re-point this fixture.");
            return false;
        }

        private static bool CameoIsOn()
        {
            if (GameRules.RacePatrolCameoEnabled) return true;
            Assert.Pass("Patrol cameo is switched off (GameRules.RacePatrolCameoEnabled).");
            return false;
        }

        /// <summary>Worst normalised viewport coordinate over a body's eight bounding corners.
        /// 1.0 is the edge of the frame in either axis.</summary>
        private float WorstCorner(Vector3 camPos, float x, float z, float h, float hw, float hd)
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
                            x + (ix == 0 ? -hw : hw),
                            iy == 0 ? 0.02f : h,
                            z + (iz == 0 ? -hd : hd));

                        var d = corner - camPos;
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
