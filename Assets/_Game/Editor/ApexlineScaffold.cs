using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Apexline.Util;
using Apexline.Vehicle;

namespace Apexline.EditorTools
{
    /// <summary>
    /// Builds the car prefab and a flat test scene headlessly:
    ///   Unity -batchmode -quit -projectPath . \
    ///         -executeMethod Apexline.EditorTools.ApexlineScaffold.Build
    ///
    /// Idempotent: re-running overwrites the prefab, materials and scene, so it
    /// doubles as a way to reset a test setup that has been fiddled with.
    ///
    /// Every tuning number here comes from docs/TUNING.md. If you change one,
    /// change it there too or the documented reference values become fiction.
    /// </summary>
    public static class ApexlineScaffold
    {
        const string MatDir      = "Assets/_Game/Materials";
        const string PrefabDir   = "Assets/_Game/Prefabs";
        const string SceneDir    = "Assets/_Game/Scenes";
        const string CarPrefab   = PrefabDir + "/Car_Prototype.prefab";
        const string TestScene   = SceneDir + "/TestTrack_Flat.unity";

        // Wheel geometry. Radius 0.35 m is a realistic road-car wheel.
        const float WheelRadius  = 0.35f;
        const float TrackHalf    = 0.8f;    // half the axle width
        const float WheelBaseHalf = 1.35f;  // half the wheelbase
        const float WheelY       = -0.1f;   // local height of the wheel centres

        public static void Build()
        {
            int failed = 0;
            failed += Step("directories", EnsureDirectories);

            Material body = null, wheel = null, ground = null, cone = null;
            failed += Step("materials", () =>
            {
                body   = MakeMaterial("Car_Body",  new Color(0.70f, 0.09f, 0.12f), 0.9f, 0.85f);
                wheel  = MakeMaterial("Wheel",     new Color(0.07f, 0.07f, 0.08f), 0.0f, 0.25f);
                ground = MakeMaterial("Asphalt",   new Color(0.22f, 0.22f, 0.24f), 0.0f, 0.35f);
                cone   = MakeMaterial("Cone",      new Color(0.95f, 0.35f, 0.05f), 0.0f, 0.40f);
            });

            GameObject prefab = null;
            failed += Step("car prefab", () => prefab = BuildCarPrefab(body, wheel));
            failed += Step("test scene", () => BuildTestScene(prefab, ground, cone));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ApexlineScaffold] done, {failed} step(s) failed");
            if (failed > 0) EditorApplication.Exit(1);
        }

