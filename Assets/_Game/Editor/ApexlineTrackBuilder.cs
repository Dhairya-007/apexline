using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Apexline.AI;
using Apexline.Race;
using Apexline.Track;
using Apexline.Util;
using Apexline.Vehicle;

namespace Apexline.EditorTools
{
    /// <summary>
    /// Generates the circuit: road mesh with banking and elevation, barriers,
    /// the waypoint racing line, sector triggers, a starting grid, and a
    /// playable scene. Headless and idempotent:
    ///   Unity -batchmode -quit -projectPath . \
    ///         -executeMethod Apexline.EditorTools.ApexlineTrackBuilder.Build
    ///
    /// The circuit is authored as control points here rather than modelled in
    /// Blender, so the road, the racing line and the sector triggers can never
    /// disagree about where the track is - they are all derived from one spline.
    /// </summary>
    public static class ApexlineTrackBuilder
    {
        const string MeshDir   = "Assets/_Game/Meshes";
        const string MatDir    = "Assets/_Game/Materials";
        const string SceneDir  = "Assets/_Game/Scenes";
        const string ScenePath = SceneDir + "/Circuit_Apexline.unity";

        // Scale was chosen by measuring: the raw layout came out at 1250 m, and
        // 2.0 lands it just over the 2.5 km the roadmap calls for.
        const float Scale       = 2.0f;
        const float RoadHalf    = 6.0f;    // 12 m road
        const float Spacing     = 4.0f;    // mesh sample spacing, metres
        const float WaypointGap = 18.0f;
        const float BarrierH    = 1.45f;
        const float BarrierOut  = 1.1f;    // beyond the road edge
        const int   SectorCount = 4;
        const int   GridSlots   = 6;

        /// <summary>Measured peak lateral grip, m/s^2. See docs/TUNING.md.</summary>
        const float LateralGrip      = 13.9f;      // 1.42 g
        /// <summary>AI should not drive at the absolute limit of adhesion.</summary>
        const float CornerSafety     = 0.85f;
        /// <summary>Must match CarController.maxSpeedKph on the car prefab.</summary>
        const float CarTopSpeedKph   = 260f;

        /// <summary>
        /// Circuit layout: a long main straight, a fast right, a long left
        /// sweeper, a tight hairpin, an esse section and a climb back onto the
        /// straight. Y is elevation.
        /// </summary>
        /// <summary>
        /// Exposed so the environment builder derives its terrain, kerbs and
        /// trees from the SAME control points. Two copies of the layout would
        /// drift apart and put trees on the racing line.
        /// </summary>
        public static Vector3[] LayoutPoints => Layout;

        static readonly Vector3[] Layout =
        {
            new Vector3(   0f, 0f,    0f),   // start/finish
            new Vector3(   0f, 0f,  120f),
            new Vector3(   0f, 2f,  240f),   // main straight, climbing
            new Vector3(  25f, 4f,  300f),   // T1 fast right
            new Vector3(  85f, 6f,  330f),
            new Vector3( 150f, 6f,  318f),   // T2 long left sweeper
            new Vector3( 200f, 4f,  270f),
            new Vector3( 215f, 2f,  200f),   // T3 right, downhill
            new Vector3( 195f, 0f,  140f),
            new Vector3( 150f, 0f,  118f),   // T4 hairpin
            new Vector3( 115f, 1f,  140f),
            new Vector3( 108f, 3f,  190f),   // T5 left, climbing
            new Vector3(  85f, 5f,  225f),
            new Vector3(  40f, 6f,  232f),   // T6 esse
            new Vector3(  10f, 5f,  205f),
            new Vector3( -25f, 3f,  170f),   // T7 left
            new Vector3( -45f, 1f,  120f),
            new Vector3( -40f, 0f,   60f),   // T8 onto the straight
            new Vector3( -20f, 0f,   20f),
        };

