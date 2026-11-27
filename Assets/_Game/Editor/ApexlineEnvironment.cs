using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Apexline.Track;

namespace Apexline.EditorTools
{
    /// <summary>
    /// Trackside environment: kerbs, rolling terrain, trees, grandstands and a
    /// start/finish gantry. All generated, so it stays CC0 by construction.
    ///   -executeMethod Apexline.EditorTools.ApexlineEnvironment.Build
    ///
    /// A race track reads as a race track because of what is BESIDE it. A grey
    /// ribbon on a flat green plane reads as a prototype no matter how good the
    /// asphalt shader is.
    /// </summary>
    public static class ApexlineEnvironment
    {
        const string MeshDir  = "Assets/_Game/Meshes";
        const string MatDir   = "Assets/_Game/Materials";
        const string Scene    = "Assets/_Game/Scenes/Circuit_Apexline.unity";

        const float Scale     = 2.0f;
        const float RoadHalf  = 6.0f;
        const float Spacing   = 4.0f;

        [MenuItem("Apexline/Build Environment", false, 24)]
        public static void Build()
        {
            int failed = 0;
            Directory.CreateDirectory(MeshDir);
            AssetDatabase.Refresh();

            List<TrackSpline.Sample> s = null;
            failed += Step("spline", () =>
            {
                var pts = new List<Vector3>();
                foreach (var p in ApexlineTrackBuilder.LayoutPoints) pts.Add(p * Scale);
                s = TrackSpline.Resample(pts, Spacing);
            });
            if (failed > 0 || s == null) { EditorApplication.Exit(1); return; }

            failed += Step("open scene", () =>
                EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single));

            failed += Step("environment", () => Populate(s));

            failed += Step("save", () =>
            {
                var a = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(a);
                EditorSceneManager.SaveScene(a);
            });

            AssetDatabase.SaveAssets();
            Debug.Log($"[Env] done, {failed} step(s) failed");
            if (failed > 0) EditorApplication.Exit(1);
        }

        static int Step(string n, System.Action a)
        {
            try { a(); Debug.Log($"[Env] OK   {n}"); return 0; }
            catch (System.Exception e)
            {
                Debug.LogError($"[Env] FAIL {n}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                return 1;
            }
        }

        static Material Mat(string n) => AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{n}.mat");

        static Mesh SaveMesh(Mesh m, string name)
        {
            string path = $"{MeshDir}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        static GameObject Obj(string name, Transform parent, Mesh m, Material mat, bool collide)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (collide) go.AddComponent<MeshCollider>().sharedMesh = m;
            go.isStatic = true;
            return go;
        }

        // ---------------------------------------------------------- populate
        static void Populate(List<TrackSpline.Sample> s)
        {
            var old = GameObject.Find("Environment");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Environment").transform;

            BuildKerbs(s, root);
            BuildTerrain(s, root);
            BuildTrees(s, root);
            BuildGrandstands(s, root);
            BuildGantry(s, root);
        }

        // ------------------------------------------------------------- kerbs
        /// <summary>
        /// Kerbs only where the track actually turns. A kerb down a straight
        /// looks wrong and costs triangles for nothing.
        /// </summary>
        static void BuildKerbs(List<TrackSpline.Sample> s, Transform root)
        {
            const float kerbW = 1.25f, kerbH = 0.10f, minCurve = 0.0045f;

            foreach (int side in new[] { -1, 1 })
            {
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();

                int i = 0;
                while (i < s.Count)
                {
                    if (s[i].curvature < minCurve) { i++; continue; }

                    int start = Mathf.Max(0, i - 3);
                    int end = i;
                    while (end + 1 < s.Count && s[end + 1].curvature >= minCurve * 0.6f) end++;
                    end = Mathf.Min(s.Count - 1, end + 3);

                    for (int k = start; k <= end; k++)
                    {
                        Vector3 inner = s[k].position + s[k].right * (side * RoadHalf);
                        Vector3 outer = inner + s[k].right * (side * kerbW);
                        int b = verts.Count;

                        verts.Add(inner);
                        verts.Add(inner + Vector3.up * kerbH);
                        verts.Add(outer + Vector3.up * kerbH * 0.75f);

                        float u = s[k].distance / 3f;
                        uvs.Add(new Vector2(u, 0f));
                        uvs.Add(new Vector2(u, 0.5f));
                        uvs.Add(new Vector2(u, 1f));

                        if (k > start)
                        {
                            int a = b - 3;
                            for (int q = 0; q < 2; q++)
                            {
                                if (side > 0)
                                {
                                    tris.Add(a + q); tris.Add(a + q + 1); tris.Add(b + q + 1);
                                    tris.Add(a + q); tris.Add(b + q + 1); tris.Add(b + q);
                                }
                                else
                                {
                                    tris.Add(a + q); tris.Add(b + q + 1); tris.Add(a + q + 1);
                                    tris.Add(a + q); tris.Add(b + q); tris.Add(b + q + 1);
                                }
                            }
                        }
                    }
                    i = end + 1;
                }

                if (verts.Count == 0) continue;
                var m = new Mesh { name = $"Kerb_{(side < 0 ? "L" : "R")}" };
                m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
                m.RecalculateNormals(); m.RecalculateBounds();
                Obj($"Kerb_{(side < 0 ? "L" : "R")}", root, SaveMesh(m, m.name), Mat("Kerb"), true);
            }
            Debug.Log("[Env] kerbs built on corners only");
        }

        // ----------------------------------------------------------- terrain
        /// <summary>
        /// Rolling ground that flattens to the road height near the track.
        /// A dead-flat plane is the single clearest "this is a prototype" tell.
        /// </summary>
        static void BuildTerrain(List<TrackSpline.Sample> s, Transform root)
        {
            var grass = GameObject.Find("Grass");
            if (grass != null) Object.DestroyImmediate(grass);

            Bounds b = new Bounds(s[0].position, Vector3.zero);
            foreach (var x in s) b.Encapsulate(x.position);
            b.Expand(900f);

            const int N = 140;
            var verts = new Vector3[N * N];
            var uvs = new Vector2[N * N];

            for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                float fx = x / (float)(N - 1), fz = z / (float)(N - 1);
                float wx = b.min.x + fx * b.size.x;
                float wz = b.min.z + fz * b.size.z;

                // Nearest track point decides both height and how flat it is.
                float best = float.MaxValue, trackY = 0f;
                foreach (var t in s)
                {
                    float dx = t.position.x - wx, dz = t.position.z - wz;
                    float d = dx * dx + dz * dz;
                    if (d < best) { best = d; trackY = t.position.y; }
                }
                float dist = Mathf.Sqrt(best);

                // 0 at the verge, 1 once we are well away from the track.
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14f, 150f, dist));
                float hills =
                    (Mathf.PerlinNoise(wx * 0.0016f, wz * 0.0016f) - 0.5f) * 90f +
                    (Mathf.PerlinNoise(wx * 0.0065f, wz * 0.0065f) - 0.5f) * 14f;

