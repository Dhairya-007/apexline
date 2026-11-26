using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Apexline.Util;
using Apexline.Vehicle;

namespace Apexline.EditorTools
{
    /// <summary>
    /// Asserts that the scaffolded prefab and scene are actually wired, rather
    /// than merely present. "The file exists" is not the same claim as "the car
    /// has four driven wheels pointing the right way".
    ///   Unity -batchmode -quit -projectPath . \
    ///         -executeMethod Apexline.EditorTools.ApexlineVerify.Run
    /// </summary>
    public static class ApexlineVerify
    {
        static int _fail;

        static void Check(bool ok, string what, string detail = "")
        {
            if (ok) Debug.Log($"[Verify] PASS {what}");
            else { _fail++; Debug.LogError($"[Verify] FAIL {what} {detail}"); }
        }

        static void Near(float actual, float expected, string what, float tol = 0.001f)
            => Check(Mathf.Abs(actual - expected) <= tol, what, $"(got {actual}, want {expected})");

        public static void Run()
        {
            _fail = 0;
            VerifyPrefab();
            VerifyScene();
            Debug.Log($"[Verify] {_fail} failure(s)");
            if (_fail > 0) EditorApplication.Exit(1);
        }

        static void VerifyPrefab()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Car_Prototype.prefab");
            Check(p != null, "prefab loads");
            if (p == null) return;

            var rb = p.GetComponent<Rigidbody>();
            Check(rb != null, "has Rigidbody");
            if (rb != null) Near(rb.mass, 1300f, "rigidbody mass", 0.5f);

            Check(p.GetComponent<BoxCollider>() != null, "has body BoxCollider");
            Check(p.GetComponent<PlayerDriver>() != null, "has PlayerDriver");

            var car = p.GetComponent<CarController>();
            Check(car != null, "has CarController");
            if (car == null) return;

            Check(car.wheels != null && car.wheels.Length == 4, "exactly 4 wheels",
                  $"(got {car.wheels?.Length})");
            if (car.wheels == null || car.wheels.Length != 4) return;

            for (int i = 0; i < 4; i++)
            {
                Check(car.wheels[i].collider != null, $"wheel[{i}] collider assigned");
                Check(car.wheels[i].mesh != null,     $"wheel[{i}] mesh assigned");
            }

            // Order is load-bearing: ApplyAntiRoll pairs 0/1 front, 2/3 rear.
            Check(car.wheels[0].steer && car.wheels[1].steer, "wheels 0,1 steer (front axle)");
            Check(!car.wheels[2].steer && !car.wheels[3].steer, "wheels 2,3 do not steer (rear axle)");
            Check(car.wheels[2].drive && car.wheels[3].drive, "wheels 2,3 drive (RWD)");
            Check(!car.wheels[0].drive && !car.wheels[1].drive, "wheels 0,1 undriven");
            Check(car.wheels[2].handbrake && car.wheels[3].handbrake, "handbrake on rear axle");

            // Front wheels must actually be in front, or steering is inverted.
            float frontZ = car.wheels[0].collider.transform.localPosition.z;
            float rearZ  = car.wheels[2].collider.transform.localPosition.z;
            Check(frontZ > rearZ, "front axle is ahead of rear", $"(front {frontZ}, rear {rearZ})");

            // Left wheels must be on -X, or anti-roll is inverted.
            Check(car.wheels[0].collider.transform.localPosition.x < 0f, "wheel 0 is on the left");
            Check(car.wheels[1].collider.transform.localPosition.x > 0f, "wheel 1 is on the right");

            WheelCollider wc = car.wheels[0].collider;
            Near(wc.radius, 0.35f, "wheel radius");
            Near(wc.suspensionDistance, 0.25f, "suspension distance");
            Near(wc.suspensionSpring.spring, 35000f, "suspension spring", 1f);
            Near(wc.suspensionSpring.damper, 4500f, "suspension damper", 1f);
            Near(wc.sidewaysFriction.stiffness, 2.2f, "sideways stiffness", 0.01f);
            Near(wc.forwardFriction.stiffness, 2.0f, "forward stiffness", 0.01f);

            Near(car.centerOfMassOffset.y, -0.30f, "centre of mass offset");
            Near(car.maxSteerAngleLowSpeed, 28f, "low-speed steer limit", 0.01f);
            Near(car.maxSteerAngleHighSpeed, 8f, "high-speed steer limit", 0.01f);
            Near(car.tractionControl, 0.40f, "traction control", 0.001f);

            // Materials must be URP, or everything renders magenta.
            foreach (var r in p.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material m = r.sharedMaterial;
                Check(m != null, $"{r.name} has a material");
                if (m != null)
                    Check(m.shader != null && m.shader.name.StartsWith("Universal Render Pipeline"),
                          $"{r.name} uses a URP shader", $"(got {m.shader?.name})");
            }
        }

        static void VerifyScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/TestTrack_Flat.unity", OpenSceneMode.Single);
            Check(scene.IsValid(), "test scene opens");
            if (!scene.IsValid()) return;

            var car = Object.FindFirstObjectByType<CarController>();
            Check(car != null, "scene contains a car");
            if (car != null)
                Check(car.transform.position.y > 0.4f && car.transform.position.y < 1.0f,
                      "car spawns clear of the ground", $"(y={car.transform.position.y})");

            var chase = Object.FindFirstObjectByType<ChaseCamera>();
            Check(chase != null, "scene contains a ChaseCamera");
            if (chase != null)
            {
                Check(chase.target != null, "chase camera has a target");
                Check(chase.GetComponent<Camera>() != null, "chase camera has a Camera");
            }

            var light = Object.FindFirstObjectByType<Light>();
            Check(light != null && light.type == LightType.Directional, "scene has a directional light");

            GameObject ground = GameObject.Find("Ground");
            Check(ground != null, "scene has Ground");
            if (ground != null)
                Check(ground.GetComponent<MeshCollider>() != null, "ground has a collider to drive on");

            GameObject cones = GameObject.Find("ConeSlalom");
            Check(cones != null && cones.transform.childCount == 12, "12 slalom cones",
                  $"(got {cones?.transform.childCount})");

            Check(EditorBuildSettings.scenes.Length > 0 &&
                  EditorBuildSettings.scenes[0].path == "Assets/_Game/Scenes/TestTrack_Flat.unity",
                  "test scene is first in build settings");
        }
    }
}
