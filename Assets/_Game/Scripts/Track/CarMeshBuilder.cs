using System.Collections.Generic;
using UnityEngine;

namespace Apexline.Track
{
    /// <summary>
    /// Generates a low-poly car body mesh. Procedural rather than a downloaded
    /// model, so it is CC0 by construction and needs no entry in
    /// ASSET-LICENSES.md - and so the silhouette can be tuned by editing
    /// numbers instead of opening Blender.
    ///
    /// The shape is a lofted series of cross-sections along Z. Each section is
    /// a six-point car profile: narrow at the sill, widest at the shoulder,
    /// narrower again at the roof. The sill being narrower than the shoulder is
    /// what exposes the wheels - the previous box body was 1.75 m wide with the
    /// wheels at 1.60 m track, so they sat entirely inside the bodywork and were
    /// invisible from behind.
    /// </summary>
    public static class CarMeshBuilder
    {
        /// <summary>One cross-section of the body.</summary>
        public struct Section
        {
            public float z;          // along the car, +Z forward
            public float sillHalf;   // half-width at the bottom edge
            public float shoulderHalf;
            public float roofHalf;   // 0 collapses the section to a crease
            public float bottomY;
            public float shoulderY;
            public float roofY;
        }

        /// <summary>A mid-engine sports car silhouette, roughly 4.1 m long.</summary>
        /// <summary>
        /// A mid-engine sports car, about 4.1 m long and 2.0 m wide.
        ///
        /// The sections at z = -1.35 and +1.35 are WHEEL ARCHES: deliberately
        /// wider than their neighbours so the bodywork swells over the wheels.
        /// Without them the wheels read as floating outriggers, which is exactly
        /// how the first version looked.
        /// </summary>
        public static readonly Section[] SportsCar =
        {
            //            z        sill  shldr  roof    botY  shldY  roofY
            new Section { z = -2.05f, sillHalf = 0.74f, shoulderHalf = 0.86f, roofHalf = 0.58f, bottomY = 0.30f, shoulderY = 0.62f, roofY = 0.78f },
            new Section { z = -1.75f, sillHalf = 0.84f, shoulderHalf = 0.96f, roofHalf = 0.66f, bottomY = 0.18f, shoulderY = 0.62f, roofY = 0.86f },
            new Section { z = -1.35f, sillHalf = 0.94f, shoulderHalf = 1.02f, roofHalf = 0.70f, bottomY = 0.12f, shoulderY = 0.64f, roofY = 0.95f }, // rear arch
            new Section { z = -0.95f, sillHalf = 0.80f, shoulderHalf = 0.92f, roofHalf = 0.72f, bottomY = 0.18f, shoulderY = 0.60f, roofY = 1.05f },
            new Section { z = -0.35f, sillHalf = 0.78f, shoulderHalf = 0.92f, roofHalf = 0.70f, bottomY = 0.16f, shoulderY = 0.58f, roofY = 1.10f }, // roof peak
            new Section { z =  0.25f, sillHalf = 0.78f, shoulderHalf = 0.92f, roofHalf = 0.64f, bottomY = 0.16f, shoulderY = 0.58f, roofY = 1.02f },
            new Section { z =  0.80f, sillHalf = 0.80f, shoulderHalf = 0.92f, roofHalf = 0.56f, bottomY = 0.18f, shoulderY = 0.56f, roofY = 0.76f },
            new Section { z =  1.35f, sillHalf = 0.92f, shoulderHalf = 1.00f, roofHalf = 0.56f, bottomY = 0.12f, shoulderY = 0.58f, roofY = 0.68f }, // front arch
            new Section { z =  1.75f, sillHalf = 0.80f, shoulderHalf = 0.92f, roofHalf = 0.52f, bottomY = 0.18f, shoulderY = 0.52f, roofY = 0.60f },
            new Section { z =  2.05f, sillHalf = 0.58f, shoulderHalf = 0.70f, roofHalf = 0.44f, bottomY = 0.30f, shoulderY = 0.46f, roofY = 0.54f },
        };

        /// <summary>
        /// Six points per ring, ordered anticlockwise seen from +Z:
        /// 0 sill-R, 1 shoulder-R, 2 roof-R, 3 roof-L, 4 shoulder-L, 5 sill-L.
        /// </summary>
        const int Ring = 6;