                float y = Mathf.Lerp(trackY - 0.45f, trackY + hills, blend);

                verts[z * N + x] = new Vector3(wx, y, wz);
                uvs[z * N + x] = new Vector2(fx * 60f, fz * 60f);
            }

            var tris = new List<int>((N - 1) * (N - 1) * 6);
            for (int z = 0; z < N - 1; z++)
            for (int x = 0; x < N - 1; x++)
            {
                int i = z * N + x;
                tris.Add(i); tris.Add(i + N); tris.Add(i + N + 1);
                tris.Add(i); tris.Add(i + N + 1); tris.Add(i + 1);
            }

            var m = new Mesh { name = "Terrain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.vertices = verts; m.uv = uvs; m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            Obj("Terrain", root, SaveMesh(m, "Terrain"), Mat("Grass"), true);
            Debug.Log($"[Env] terrain {N}x{N} over {b.size.x:F0}x{b.size.z:F0} m");
        }

        // ------------------------------------------------------------- trees
        static void BuildTrees(List<TrackSpline.Sample> s, Transform root)
        {
            Mesh trunk = SaveMesh(Cylinder(0.45f, 5.5f, 6), "Tree_Trunk");
            Mesh canopy = SaveMesh(Cone(4.2f, 11.0f, 8), "Tree_Canopy");

            Material bark = Mat("Bark") ?? Mat("Wheel");
            Material leaf = Mat("Foliage") ?? Mat("Grass");

            var parent = new GameObject("Trees").transform;
            parent.SetParent(root, false);

            var rng = new System.Random(20261127);
            int placed = 0;

            for (int i = 0; i < s.Count; i += 1)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    if (rng.NextDouble() > 0.55) continue;

                    // Well clear of the barriers, with scatter so they do not
                    // form a corridor of identical trees.
                    float off = RoadHalf + 42f + (float)rng.NextDouble() * 110f;
                    Vector3 p = s[i].position + s[i].right * (side * off);
                    p += new Vector3((float)rng.NextDouble() * 14f - 7f, 0f,
                                     (float)rng.NextDouble() * 14f - 7f);

                    if (!Physics.Raycast(p + Vector3.up * 300f, Vector3.down,
                                         out RaycastHit hit, 600f,
                                         ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.gameObject.name != "Terrain") continue;

                    var t = new GameObject($"Tree_{placed:000}");
                    t.transform.SetParent(parent, false);
                    t.transform.position = hit.point;
                    t.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    float sc = 0.85f + (float)rng.NextDouble() * 1.05f;
                    t.transform.localScale = Vector3.one * sc;
                    t.isStatic = true;

                    Obj("Trunk", t.transform, trunk, bark, false);
                    var c = Obj("Canopy", t.transform, canopy, leaf, false);
                    c.transform.localPosition = new Vector3(0f, 4.4f, 0f);
                    placed++;
                }
            }
            Debug.Log($"[Env] {placed} trees");
        }

        // ------------------------------------------------------- grandstands
        static void BuildGrandstands(List<TrackSpline.Sample> s, Transform root)
        {
            Material concrete = Mat("Concrete") ?? Mat("Barrier");
            var parent = new GameObject("Grandstands").transform;
            parent.SetParent(root, false);

            // Along the main straight, which is where a crowd would be.
            // 38 m back, not 16: the first version put the lowest tier on the
            // racing line, because a grandstand is 9 m deep and the tiers step
            // AWAY from the origin.
            int[] at = { 8, 20, 32 };
            int n = 0;
            foreach (int idx in at)
            {
                if (idx >= s.Count) continue;
                Vector3 pos = s[idx].position + s[idx].right * -(RoadHalf + 38f);

                // Sit it on the ground rather than at road height; the terrain
                // has already started to rise this far out.
                if (Physics.Raycast(pos + Vector3.up * 300f, Vector3.down,
                                    out RaycastHit gh, 600f, ~0, QueryTriggerInteraction.Ignore))
                    pos.y = gh.point.y;

                var g = new GameObject($"Grandstand_{n}");
                g.transform.SetParent(parent, false);
                g.transform.position = pos;
                g.transform.rotation = Quaternion.LookRotation(s[idx].forward, Vector3.up);
                g.isStatic = true;

                // Stepped seating tiers.
                for (int tier = 0; tier < 7; tier++)
                {
                    var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(step.GetComponent<BoxCollider>());
                    step.name = $"Tier_{tier}";
                    step.transform.SetParent(g.transform, false);
                    step.transform.localPosition = new Vector3(-tier * 1.5f, 0.8f + tier * 1.1f, 0f);
                    step.transform.localScale = new Vector3(1.4f, 1.0f, 34f);
                    step.GetComponent<MeshRenderer>().sharedMaterial = concrete;
                    step.isStatic = true;
                }

                var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(roof.GetComponent<BoxCollider>());
                roof.name = "Roof";
                roof.transform.SetParent(g.transform, false);
                roof.transform.localPosition = new Vector3(-5.5f, 12.6f, 0f);
                roof.transform.localScale = new Vector3(13f, 0.4f, 36f);
                roof.GetComponent<MeshRenderer>().sharedMaterial = concrete;
                roof.isStatic = true;
                n++;
            }
            Debug.Log($"[Env] {n} grandstands");
        }

        // ------------------------------------------------------------ gantry
        static void BuildGantry(List<TrackSpline.Sample> s, Transform root)
        {
            Material concrete = Mat("Concrete") ?? Mat("Barrier");
            var g = new GameObject("StartGantry");
            g.transform.SetParent(root, false);
            g.transform.position = s[0].position;
            g.transform.rotation = Quaternion.LookRotation(s[0].forward, Vector3.up);
            g.isStatic = true;

            foreach (int side in new[] { -1, 1 })
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(post.GetComponent<BoxCollider>());
                post.transform.SetParent(g.transform, false);
                post.transform.localPosition = new Vector3(side * (RoadHalf + 1.6f), 4.0f, 0f);
                post.transform.localScale = new Vector3(0.6f, 8.0f, 0.6f);
                post.GetComponent<MeshRenderer>().sharedMaterial = concrete;
                post.isStatic = true;
            }

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(beam.GetComponent<BoxCollider>());
            beam.name = "Beam";
            beam.transform.SetParent(g.transform, false);
            beam.transform.localPosition = new Vector3(0f, 8.2f, 0f);
            beam.transform.localScale = new Vector3(RoadHalf * 2f + 4f, 1.4f, 0.8f);
            beam.GetComponent<MeshRenderer>().sharedMaterial = concrete;
            beam.isStatic = true;
            Debug.Log("[Env] start gantry");
        }

        // ------------------------------------------------------- primitives
        static Mesh Cylinder(float r, float h, int seg)
        {
            var v = new List<Vector3>(); var u = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                v.Add(new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r));
                u.Add(new Vector2(i / (float)seg, 0f)); u.Add(new Vector2(i / (float)seg, 1f));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2, b = ((i + 1) % seg) * 2;
                t.Add(a); t.Add(a + 1); t.Add(b + 1);
                t.Add(a); t.Add(b + 1); t.Add(b);
            }
            var m = new Mesh { name = "Cyl" };
            m.SetVertices(v); m.SetUVs(0, u); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        static Mesh Cone(float r, float h, int seg)
        {
            var v = new List<Vector3>(); var u = new List<Vector2>(); var t = new List<int>();
            int apex = 0;
            v.Add(new Vector3(0f, h, 0f)); u.Add(new Vector2(0.5f, 1f));
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                u.Add(new Vector2(i / (float)seg, 0f));
            }
            for (int i = 0; i < seg; i++)
            {
                t.Add(apex); t.Add(1 + i); t.Add(1 + (i + 1) % seg);
            }
            var m = new Mesh { name = "Cone" };
            m.SetVertices(v); m.SetUVs(0, u); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
    }
}
