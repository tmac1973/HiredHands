using System.Collections.Generic;
using UnityEngine;

namespace VikingsForHire.Hirelings.Gear
{
    /// <summary>Simple shapes for the hirelings' homemade tools (the broom, the ladle), built along +y in metres.</summary>
    internal static class MeshBuilder
    {
        public const int Sides = 10;

        /// <summary>An elliptical tube along +y from y0 (radii r0) to y1 (radii r1), capped at either end; jag makes the far rim ragged.</summary>
        public static void Tube(List<Vector3> v, List<Vector2> uv, List<int> tri, float y0, float y1, Vector2 r0, Vector2 r1, float jag, bool cap0, bool cap1)
        {
            int ring = v.Count;
            for (int i = 0; i <= Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float end = y1 - (i % 2 == 1 ? jag : 0f);
                v.Add(new Vector3(c * r0.x, y0, s * r0.y));
                uv.Add(new Vector2((float)i / Sides, 0f));
                v.Add(new Vector3(c * r1.x, end, s * r1.y));
                uv.Add(new Vector2((float)i / Sides, 1f));
            }
            for (int i = 0; i < Sides; i++)
            {
                int a0 = ring + i * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                tri.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
            }
            if (cap0)
                Cap(v, uv, tri, ring, 0, y0, false);
            if (cap1)
                Cap(v, uv, tri, ring, 1, y1 - jag * 0.5f, true);
        }

        private static void Cap(List<Vector3> v, List<Vector2> uv, List<int> tri, int ring, int offset, float y, bool up)
        {
            int centre = v.Count;
            v.Add(new Vector3(0f, y, 0f));
            uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < Sides; i++)
            {
                int a = ring + i * 2 + offset, b = ring + (i + 1) * 2 + offset;
                if (up)
                    tri.AddRange(new[] { centre, b, a });
                else
                    tri.AddRange(new[] { centre, a, b });
            }
        }

        /// <summary>
        /// A bowl (half a sphere, both faces drawn) centred at <paramref name="centre"/>, its rim facing <paramref name="opening"/>.
        /// </summary>
        public static void Bowl(List<Vector3> v, List<Vector2> uv, List<int> tri, Vector3 centre, float radius, Vector3 opening)
        {
            const int rings = 5;
            Quaternion turn = Quaternion.FromToRotation(Vector3.down, -opening.normalized);
            int start = v.Count;
            for (int r = 0; r <= rings; r++)
            {
                float lat = Mathf.PI / 2f * r / rings; // 0 at the bottom, 90° at the rim
                float y = -Mathf.Cos(lat) * radius, ring = Mathf.Sin(lat) * radius;
                for (int i = 0; i <= Sides; i++)
                {
                    float a = i * Mathf.PI * 2f / Sides;
                    v.Add(centre + turn * new Vector3(Mathf.Cos(a) * ring, y, Mathf.Sin(a) * ring));
                    uv.Add(new Vector2((float)i / Sides, (float)r / rings));
                }
            }
            int row = Sides + 1;
            for (int r = 0; r < rings; r++)
                for (int i = 0; i < Sides; i++)
                {
                    int a0 = start + r * row + i, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    tri.AddRange(new[] { a0, b0, a1, a1, b0, b1 }); // outside
                    tri.AddRange(new[] { a0, a1, b0, a1, b1, b0 }); // inside
                }
        }
    }
}
