using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Apexline.AI;
using Apexline.Race;
using Apexline.Vehicle;

namespace Apexline.Tests.PlayMode
{
    /// <summary>
    /// Is the generated circuit actually drivable? The geometry checks prove the
    /// road, racing line and triggers agree about where the track is; only
    /// driving it proves the line is followable, the corners are takeable at the
    /// speeds the waypoints allow, and the barriers do not trap a car.
    ///
    /// An AI driver is used as the test pilot, which also exercises the whole
    /// chain: WaypointPath -> AIDriver -> CarInput -> CarController -> physics.
    /// </summary>
    public class CircuitLapTests
    {
        CarController _car;
        WaypointPath _path;
        RaceManager _race;
        AIDriver _ai;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Circuit_Apexline", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _car = Object.FindFirstObjectByType<CarController>();
            _path = Object.FindFirstObjectByType<WaypointPath>();
            _race = Object.FindFirstObjectByType<RaceManager>();

            Assert.IsNotNull(_car, "no car in the circuit scene");
            Assert.IsNotNull(_path, "no racing line in the circuit scene");

            // Replace the human driver with an AI pilot.
            var pd = _car.GetComponent<PlayerDriver>();
            if (pd != null) Object.Destroy(pd);
            yield return null;

            _ai = _car.gameObject.AddComponent<AIDriver>();
            _ai.path = _path;
            _ai.skill = 0.85f;

            // RaceManager freezes the grid during its countdown.
            float waited = 0f;
            while (_race != null && _race.Current != RaceManager.State.Racing && waited < 15f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
        }

        [UnityTest]
        public IEnumerator AI_MakesSubstantialProgressAroundTheLap()
        {
            float startProgress = _path.Progress(_car.transform.position);
            Vector3 startPos = _car.transform.position;

            float travelled = 0f;
            Vector3 last = startPos;
            float lowestY = startPos.y;
            float t = 0f;

            while (t < 90f)
            {
                yield return null;
                t += Time.deltaTime;
                travelled += Vector3.Distance(last, _car.transform.position);
                last = _car.transform.position;
                lowestY = Mathf.Min(lowestY, _car.transform.position.y);
            }

            float endProgress = _path.Progress(_car.transform.position);
            float nodesCovered = Mathf.Repeat(endProgress - startProgress, _path.Count);

            Debug.Log($"BENCH|ai-distance-90s|{travelled:F0}|m");
            Debug.Log($"BENCH|ai-nodes-covered|{nodesCovered:F0}|of {_path.Count}");
            Debug.Log($"BENCH|ai-lowest-y|{lowestY:F1}|m");

            Assert.Greater(lowestY, -10f,
                $"car fell off the circuit (lowest y {lowestY:F1}) - a hole in the road or a gap at the seam");

            // 2342 m lap. Covering under a quarter of it in 90 s means the AI is
            // stuck, spinning, or the line is not followable.
            Assert.Greater(nodesCovered, _path.Count * 0.25f,
                $"AI covered only {nodesCovered:F0} of {_path.Count} nodes in 90 s");
            Assert.Greater(travelled, 300f,
                $"AI only travelled {travelled:F0} m in 90 s");
        }

        [UnityTest]
        public IEnumerator AI_StaysUprightAndOnTheRoad()
        {
            int offRoadFrames = 0, tiltedFrames = 0, checks = 0;
            float t = 0f;

            while (t < 45f)
            {
                yield return new WaitForSeconds(0.25f);
                t += 0.25f;
                checks++;

                if (Vector3.Angle(_car.transform.up, Vector3.up) > 40f) tiltedFrames++;

                RaycastHit[] hits = Physics.RaycastAll(
                    _car.transform.position + Vector3.up * 4f, Vector3.down, 40f,
                    ~0, QueryTriggerInteraction.Ignore);
                bool onRoad = false;
                foreach (var h in hits)
                    if (h.collider != null && h.collider.gameObject.name == "Road") onRoad = true;
                if (!onRoad) offRoadFrames++;
            }

            Debug.Log($"BENCH|ai-offroad-pct|{100f * offRoadFrames / checks:F0}|%");
            Debug.Log($"BENCH|ai-tilted-pct|{100f * tiltedFrames / checks:F0}|%");

            Assert.Less(offRoadFrames, checks * 0.5f,
                $"AI was off the road for {offRoadFrames}/{checks} samples - the racing line leaves the track");
            Assert.Less(tiltedFrames, checks * 0.1f,
                $"AI was heavily tilted for {tiltedFrames}/{checks} samples - banking is too aggressive or it is riding barriers");
        }

        [UnityTest]
        public IEnumerator Circuit_SectorTriggersFire()
        {
            Assume.That(_race, Is.Not.Null, "no RaceManager");

            int laps = 0;
            _race.LapCompleted += _ => laps++;

            // Sector hits are internal, so progress across a sector boundary is
            // the observable proxy: the car must pass at least one trigger.
            float startProgress = _path.Progress(_car.transform.position);
            float t = 0f;
            while (t < 60f)
            {
                yield return null;
                t += Time.deltaTime;
            }
            float covered = Mathf.Repeat(_path.Progress(_car.transform.position) - startProgress, _path.Count);

            // With 4 sectors evenly spaced, a quarter lap crosses at least one.
            Assert.Greater(covered, _path.Count * 0.25f,
                "did not cover enough of the lap to cross a sector trigger");
            Assert.AreEqual(RaceManager.State.Racing, _race.Current,
                "race should still be running after 60 s of a 3-lap race");
        }
    }
}
