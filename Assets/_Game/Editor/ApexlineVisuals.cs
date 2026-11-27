using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Apexline.EditorTools
{
    /// <summary>
    /// The visual pass: procedural textures, a post-processing volume and
    /// lighting. Everything is generated, so it is CC0 by construction and
    /// needs no third-party assets.
    ///   -executeMethod Apexline.EditorTools.ApexlineVisuals.Apply
    /// </summary>
    public static class ApexlineVisuals
    {
        const string TexDir  = "Assets/_Game/Textures";
        const string MatDir  = "Assets/_Game/Materials";
        const string PPDir   = "Assets/_Game/Settings";

        [MenuItem("Apexline/Apply Visual Pass", false, 23)]
        public static void Apply()
        {
            int failed = 0;
            foreach (string d in new[] { TexDir, MatDir, PPDir }) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();

            failed += Step("textures", BuildTextures);
            failed += Step("materials", BuildMaterials);
            failed += Step("post-processing", BuildPostProcessing);
            failed += Step("scene lighting", ApplyToScenes);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Visuals] done, {failed} step(s) failed");
            if (failed > 0) EditorApplication.Exit(1);
        }

        static int Step(string n, System.Action a)
        {
            try { a(); Debug.Log($"[Visuals] OK   {n}"); return 0; }
            catch (System.Exception e)
            {
                Debug.LogError($"[Visuals] FAIL {n}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                return 1;
            }
        }

        // ------------------------------------------------------------ textures
        static float Noise(float x, float y, float scale, int octaves)
        {
            float v = 0f, amp = 1f, tot = 0f;
            for (int o = 0; o < octaves; o++)
            {
                v += Mathf.PerlinNoise(x * scale, y * scale) * amp;
                tot += amp;
                amp *= 0.5f;
                scale *= 2.1f;
            }
            return v / tot;
        }

        static Texture2D Save(Texture2D t, string name)
        {
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = 8;
            t.Apply(true);

            string path = $"{TexDir}/{name}.png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.anisoLevel = 8;
            imp.maxTextureSize = 1024;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void BuildTextures()
        {
            const int S = 512;

            // --- asphalt: u runs ACROSS the road, so edge lines are columns ---
            var road = new Texture2D(S, S, TextureFormat.RGB24, true);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S;
                float g = 0.17f + Noise(x, y, 0.08f, 4) * 0.10f;
                // coarse aggregate speckle
                if (Noise(x + 500f, y + 500f, 0.45f, 2) > 0.74f) g += 0.06f;

                Color c = new Color(g, g, g * 1.02f);

                // white edge lines
                if ((u > 0.020f && u < 0.048f) || (u > 0.952f && u < 0.980f))
                    c = Color.Lerp(c, new Color(0.88f, 0.88f, 0.86f), 0.92f);

                // a darker, polished racing line down the middle
                float mid = Mathf.Abs(u - 0.5f);
                if (mid < 0.20f) c *= Mathf.Lerp(0.86f, 1f, mid / 0.20f);

                road.SetPixel(x, y, c);
            }
            Save(road, "Asphalt_Albedo");

            // --- grass -----------------------------------------------------
            var grass = new Texture2D(S, S, TextureFormat.RGB24, true);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = Noise(x, y, 0.10f, 4);
                float n2 = Noise(x + 900f, y + 900f, 0.55f, 2);
                // Desaturated and darker: bright saturated green reads as a
                // golf course, not trackside grass.
                grass.SetPixel(x, y, new Color(
                    0.115f + n * 0.075f + n2 * 0.030f,
                    0.165f + n * 0.095f + n2 * 0.035f,
                    0.085f + n * 0.050f));
            }
            Save(grass, "Grass_Albedo");

            // --- barrier: red and white panels, like trackside advertising ---
            var bar = new Texture2D(S, 64, TextureFormat.RGB24, true);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < S; x++)
            {
                int panel = (x / 64) % 2;
                Color c = panel == 0
                    ? new Color(0.80f, 0.11f, 0.12f)
                    : new Color(0.92f, 0.92f, 0.92f);
                if (y < 6 || y > 57) c *= 0.55f;             // grimy top and bottom rails
                c *= 0.92f + Noise(x, y, 0.3f, 2) * 0.16f;
                bar.SetPixel(x, y, c);
            }
            Save(bar, "Barrier_Albedo");

            // --- kerb: classic red/white diagonal stripes --------------------
            var kerb = new Texture2D(128, 128, TextureFormat.RGB24, true);
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                bool red = ((x + y) / 24) % 2 == 0;
                Color c = red ? new Color(0.78f, 0.10f, 0.11f) : new Color(0.90f, 0.90f, 0.88f);
                kerb.SetPixel(x, y, c * (0.9f + Noise(x, y, 0.4f, 2) * 0.2f));
            }
            Save(kerb, "Kerb_Albedo");
        }

        // ----------------------------------------------------------- materials
        static Material Mat(string name, Color tint, float metallic, float smooth,
                            Texture2D albedo = null, Vector2? tiling = null)
        {
            string path = $"{MatDir}/{name}.mat";
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) throw new System.Exception("URP Lit shader missing");

            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = sh;
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            if (albedo != null)
            {
                m.SetTexture("_BaseMap", albedo);
                if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void BuildMaterials()
        {
            Texture2D asphalt = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/Asphalt_Albedo.png");
            Texture2D grass   = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/Grass_Albedo.png");
            Texture2D barrier = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/Barrier_Albedo.png");
            Texture2D kerb    = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/Kerb_Albedo.png");

            // Road UVs are u across, v = metres/8, so the texture must not tile
            // across the road or the edge lines would repeat mid-track.
            Mat("Asphalt", Color.white, 0f, 0.30f, asphalt, new Vector2(1f, 1f));
            Mat("Grass",   Color.white, 0f, 0.12f, grass,   new Vector2(8f, 8f));
            Mat("Barrier", Color.white, 0f, 0.35f, barrier, new Vector2(1f, 1f));
            Mat("Kerb",    Color.white, 0f, 0.30f, kerb,    new Vector2(1f, 1f));

            // Car paint: metallic with a high gloss, which is what makes a car
            // read as a car under a bright sky.
            Mat("Car_Body",  new Color(0.62f, 0.05f, 0.08f), 0.85f, 0.88f);
            Mat("Car_Body2", new Color(0.05f, 0.18f, 0.55f), 0.85f, 0.88f);
            Mat("Car_Body3", new Color(0.92f, 0.78f, 0.08f), 0.85f, 0.88f);
            Mat("Car_Body4", new Color(0.06f, 0.07f, 0.09f), 0.85f, 0.90f);
            Mat("Car_Body5", new Color(0.85f, 0.85f, 0.88f), 0.80f, 0.86f);
            Mat("Car_Body6", new Color(0.08f, 0.45f, 0.22f), 0.85f, 0.88f);
            Mat("Wheel",     new Color(0.045f, 0.045f, 0.05f), 0.0f, 0.22f);
            Mat("Glass",     new Color(0.05f, 0.08f, 0.11f), 0.30f, 0.95f);
            Mat("Cone",      new Color(0.95f, 0.35f, 0.05f), 0f, 0.4f);

            // Environment
            Mat("Bark",      new Color(0.21f, 0.15f, 0.11f), 0f, 0.15f);
            Mat("Foliage",   new Color(0.13f, 0.27f, 0.12f), 0f, 0.10f);
            Mat("Concrete",  new Color(0.56f, 0.56f, 0.58f), 0f, 0.18f);
        }

        // ----------------------------------------------------- post-processing
        static void BuildPostProcessing()
        {
            string path = $"{PPDir}/Apexline_PostFX.asset";
            AssetDatabase.DeleteAsset(path);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            // ACES: the single biggest contributor to a scene looking "graded"
            // rather than flat. Raw linear output always looks washed out.
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.ACES;

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.overrideState = true; bloom.threshold.value = 1.05f;
            bloom.intensity.overrideState = true; bloom.intensity.value = 0.55f;
            bloom.scatter.overrideState = true;   bloom.scatter.value = 0.62f;

            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.overrideState = true; ca.postExposure.value = 0.35f;
            ca.contrast.overrideState = true;     ca.contrast.value = 18f;
            ca.saturation.overrideState = true;   ca.saturation.value = 12f;

            // Cool shadows, warm highlights: the standard "sunny day" grade.
            var sms = profile.Add<ShadowsMidtonesHighlights>(true);
            sms.shadows.overrideState = true;
            sms.shadows.value = new Vector4(0.92f, 0.97f, 1.10f, 0f);
            sms.highlights.overrideState = true;
            sms.highlights.value = new Vector4(1.06f, 1.02f, 0.94f, 0f);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.overrideState = true; vig.intensity.value = 0.26f;
            vig.smoothness.overrideState = true; vig.smoothness.value = 0.45f;

            var mb = profile.Add<MotionBlur>(true);
            mb.intensity.overrideState = true; mb.intensity.value = 0.22f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------- scenes
        static void ApplyToScenes()
        {
            foreach (string scene in new[]
            {
                "Assets/_Game/Scenes/Circuit_Apexline.unity",
                "Assets/_Game/Scenes/TestTrack_Flat.unity",
            })
            {
                if (!File.Exists(scene)) continue;
                EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);

                // --- post-processing volume ------------------------------
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{PPDir}/Apexline_PostFX.asset");
                var existing = Object.FindFirstObjectByType<Volume>();
                GameObject volGo = existing != null ? existing.gameObject : new GameObject("PostProcessing");
                var vol = volGo.GetComponent<Volume>() ?? volGo.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.priority = 1f;
                vol.profile = profile;

                // A volume does nothing unless the camera opts in.
                foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                {
                    var data = cam.GetUniversalAdditionalCameraData();
                    if (data != null)
                    {
                        data.renderPostProcessing = true;
                        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                    }
                }

                // --- sun --------------------------------------------------
                Light sun = null;
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }

                if (sun != null)
                {
                    // Lower sun angle: long shadows and raking light read as
                    // depth. The previous 48 degrees was close to flat noon.
                    sun.transform.rotation = Quaternion.Euler(34f, -42f, 0f);
                    sun.intensity = 1.5f;
                    sun.color = new Color(1f, 0.96f, 0.89f);
                    sun.shadows = LightShadows.Soft;
                    sun.shadowStrength = 0.82f;
                }

                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor    = new Color(0.42f, 0.52f, 0.68f);
                RenderSettings.ambientEquatorColor = new Color(0.33f, 0.36f, 0.40f);
                RenderSettings.ambientGroundColor  = new Color(0.14f, 0.16f, 0.13f);

                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(0.68f, 0.76f, 0.86f);
                RenderSettings.fogStartDistance = 220f;
                RenderSettings.fogEndDistance = 900f;

                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Debug.Log($"[Visuals] applied to {Path.GetFileName(scene)}");
            }
        }
    }
}
