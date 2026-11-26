using NUnit.Framework;
using UnityEngine;
using Apexline.AI;

namespace Apexline.Tests
{
    /// <summary>
    /// WaypointPath is load-bearing: AI steering, braking points, race position
    /// sorting and respawn all read it. These tests pin the maths that the rest
    /// of the game trusts.
    /// </summary>
    public class WaypointPathTests
    {
        GameObject _root;
        WaypointPath _path;

        /// <summary>Builds a square loop: (0,0) -> (100,0) -> (100,100) -> (0,100).</summary>
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestPath");
            _path = _root.AddComponent<WaypointPath>();
            _path.closedLoop = true;

            Vector3[] corners =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(100f, 0f, 0f),
                new Vector3(100f, 0f, 100f),
                new Vector3(0f, 0f, 100f)
            };

            foreach (var c in corners)
            {
                var node = new GameObject("Node").transform;
                node.SetParent(_root.transform);
                node.position = c;
                _path.nodes.Add(new WaypointPath.Node { point = node, halfWidth = 5f });
            }
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void Wrap_IndexesCycle_WhenClosedLoop()
        {
            Assert.AreEqual(0, _path.Wrap(4), "index == Count should wrap to 0");
            Assert.AreEqual(3, _path.Wrap(-1), "negative index should wrap from the end");
            Assert.AreEqual(1, _path.Wrap(9), "multiple laps of wrapping should fold down");
        }

        [Test]
        public void Wrap_Clamps_WhenNotClosedLoop()
        {
            _path.closedLoop = false;
            Assert.AreEqual(3, _path.Wrap(7));
            Assert.AreEqual(0, _path.Wrap(-3));
        }

        [Test]
        public void NearestIndex_FindsClosestNode()
        {
            Assert.AreEqual(1, _path.NearestIndex(new Vector3(98f, 0f, 4f)));
            Assert.AreEqual(3, _path.NearestIndex(new Vector3(-6f, 0f, 103f)));
        }

        [Test]
        public void Progress_IncreasesAlongFirstSegment()
        {
            float atStart = _path.Progress(Vector3.zero);
            float quarter = _path.Progress(new Vector3(25f, 0f, 0f));
            float threeQuarter = _path.Progress(new Vector3(75f, 0f, 0f));

            Assert.Less(atStart, quarter);
            Assert.Less(quarter, threeQuarter);
            Assert.AreEqual(0.25f, quarter, 0.01f, "should read as a quarter of segment 0");
        }

        [Test]
        public void Progress_IsOffsetByNodeIndex()
        {
            // Halfway along the second segment -> node 1 plus 0.5.
            float p = _path.Progress(new Vector3(100f, 0f, 50f));
            Assert.AreEqual(1.5f, p, 0.02f);
        }

        [Test]
        public void Forward_PointsToNextNode()
        {
            Vector3 fwd = _path.Forward(0);
            Assert.AreEqual(1f, fwd.x, 0.001f);
            Assert.AreEqual(0f, fwd.z, 0.001f);
            Assert.AreEqual(1f, fwd.magnitude, 0.001f, "Forward must be normalised");
        }

        [Test]
        public void CurvatureAhead_IsZero_OnAStraight()
        {
            var straightRoot = new GameObject("Straight");
            var straight = straightRoot.AddComponent<WaypointPath>();
            straight.closedLoop = false;

            for (int i = 0; i < 10; i++)
            {
                var node = new GameObject($"N{i}").transform;
                node.SetParent(straightRoot.transform);
                node.position = new Vector3(i * 10f, 0f, 0f);
                straight.nodes.Add(new WaypointPath.Node { point = node });
            }

            Assert.AreEqual(0f, straight.CurvatureAhead(0, 1), 0.001f);
            Object.DestroyImmediate(straightRoot);
        }

        [Test]
        public void CurvatureAhead_IsHigh_OnARightAngle()
        {
            // The square turns 90 degrees at every node, which is full curvature.
            float c = _path.CurvatureAhead(0, 1);
            Assert.Greater(c, 0.9f, "a 90 degree corner should read near 1");
        }

        [Test]
        public void CurvatureAhead_IsSafe_OnDegeneratePath()
        {
            var tiny = new GameObject("Tiny");
            var p = tiny.AddComponent<WaypointPath>();
            Assert.AreEqual(0f, p.CurvatureAhead(0), 0.0001f, "must not divide by zero");
            Object.DestroyImmediate(tiny);
        }
    }
}
