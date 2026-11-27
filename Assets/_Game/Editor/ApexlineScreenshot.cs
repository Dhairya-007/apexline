using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Apexline.Vehicle;

namespace Apexline.EditorTools
{
    /// <summary>
    /// Renders the scene to a PNG headlessly, so the look of the game can be
    /// inspected without opening the editor or playing a build:
    ///   Unity -batchmode -quit -projectPath . \
    ///         -executeMethod Apexline.EditorTools.ApexlineScreenshot.Capture
    ///
    /// Note: must run WITHOUT -nographics. With it, there is no graphics device
    /// and every render returns an empty image.
    /// </summary>
    public static class ApexlineScreenshot
    {
        const string OutDir = "Screenshots";
        const int Width = 1600;
        const int Height = 900;

        [MenuItem("Apexline/Capture Screenshots", false, 70)]
        public static void Capture()
        {
            Directory.CreateDirectory(OutDir);

            Shot("Assets/_Game/Scenes/Circuit_Apexline.unity", "01_chase",
                 cam => FrameCar(cam, new Vector3(0f, 2.0f, -7.5f), 1.0f));

            Shot("Assets/_Game/Scenes/Circuit_Apexline.unity", "02_low_rear",
                 cam => FrameCar(cam, new Vector3(1.6f, 1.0f, -5.5f), 0.8f));

            Shot("Assets/_Game/Scenes/Circuit_Apexline.unity", "03_front_three_quarter",
                 cam => FrameCar(cam, new Vector3(3.2f, 1.4f, 5.0f), 0.7f));

            Shot("Assets/_Game/Scenes/Circuit_Apexline.unity", "04_track_wide",
                 cam =>
                 {
                     var car = Object.FindFirstObjectByType<CarController>();
                     Vector3 p = car != null ? car.transform.position : Vector3.zero;
                     cam.transform.position = p + new Vector3(-40f, 28f, -40f);
                     cam.transform.LookAt(p + Vector3.forward * 60f);
                     cam.fieldOfView = 55f;
                 });

            Debug.Log($"[Shot] wrote images to {Path.GetFullPath(OutDir)}");
        }

        /// <summary>
        /// Drops every car onto the surface below it. Grid slots sit 0.6 m above
        /// the road so the suspension can settle at run time; in edit mode
        /// nothing settles, and every screenshot showed the field hovering.
        /// </summary>
        static void SettleCars()
        {
            foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
            {
                Vector3 p = c.transform.position;
                RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 5f, Vector3.down, 40f,
                                                       ~0, QueryTriggerInteraction.Ignore);
                float best = float.MinValue;
                foreach (var h in hits)
                {
                    string n = h.collider.gameObject.name;
                    if (n == "Road" || n == "Terrain") best = Mathf.Max(best, h.point.y);
                }
                if (best > float.MinValue) c.transform.position = new Vector3(p.x, best + 0.33f, p.z);
            }
        }

        static void FrameCar(Camera cam, Vector3 localOffset, float lookHeight)
        {
            var car = Object.FindFirstObjectByType<CarController>();
            if (car == null)
            {
                cam.transform.position = new Vector3(0f, 3f, -8f);
                cam.transform.rotation = Quaternion.identity;
                return;
            }

            Transform t = car.transform;
            cam.transform.position = t.TransformPoint(localOffset);
            cam.transform.LookAt(t.position + Vector3.up * lookHeight);
            cam.fieldOfView = 62f;
        }

        static void Shot(string scenePath, string name, System.Action<Camera> place)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Camera cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("[Shot] no camera in the scene");
                return;
            }

            // The chase camera would overwrite the framing on its first update.
            // Camera derives from Behaviour, not MonoBehaviour, so it is not in
            // this list and cannot be disabled by accident.
            foreach (var mb in cam.GetComponents<MonoBehaviour>())
                if (mb != null) mb.enabled = false;

            SettleCars();
            place(cam);

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
            };
            cam.targetTexture = rt;

            // Two renders: the first warms shaders and lets volumes initialise,
            // the second is the one that looks right.
            cam.Render();
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            cam.targetTexture = null;
            string path = Path.Combine(OutDir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);

            Debug.Log($"[Shot] {path}");
        }
    }
}
