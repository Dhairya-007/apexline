using System.Collections.Generic;
using UnityEngine;

namespace Apexline.Track
{
    /// <summary>
    /// A closed Catmull-Rom spline through control points, resampled at uniform
    /// arc length. Uniform spacing matters: the road mesh, the waypoints and the
    /// sector triggers are all generated from these samples, and raw spline
    /// parameter spacing bunches up in corners and stretches on straights.
    ///
    /// Pure geometry, no dependencies on the rest of the game, so it is usable
    /// from an editor tool and testable without the engine's physics.
    /// </summary>
    public static class TrackSpline
    {
        public struct Sample
        {
            public Vector3 position;
            public Vector3 forward;   // unit tangent
            public Vector3 right;     // unit, banked
            public Vector3 up;        // unit, banked
            public float curvature;   // 1/radius, metres^-1
            public float distance;    // arc length from the start
            public float bankDegrees;
        }

        /// <summary>Catmull-Rom position on the segment between p1 and p2.</summary>
        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static Vector3 At(IList<Vector3> cps, int i)
        {
            int n = cps.Count;
            return cps[((i % n) + n) % n];
        }

        /// <summary>
        /// Resamples the closed spline at approximately <paramref name="spacing"/>
        /// metres. Done in two passes: a dense walk to measure true arc length,
        /// then uniform picks along that measured length.
        /// </summary>
        public static List<Sample> Resample(IList<Vector3> controlPoints, float spacing,
                                            float maxBankDegrees = 8f)
        {
            if (controlPoints == null || controlPoints.Count < 4)
                throw new System.ArgumentException("a closed Catmull-Rom spline needs at least 4 control points");

            // --- pass 1: dense points with cumulative length ----------------
            const int perSegment = 64;
            var dense = new List<Vector3>();
            var cumulative = new List<float> { 0f };

            for (int i = 0; i < controlPoints.Count; i++)
            {
                for (int s = 0; s < perSegment; s++)
                {
                    float t = s / (float)perSegment;
                    Vector3 p = CatmullRom(At(controlPoints, i - 1), At(controlPoints, i),
                                           At(controlPoints, i + 1), At(controlPoints, i + 2), t);
                    if (dense.Count > 0)
                        cumulative.Add(cumulative[cumulative.Count - 1] + Vector3.Distance(dense[dense.Count - 1], p));
                    dense.Add(p);
                }
            }
            // close the loop
            float total = cumulative[cumulative.Count - 1] + Vector3.Distance(dense[dense.Count - 1], dense[0]);

            // --- pass 2: uniform picks -------------------------------------
            int count = Mathf.Max(8, Mathf.RoundToInt(total / spacing));
            var samples = new List<Sample>(count);

            for (int i = 0; i < count; i++)
            {
                float target = total * i / count;
                Vector3 pos = PointAtDistance(dense, cumulative, total, target);

                // Neighbours a little either side give a stable tangent and a
                // curvature estimate that does not depend on sample spacing.
                float h = Mathf.Max(1f, spacing * 0.5f);
                Vector3 behind = PointAtDistance(dense, cumulative, total, Mathf.Repeat(target - h, total));
                Vector3 ahead  = PointAtDistance(dense, cumulative, total, Mathf.Repeat(target + h, total));

                Vector3 fwd = (ahead - behind);
                fwd.y *= 0.35f;          // flatten slightly so elevation does not tilt the road
                fwd = fwd.normalized;
                if (fwd.sqrMagnitude < 0.5f) fwd = Vector3.forward;

                // Menger curvature of the triangle behind-pos-ahead.
                float curvature = Curvature(behind, pos, ahead);

                // Sign the bank by which way the corner turns.
                Vector3 flatFwd = new Vector3(fwd.x, 0f, fwd.z).normalized;
                Vector3 toAhead = ahead - pos; toAhead.y = 0f;
                float side = Mathf.Sign(Vector3.Dot(Vector3.Cross(Vector3.up, flatFwd), toAhead.normalized));

                // Bank into the corner, proportional to curvature and capped.
                float bank = Mathf.Clamp(curvature * 900f, 0f, maxBankDegrees) * -side;

                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                Quaternion roll = Quaternion.AngleAxis(bank, fwd);
                right = roll * right;
                Vector3 up = Vector3.Cross(fwd, right).normalized;
                if (up.y < 0f) up = -up;

                samples.Add(new Sample
                {
                    position = pos,
                    forward = fwd,
                    right = right,
                    up = up,
                    curvature = curvature,
                    distance = target,
                    bankDegrees = bank,
                });
            }

            return samples;
        }

        static Vector3 PointAtDistance(List<Vector3> dense, List<float> cumulative, float total, float d)
        {
            d = Mathf.Repeat(d, total);

            // cumulative is sorted, so a binary search is correct and cheap.
            int lo = 0, hi = cumulative.Count - 1;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) / 2;
                if (cumulative[mid] <= d) lo = mid; else hi = mid;
            }

            float segLen = cumulative[hi] - cumulative[lo];
            float t = segLen > 0.0001f ? (d - cumulative[lo]) / segLen : 0f;
            int a = lo % dense.Count;
            int b = hi % dense.Count;
            return Vector3.Lerp(dense[a], dense[b], t);
        }

        /// <summary>Curvature through three points: 1/R of their circumcircle.</summary>
        public static float Curvature(Vector3 a, Vector3 b, Vector3 c)
        {
            a.y = b.y = c.y = 0f;   // curvature of the plan view, not the hill
            float ab = Vector3.Distance(a, b);
            float bc = Vector3.Distance(b, c);
            float ca = Vector3.Distance(c, a);
            if (ab < 0.0001f || bc < 0.0001f || ca < 0.0001f) return 0f;

            // Twice the triangle area via the cross product.
            float area2 = Vector3.Cross(b - a, c - a).magnitude;
            if (area2 < 0.000001f) return 0f;      // collinear: straight

            // R = (ab * bc * ca) / (4 * area) -> 1/R = 2 * area2 / (ab*bc*ca)
            return (2f * area2) / (ab * bc * ca);
        }

        /// <summary>Total length of the closed spline, in metres.</summary>
        public static float Length(IList<Vector3> controlPoints)
        {
            var s = Resample(controlPoints, 5f);
            if (s.Count < 2) return 0f;
            return s[s.Count - 1].distance + Vector3.Distance(s[s.Count - 1].position, s[0].position);
        }
    }
}
