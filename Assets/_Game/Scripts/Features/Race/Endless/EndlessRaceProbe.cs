// TEMPORARY MEASUREMENT PROBE — created for the F55 pass, deleted before hand-off.
// Reads the director's private state each frame so timings and bounds are measured in a
// running game rather than reasoned about from source.
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SummaRace.Features.Race.Endless
{
    public class EndlessRaceProbe : MonoBehaviour
    {
        public static EndlessRaceProbe Instance;
        public static bool ForceMenace;               // hold the surge on so it can be measured
        public static bool KeepAlive = true;           // beat their focus-loss pause while measuring
        public static bool AutoStart;                  // tap START as soon as it unlocks
        public static float MinIntersect = 999f;       // worst cop/kid overlap seen all run
        public static int IntersectFrames;
        public static string ShotPrefix;               // burst-capture frames when set
        private int _shot; private float _nextShot;
        public static readonly List<string> Events = new List<string>();
        public static readonly List<string> Samples = new List<string>();

        private EndlessRaceDirector _dir;
        private static readonly BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;

        private FieldInfo _fActiveGateDistance, _fActiveElement, _fPreviewRoot, _fMenace,
                          _fPatrol, _fRunReleased, _fPendingGateDistance, _fPendingElement,
                          _fPreviewArmed;

        private bool _prevPreview;
        private int _prevElement = -99;
        private float _previewOnTime = -1f, _previewOnDist = -1f;
        private float _gateStartTime = -1f;

        void Awake()
        {
            Instance = this;
            var t = typeof(EndlessRaceDirector);
            _fActiveGateDistance = t.GetField("_activeGateDistance", F);
            _fActiveElement = t.GetField("_activeElement", F);
            _fPreviewRoot = t.GetField("_previewRoot", F);
            _fMenace = t.GetField("_menaceTimer", F);
            _fPatrol = t.GetField("_patrol", F);
            _fRunReleased = t.GetField("_runReleased", F);
            _fPendingGateDistance = t.GetField("_pendingGateDistance", F);
            _fPendingElement = t.GetField("_pendingElement", F);
            _fPreviewArmed = t.GetField("_previewArmed", F); // may not exist yet (pre-fix)
        }

        public static string FieldReport()
        {
            var t = typeof(EndlessRaceDirector);
            var sb = new System.Text.StringBuilder();
            foreach (var n in new[] { "_activeGateDistance", "_activeElement", "_previewRoot",
                                      "_menaceTimer", "_patrol", "_runReleased", "_previewArmed" })
                sb.Append(n).Append('=').Append(t.GetField(n, F) != null).Append(' ');
            return sb.ToString();
        }

        void Update()
        {
            if (_dir == null)
            {
                _dir = FindObjectOfType<EndlessRaceDirector>();
                if (_dir == null) return;
            }
            if (KeepAlive)
            {
                // Their GameState.OnApplicationFocus freezes the game when the Editor loses
                // focus (timeScale 0 + StopMove). Measurement must not be at its mercy.
                if (Time.timeScale == 0f) Time.timeScale = 1f;
                AudioListener.pause = false;
                var tk = TrackManager.instance;
                var pf = typeof(EndlessRaceDirector).GetField("_paused", F);
                if (tk != null && _dir != null && !tk.isMoving &&
                    _fRunReleased != null && (bool)_fRunReleased.GetValue(_dir) &&
                    (pf == null || !(bool)pf.GetValue(_dir)))
                    tk.StartMove(false);
            }

            if (ShotPrefix != null && Time.unscaledTime >= _nextShot)
            {
                _nextShot = Time.unscaledTime + 0.25f;
                var dir2 = System.IO.Path.Combine(Application.dataPath, "../Captures");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(
                    dir2, ShotPrefix + "_" + _shot.ToString("00") + ".png"));
                if (++_shot >= 16) ShotPrefix = null;
            }

            var track = TrackManager.instance;
            if (track == null) return;

            if (ForceMenace && _fMenace != null) _fMenace.SetValue(_dir, 3f);

            bool released = _fRunReleased != null && (bool)_fRunReleased.GetValue(_dir);
            if (!released)
            {
                if (AutoStart)
                {
                    var f = typeof(EndlessRaceDirector).GetField("_bootReady", F);
                    if (f != null && (bool)f.GetValue(_dir))
                    {
                        AutoStart = false;
                        typeof(EndlessRaceDirector)
                            .GetMethod("DismissBriefing", F).Invoke(_dir, null);
                        Events.Add("t=" + Time.time.ToString("F2") + " START tapped");
                    }
                }
                return;
            }

            // Standing invariant, checked EVERY frame of the run rather than at sampled moments:
            // the chaser may never intersect the runner (GDD D7 — he never catches).
            var patrolT = _fPatrol.GetValue(_dir) as Transform;
            var rn = track.characterController;
            var bt = rn != null && rn.characterCollider != null ? rn.characterCollider.transform : null;
            if (patrolT != null && patrolT.gameObject.activeSelf && bt != null)
            {
                Bounds cb, kb;
                if (Body(patrolT, out cb) && Body(bt, out kb))
                {
                    if (cb.Intersects(kb)) IntersectFrames++;
                    float sepX = Mathf.Abs(cb.center.x - kb.center.x) - (cb.extents.x + kb.extents.x);
                    if (sepX < MinIntersect) MinIntersect = sepX;
                }
            }

            float dist = track.worldDistance;
            float speed = track.speed;
            int el = _fActiveElement != null ? (int)_fActiveElement.GetValue(_dir) : -1;
            float gateDist = _fActiveGateDistance != null ? (float)_fActiveGateDistance.GetValue(_dir) : -1f;
            var previewGo = _fPreviewRoot != null ? _fPreviewRoot.GetValue(_dir) as GameObject : null;
            bool preview = previewGo != null && previewGo.activeSelf;

            Samples.Add(string.Format("{0:F2},{1:F1},{2:F2},{3},{4:F1},{5}",
                Time.time, dist, speed, el, gateDist, preview ? 1 : 0));

            if (el != _prevElement)
            {
                _prevElement = el;
                _gateStartTime = Time.time;
                Events.Add(string.Format("t={0:F2} ELEMENT->{1} dist={2:F1} speed={3:F2} gateDist={4:F1} lead={5:F1}m",
                    Time.time, el, dist, speed, gateDist, gateDist - dist));
            }

            if (preview != _prevPreview)
            {
                _prevPreview = preview;
                if (preview)
                {
                    _previewOnTime = Time.time; _previewOnDist = dist;
                    Events.Add(string.Format("t={0:F2} PREVIEW ON  el={1} dist={2:F1} speed={3:F2} gateAhead={4:F1}m ({5:F2}s)",
                        Time.time, el, dist, speed, gateDist - dist, (gateDist - dist) / Mathf.Max(1f, speed)));
                }
                else
                {
                    Events.Add(string.Format("t={0:F2} PREVIEW OFF el={1} dist={2:F1} visibleFor={3:F2}s over {4:F1}m",
                        Time.time, el, dist, Time.time - _previewOnTime, dist - _previewOnDist));
                }
            }
        }

        // ---- Task 2: patrol geometry, measured ----
        public static string PatrolReport()
        {
            var probe = Instance;
            if (probe == null || probe._dir == null) return "no probe/director";
            var dir = probe._dir;
            var track = TrackManager.instance;
            if (track == null) return "no track";
            var patrol = probe._fPatrol.GetValue(dir) as Transform;
            if (patrol == null) return "no patrol";
            var cam = Camera.main;
            var sb = new System.Text.StringBuilder();

            Bounds cop;
            if (!Body(patrol, out cop)) return "cop has no visible renderers";
            sb.AppendLine(string.Format("menace={0:F2} copPivot={1} copBounds c={2} e={3}",
                (float)probe._fMenace.GetValue(dir), Fmt(patrol.position), Fmt(cop.center), Fmt(cop.extents)));

            var runner = track.characterController;
            Transform bodyT = runner != null && runner.characterCollider != null
                ? runner.characterCollider.transform : (runner != null ? runner.transform : null);
            Bounds kid;
            if (bodyT != null && Body(bodyT, out kid))
            {
                sb.AppendLine(string.Format("kidBounds c={0} e={1} Intersects(cop,kid)={2} dx={3:F2} dz={4:F2}",
                    Fmt(kid.center), Fmt(kid.extents), cop.Intersects(kid),
                    Mathf.Abs(cop.center.x - kid.center.x) - (cop.extents.x + kid.extents.x),
                    Mathf.Abs(cop.center.z - kid.center.z) - (cop.extents.z + kid.extents.z)));
            }

            // Everything solid near him: which renderers his body actually overlaps.
            var hits = new List<string>();
            float nearest = 999f; string nearestName = "-";
            var all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in all)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer) continue;
                if (r.transform.IsChildOf(patrol)) continue;
                if (bodyT != null && r.transform.IsChildOf(bodyT.root)) continue;
                var b = r.bounds;
                if (Mathf.Abs(b.center.z - cop.center.z) > 40f) continue;
                if (b.size.x > 300f || b.size.z > 300f) continue; // skip whole-track roots
                if (b.Intersects(cop))
                    hits.Add(Path(r.transform) + " c=" + Fmt(b.center) + " e=" + Fmt(b.extents));
                else
                {
                    float d = Vector3.Distance(b.ClosestPoint(cop.center), cop.center);
                    if (d < nearest) { nearest = d; nearestName = Path(r.transform); }
                }
            }
            sb.AppendLine("INTERSECTING RENDERERS: " + hits.Count);
            for (int i = 0; i < hits.Count && i < 25; i++) sb.AppendLine("   * " + hits[i]);
            sb.AppendLine(string.Format("nearest non-intersecting: {0} at {1:F2}m", nearestName, nearest));

            if (cam != null)
            {
                // screen rect of the 8 bound corners
                float xmin = 1e9f, xmax = -1e9f, ymin = 1e9f, ymax = -1e9f; bool behind = false;
                for (int i = 0; i < 8; i++)
                {
                    var c = cop.center + Vector3.Scale(cop.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var sp = cam.WorldToScreenPoint(c);
                    if (sp.z < 0f) behind = true;
                    xmin = Mathf.Min(xmin, sp.x); xmax = Mathf.Max(xmax, sp.x);
                    ymin = Mathf.Min(ymin, sp.y); ymax = Mathf.Max(ymax, sp.y);
                }
                sb.AppendLine(string.Format("screen {0}x{1} copRect x[{2:F0},{3:F0}] y[{4:F0},{5:F0}] normX[{6:F3},{7:F3}] normY[{8:F3},{9:F3}] behind={10}",
                    Screen.width, Screen.height, xmin, xmax, ymin, ymax,
                    xmin / Screen.width, xmax / Screen.width, ymin / Screen.height, ymax / Screen.height, behind));
                float camDist = Vector3.Dot(cop.ClosestPoint(cam.transform.position) - cam.transform.position,
                                            cam.transform.forward);
                sb.AppendLine(string.Format("cam pos={0} fov={1:F1} aspect={2:F3} near={3:F2} distToCopNearestPoint={4:F2}",
                    Fmt(cam.transform.position), cam.fieldOfView, cam.aspect, cam.nearClipPlane, camDist));

                // HUD rects in screen space
                foreach (var name in new[] { "OptionPreview", "SwbstTracker", "PauseChip" })
                {
                    var go = FindDeep(name);
                    if (go == null) { sb.AppendLine("HUD " + name + ": not found"); continue; }
                    var rt = go.transform as RectTransform;
                    var corners = new Vector3[4]; rt.GetWorldCorners(corners);
                    sb.AppendLine(string.Format("HUD {0} active={1} normX[{2:F3},{3:F3}] normY[{4:F3},{5:F3}]",
                        name, go.activeInHierarchy,
                        corners[0].x / Screen.width, corners[2].x / Screen.width,
                        corners[0].y / Screen.height, corners[2].y / Screen.height));
                }
            }
            return sb.ToString();
        }

        private static GameObject FindDeep(string name)
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t.gameObject;
            return null;
        }

        private static string Path(Transform t)
        {
            var s = t.name; int guard = 0;
            while (t.parent != null && guard++ < 4) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        private static string Fmt(Vector3 v) { return string.Format("({0:F2},{1:F2},{2:F2})", v.x, v.y, v.z); }

        private static bool Body(Transform root, out Bounds b)
        {
            b = default(Bounds); bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