        [MenuItem("Apexline/Build Circuit (track + scene)", false, 20)]
        public static void Build()
        {
            int failed = 0;
            foreach (string d in new[] { MeshDir, MatDir, SceneDir }) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();

            List<TrackSpline.Sample> samples = null;
            failed += Step("spline", () =>
            {
                var pts = new List<Vector3>();
                foreach (var p in Layout) pts.Add(p * Scale);
                samples = TrackSpline.Resample(pts, Spacing);

                float len = samples[samples.Count - 1].distance +
                            Vector3.Distance(samples[samples.Count - 1].position, samples[0].position);
                Debug.Log($"[Track] lap length {len:F0} m, {samples.Count} samples, " +
                          $"elevation range {MinY(samples):F1} to {MaxY(samples):F1} m");
            });

            if (failed > 0 || samples == null) { EditorApplication.Exit(1); return; }
            failed += Step("scene", () => BuildScene(samples));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Track] done, {failed} step(s) failed");
            if (failed > 0) EditorApplication.Exit(1);
        }

        static float MinY(List<TrackSpline.Sample> s)
        { float m = float.MaxValue; foreach (var x in s) m = Mathf.Min(m, x.position.y); return m; }
        static float MaxY(List<TrackSpline.Sample> s)
        { float m = float.MinValue; foreach (var x in s) m = Mathf.Max(m, x.position.y); return m; }

        static int Step(string name, System.Action a)
        {
            try { a(); Debug.Log($"[Track] OK   {name}"); return 0; }
            catch (System.Exception e)
            {
                Debug.LogError($"[Track] FAIL {name}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                return 1;
            }
        }

        // -------------------------------------------------------------- meshes
        /// <summary>Road surface: a closed quad strip between the two road edges.</summary>
        static Mesh BuildRoadMesh(List<TrackSpline.Sample> s)
        {
            int n = s.Count;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];

            for (int i = 0; i < n; i++)
            {
                verts[i * 2 + 0] = s[i].position - s[i].right * RoadHalf;
                verts[i * 2 + 1] = s[i].position + s[i].right * RoadHalf;
                float v = s[i].distance / 8f;      // asphalt tiles every 8 m
                uvs[i * 2 + 0] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
            }

            var tris = new List<int>(n * 6);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;              // wraps, closing the lap
                int a = i * 2, b = i * 2 + 1, c = j * 2, d = j * 2 + 1;
                tris.Add(a); tris.Add(b); tris.Add(d);
                tris.Add(a); tris.Add(d); tris.Add(c);
            }

