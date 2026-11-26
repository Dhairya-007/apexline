using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Apexline.Track;

namespace Apexline.Tests
{
    /// <summary>
    /// The road mesh, the waypoints and the sector triggers are all generated
    /// from TrackSpline's samples, so an error here is an error in every one of
    /// them at once. Tested against shapes whose answers are known analytically:
    /// a circle of radius R has curvature 1/R and circumference 2*pi*R.
    /// </summary>
    public class TrackSplineTests
    {
        /// <summary>A closed ring of control points on a circle of radius r.</summary>
        static List<Vector3> Circle(float r, int n = 16)
        {
            var pts = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                pts.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            return pts;
        }

        [Test]
        public void Curvature_IsZero_OnCollinearPoints()
        {
            Assert.AreEqual(0f, TrackSpline.Curvature(
                new Vector3(0, 0, 0), new Vector3(0, 0, 10), new Vector3(0, 0, 20)), 1e-5f);
        }

        [Test]
        public void Curvature_IsZero_OnDegeneratePoints()
        {
            Vector3 p = new Vector3(3f, 0f, 4f);
            Assert.AreEqual(0f, TrackSpline.Curvature(p, p, p), 1e-6f,
                "coincident points must not divide by zero");
        }

        [Test]
        public void Curvature_MatchesInverseRadius_OnACircle()
        {
            // Three points on a circle of radius 50 -> curvature 0.02
            const float r = 50f;
            Vector3 a = new Vector3(r, 0f, 0f);
            Vector3 b = new Vector3(Mathf.Cos(0.3f) * r, 0f, Mathf.Sin(0.3f) * r);
            Vector3 c = new Vector3(Mathf.Cos(0.6f) * r, 0f, Mathf.Sin(0.6f) * r);

            Assert.AreEqual(1f / r, TrackSpline.Curvature(a, b, c), 1e-4f);
        }

        [Test]
        public void Curvature_IgnoresElevation()
        {
            // A straight that climbs is still a straight in plan view.
            Assert.AreEqual(0f, TrackSpline.Curvature(
                new Vector3(0, 0, 0), new Vector3(0, 5, 10), new Vector3(0, 10, 20)), 1e-5f);
        }

        [Test]
        public void Length_ApproximatesCircumference_OfACircle()
        {
            const float r = 100f;
            float expected = 2f * Mathf.PI * r;
            float actual = TrackSpline.Length(Circle(r, 24));

            // A 24-point Catmull-Rom ring is very slightly inside the circle.
            Assert.AreEqual(expected, actual, expected * 0.02f,
                $"expected about {expected:F0} m, got {actual:F0} m");
        }

        [Test]
        public void Resample_ProducesUniformSpacing()
        {
            var s = TrackSpline.Resample(Circle(80f, 20), 10f);
            Assert.Greater(s.Count, 10);

            for (int i = 1; i < s.Count; i++)
            {
                float step = Vector3.Distance(s[i - 1].position, s[i].position);
                Assert.AreEqual(10f, step, 1.5f,
                    $"sample {i} is {step:F2} m from the previous one; spacing is not uniform");
            }
        }

        [Test]
        public void Resample_ClosesTheLoop()
        {
            var s = TrackSpline.Resample(Circle(80f, 20), 10f);
            float gap = Vector3.Distance(s[s.Count - 1].position, s[0].position);
            Assert.AreEqual(10f, gap, 2.5f,
                "the gap between the last and first sample should be one spacing step");
        }

        [Test]
        public void Resample_FramesAreOrthonormal()
        {
            var s = TrackSpline.Resample(Circle(80f, 20), 10f);
            foreach (var x in s)
            {
                Assert.AreEqual(1f, x.forward.magnitude, 0.01f, "forward not unit length");
                Assert.AreEqual(1f, x.right.magnitude, 0.01f, "right not unit length");
                Assert.AreEqual(1f, x.up.magnitude, 0.01f, "up not unit length");
                Assert.AreEqual(0f, Vector3.Dot(x.forward, x.right), 0.02f, "forward and right not perpendicular");
                Assert.AreEqual(0f, Vector3.Dot(x.forward, x.up), 0.02f, "forward and up not perpendicular");
                Assert.Greater(x.up.y, 0f, "up vector points downward - the road would be upside down");
            }
        }

        [Test]
        public void Resample_CurvatureIsNonZero_OnACircle()
        {
            var s = TrackSpline.Resample(Circle(60f, 20), 10f);
            foreach (var x in s)
                Assert.Greater(x.curvature, 0.005f,
                    "a circle should register curvature everywhere");
        }

        [Test]
        public void Resample_DistancesIncreaseMonotonically()
        {
            var s = TrackSpline.Resample(Circle(80f, 20), 10f);
            for (int i = 1; i < s.Count; i++)
                Assert.Greater(s[i].distance, s[i - 1].distance,
                    $"distance went backwards at sample {i}");
        }

        [Test]
        public void Resample_BanksIntoTheCorner_NotOutOf()
        {
            // Anticlockwise circle: the road should roll so its inner edge
            // drops. Sign convention is checked by comparing the banked right
            // vector's height against the unbanked one.
            var s = TrackSpline.Resample(Circle(60f, 20), 10f, 8f);

            int banked = 0;
            foreach (var x in s)
                if (Mathf.Abs(x.bankDegrees) > 0.5f) banked++;

            Assert.Greater(banked, s.Count / 2,
                "most of a constant-radius circle should be banked");

            // All banking on a constant circle must be the same direction.
            float first = Mathf.Sign(s[0].bankDegrees);
            foreach (var x in s)
                if (Mathf.Abs(x.bankDegrees) > 0.5f)
                    Assert.AreEqual(first, Mathf.Sign(x.bankDegrees),
                        "banking flipped direction on a constant-radius circle");
        }

        [Test]
        public void Resample_Throws_OnTooFewControlPoints()
        {
            Assert.Throws<System.ArgumentException>(() =>
                TrackSpline.Resample(new List<Vector3> { Vector3.zero, Vector3.one }, 5f));
        }
    }
}
