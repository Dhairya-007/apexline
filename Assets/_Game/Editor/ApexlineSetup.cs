using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Apexline.EditorTools
{
    /// <summary>
    /// One-time project bootstrap, runnable headlessly:
    ///   Unity -batchmode -quit -projectPath . \
    ///         -executeMethod Apexline.EditorTools.ApexlineSetup.AddPackages
    ///
    /// Package versions are deliberately NOT pinned here. Client.Add with a bare
    /// name resolves the newest version compatible with this editor, which is
    /// more durable than a version string guessed at authoring time.
    /// </summary>
    public static class ApexlineSetup
    {
        static readonly string[] Required =
        {
            "com.unity.inputsystem",               // PlayerDriver's ENABLE_INPUT_SYSTEM
            "com.unity.render-pipelines.universal", // URP
            "com.unity.cinemachine",               // chase camera
            "com.unity.test-framework",            // EditMode tests
        };

        public static void AddPackages()
        {
            int failed = 0;

            foreach (string id in Required)
            {
                Debug.Log($"[ApexlineSetup] adding {id} ...");
                AddRequest req = Client.Add(id);

                // Batch mode has no editor loop to pump, so block until done.
                while (!req.IsCompleted) Thread.Sleep(100);

                if (req.Status == StatusCode.Success)
                {
                    Debug.Log($"[ApexlineSetup] OK {req.Result.packageId}");
                }
                else
                {
                    failed++;
                    Debug.LogError($"[ApexlineSetup] FAILED {id}: {req.Error?.message}");
                }
            }

            Client.Resolve();
            Debug.Log($"[ApexlineSetup] done, {failed} failure(s)");

            if (failed > 0) EditorApplication.Exit(1);
        }
    }
}
