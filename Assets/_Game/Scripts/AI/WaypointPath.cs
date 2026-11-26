using System.Collections.Generic;
using UnityEngine;

namespace Apexline.AI
{
    /// <summary>
    /// The racing line. Drives AI steering, race position sorting and off-track
    /// respawns. Add child transforms in lap order; width controls how far AI
    /// may offset sideways to overtake.
    /// </summary>
    public class WaypointPath : MonoBehaviour
    {
        [System.Serializable]
        public class Node
        {
            public Transform point;
            [Tooltip("Usable track half-width at this node, in metres.")]
            public float halfWidth = 5f;
            [Tooltip("0..1 multiplier on AI target speed. Drop it for tight corners.")]
            [Range(0.15f, 1f)] public float speedLimit = 1f;
        }

        public List<Node> nodes = new List<Node>();
        public bool closedLoop = true;

        [Header("Editor")]
        public Color gizmoColor = new Color(0.1f, 0.85f, 1f);
        public bool drawWidths = true;

        public int Count => nodes.Count;

        public int Wrap(int i)
        {
            if (Count == 0) return 0;
            if (closedLoop) return ((i % Count) + Count) % Count;
            return Mathf.Clamp(i, 0, Count - 1);
        }

        public Vector3 Position(int i) => nodes[Wrap(i)].point.position;

        public Node At(int i) => nodes[Wrap(i)];

        /// <summary>Index of the node nearest to a world position.</summary>
        public int NearestIndex(Vector3 world)
        {
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < Count; i++)
            {
                float d = (Position(i) - world).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Signed curvature estimate over the next few nodes, 0 = straight.
        /// AI uses this to decide braking points.
        /// </summary>
        public float CurvatureAhead(int index, int lookNodes = 3)
        {
            if (Count < 3) return 0f;

            Vector3 a = Position(index);
            Vector3 b = Position(index + lookNodes);
            Vector3 c = Position(index + lookNodes * 2);

            Vector3 ab = (b - a); ab.y = 0f;
            Vector3 bc = (c - b); bc.y = 0f;
            if (ab.sqrMagnitude < 0.01f || bc.sqrMagnitude < 0.01f) return 0f;

            float angle = Vector3.Angle(ab.normalized, bc.normalized);
            return Mathf.Clamp01(angle / 90f);
        }

        /// <summary>Forward direction of the track at a node - used for respawn facing.</summary>
        public Vector3 Forward(int index)
        {
            Vector3 d = Position(index + 1) - Position(index);
            d.y = 0f;
            return d.sqrMagnitude > 0.001f ? d.normalized : transform.forward;
        }

        /// <summary>
        /// How far along the lap a world position is, in node units with a
        /// fractional part. Race position sorting compares this directly.
        /// </summary>
        public float Progress(Vector3 world)
        {
            if (Count == 0) return 0f;

            int near = NearestIndex(world);
            Vector3 a = Position(near);
            Vector3 b = Position(near + 1);
            Vector3 seg = b - a;

            float t = seg.sqrMagnitude < 0.001f
                ? 0f
                : Mathf.Clamp01(Vector3.Dot(world - a, seg) / seg.sqrMagnitude);

            return near + t;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (Count < 2) return;

            Gizmos.color = gizmoColor;
            int last = closedLoop ? Count : Count - 1;

            for (int i = 0; i < last; i++)
            {
                if (nodes[Wrap(i)].point == null || nodes[Wrap(i + 1)].point == null) continue;

                Vector3 a = Position(i), b = Position(i + 1);
                Gizmos.DrawLine(a, b);
                Gizmos.DrawSphere(a, 0.4f);

                if (!drawWidths) continue;

                Vector3 right = Vector3.Cross(Vector3.up, (b - a).normalized);
                float hw = nodes[Wrap(i)].halfWidth;
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.35f);
                Gizmos.DrawLine(a - right * hw, a + right * hw);
                Gizmos.color = gizmoColor;
            }
        }

        [ContextMenu("Collect Child Transforms As Nodes")]
        void CollectChildren()
        {
            nodes.Clear();
            foreach (Transform child in transform)
                nodes.Add(new Node { point = child });
            Debug.Log($"[WaypointPath] Collected {nodes.Count} nodes.");
        }
#endif
    }
}
