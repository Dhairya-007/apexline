using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Apexline.AI;
using Apexline.Race;
using Apexline.UI;
using Apexline.Vehicle;

namespace Apexline.EditorTools
{
    /// <summary>
    /// Turns the circuit into an actual race: fills the grid with AI opponents
    /// and adds the HUD. Idempotent - re-running removes the previous AI cars
    /// first, so it will not stack six more onto the grid every time.
    ///   -executeMethod Apexline.EditorTools.ApexlineRaceBuilder.Build
    /// </summary>
    public static class ApexlineRaceBuilder
    {
        const string ScenePath = "Assets/_Game/Scenes/Circuit_Apexline.unity";
        const string CarPrefab = "Assets/_Game/Prefabs/Car_Prototype.prefab";

        /// <summary>
        /// Per-opponent skill. Spread rather than uniform so the field strings
        /// out into a race instead of running as one block; AIDriver adds its
        /// own +/-6% jitter on top.
        /// </summary>
        static readonly float[] Skills = { 0.95f, 0.88f, 0.82f, 0.74f, 0.66f };

        static readonly string[] Names = { "Vega", "Cassini", "Ridge", "Okonkwo", "Marlow" };

        [MenuItem("Apexline/Build Race (AI opponents + HUD)", false, 22)]
        public static void Build()
        {
            int failed = 0;
            failed += Step("open circuit", () =>
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (!scene.IsValid()) throw new System.Exception($"could not open {ScenePath}");
            });
            if (failed > 0) { EditorApplication.Exit(1); return; }

            failed += Step("AI opponents", AddOpponents);
            failed += Step("HUD", AddHud);

            failed += Step("save", () =>
            {
                var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(active);
                EditorSceneManager.SaveScene(active);
            });

            AssetDatabase.SaveAssets();
            Debug.Log($"[Race] done, {failed} step(s) failed");
            if (failed > 0) EditorApplication.Exit(1);
        }

        static int Step(string name, System.Action a)
        {
            try { a(); Debug.Log($"[Race] OK   {name}"); return 0; }
            catch (System.Exception e)
            {
                Debug.LogError($"[Race] FAIL {name}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                return 1;
            }
        }

        static void AddOpponents()
        {
            var race = Object.FindFirstObjectByType<RaceManager>();
            if (race == null) throw new System.Exception("no RaceManager in the circuit scene");
            if (race.path == null) throw new System.Exception("RaceManager has no racing line");
            if (race.gridSlots == null || race.gridSlots.Length < 2)
                throw new System.Exception("RaceManager has fewer than 2 grid slots");

            // Idempotence: clear any AI cars a previous run added.
            int removed = 0;
            foreach (var ai in Object.FindObjectsByType<AIDriver>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(ai.gameObject);
                removed++;
            }
            if (removed > 0) Debug.Log($"[Race] removed {removed} AI car(s) from a previous run");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefab);
            if (prefab == null) throw new System.Exception($"{CarPrefab} missing");

            var opponents = new GameObject("Opponents").transform;

            // Slot 0 is the player's. Opponents fill the rest.
            int count = Mathf.Min(Skills.Length, race.gridSlots.Length - 1);
            for (int i = 0; i < count; i++)
            {
                Transform slot = race.gridSlots[i + 1];
                if (slot == null) continue;

                var car = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                car.name = Names[i % Names.Length];
                car.transform.SetParent(opponents, true);
                car.transform.SetPositionAndRotation(slot.position, slot.rotation);

                // An AI must never also carry a human driver, or both write
                // CarInput in the same frame and the car twitches.
                var pd = car.GetComponent<PlayerDriver>();
                if (pd != null) Object.DestroyImmediate(pd);

                var ai = car.AddComponent<AIDriver>();
                ai.path = race.path;
                ai.skill = Skills[i];

                // Only cars should trigger avoidance casts; the road and
                // barriers are static geometry the driver already follows.
                ai.carLayers = ~0;
            }

            Debug.Log($"[Race] {count} AI opponents on the grid, skills " +
                      string.Join("/", System.Array.ConvertAll(Skills, s => s.ToString("0.00"))));
        }

        static void AddHud()
        {
            var existing = Object.FindFirstObjectByType<RaceHud>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject("RaceHUD");
            var hud = go.AddComponent<RaceHud>();
            hud.race = Object.FindFirstObjectByType<RaceManager>();

            // Player car = the one without an AI driver.
            foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
            {
                if (c.GetComponent<AIDriver>() == null) { hud.playerCar = c; break; }
            }

            if (hud.race == null) throw new System.Exception("HUD has no RaceManager to read");
            if (hud.playerCar == null) throw new System.Exception("HUD found no player car");

            Debug.Log($"[Race] HUD added, player car '{hud.playerCar.name}'");
        }
    }
}
