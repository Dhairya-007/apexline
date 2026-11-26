using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Apexline.EditorTools
{
    /// <summary>
    /// Applies the project settings that are painful or impossible to change
    /// later, and creates the URP assets. Runnable headlessly:
    ///   Unity -batchmode -quit -projectPath . \
    ///         -executeMethod Apexline.EditorTools.ApexlineConfigure.Run
    ///
    /// Each step is independently guarded: a single API mismatch between Unity
    /// versions logs and continues rather than aborting the whole configure.
    /// </summary>
    public static class ApexlineConfigure
    {
        const string SettingsDir   = "Assets/Settings";
        const string RendererPath  = SettingsDir + "/Apexline_Renderer.asset";
        const string PipelinePath  = SettingsDir + "/Apexline_URP.asset";

        public static void Run()
        {
            int failed = 0;
            failed += Step("URP assets",        CreateUrpAssets);
            failed += Step("colour space",      () => PlayerSettings.colorSpace = ColorSpace.Linear);
            failed += Step("scripting backend", SetScriptingBackend);
            failed += Step("android arch",      () => PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64);
            failed += Step("graphics APIs",     SetGraphicsApis);
            failed += Step("orientation",       () => PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft);
            failed += Step("fixed timestep",    () => Time.fixedDeltaTime = 0.02f);
            failed += Step("input backend",     SetInputBackend);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ApexlineConfigure] done, {failed} step(s) failed");
            if (failed > 0) EditorApplication.Exit(1);
        }

        static int Step(string name, Action action)
        {
            try
            {
                action();
                Debug.Log($"[ApexlineConfigure] OK   {name}");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ApexlineConfigure] FAIL {name}: {e.GetType().Name}: {e.Message}");
                return 1;
            }
        }

        static void CreateUrpAssets()
        {
            Directory.CreateDirectory(SettingsDir);

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererPath);

            UniversalRenderPipelineAsset pipeline =
                UniversalRenderPipelineAsset.Create(rendererData);

            // Mobile-first defaults. HDR and MSAA are the two biggest wins to
            // give back on a phone if the frame budget gets tight.
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 60f;
            pipeline.shadowCascadeCount = 1;

            AssetDatabase.CreateAsset(pipeline, PipelinePath);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        static void SetScriptingBackend()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        }

        static void SetGraphicsApis()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
            {
                GraphicsDeviceType.Vulkan,
                GraphicsDeviceType.OpenGLES3,
            });
        }

        static void SetInputBackend()
        {
            // 0 = legacy only, 1 = new Input System only, 2 = both.
            // No public API for this; it lives in ProjectSettings.asset.
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
                throw new Exception("could not load ProjectSettings.asset");

            var so = new SerializedObject(assets[0]);
            SerializedProperty prop = so.FindProperty("activeInputHandler");
            if (prop == null) throw new Exception("activeInputHandler property not found");

            prop.intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
