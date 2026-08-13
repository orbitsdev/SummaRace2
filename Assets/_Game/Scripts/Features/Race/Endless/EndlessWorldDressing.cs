using System.Collections.Generic;
using UnityEngine;
using SummaRace.Constants;

namespace SummaRace.Features.Race.Endless
{
    /// <summary>
    /// Makes a <see cref="RaceWorlds"/> row mean a PLACE and not just a light setting (F48).
    ///
    /// THE BUG THIS EXISTS TO FIX. P4's ten worlds only ever touched RenderSettings, the sun and
    /// a colour grade. Geometry came from wherever Trash Dash's track happened to be: their
    /// <c>Begin()</c> hard-starts at zone 0 and rotates families every 500m, and a five-gate race
    /// is ~840m, so every one of the thirty races opened on the same Industrial warehouses and
    /// ended in the same Suburbs. A learner sat through the identical corridor thirty times
    /// across ten sessions, and a complete second theme (NightTime — its own models, sky dome and
    /// clouds, ~5.6MB, already Addressable in the Night-Zones group) was never once loaded.
    ///
    /// FOUR LEVERS, ALL THROUGH THEIR OWN PUBLIC API — their scripts are not touched:
    ///   1. THEME     <c>PlayerData.usedTheme</c>, set before their <c>Begin()</c> reads it.
    ///   2. ZONE      <c>TrackManager.ChangeZone()</c>, stepped to the family the world wants and
    ///                then held there (see GameRules.RaceZoneHoldSeconds for the no-op trick).
    ///                F49: no longer ONE family for the whole run. A world lays alternating
    ///                BLOCKS of its primary family and one or two accents, because the three
    ///                families hold 3 / 4 / 14 segment prefabs and five of the ten worlds were
    ///                building all ~55 of a race's segments out of the same four Suburbs pieces.
    ///                See RaceWorlds.Mix for the recipe and AdvanceMix below for the sequencer.
    ///   3. SKY DOME  <c>TrackManager.skyMeshFilter</c> is a public field, so the dome can be
    ///                chosen independently of the theme — daytime houses under a dusk sky is what
    ///                blue hour actually looks like, and neither theme ships that combination.
    ///                Its material is then swapped for SummaRace/SkyTint, because two domes for
    ///                ten worlds means overcast, misty, golden and sunset all rendered as the
    ///                same clear noon blue — which is the half of the complaint that named skies.
    ///   4. GREENERY  the theme's own Tree01 / GrassClump, scattered on the verge.
    ///
    /// WHY [DefaultExecutionOrder(-2000)]. The zone must be right BEFORE their first Update, not
    /// after it: that Update starts ten SpawnNewSegment coroutines in one go, and each reads
    /// <c>m_CurrentZone</c> before its first yield, so all ten come from whatever family is
    /// current at that instant. Their TrackManager GameObject is inactive until Begin() activates
    /// it from a coroutine (i.e. after the Update phase), so its first Update is the following
    /// frame — and a negative order guarantees we get there first on that frame. A coroutine
    /// could not do this: coroutines resume after every Update, never before one.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public class EndlessWorldDressing : MonoBehaviour
    {
        /// <summary>
        /// The art this component needs, handed in from the director (which is in the scene and
        /// can therefore serialise it). Both themes share the same two greenery materials and
        /// differ only in the mesh, because the colour is baked into the vertices.
        /// </summary>
        public struct WorldArt
        {
            public Mesh treeDay;
            public Mesh treeNight;
            public Mesh grassDay;
            public Mesh grassNight;
            public Material leaf;    // Materials/VCOL      — submesh 0 of both meshes
            public Material branch;  // Materials/TreeBranch — submesh 1 of the tree
            // Referenced rather than Shader.Find'd: nothing in any scene uses this shader
            // through a material asset, so the build would strip it and every world would fall
            // back to the one blue dome — a bug that cannot happen in the editor and would only
            // appear on the tablet. A direct scene reference is what keeps it in the player.
            public Shader skyTint;   // _Game/Art/Shaders/SkyTint
        }

        private RaceWorlds.World _world;
        private WorldArt _art;
        private bool _configured;
        private bool _subscribed;
        private float _zoneHoldTimer;
        private System.Random _rng;
        private readonly List<Bounds> _blockers = new List<Bounds>();

        // ---- zone mix (F49). The family currently being laid, and how much of it is left.
        private int _blockZone = -1;
        private float _blockMetresLeft;
        private bool _inAccent;

        /// <summary>Fraction of a blocker's renderer bounds treated as solid — see Blocked().</summary>
        private const float BlockerSolidity = 0.62f;

        private Material _skyMaterial;   // per-race instance; freed in OnDestroy
        private static readonly int SkyColorId = Shader.PropertyToID("_SkyColor");
        private static readonly int SkyBlendId = Shader.PropertyToID("_SkyBlend");

        /// <summary>
        /// Picks the theme for this world. MUST run before their <c>Begin()</c>, which reads
        /// <c>PlayerData.themes[usedTheme]</c> once and never looks again.
        ///
        /// Fails safe rather than loudly: if the theme database has not finished loading, or the
        /// named theme is not in it, their existing selection is left exactly as it was. The
        /// alternative — writing an index whose theme resolves to null — makes their Begin()
        /// dereference a null ThemeData and take the whole race down, which is the one outcome a
        /// cosmetic feature must never cause two days before a study.
        /// </summary>
        public static void SelectTheme(RaceWorlds.World world)
        {
            var data = PlayerData.instance;
            if (data == null || data.themes == null) return;

            string want = world.theme;
            if (string.IsNullOrEmpty(want) || ThemeDatabase.GetThemeData(want) == null)
                want = RaceWorlds.ThemeDay;
            if (ThemeDatabase.GetThemeData(want) == null) return;   // database not up yet

            // Their save's "owned themes" list is also the index space for usedTheme, so a theme
            // has to be in it to be selectable. Adding it does eventually reach their save file
            // (TrackManager calls PlayerData.Save() on every 300m rank-up), which in their game
            // would mean the learner "owns" a shop item — harmless here: the shop is stripped and
            // every race sets usedTheme explicitly anyway.
            int index = data.themes.IndexOf(want);
            if (index < 0)
            {
                data.themes.Add(want);
                index = data.themes.Count - 1;
            }
            data.usedTheme = index;
        }

        public void Configure(RaceWorlds.World world, WorldArt art, int seed)
        {
            _world = world;
            _art = art;
            _rng = new System.Random(seed);

            // The run always OPENS on the world's own family — the first block is primary. That
            // is not only taste: their first Update starts all ten spawn coroutines in one go and
            // each reads m_CurrentZone before its first yield, so the opening ~150m is a single
            // family whatever we do. Making that family the primary one is what lets the opening
            // establish the place before anything else is cut into it.
            _blockZone = world.zone;
            _inAccent = false;
            _blockMetresLeft = JitterBlock(world.primaryRunMetres);

            _configured = true;
        }

        private void OnDestroy()
        {
            if (_subscribed && TrackManager.instance != null)
                TrackManager.instance.newSegmentCreated -= OnNewSegment;
            _subscribed = false;

            // Owned by no asset, so nothing else will ever reclaim it — three races a session
            // across ten sessions on a 2GB device is exactly the kind of drip the vignette
            // sprite had to be fixed for in F44.
            if (_skyMaterial != null) { Destroy(_skyMaterial); _skyMaterial = null; }
        }

        private void Update()
        {
            if (!_configured) return;

            var track = TrackManager.instance;
            if (track == null) return;
            var theme = track.currentTheme;
            if (theme == null || theme.zones == null || theme.zones.Length == 0) return;

            if (!_subscribed)
            {
                track.newSegmentCreated += OnNewSegment;
                _subscribed = true;
                // Anything already standing (their Begin() can complete a spawn before we first
                // see the instance) still gets dressed, so the run never starts on a bare verge.
                var live = track.segments;
                for (int i = 0; i < live.Count; i++) Dress(live[i]);
            }

            HoldZone(track, theme);
            HoldSky(track);
        }

        // ------------------------------------------------------------------ zone + sky

        private void HoldZone(TrackManager track, ThemeData theme)
        {
            int want = Mathf.Clamp(_blockZone < 0 ? _world.zone : _blockZone,
                                   0, theme.zones.Length - 1);

            if (track.currentZone != want)
            {
                // At most zones.Length - 1 steps; the loop bound is a guard, not the intent.
                for (int i = 0; i < theme.zones.Length && track.currentZone != want; i++)
                    track.ChangeZone();
                _zoneHoldTimer = GameRules.RaceZoneHoldSeconds;
                return;
            }

            _zoneHoldTimer -= Time.deltaTime;
            if (_zoneHoldTimer > 0f) return;
            _zoneHoldTimer = GameRules.RaceZoneHoldSeconds;

            // A full lap of ChangeZone() lands back on the same family and resets the distance
            // counter, which is the only way to stop their 500m rotation without touching their
            // code. See GameRules.RaceZoneHoldSeconds.
            for (int i = 0; i < theme.zones.Length; i++) track.ChangeZone();
        }

        private void HoldSky(TrackManager track)
        {
            if (track.skyMeshFilter == null) return;
            // --- the dome MESH. Needs the other theme's ThemeData, so it can legitimately be
            //     unavailable. Re-asserted rather than set once: their Begin() writes this from
            //     the theme, and on a slow device that can land after we have. Assigning an asset
            //     mesh to a MeshFilter mutates nothing on disk, and the equality test makes the
            //     steady state free.
            var domeTheme = ThemeDatabase.GetThemeData(_world.nightSky ? RaceWorlds.ThemeNight
                                                                       : RaceWorlds.ThemeDay);
            if (domeTheme != null && domeTheme.skyMesh != null
                && track.skyMeshFilter.sharedMesh != domeTheme.skyMesh)
                track.skyMeshFilter.sharedMesh = domeTheme.skyMesh;

            // --- the TINT. Deliberately NOT behind the check above: it needs nothing from
            //     domeTheme, only the sky filter, its Renderer and our own shader. They used to
            //     share one early return, so a ThemeDatabase that had not resolved NightTime cost
            //     blue_hour_suburbs (session 4), night_city (7) and starlit_finale (10) not just
            //     the night dome but the ENTIRE repaint — falling back to the one flat blue dome,
            //     which is the exact symptom the sky tint exists to remove.
            if (_skyMaterial != null) return;   // done once; the tint never changes mid-race

            var renderer = track.skyMeshFilter.GetComponent<Renderer>();
            if (renderer == null) return;
            var shader = _art.skyTint;
            if (shader == null) return;         // unwired => their flat dome, no harm

            // A fresh instance, never their shared asset: writing to renderer.sharedMaterial in
            // the editor edits Assets/Materials/Sky.mat on disk and the change survives play mode.
            _skyMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            _skyMaterial.SetColor(SkyColorId, _world.sky);
            _skyMaterial.SetFloat(SkyBlendId, _world.nightSky ? GameRules.RaceSkyTintNight
                                                              : GameRules.RaceSkyTintDay);
            renderer.material = _skyMaterial;
        }

        // ------------------------------------------------------------------ greenery

        private void OnNewSegment(TrackSegment segment)
        {
            AdvanceMix(segment);
            Dress(segment);
        }

        // ------------------------------------------------------------------ zone mix (F49)

        /// <summary>
        /// Bills a freshly laid segment against the current block and starts the next block when
        /// this one runs out. Called from their <c>newSegmentCreated</c>, which is the only event
        /// that reports what was ACTUALLY laid — the alternative, counting segments, would make a
        /// block of 9m brick walls a third the length of a block of 27m warehouses.
        ///
        /// WHY THIS IS SAFE AGAINST THEIR SPAWN ORDER. After the opening batch their Update lays
        /// at most ONE segment per frame (the while loop refills to k_DesiredSegmentCount and the
        /// count only drops when the runner passes a segment), and our Update runs first at
        /// execution order -2000. So the zone this decides is always the zone their next spawn
        /// reads. During the opening batch every callback lands after all ten have already read
        /// the zone; the block simply goes overdrawn and the next block starts immediately, which
        /// is the correct behaviour rather than a special case.
        ///
        /// A world with no accents never leaves its primary family — F48's behaviour, unchanged.
        /// </summary>
        private void AdvanceMix(TrackSegment segment)
        {
            if (!_world.HasAccents || segment == null) return;

            _blockMetresLeft -= Mathf.Max(1f, segment.worldLength);
            if (_blockMetresLeft > 0f) return;

            if (_inAccent)
            {
                _inAccent = false;
                _blockZone = _world.zone;
                _blockMetresLeft = JitterBlock(_world.primaryRunMetres);
                return;
            }

            int accent = DrawAccent();
            if (accent < 0 || accent == _world.zone)
            {
                // Nothing to cut in (or the recipe names the primary as its own accent) — stay
                // put rather than emitting a zero-length block that would spin this every frame.
                _blockMetresLeft = JitterBlock(_world.primaryRunMetres);
                return;
            }

            _inAccent = true;
            _blockZone = accent;
            _blockMetresLeft = JitterBlock(_world.accentRunMetres);
        }

        /// <summary>Weighted pick between the world's two accent families; -1 when it has none.</summary>
        private int DrawAccent()
        {
            int a = Mathf.Max(0, _world.accentAWeight);
            int b = Mathf.Max(0, _world.accentBWeight);
            int total = a + b;
            if (total <= 0) return -1;
            return _rng.Next(total) < a ? _world.accentA : _world.accentB;
        }

        /// <summary>
        /// A recipe's block length is a target, not a constant. Without the jitter the three
        /// stories of a session — which share a world and therefore share every block length —
        /// would lay their families on the same metre marks, and the second and third races of a
        /// session would feel like the first with different lighting. The draw comes from the
        /// story-seeded RNG, so one story still plays identically every time it is run.
        /// </summary>
        private float JitterBlock(float target)
        {
            if (target <= 0f) return GameRules.RaceZoneMixMinBlockMetres;
            float j = GameRules.RaceZoneMixJitter;
            return Mathf.Max(GameRules.RaceZoneMixMinBlockMetres,
                             target * (1f + Range(-j, j)));
        }

        /// <summary>
        /// Scatters the world's greenery on this segment's verges. Everything is parented under
        /// the segment's own objectRoot, so it recycles and dies with the segment exactly like
        /// their own props — no separate lifetime to leak or to strand on a floating-origin
        /// recenter.
        /// </summary>
        private void Dress(TrackSegment segment)
        {
            if (segment == null) return;
            int trees = _world.trees;
            int grass = _world.grass;
            if (trees <= 0 && grass <= 0) return;
            if (trees + grass > GameRules.RaceMaxSceneryPerSegment)
            {
                // Keep the recipe's ratio, just cap the total.
                float scale = GameRules.RaceMaxSceneryPerSegment / (float)(trees + grass);
                trees = Mathf.RoundToInt(trees * scale);
                grass = GameRules.RaceMaxSceneryPerSegment - trees;
            }

            // Ask the theme that ACTUALLY LOADED, not the one the recipe wanted. SelectTheme has
            // two silent bail-outs (no PlayerData, or ThemeDatabase not up yet) that leave
            // PlayerData.usedTheme at its previous value — and that value is PERSISTED, because
            // TrackManager saves it on every 300m rank-up. So after any night race the tablet's
            // save holds NightTime, and if the next race's SelectTheme bails (a cold first launch
            // on the 2GB floor device is exactly when the database might not be up inside the 2s
            // wait) the track loads NightTime art for a morning_suburbs race while the recipe
            // still says Day. Trusting the recipe here then picked daylit trees to stand in a
            // night street. Falls back to the recipe only when the track cannot be asked.
            var loadedTheme = TrackManager.instance != null ? TrackManager.instance.currentTheme : null;
            bool night = loadedTheme != null
                ? loadedTheme.themeName == RaceWorlds.ThemeNight
                : _world.theme == RaceWorlds.ThemeNight;
            Mesh treeMesh = night ? _art.treeNight : _art.treeDay;
            Mesh grassMesh = night ? _art.grassNight : _art.grassDay;

            CollectBlockers(segment);
            var root = segment.objectRoot != null ? segment.objectRoot : segment.transform;

            // The footprint that must stay clear is the TRUNK, not the canopy. Tree01 is 7.7m
            // across at scale 1, and reserving that much rejected 59 of 60 placements in the
            // Suburbs zone — measured, not guessed — because their houses' renderer bounds are
            // 22m wide (roof, garden, plot) and their own tree stands at x = 8.1, i.e. inside
            // that box. Canopies are meant to overlap; trunks are not.
            for (int i = 0; i < trees; i++)
                TryPlace(segment, root, treeMesh, true,
                         GameRules.RaceTreeSideMin, GameRules.RaceTreeSideMax,
                         GameRules.RaceTreeScaleMin, GameRules.RaceTreeScaleMax, 1.1f);

            for (int i = 0; i < grass; i++)
                TryPlace(segment, root, grassMesh, false,
                         GameRules.RaceGrassSideMin, GameRules.RaceGrassSideMax,
                         GameRules.RaceGrassScaleMin, GameRules.RaceGrassScaleMax, 0.4f);
        }

        /// <summary>
        /// The segment's own geometry, as the boxes a new prop must not land in. Two families are
        /// deliberately excluded or nothing could ever be placed: the road slabs (20.6m wide but
        /// only 0.1m tall — flat ground, which is exactly where greenery belongs beside) and the
        /// far-background building strip (76m wide, i.e. the whole world).
        /// </summary>
        private void CollectBlockers(TrackSegment segment)
        {
            _blockers.Clear();
            var renderers = segment.GetComponentsInChildren<MeshRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                var b = renderers[i].bounds;
                if (b.size.y < 0.5f) continue;    // road / manhole / anything flat
                if (b.size.x > 30f) continue;     // the painted-on backdrop
                _blockers.Add(b);
            }
        }