            FixWinding(verts, tris);
            return Finish("Track_Road", verts, uvs, tris);
        }

        /// <summary>A vertical wall along one edge. side is -1 for left, +1 for right.</summary>
        static Mesh BuildBarrierMesh(List<TrackSpline.Sample> s, int side, string name)
        {
            int n = s.Count;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];

            for (int i = 0; i < n; i++)
            {
                Vector3 baseP = s[i].position + s[i].right * (side * (RoadHalf + BarrierOut));
                verts[i * 2 + 0] = baseP;
                verts[i * 2 + 1] = baseP + Vector3.up * BarrierH;
                float u = s[i].distance / 4f;
                uvs[i * 2 + 0] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
            }

            var tris = new List<int>(n * 6);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int a = i * 2, b = i * 2 + 1, c = j * 2, d = j * 2 + 1;
                // Faces inward, toward the road, which is the side cars hit.
                if (side > 0) { tris.Add(a); tris.Add(b); tris.Add(d); tris.Add(a); tris.Add(d); tris.Add(c); }
                else          { tris.Add(a); tris.Add(d); tris.Add(b); tris.Add(a); tris.Add(c); tris.Add(d); }
            }

            return Finish(name, verts, uvs, tris);
        }

        /// <summary>
        /// Flips the whole index buffer if the surface ended up facing down.
        /// Winding depends on the handedness of the spline's frames, which is
        /// easier to correct here than to reason about at authoring time.
        /// </summary>
        static void FixWinding(Vector3[] verts, List<int> tris)
        {
            if (tris.Count < 3) return;
            Vector3 normal = Vector3.Cross(verts[tris[1]] - verts[tris[0]],
                                           verts[tris[2]] - verts[tris[0]]);
            if (normal.y >= 0f) return;
            for (int i = 0; i < tris.Count; i += 3)
                (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
        }

        static Mesh Finish(string name, Vector3[] verts, Vector2[] uvs, List<int> tris)
        {
            var m = new Mesh { name = name };
            m.indexFormat = verts.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris.ToArray();
            m.RecalculateNormals();
            m.RecalculateBounds();

            string path = $"{MeshDir}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        static Material Mat(string name, Color c, float metallic, float smooth)
        {
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) throw new System.Exception("URP Lit shader not found");
            var m = new Material(sh);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            AssetDatabase.CreateAsset(m, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            go.isStatic = true;
            return go;
        }

        // --------------------------------------------------------------- scene
        static void BuildScene(List<TrackSpline.Sample> s)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Material asphalt = Mat("Asphalt", new Color(0.22f, 0.22f, 0.24f), 0f, 0.35f);
            Material barrier = Mat("Barrier", new Color(0.85f, 0.85f, 0.88f), 0f, 0.30f);
            Material grass   = Mat("Grass",   new Color(0.16f, 0.30f, 0.14f), 0f, 0.20f);

            var root = new GameObject("Circuit").transform;

            // --- surroundings -------------------------------------------
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Grass";
            ground.transform.SetParent(root, false);
            ground.transform.localScale = new Vector3(200f, 1f, 200f);
            ground.transform.position = new Vector3(150f, -0.5f, 300f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = grass;
            ground.isStatic = true;

            // --- road and barriers --------------------------------------
            MeshObject("Road", root, BuildRoadMesh(s), asphalt);
            MeshObject("Barrier_Left",  root, BuildBarrierMesh(s, -1, "Track_Barrier_L"), barrier);
            MeshObject("Barrier_Right", root, BuildBarrierMesh(s, +1, "Track_Barrier_R"), barrier);

            // --- racing line --------------------------------------------
            var pathGo = new GameObject("RacingLine");
            pathGo.transform.SetParent(root, false);
            var path = pathGo.AddComponent<WaypointPath>();
            path.closedLoop = true;

            int stride = Mathf.Max(1, Mathf.RoundToInt(WaypointGap / Spacing));
            for (int i = 0; i < s.Count; i += stride)
            {
                var node = new GameObject($"WP_{path.nodes.Count:000}").transform;
                node.SetParent(pathGo.transform, false);
                node.position = s[i].position + Vector3.up * 0.5f;

                // Corner speed from physics, not a linear fit. A corner of
                // curvature k can be taken at v = sqrt(a_lat / k), where a_lat
                // is the grip the tyres actually have - 1.42 g, measured by the
                // Day 2 benchmarks.
                //
                // The previous formula, 1 - k*14, asked for roughly DOUBLE the
                // possible speed at every radius on this circuit: 169 km/h
                // through a 40 m corner the car can only take at 85. The AI
                // charged every turn, ran wide into the barriers and recovered,
                // travelling 2721 m while netting 319 m of lap progress.
                float k = s[i].curvature;
                float vMaxKph = k > 1e-4f ? Mathf.Sqrt(LateralGrip / k) * 3.6f : 9999f;
                float limit = Mathf.Clamp(vMaxKph * CornerSafety / CarTopSpeedKph, 0.12f, 1f);

                path.nodes.Add(new WaypointPath.Node
                {
                    point = node,
                    halfWidth = RoadHalf - 1.2f,
                    speedLimit = limit,
                });
            }

            // --- sector triggers ----------------------------------------
            var sectorRoot = new GameObject("Sectors");
            sectorRoot.transform.SetParent(root, false);
            var sectors = new List<RaceSector>();
            for (int i = 0; i < SectorCount; i++)
            {
                int idx = Mathf.RoundToInt(s.Count * i / (float)SectorCount) % s.Count;
                var go = new GameObject(i == 0 ? "StartFinish" : $"Sector_{i}");
                go.transform.SetParent(sectorRoot.transform, false);
                go.transform.position = s[idx].position + Vector3.up * 2f;
                go.transform.rotation = Quaternion.LookRotation(s[idx].forward, s[idx].up);

                var bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(RoadHalf * 2.4f, 5f, 1.5f);

                var rs = go.AddComponent<RaceSector>();
                rs.sectorIndex = i;
                sectors.Add(rs);
            }

            // --- starting grid ------------------------------------------
            var gridRoot = new GameObject("Grid");
            gridRoot.transform.SetParent(root, false);
            var slots = new List<Transform>();
            for (int i = 0; i < GridSlots; i++)
            {
                // Staggered pairs, walking backwards from the line.
                float back = 10f + (i / 2) * 9f;
                float lateral = (i % 2 == 0) ? -2.6f : 2.6f;

                float d = Mathf.Repeat(-back, s[s.Count - 1].distance);
                int idx = NearestSampleToDistance(s, d);

                var slot = new GameObject($"Grid_{i + 1}").transform;
                slot.SetParent(gridRoot.transform, false);
                slot.position = s[idx].position + s[idx].right * lateral + Vector3.up * 0.6f;
                slot.rotation = Quaternion.LookRotation(s[idx].forward, Vector3.up);
                slots.Add(slot);
            }

            // --- player car ---------------------------------------------
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Car_Prototype.prefab");
            if (prefab == null) throw new System.Exception("Car_Prototype.prefab missing - run ApexlineScaffold.Build first");

            GameObject car = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            car.name = "PlayerCar";
            car.transform.SetPositionAndRotation(slots[0].position, slots[0].rotation);

            // --- race manager -------------------------------------------
            var raceGo = new GameObject("RaceManager");
            var race = raceGo.AddComponent<RaceManager>();
            race.path = path;
            race.totalLaps = 3;
            race.gridSlots = slots.ToArray();
            race.sectors = sectors.ToArray();
            foreach (var rs in sectors) rs.race = race;

            // --- camera + telemetry -------------------------------------
            Camera main = Object.FindFirstObjectByType<Camera>();
            GameObject camGo = main != null ? main.gameObject : new GameObject("Main Camera");
            if (main == null) camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            var chase = camGo.GetComponent<ChaseCamera>() ?? camGo.AddComponent<ChaseCamera>();
            chase.target = car.GetComponent<CarController>();
            var telem = camGo.GetComponent<VehicleTelemetry>() ?? camGo.AddComponent<VehicleTelemetry>();
            telem.car = car.GetComponent<CarController>();

            // --- sun ----------------------------------------------------
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
            }

            EditorSceneManager.SaveScene(scene, ScenePath);

            // Circuit first, flat testbed kept second for handling work.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(SceneDir + "/TestTrack_Flat.unity", true),
            };

            float minLim = 1f, maxLim = 0f, sumLim = 0f;
            foreach (var n in path.nodes)
            {
                minLim = Mathf.Min(minLim, n.speedLimit);
                maxLim = Mathf.Max(maxLim, n.speedLimit);
                sumLim += n.speedLimit;
            }
            Debug.Log($"[Track] scene built: {path.nodes.Count} waypoints, {sectors.Count} sectors, {slots.Count} grid slots");
            Debug.Log($"[Track] corner speed limits: tightest {minLim * CarTopSpeedKph:F0} kph, " +
                      $"fastest {maxLim * CarTopSpeedKph:F0} kph, mean {sumLim / path.nodes.Count * CarTopSpeedKph:F0} kph");
        }

        static int NearestSampleToDistance(List<TrackSpline.Sample> s, float d)
        {
            int best = 0;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < s.Count; i++)
            {
                float diff = Mathf.Abs(s[i].distance - d);
                if (diff < bestDiff) { bestDiff = diff; best = i; }
            }
            return best;
        }
    }
}