        public static Mesh Build(Section[] sections, string name = "Car_Body")
        {
            if (sections == null || sections.Length < 2)
                throw new System.ArgumentException("need at least two sections");

            int n = sections.Length;
            var verts = new List<Vector3>(n * Ring + 2);
            var uvs = new List<Vector2>(n * Ring + 2);

            for (int i = 0; i < n; i++)
            {
                Section s = sections[i];
                float v = i / (float)(n - 1);

                verts.Add(new Vector3( s.sillHalf,     s.bottomY,   s.z));
                verts.Add(new Vector3( s.shoulderHalf, s.shoulderY, s.z));
                verts.Add(new Vector3( s.roofHalf,     s.roofY,     s.z));
                verts.Add(new Vector3(-s.roofHalf,     s.roofY,     s.z));
                verts.Add(new Vector3(-s.shoulderHalf, s.shoulderY, s.z));
                verts.Add(new Vector3(-s.sillHalf,     s.bottomY,   s.z));

                for (int k = 0; k < Ring; k++)
                    uvs.Add(new Vector2(k / (float)(Ring - 1), v));
            }

            var tris = new List<int>();

            // --- skin between consecutive rings ---------------------------
            for (int i = 0; i < n - 1; i++)
            {
                int a = i * Ring, b = (i + 1) * Ring;
                for (int k = 0; k < Ring - 1; k++)
                {
                    tris.Add(a + k);     tris.Add(b + k);     tris.Add(b + k + 1);
                    tris.Add(a + k);     tris.Add(b + k + 1); tris.Add(a + k + 1);
                }
                // floor: closes sill-L back round to sill-R
                tris.Add(a + Ring - 1); tris.Add(b + Ring - 1); tris.Add(b + 0);
                tris.Add(a + Ring - 1); tris.Add(b + 0);        tris.Add(a + 0);
            }

            // --- caps ------------------------------------------------------
            int tailCentre = verts.Count;
            verts.Add(Centre(sections[0]));
            uvs.Add(new Vector2(0.5f, 0f));

            int noseCentre = verts.Count;
            verts.Add(Centre(sections[n - 1]));
            uvs.Add(new Vector2(0.5f, 1f));

            for (int k = 0; k < Ring; k++)
            {
                int k2 = (k + 1) % Ring;
                // tail faces -Z, nose faces +Z: opposite winding
                tris.Add(tailCentre); tris.Add(k2); tris.Add(k);
                int o = (n - 1) * Ring;
                tris.Add(noseCentre); tris.Add(o + k); tris.Add(o + k2);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3 Centre(Section s) =>
            new Vector3(0f, (s.bottomY + s.roofY) * 0.5f, s.z);

        /// <summary>
        /// A wheel: a tyre ring plus a slightly inset rim face, so a wheel reads
        /// as a wheel rather than a grey cylinder. Axis is X, matching how
        /// CarController poses the wheel transform.
        /// </summary>
        public static Mesh BuildWheel(float radius, float width, int segments = 20,
                                      string name = "Car_Wheel")
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float hw = width * 0.5f;
            float rimR = radius * 0.62f;

            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float cy = Mathf.Cos(a), sy = Mathf.Sin(a);

                verts.Add(new Vector3(-hw, cy * radius, sy * radius));   // 0 outer -X
                verts.Add(new Vector3( hw, cy * radius, sy * radius));   // 1 outer +X
                verts.Add(new Vector3(-hw * 0.6f, cy * rimR, sy * rimR)); // 2 rim -X
                verts.Add(new Vector3( hw * 0.6f, cy * rimR, sy * rimR)); // 3 rim +X

                float u = i / (float)segments;
                uvs.Add(new Vector2(u, 0f)); uvs.Add(new Vector2(u, 1f));
                uvs.Add(new Vector2(u, 0.3f)); uvs.Add(new Vector2(u, 0.7f));
            }

            int hubA = verts.Count; verts.Add(new Vector3(-hw * 0.6f, 0f, 0f)); uvs.Add(new Vector2(0.5f, 0.5f));
            int hubB = verts.Count; verts.Add(new Vector3( hw * 0.6f, 0f, 0f)); uvs.Add(new Vector2(0.5f, 0.5f));

            for (int i = 0; i < segments; i++)
            {
                int c = i * 4, d = ((i + 1) % segments) * 4;

                // tread
                tris.Add(c + 0); tris.Add(c + 1); tris.Add(d + 1);
                tris.Add(c + 0); tris.Add(d + 1); tris.Add(d + 0);
                // sidewalls
                tris.Add(c + 0); tris.Add(d + 0); tris.Add(d + 2);
                tris.Add(c + 0); tris.Add(d + 2); tris.Add(c + 2);
                tris.Add(c + 1); tris.Add(c + 3); tris.Add(d + 3);
                tris.Add(c + 1); tris.Add(d + 3); tris.Add(d + 1);
                // rim faces
                tris.Add(hubA); tris.Add(d + 2); tris.Add(c + 2);
                tris.Add(hubB); tris.Add(c + 3); tris.Add(d + 3);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
