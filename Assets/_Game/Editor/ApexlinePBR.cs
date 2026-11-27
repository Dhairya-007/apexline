using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apexline.EditorTools
{
    /// <summary>
    /// Wires the downloaded CC0 PBR texture sets and HDRI into materials and
    /// lighting:
    ///   -executeMethod Apexline.EditorTools.ApexlinePBR.Apply
    ///
    /// This is the step that moves surfaces from "flat colour" to "material".
    /// Albedo alone cannot look real: a normal map gives surface relief, a
    /// roughness map varies reflectivity across the surface, and an HDRI sky
    /// gives everything something to reflect. Car paint in particular is just
    /// coloured plastic until it has an environment to mirror.
    /// </summary>
    public static class ApexlinePBR
    {
        const string PbrDir  = "Assets/_Game/Textures/PBR";
        const string HdriDir = "Assets/_Game/Textures/HDRI";
        const string MatDir  = "Assets/_Game/Materials";

        [MenuItem("Apexline/Apply PBR + HDRI", false, 25)]
        public static void Apply()
        {
            int failed = 0;
            failed += Step("import settings", ConfigureImporters);
            failed += Step("metallic/smoothness maps", BuildMetallicGloss);
            failed += Step("PBR materials", BuildMaterials);
            failed += Step("skybox + reflections", BuildSkyAndReflections);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PBR] done, {failed} step(s) failed");
            if (failed > 0) EditorApplication.Exit(1);
        }

        static int Step(string n, System.Action a)
        {
            try { a(); Debug.Log($"[PBR] OK   {n}"); return 0; }
            catch (System.Exception e)
            {
                Debug.LogError($"[PBR] FAIL {n}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                return 1;
            }
        }

        static readonly string[] Sets = { "Asphalt026A", "Grass004", "Concrete034" };

        // ------------------------------------------------------------ import
        static void ConfigureImporters()
        {
            foreach (string set in Sets)
            {
                // A normal map imported as a colour texture is the classic
                // "why does my surface look wrong" bug: Unity needs to know so
                // it can pack it correctly.
                SetImporter($"{PbrDir}/{set}_NormalGL.jpg", TextureImporterType.NormalMap, false);
                SetImporter($"{PbrDir}/{set}_Color.jpg", TextureImporterType.Default, true);
                // Roughness and AO are data, not colour: they must be linear.
                SetImporter($"{PbrDir}/{set}_Roughness.jpg", TextureImporterType.Default, false);
                SetImporter($"{PbrDir}/{set}_AmbientOcclusion.jpg", TextureImporterType.Default, false);
            }

            string hdri = Directory.Exists(HdriDir)
                ? Directory.GetFiles(HdriDir, "*.hdr").Length > 0
                    ? Directory.GetFiles(HdriDir, "*.hdr")[0] : null
                : null;
            if (hdri != null)
            {
                var ti = AssetImporter.GetAtPath(hdri) as TextureImporter;
                if (ti != null)
                {
                    ti.textureShape = TextureImporterShape.TextureCube;
                    ti.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
                    ti.maxTextureSize = 2048;
                    ti.SaveAndReimport();
                }
            }
        }

        static void SetImporter(string path, TextureImporterType type, bool srgb)
        {
            if (!File.Exists(path)) return;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = type;
            ti.sRGBTexture = srgb;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.anisoLevel = 8;
            ti.maxTextureSize = 1024;
            ti.isReadable = false;
            ti.SaveAndReimport();
        }

        // ------------------------------------------- metallic/smoothness map
        /// <summary>
        /// URP Lit reads smoothness from the ALPHA of the metallic map, but the
        /// texture sets ship roughness as a separate greyscale image. This
        /// combines them: R = metallic (0 for these surfaces), A = 1 - roughness.
        /// </summary>
        static void BuildMetallicGloss()
        {
            foreach (string set in Sets)
            {
                string rough = $"{PbrDir}/{set}_Roughness.jpg";
                if (!File.Exists(rough)) continue;

                var ti = AssetImporter.GetAtPath(rough) as TextureImporter;
                bool wasReadable = ti.isReadable;
                ti.isReadable = true;
                ti.SaveAndReimport();

                var src = AssetDatabase.LoadAssetAtPath<Texture2D>(rough);
                int w = src.width, h = src.height;
                var dst = new Texture2D(w, h, TextureFormat.RGBA32, true, true);

                Color[] sp = src.GetPixels();
                var dp = new Color[sp.Length];
                for (int i = 0; i < sp.Length; i++)
                    dp[i] = new Color(0f, 0f, 0f, 1f - sp[i].r);
                dst.SetPixels(dp);
                dst.Apply(true);

                string outPath = $"{PbrDir}/{set}_MetallicSmoothness.png";
                File.WriteAllBytes(outPath, dst.EncodeToPNG());
                Object.DestroyImmediate(dst);
                AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
                SetImporter(outPath, TextureImporterType.Default, false);

                ti.isReadable = wasReadable;
                ti.SaveAndReimport();
            }
        }

        // --------------------------------------------------------- materials
        static Texture2D Tex(string p) => AssetDatabase.LoadAssetAtPath<Texture2D>(p);

        static void Pbr(string matName, string set, Vector2 tiling, Color? tint = null,
                        float smoothnessScale = 1f)
        {
            string path = $"{MatDir}/{matName}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }

            // The raw albedos are lit for a studio swatch, not a sunlit track:
            // straight out of the box the asphalt reads near-white.
            m.SetColor("_BaseColor", tint ?? Color.white);
            m.SetTexture("_BaseMap", Tex($"{PbrDir}/{set}_Color.jpg"));

            Texture2D n = Tex($"{PbrDir}/{set}_NormalGL.jpg");
            if (n != null)
            {
                m.SetTexture("_BumpMap", n);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }

            Texture2D mg = Tex($"{PbrDir}/{set}_MetallicSmoothness.png");
            if (mg != null)
            {
                m.SetTexture("_MetallicGlossMap", mg);
                m.SetFloat("_Smoothness", smoothnessScale);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            Texture2D ao = Tex($"{PbrDir}/{set}_AmbientOcclusion.jpg");
            if (ao != null)
            {
                m.SetTexture("_OcclusionMap", ao);
                m.SetFloat("_OcclusionStrength", 1f);
                m.EnableKeyword("_OCCLUSIONMAP");
            }

            foreach (string prop in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                m.SetTextureScale(prop, tiling);

            EditorUtility.SetDirty(m);
        }

        static void BuildMaterials()
        {
            // Road UVs are u across the road and v in metres/8, so the asphalt
            // must not tile across or the painted edge lines would repeat
            // mid-track. Tiling only runs along the road.
            Pbr("Asphalt",  "Asphalt026A", new Vector2(2f, 3f),
                new Color(0.34f, 0.34f, 0.36f), 0.55f);
            // Grass must be matte: shiny grass reads as wet moss.
            Pbr("Grass",    "Grass004",    new Vector2(26f, 26f),
                new Color(0.46f, 0.52f, 0.40f), 0.18f);
            Pbr("Concrete", "Concrete034", new Vector2(4f, 4f),
                new Color(0.62f, 0.62f, 0.63f), 0.40f);
            Pbr("Barrier",  "Concrete034", new Vector2(8f, 1f),
                new Color(0.72f, 0.72f, 0.74f), 0.45f);

            // Car paint: high smoothness and real metallic so the HDRI shows in
            // it. This is what separates car paint from coloured plastic.
            foreach (var (name, col) in new (string, Color)[]
            {
                ("Car_Body",  new Color(0.55f, 0.03f, 0.05f)),
                ("Car_Body2", new Color(0.03f, 0.13f, 0.48f)),
                ("Car_Body3", new Color(0.88f, 0.70f, 0.04f)),
                ("Car_Body4", new Color(0.04f, 0.045f, 0.06f)),
                ("Car_Body5", new Color(0.82f, 0.82f, 0.86f)),
                ("Car_Body6", new Color(0.04f, 0.38f, 0.17f)),
            })
            {
                string path = $"{MatDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) continue;
                m.SetColor("_BaseColor", col);
                m.SetFloat("_Metallic", 0.92f);
                m.SetFloat("_Smoothness", 0.93f);
                m.SetTexture("_BaseMap", null);
                EditorUtility.SetDirty(m);
            }
        }

        // ------------------------------------------------ sky + reflections
        static void BuildSkyAndReflections()
        {
            string[] hdrs = Directory.Exists(HdriDir) ? Directory.GetFiles(HdriDir, "*.hdr") : new string[0];
            if (hdrs.Length == 0) throw new System.Exception("no HDRI found");

            var cube = AssetDatabase.LoadAssetAtPath<Texture>(hdrs[0]);
            if (cube == null) throw new System.Exception($"could not load {hdrs[0]}");

            string skyPath = $"{MatDir}/Sky_HDRI.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Cubemap"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetTexture("_Tex", cube);
            sky.SetFloat("_Exposure", 1.05f);
            EditorUtility.SetDirty(sky);

            foreach (string scene in new[]
            {
                "Assets/_Game/Scenes/Circuit_Apexline.unity",
                "Assets/_Game/Scenes/TestTrack_Flat.unity",
            })
            {
                if (!File.Exists(scene)) continue;
                EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);

                RenderSettings.skybox = sky;
                // Ambient AND reflections from the sky: this is what puts the
                // environment into every glossy surface without baking.
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1.0f;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                RenderSettings.defaultReflectionResolution = 256;
                RenderSettings.reflectionIntensity = 1f;

                DynamicGI.UpdateEnvironment();

                var a = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(a);
                EditorSceneManager.SaveScene(a);
                Debug.Log($"[PBR] sky + reflections applied to {Path.GetFileName(scene)}");
            }
        }
    }
}
