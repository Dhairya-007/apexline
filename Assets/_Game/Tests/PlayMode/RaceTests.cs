using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Apexline.AI;
using Apexline.Race;
using Apexline.UI;
using Apexline.Vehicle;

namespace Apexline.Tests.PlayMode
{
    /// <summary>
    /// Does the race work as a race? Six cars, a countdown, positions that
    /// reorder as the field spreads, lap counting that cannot be cut, and a HUD
    /// that reads the right values.
    ///
    /// These are the properties that make the thing playable rather than
    /// drivable, and none of them can be checked by looking at the scene.
    /// </summary>
    public class RaceTests
    {
        RaceManager _race;
        RaceHud _hud;
        CarController _player;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Circuit_Apexline", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _race = Object.FindFirstObjectByType<RaceManager>();
            _hud = Object.FindFirstObjectByType<RaceHud>();
            Assert.IsNotNull(_race, "no RaceManager");
            Assert.IsNotNull(_hud, "no RaceHud");

            foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
                if (c.GetComponent<AIDriver>() == null) { _player = c; break; }
            Assert.IsNotNull(_player, "no player car");
        }

        IEnumerator WaitForRacing(float timeout = 15f)
        {
            float t = 0f;
            while (_race.Current != RaceManager.State.Racing && t < timeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Grid_HasAFullFieldOfSixCars()
        {
            yield return null;

            var cars = Object.FindObjectsByType<CarController>(FindObjectsSortMode.None);
            Assert.AreEqual(6, cars.Length, "expected 1 player + 5 AI on the grid");

            int ai = 0, human = 0;
            foreach (var c in cars)
            {
                bool hasAi = c.GetComponent<AIDriver>() != null;
                bool hasPlayer = c.GetComponent<PlayerDriver>() != null;
                if (hasAi) ai++;
                if (hasPlayer) human++;

                // Both drivers on one car means two things write CarInput each
                // frame and the car twitches.
                Assert.IsFalse(hasAi && hasPlayer,
                    $"{c.name} has both an AIDriver and a PlayerDriver");
            }
            Assert.AreEqual(5, ai, "expected 5 AI drivers");
            Assert.AreEqual(1, human, "expected exactly 1 player driver");

            // Nobody should start inside anybody else.
            for (int i = 0; i < cars.Length; i++)
                for (int j = i + 1; j < cars.Length; j++)
                    Assert.Greater(Vector3.Distance(cars[i].transform.position, cars[j].transform.position), 2.5f,
                        $"{cars[i].name} and {cars[j].name} start on top of each other");
        }

        [UnityTest]
        public IEnumerator Countdown_RunsThenReleasesTheField()
        {
            // Frozen during the countdown, so nobody jumps the start.
            Assert.AreEqual(RaceManager.State.Countdown, _race.Current,
                "race should begin in Countdown");

            Vector3 before = _player.transform.position;
            yield return new WaitForSeconds(1f);
            Assert.Less(Vector3.Distance(before, _player.transform.position), 1f,
                "car moved during the countdown");

            yield return WaitForRacing();
            Assert.AreEqual(RaceManager.State.Racing, _race.Current,
                "race never left Countdown");
        }

        [UnityTest]
        public IEnumerator Hud_IsBuiltAndReadsTheRace()
        {
            yield return WaitForRacing();
            yield return new WaitForSeconds(2f);

            Assert.IsTrue(_hud.IsBuilt, "HUD did not build its canvas");
            Assert.IsNotNull(_hud.race, "HUD has no RaceManager");
            Assert.IsNotNull(_hud.playerCar, "HUD has no player car");
            Assert.IsNull(_hud.playerCar.GetComponent<AIDriver>(),
                "HUD is following an AI car instead of the player");

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "no Canvas in the scene");

            var texts = canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            Assert.Greater(texts.Length, 6, $"HUD has only {texts.Length} text elements");

            int withFont = 0, nonEmpty = 0;
            foreach (var t in texts)
            {
                if (t.font != null) withFont++;
                if (!string.IsNullOrEmpty(t.text)) nonEmpty++;
            }
            Assert.AreEqual(texts.Length, withFont,
                "some HUD text has no font and would render invisible");
            Assert.Greater(nonEmpty, 5, "most HUD fields are blank");

            // The lap readout must say something sensible, not "LAP 0/0".
            bool sawLap = false;
            foreach (var t in texts)
                if (t.text.StartsWith("LAP ") && t.text.Contains("/" + _race.totalLaps)) sawLap = true;
            Assert.IsTrue(sawLap, $"no lap readout showing /{_race.totalLaps}");
        }

        [UnityTest]
        public IEnumerator Standings_OrderByProgressAndTheFieldSpreads()
        {
            yield return WaitForRacing();
            yield return new WaitForSeconds(70f);

            IReadOnlyList<RaceManager.Racer> table = _race.Standings;
            Assert.AreEqual(6, table.Count, "standings should list all six cars");

            // Sorted leader first.
            for (int i = 1; i < table.Count; i++)
                Assert.GreaterOrEqual(table[i - 1].totalProgress, table[i].totalProgress,
                    $"P{i} is behind P{i + 1} but listed ahead");

            // Positions must match the table order.
            for (int i = 0; i < table.Count; i++)
                Assert.AreEqual(i + 1, table[i].position,
                    $"{table[i].displayName} is at index {i} but reports P{table[i].position}");

            float spread = table[0].totalProgress - table[table.Count - 1].totalProgress;
            Debug.Log($"BENCH|field-spread-70s|{spread:F1}|nodes");
            Debug.Log($"BENCH|leader-progress-70s|{table[0].totalProgress:F1}|nodes");

            // Skill variance should string the field out rather than leave it
            // as one block.
            Assert.Greater(spread, 3f,
                $"field spread only {spread:F1} nodes after 70 s - the AI are running as a train");
            Assert.Greater(table[0].totalProgress, 20f,
                "the leader has barely moved");
        }

        [UnityTest]
        public IEnumerator Laps_AreCountedAndRequireAFullCircuit()
        {
            _race.totalLaps = 1;   // keep the test to a sane length

            int lapEvents = 0;
            RaceManager.Racer firstFinisher = null;
            _race.LapCompleted += _ => lapEvents++;
            _race.RacerFinished += r => firstFinisher ??= r;

            yield return WaitForRacing();

            float t = 0f;
            while (t < 180f && lapEvents == 0)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Debug.Log($"BENCH|first-lap-at|{t:F0}|s");
            Debug.Log($"BENCH|lap-events|{lapEvents}|count");

            Assert.Greater(lapEvents, 0,
                "nobody completed a lap in 180 s - sector validation may be rejecting valid laps");
            Assert.IsNotNull(firstFinisher,
                "a lap was counted but no racer was marked finished on a 1-lap race");
            Assert.AreEqual(1, firstFinisher.lap, "finisher should have exactly 1 completed lap");
            Assert.Greater(firstFinisher.bestLapTime, 20f,
                $"best lap of {firstFinisher.bestLapTime:F1}s is impossibly quick - the lap was cut");
            Assert.Less(firstFinisher.bestLapTime, 180f, "lap time is implausibly slow");
        }
    }
}
