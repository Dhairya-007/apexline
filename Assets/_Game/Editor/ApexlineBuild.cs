using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Apexline.EditorTools
{
    /// <summary>
    /// Player builds, runnable headlessly:
    ///   Unity -batchmode -quit -projectPath . \
    ///         -executeMethod Apexline.EditorTools.ApexlineBuild.WebGL
    ///
    /// Output goes to Build/&lt;target&gt;/, which .gitignore excludes - a built
    /// player does not belong in the source repository.
    /// </summary>
    public static class ApexlineBuild
    {
        static string[] Scenes =>
            EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        [MenuItem("Apexline/Build/WebGL", false, 60)]
        public static void WebGL()
        {
            // Gzip plus a decompression fallback: the fallback makes the build
            // work on a static host that does not set Content-Encoding, which
            // GitHub Pages does not. Brotli would be smaller but needs server
            // configuration we will not have.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            // APPLICATION: = a template shipped with the editor.
            // PROJECT: would look for Assets/WebGLTemplates/Default, which does
            // not exist here, and the build fails in CheckBuildPrerequisites.
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;

            // Explicitly-thrown exceptions only: full checks cost a lot of
            // performance and size, none at all makes a crash silent.
            PlayerSettings.SetAdditionalIl2CppArgs("");
            EditorUserBuildSettings.SetPlatformSettings("WebGL", "CodeOptimization", "size");

            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            Run(BuildTarget.WebGL, "Build/WebGL");
        }

        [MenuItem("Apexline/Build/macOS", false, 61)]
        public static void MacOS() => Run(BuildTarget.StandaloneOSX, "Build/macOS/Apexline.app");

        [MenuItem("Apexline/Build/Android", false, 62)]
        public static void Android() => Run(BuildTarget.Android, "Build/Android/apexline.apk");

        static void Run(BuildTarget target, string outputPath)
        {
            string[] scenes = Scenes;
            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] no enabled scenes in build settings");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[Build] target {target}");
            foreach (string s in scenes) Debug.Log($"[Build]   scene {s}");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None,     // release: no profiler, no dev build
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[Build] result {summary.result}");
            Debug.Log($"[Build] size {summary.totalSize / (1024f * 1024f):F1} MB");
            Debug.Log($"[Build] time {summary.totalTime.TotalMinutes:F1} min");
            Debug.Log($"[Build] errors {summary.totalErrors}, warnings {summary.totalWarnings}");

            if (summary.result != BuildResult.Succeeded || summary.totalErrors > 0)
            {
                Debug.LogError($"[Build] FAILED: {summary.result}, {summary.totalErrors} error(s)");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[Build] OK -> {outputPath}");
        }
    }
}
