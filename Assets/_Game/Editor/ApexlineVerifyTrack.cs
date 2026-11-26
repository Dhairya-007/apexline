using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Apexline.AI;
using Apexline.Race;
using Apexline.Vehicle;

namespace Apexline.EditorTools
{
    /// <summary>
    /// Geometric checks on the generated circuit. The road, the racing line and
    /// the sector triggers all come from one spline, so the thing worth proving
    /// is that they still agree about where the track is - a waypoint floating
    /// above the road, or a sector trigger off the surface, produces AI and lap
    /// counting that fail in ways that look like logic bugs.
    ///   -executeMethod Apexline.EditorTools.ApexlineVerifyTrack.Run
    /// </summary>
    public static class ApexlineVerifyTrack
    {
        static int _fail;

        static void Check(bool ok, string what, string detail = "")
        {
            if (ok) Debug.Log($"[VerifyTrack] PASS {what}");
            else { _fail++; Debug.LogError($"[VerifyTrack] FAIL {what} {detail}"); }
        }

        /// <summary>
        /// Is there road directly below this point? Uses RaycastAll and ignores
        /// triggers: a single Raycast hits the sector's own trigger volume, or
        /// the car's own body collider, long before it reaches the surface -
        /// which reads as "not over the road" when it plainly is.
        /// </summary>
        static bool RoadBelow(Vector3 p, float up = 5f, float depth = 60f)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                p + Vector3.up * up, Vector3.down, depth,
                ~0, QueryTriggerInteraction.Ignore);

            foreach (var h in hits)
                if (h.collider != null && h.collider.gameObject.name == "Road")
                    return true;
            return false;
        }

        [MenuItem("Apexline/Verify Circuit", false, 43)]
        public static void Run()
        {
            _fail = 0;

            var scene = EditorSceneManager.OpenScene(
                "Assets/_Game/Scenes/Circuit_Apexline.unity", OpenSceneMode.Single);
            Check(scene.IsValid(), "circuit scene opens");
            if (!scene.IsValid()) { EditorApplication.Exit(1); return; }

            // --- road mesh ------------------------------------------------
            GameObject road = GameObject.Find("Road");
            Check(road != null, "Road object exists");
            Mesh roadMesh = road?.GetComponent<MeshFilter>()?.sharedMesh;
            Check(roadMesh != null, "road has a mesh");
            Check(road?.GetComponent<MeshCollider>()?.sharedMesh != null,
                  "road has a MeshCollider (otherwise you drive through it)");

            if (roadMesh != null)
            {
                Check(roadMesh.vertexCount > 100, "road mesh has geometry",
                      $"({roadMesh.vertexCount} verts)");

                // A road whose faces point down is invisible from above and
                // renders as a hole.
                int down = 0;
                foreach (Vector3 n in roadMesh.normals)
                    if (n.y < 0.2f) down++;
                Check(down < roadMesh.normals.Length / 10,
                      "road normals point upward",
                      $"({down}/{roadMesh.normals.Length} face down or sideways)");

                Bounds b = roadMesh.bounds;
                Check(b.size.x > 100f && b.size.z > 100f, "road spans a circuit-sized area",
                      $"({b.size.x:F0} x {b.size.z:F0} m)");
                Check(b.size.y > 3f, "road has elevation change", $"({b.size.y:F1} m)");
            }

            Check(GameObject.Find("Barrier_Left") != null, "left barrier exists");
            Check(GameObject.Find("Barrier_Right") != null, "right barrier exists");
            foreach (string n in new[] { "Barrier_Left", "Barrier_Right" })
            {
                var g = GameObject.Find(n);
                Check(g?.GetComponent<MeshCollider>()?.sharedMesh != null,
                      $"{n} has a collider to stop cars");
            }

            // --- racing line ----------------------------------------------
            var path = Object.FindFirstObjectByType<WaypointPath>();
            Check(path != null, "racing line exists");
            if (path != null)
            {
                Check(path.closedLoop, "racing line is a closed loop");
                Check(path.Count > 80, "racing line has enough nodes", $"({path.Count})");

                int offRoad = 0, nullNode = 0, badLimit = 0;
                float maxGap = 0f;

                for (int i = 0; i < path.Count; i++)
                {
                    var node = path.nodes[i];
                    if (node.point == null) { nullNode++; continue; }

                    // Every waypoint must sit above actual road surface.
                    if (!RoadBelow(node.point.position)) offRoad++;

                    if (node.speedLimit < 0.15f || node.speedLimit > 1.001f) badLimit++;

                    float gap = Vector3.Distance(path.Position(i), path.Position(i + 1));
                    if (gap > maxGap) maxGap = gap;
                }

                Check(nullNode == 0, "no null waypoint transforms", $"({nullNode})");
                Check(offRoad == 0, "every waypoint sits over the road surface",
                      $"({offRoad} of {path.Count} are not)");
                Check(badLimit == 0, "every speed limit is in range", $"({badLimit} out of range)");
                Check(maxGap < 40f, "no large gap between waypoints", $"(largest {maxGap:F1} m)");

                // A corner somewhere must actually be slowed for, or the AI
                // will carry straight-line speed into every turn.
                float minLimit = 1f;
                foreach (var n in path.nodes) minLimit = Mathf.Min(minLimit, n.speedLimit);
                Check(minLimit < 0.75f, "at least one corner is speed-limited",
                      $"(tightest limit {minLimit:F2})");
            }

            // --- race setup -----------------------------------------------
            var race = Object.FindFirstObjectByType<RaceManager>();
            Check(race != null, "RaceManager exists");
            if (race != null)
            {
                Check(race.path != null, "RaceManager has the racing line assigned");
                Check(race.sectors != null && race.sectors.Length >= 3,
                      "at least 3 sectors (so a lap cannot be cut)",
                      $"({race.sectors?.Length})");
                Check(race.gridSlots != null && race.gridSlots.Length >= 2,
                      "grid has slots", $"({race.gridSlots?.Length})");
                Check(race.totalLaps > 0, "lap count is set");

                if (race.sectors != null)
                {
                    bool zero = false;
                    foreach (var s in race.sectors) if (s.sectorIndex == 0) zero = true;
                    Check(zero, "a sector with index 0 exists (the start/finish line)");

                    int offTrack = 0;
                    foreach (var s in race.sectors)
                        if (!RoadBelow(s.transform.position)) offTrack++;
                    Check(offTrack == 0, "every sector trigger is over the road", $"({offTrack} are not)");
                }

                if (race.gridSlots != null)
                {
                    int bad = 0;
                    foreach (var g in race.gridSlots)
                    {
                        if (g == null) { bad++; continue; }
                        if (!RoadBelow(g.position)) bad++;
                    }
                    Check(bad == 0, "every grid slot is on the road", $"({bad} are not)");
                }
            }

            // --- player ---------------------------------------------------
            var car = Object.FindFirstObjectByType<CarController>();
            Check(car != null, "a car is on the grid");
            if (car != null)
            {
                Check(RoadBelow(car.transform.position),
                      "the car starts above the road, not in the air or under it");
            }

            Debug.Log($"[VerifyTrack] {_fail} failure(s)");
            if (_fail > 0) EditorApplication.Exit(1);
        }
    }
}