        static int Step(string name, System.Action action)
        {
            try { action(); Debug.Log($"[ApexlineScaffold] OK   {name}"); return 0; }
            catch (System.Exception e)
            {
                Debug.LogError($"[ApexlineScaffold] FAIL {name}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                return 1;
            }
        }

        static void EnsureDirectories()
        {
            foreach (string d in new[] { MatDir, PrefabDir, SceneDir })
                Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------ materials
        static Material MakeMaterial(string name, Color albedo, float metallic, float smoothness)
        {
            // URP shaders must be found by name: a primitive's Default-Material
            // uses the built-in pipeline and renders magenta under URP.
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) throw new System.Exception("URP Lit shader not found - is URP installed?");

            var m = new Material(s);
            m.SetColor("_BaseColor", albedo);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);

            string path = $"{MatDir}/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        // ----------------------------------------------------------- car prefab
        static GameObject BuildCarPrefab(Material bodyMat, Material wheelMat)
        {
            var root = new GameObject("Car_Prototype");

            // --- body ---------------------------------------------------
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1300f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.15f, 0f);
            box.size = new Vector3(1.75f, 0.70f, 4.10f);

            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shell.name = "BodyMesh";
            Object.DestroyImmediate(shell.GetComponent<BoxCollider>());
            shell.transform.SetParent(root.transform, false);
            shell.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            shell.transform.localScale = new Vector3(1.75f, 0.70f, 4.10f);
            shell.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            // --- wheels -------------------------------------------------
            var colliderRoot = new GameObject("WheelColliders");
            colliderRoot.transform.SetParent(root.transform, false);
            var meshRoot = new GameObject("WheelMeshes");
            meshRoot.transform.SetParent(root.transform, false);

            // Order is load-bearing: CarController.ApplyAntiRoll pairs 0/1 as the
            // front axle and 2/3 as the rear.
            var layout = new (string name, float x, float z, bool steer, bool drive)[]
            {
                ("FL", -TrackHalf,  WheelBaseHalf, true,  false),
                ("FR",  TrackHalf,  WheelBaseHalf, true,  false),
                ("RL", -TrackHalf, -WheelBaseHalf, false, true),
                ("RR",  TrackHalf, -WheelBaseHalf, false, true),
            };

            var wheels = new List<CarController.Wheel>();

            foreach (var w in layout)
            {
                var pos = new Vector3(w.x, WheelY, w.z);

                var colGo = new GameObject(w.name + "_Collider");
                colGo.transform.SetParent(colliderRoot.transform, false);
                colGo.transform.localPosition = pos;
                WheelCollider wc = colGo.AddComponent<WheelCollider>();
                ConfigureWheelCollider(wc);

                // The mesh pivot is an empty whose rotation CarController drives
                // from GetWorldPose. The cylinder is a child carrying the fixed
                // 90-degree roll, because a baked rotation on the driven object
                // itself would be overwritten every frame.
                var pivot = new GameObject(w.name + "_Mesh");
                pivot.transform.SetParent(meshRoot.transform, false);
                pivot.transform.localPosition = pos;

                GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Disc";
                Object.DestroyImmediate(disc.GetComponent<CapsuleCollider>());
                disc.transform.SetParent(pivot.transform, false);
                disc.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                // Cylinder primitive is 2 units tall and 1 unit across.
                disc.transform.localScale = new Vector3(WheelRadius * 2f, 0.10f, WheelRadius * 2f);
                disc.GetComponent<MeshRenderer>().sharedMaterial = wheelMat;

                wheels.Add(new CarController.Wheel
                {
                    collider  = wc,
                    mesh      = pivot.transform,
                    steer     = w.steer,
                    drive     = w.drive,
                    handbrake = !w.steer,     // handbrake acts on the rear axle
                });
            }

            // --- controller + input (reference values from docs/TUNING.md) ---
            var car = root.AddComponent<CarController>();
            car.wheels = wheels.ToArray();
            car.centerOfMassOffset = new Vector3(0f, -0.30f, 0f);
            car.mass = 1300f;
            car.maxMotorTorque = 2500f;
            car.maxSpeedKph = 260f;
            car.brakeTorque = 4000f;
            car.handbrakeTorque = 6000f;
            car.brakeBiasFront = 0.62f;
            car.maxSteerAngleLowSpeed = 28f;
            car.maxSteerAngleHighSpeed = 8f;
            car.steerFalloffSpeedKph = 220f;
            car.steerRateDegPerSec = 160f;
            car.counterSteerAssist = 0.35f;
            car.antiRollFront = 9000f;
            car.antiRollRear = 7000f;
            car.downforceAtTopSpeed = 6500f;
            car.tractionControl = 0.40f;

            var driver = root.AddComponent<PlayerDriver>();
            driver.autoThrottle = false;   // desktop iteration wants a throttle key
            driver.useTouchControls = false;
            driver.steerSmoothing = 0.12f;

            Directory.CreateDirectory(PrefabDir);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CarPrefab);
            Object.DestroyImmediate(root);
            return saved;
        }

        static void ConfigureWheelCollider(WheelCollider wc)
        {
            wc.radius = WheelRadius;
            wc.mass = 20f;
            wc.wheelDampingRate = 0.25f;
            wc.suspensionDistance = 0.25f;
            wc.forceAppPointDistance = 0f;

            JointSpring spring = wc.suspensionSpring;
            spring.spring = 35000f;
            spring.damper = 4500f;
            spring.targetPosition = 0.5f;
            wc.suspensionSpring = spring;

            WheelFrictionCurve fwd = wc.forwardFriction;
            fwd.extremumSlip = 0.40f;  fwd.extremumValue = 1.00f;
            fwd.asymptoteSlip = 0.80f; fwd.asymptoteValue = 0.75f;
            fwd.stiffness = 2.0f;
            wc.forwardFriction = fwd;

            WheelFrictionCurve side = wc.sidewaysFriction;
            side.extremumSlip = 0.25f;  side.extremumValue = 1.00f;
            side.asymptoteSlip = 0.50f; side.asymptoteValue = 0.75f;
            side.stiffness = 2.2f;
            wc.sidewaysFriction = side;
        }

        // ----------------------------------------------------------- test scene
        static void BuildTestScene(GameObject carPrefab, Material groundMat, Material coneMat)
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // --- ground -------------------------------------------------
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(50f, 1f, 50f);   // 500 m square
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
            ground.isStatic = true;

            // --- sun ----------------------------------------------------
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                sun.intensity = 1.1f;
                sun.shadows = LightShadows.Soft;
            }

            // --- car ----------------------------------------------------
            GameObject car = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab);
            car.name = "Car";
            // 0.6 m clears the suspension at rest: wheel centre 0.5, minus half
            // the 0.25 travel, minus the 0.35 radius, lands the tyre on y=0.
            car.transform.position = new Vector3(0f, 0.6f, 0f);

            // --- camera -------------------------------------------------
            Camera main = Object.FindFirstObjectByType<Camera>();
            GameObject camGo = main != null ? main.gameObject : new GameObject("Main Camera");
            if (main == null) camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            var chase = camGo.GetComponent<ChaseCamera>();
            if (chase == null) chase = camGo.AddComponent<ChaseCamera>();
            chase.target = car.GetComponent<CarController>();

            // --- cone slalom --------------------------------------------
            var cones = new GameObject("ConeSlalom");
            cones.isStatic = true;
            for (int i = 0; i < 12; i++)
            {
                GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                c.name = $"Cone_{i:00}";
                // Markers, not obstacles: hitting one should not wreck a test run.
                Object.DestroyImmediate(c.GetComponent<CapsuleCollider>());
                c.transform.SetParent(cones.transform, false);
                c.transform.localPosition = new Vector3(
                    (i % 2 == 0) ? -4f : 4f, 0.35f, 25f + i * 14f);
                c.transform.localScale = new Vector3(0.5f, 0.35f, 0.5f);
                c.GetComponent<MeshRenderer>().sharedMaterial = coneMat;
                c.isStatic = true;
            }

            Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene, TestScene);

            // Make it the scene that opens, and the one a build would use.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(TestScene, true) };
        }
    }
}