        private void TryPlace(TrackSegment segment, Transform root, Mesh mesh, bool isTree,
                              float sideMin, float sideMax, float scaleMin, float scaleMax,
                              float footprintAtScaleOne)
        {
            if (mesh == null || _art.leaf == null) return;

            for (int attempt = 0; attempt < GameRules.RaceSceneryPlacementTries; attempt++)
            {
                float along = Range(0.5f, Mathf.Max(1f, segment.worldLength - 0.5f));
                float side = _rng.Next(2) == 0 ? -1f : 1f;
                float offset = Range(sideMin, sideMax) * side;
                float scale = Range(scaleMin, scaleMax);

                Vector3 pos;
                Quaternion rot;
                segment.GetPointAtInWorldUnit(along, out pos, out rot);
                pos += rot * Vector3.right * offset;

                float radius = footprintAtScaleOne * scale;
                if (Blocked(pos, radius)) continue;

                Spawn(root, mesh, isTree, pos, scale);
                return;
            }
        }

        private bool Blocked(Vector3 pos, float radius)
        {
            for (int i = 0; i < _blockers.Count; i++)
            {
                var b = _blockers[i];
                // Flat test: height never matters here, everything stands on the same ground.
                // Extents are taken at BlockerSolidity because a renderer's world bounds is an
                // axis-aligned box round the whole mesh — for a house that includes the eaves,
                // the garden and the plot, so the literal box vetoes ground that is visibly
                // empty. Shrinking it to the solid core is what makes the verge usable.
                if (Mathf.Abs(pos.x - b.center.x) < b.extents.x * BlockerSolidity + radius &&
                    Mathf.Abs(pos.z - b.center.z) < b.extents.z * BlockerSolidity + radius)
                    return true;
            }
            return false;
        }

        private void Spawn(Transform root, Mesh mesh, bool isTree, Vector3 pos, float scale)
        {
            var go = new GameObject(isTree ? "WorldTree" : "WorldGrass");
            go.transform.SetParent(root, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * scale;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = isTree && _art.branch != null
                ? new[] { _art.leaf, _art.branch }
                : new[] { _art.leaf };
            // The whole track is unlit vertex colour, so a cast shadow lands on surfaces that
            // cannot show it — pure cost on a tiler. The runner is the only lit thing out there.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private float Range(float min, float max)
        {
            return min + (float)_rng.NextDouble() * (max - min);
        }
    }
}
